using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using TMPro;
using UnityEngine.SceneManagement;

public class BossController : MonoBehaviour, IDamageable, IStaggerable {

    // Un boss ne se laisse pas promener comme un paysan.
    private const float BOSS_KNOCKBACK_RESISTANCE = 0.25f;

    // Il encaisse aussi les interruptions : une parade l'ouvre, mais brièvement.
    private const float BOSS_STAGGER_RESISTANCE = 0.5f;

    private float staggerUntil = -1f;
    private bool staggerAnimPlaying;
    private Coroutine attackRoutine;
    private const int ATTACK_LAYER = 1;

    private float windup = 0.7f;
    private float attackAngle = 100f;
    private Poise poise;

    private const float POISE_DECAY_DURATION = 3f;
    private const float STAGGER_DURATION = 0.7f;
    private const float TRACKING_CUTOFF = 0.6f;

    // Le boss n'avait qu'une attaque, identique à celle d'un paysan avec de plus gros
    // chiffres. Un coup sur trois devient un écrasement : anticipation plus longue,
    // arc bien plus large, dégâts majorés — lisible, et esquivable si on le lit.
    private int attackCounter;
    private const int SLAM_EVERY = 3;
    private const float SLAM_WINDUP_MULT = 1.6f;
    private const float SLAM_ANGLE = 200f;
    private const float SLAM_DAMAGE_MULT = 1.5f;
    private const float SLAM_RANGE_BONUS = 1.5f;

    private bool slamIncoming;
    private bool hasAttackSpeedParam;

    [SerializeField] private SpawnersController spawnersController;
    [SerializeField] private Slider healthBar;
    [SerializeField] private GameObject smokeGO;
    [SerializeField] private TextMeshProUGUI bossName;
    [SerializeField] private GameObject weaponHolder;
    [SerializeField] private Material darkSkin;

    private Transform playerPosition;
    private Transform enemyPosition;
    private ParticleSystem smokeEffect;
    private UnityEngine.AI.NavMeshAgent navMeshAgent;
    private Animator animator;
    private PlayerController playerController;
    private AudioController audio;
    private Difficulty difficultyController; 

    private string walkMethod;
    private int attack, health, maxHealth, xp;
    private float chanceToDrop, attackSpeed, range, speed, timeSinceLastAttack, sliderVelocity = 0.0f, attackDelay = 0.5f;
    private bool canMove = true, canAttack = true, isAlive = true, firstPhasePassed = false, isInTransition = false, bossRegen = false, isWaitingToAttack = false;

    void Start() {
        this.spawnersController.SetMaxEntities(0);
        this.walkMethod = "Walk";
        this.bossName.text = "Nabil";
    }

    private void Awake() {

        // Comme les mobs : sans le layer Enemy, le cône de frappe du joueur ne le ramène pas.
        CombatLayers.ApplyEnemyLayer(gameObject);

        this.playerPosition = GameObject.FindGameObjectWithTag(Names.MainCharacter).transform;
        this.navMeshAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        this.audio = GameObject.Find("AudioController").GetComponent<AudioController>();
        this.enemyPosition = this.navMeshAgent.transform;

        LoadBossStats();
        
        this.animator = GetComponentInChildren<Animator>();

        // Boss movement is handled by its NavMeshAgent and knockback coroutine.
        if(this.animator != null) this.animator.applyRootMotion = false;

        if(this.animator != null)
            foreach(AnimatorControllerParameter parameter in this.animator.parameters)
                if(parameter.name == "AttackSpeed" && parameter.type == AnimatorControllerParameterType.Float)
                    this.hasAttackSpeedParam = true;


        this.playerController = GameObject.FindGameObjectWithTag(Names.MainCharacter).GetComponent<PlayerController>();

        this.healthBar.maxValue = this.GetHealth();
        this.healthBar.value = this.GetHealth();
        
        this.SetHealthBarMax(this.maxHealth);

        this.audio.StopThemeSFX();
        this.audio.PlayBossThemeSFX(0);
    }

    private void LoadBossStats() {
        string phase = !this.firstPhasePassed ? "boss_1" : "boss_2";

        this.difficultyController = FindObjectOfType<Difficulty>();
        string difficulty = (this.difficultyController != null) ? this.difficultyController.GetDifficulty() : Difficulty.Default;

        EnemyStats enemy = EnemyStatsCache.Get(phase, difficulty);

        if(enemy != null) {
            this.chanceToDrop = enemy.chanceToDrop;
            this.attack       = enemy.attack;
            this.attackSpeed  = enemy.attackSpeed;
            this.health       = enemy.health;
            this.maxHealth    = enemy.health;
            this.range        = enemy.range;
            this.speed        = enemy.speed;
            this.xp           = enemy.xp;

            this.windup      = (enemy.windup > 0f) ? enemy.windup : 0.7f;
            this.attackAngle = (enemy.attackAngle > 0f) ? enemy.attackAngle : 100f;
            this.poise       = new Poise((enemy.poise > 0f) ? enemy.poise : 150f, POISE_DECAY_DURATION);

            this.navMeshAgent.speed = this.speed;
            this.navMeshAgent.stoppingDistance = this.range;
        }
    }

    private void FixedUpdate() {

        GameController.DrawCircleAroundObject(transform.position, this.range, 10);

        if (this.bossRegen) {
            this.BossRegen();
        }        

        if(this.isAlive) {

            if(this.poise != null) this.poise.Decay(Time.fixedDeltaTime);

            // Retour à Empty après un stagger, sinon le buste reste figé sur la
            // dernière frame de l'encaissement (couche masquée, sans transition).
            if(this.staggerAnimPlaying && !this.IsStaggered) {
                this.staggerAnimPlaying = false;

                int emptyHash = Animator.StringToHash("Empty");
                if(this.animator != null && this.animator.HasState(ATTACK_LAYER, emptyHash))
                    this.animator.CrossFade(emptyHash, 0.12f, ATTACK_LAYER, 0f);
            }

            if(this.IsStaggered) return;

            ResetAnims();

            // L'orientation est gérée par la coroutine pendant l'anticipation :
            // elle se fige en fin de geste pour rendre l'esquive possible.
            if (!this.isInTransition && !this.isWaitingToAttack)
                this.enemyPosition.LookAt(playerPosition);

            this.timeSinceLastAttack += Time.fixedDeltaTime;

            if(this.playerPosition && this.canMove) {
                if(Vector2.Distance(new Vector2(playerPosition.position.x, playerPosition.position.z), new Vector2(enemyPosition.position.x, enemyPosition.position.z)) <= this.navMeshAgent.stoppingDistance) {
                    this.animator.SetBool("Idle", true);
                    
                    if(this.timeSinceLastAttack >= this.attackSpeed && this.canAttack && !this.isWaitingToAttack){
                        this.attackRoutine = StartCoroutine(WaitAndAttack());
                    }
                }
                else {
                    this.Move();
                }
            }
        }
    }

    private IEnumerator WaitAndAttack() {

        this.isWaitingToAttack = true;

        this.attackCounter++;
        this.slamIncoming = (this.attackCounter % SLAM_EVERY == 0);

        float duration = this.windup * (this.slamIncoming ? SLAM_WINDUP_MULT : 1f);

        // L'animation partait à l'impact : le boss frappait sans prévenir.
        this.StartTelegraph(duration);

        float elapsed = 0f;

        while(elapsed < duration) {
            elapsed += Time.deltaTime;

            if(elapsed < duration * TRACKING_CUTOFF) this.FacePlayer();

            if(this.IsStaggered || !this.isAlive || this.isInTransition) {
                this.isWaitingToAttack = false;
                this.attackRoutine = null;
                yield break;
            }

            yield return null;
        }

        if(this.CanReachPlayer()) this.Attack();

        // Remise à zéro systématique : sinon sortir de portée pendant l'anticipation
        // laissait le boss prêt à frapper dès le retour du joueur.
        this.timeSinceLastAttack = 0f;

        this.isWaitingToAttack = false;
        this.attackRoutine = null;
    }

    private void StartTelegraph(float duration) {

        if(this.animator == null) return;

        if(this.hasAttackSpeedParam)
            this.animator.SetFloat("AttackSpeed", 1f / Mathf.Max(duration, 0.1f));

        // L'écrasement s'annonce par le cri de guerre déjà présent dans le controller.
        if(this.slamIncoming && this.HasState("BattleCry"))
            this.animator.CrossFade(Animator.StringToHash("BattleCry"), 0.05f, ATTACK_LAYER, 0f);
        else
            this.animator.SetTrigger("Attack");
    }

    private bool HasState(string stateName) {
        return this.animator != null && this.animator.HasState(ATTACK_LAYER, Animator.StringToHash(stateName));
    }

    private void FacePlayer() {
        Vector3 to = this.playerPosition.position - transform.position;
        to.y = 0f;

        if(to.sqrMagnitude < 0.01f) return;

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 10f * Time.deltaTime);
    }

    private bool CanReachPlayer() {

        Vector3 to = this.playerPosition.position - transform.position;
        to.y = 0f;

        float reach = this.range + 0.3f + (this.slamIncoming ? SLAM_RANGE_BONUS : 0f);
        if(to.magnitude > reach) return false;

        float angle = this.slamIncoming ? SLAM_ANGLE : this.attackAngle;

        return Vector3.Angle(transform.forward, to) <= angle * 0.5f;
    }

    private void Move() {
        this.animator.SetBool(this.walkMethod, true);

        // L'agent est désactivé pendant le recul : écrire destination à ce moment
        // lèverait "SetDestination can only be called on an active agent".
        if(this.navMeshAgent == null || !this.navMeshAgent.isActiveAndEnabled || !this.navMeshAgent.isOnNavMesh) return;

        this.navMeshAgent.destination = playerPosition.position;
    }
    
    private void Attack() {
        if(this.playerController.IsAlive()) {
            this.audio.PlaySlashSFX();

            int damage = Mathf.RoundToInt(this.attack * (this.slamIncoming ? SLAM_DAMAGE_MULT : 1f));

            DamageInfo info = new DamageInfo(damage, 0f, 0f, transform);
            info.hitPoint = this.playerController.GetTransform().position + Vector3.up;
            info.weight = HitWeight.Heavy;

            this.playerController.TakeHit(info);
        }
    }

    // Le boss était invulnérable 1.5 s après chaque coup encaissé, ce qui plafonnait
    // les dégâts quelle que soit l'arme. Le dédoublonnage se fait maintenant par coup
    // porté, côté HitboxResolver : chaque frappe compte, mais une seule fois.
    public void TakeHit(DamageInfo info) {

        if(!this.isAlive || this.isInTransition) return;

        this.health -= Mathf.Max(info.amount, 1);
        this.SetHealthBar(this.GetHealth());

        if(this.health <= 0) {
            if (this.firstPhasePassed)
                this.Death();
            else
                this.StartPhaseTwo();

            this.StopMovement();
        }
        else {
            this.ApplyKnockback(info);

            // Le boss n'avait aucun retour visuel quand il encaissait : seule la barre
            // de vie bougeait. La poise lui donne au moins une réaction lisible.
            if(this.poise != null && this.poise.Accumulate(info.poiseDamage))
                this.Stagger(STAGGER_DURATION);
        }
    }

    public bool IsAlive() {
        return this.isAlive;
    }

    public Transform GetTransform() {
        return transform;
    }

    public bool IsStaggered {
        get { return Time.time < this.staggerUntil; }
    }

    public void Stagger(float duration) {

        // Intouchable pendant la chorégraphie de changement de phase.
        if(!this.isAlive || this.isInTransition || duration <= 0f) return;

        this.staggerUntil = Mathf.Max(this.staggerUntil, Time.time + duration * BOSS_STAGGER_RESISTANCE);

        if(this.attackRoutine != null) {
            StopCoroutine(this.attackRoutine);
            this.attackRoutine = null;
        }

        this.isWaitingToAttack = false;
        this.timeSinceLastAttack = 0f;

        if(this.navMeshAgent != null && this.navMeshAgent.isActiveAndEnabled && this.navMeshAgent.isOnNavMesh)
            this.navMeshAgent.ResetPath();

        if(this.animator != null) {
            int hash = Animator.StringToHash("GetHit");

            if(this.animator.HasState(ATTACK_LAYER, hash)) {
                this.animator.CrossFade(hash, 0.05f, ATTACK_LAYER, 0f);
                this.staggerAnimPlaying = true;
            }
        }
    }

    // Un boss encaisse sans être promené : le recul est fortement atténué.
    public void ApplyKnockback(DamageInfo info) {
        Vector3 direction = info.KnockbackDirection(transform.position);
        float distance = Mathf.Max(info.knockback, 0f) * BOSS_KNOCKBACK_RESISTANCE;

        if(distance <= 0.01f) return;

        StartCoroutine(KnockbackEffect(direction, distance, 0.2f));
    }

    private IEnumerator KnockbackEffect(Vector3 direction, float distance, float duration) {

        RaycastHit obstacle;
        if(Physics.SphereCast(transform.position + Vector3.up, 0.5f, direction, out obstacle, distance, CombatLayers.ObstacleMask, QueryTriggerInteraction.Ignore))
            distance = Mathf.Max(obstacle.distance - 0.1f, 0f);

        bool hadAgent = this.navMeshAgent != null && this.navMeshAgent.isActiveAndEnabled;
        if(hadAgent) this.navMeshAgent.enabled = false;

        Vector3 start = transform.position;
        Vector3 target = start + direction * distance;
        float elapsed = 0f;

        while(elapsed < duration) {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));

            yield return null;
        }

        if(hadAgent && this.navMeshAgent != null) {
            this.navMeshAgent.enabled = true;

            NavMeshHit navHit;
            if(NavMesh.SamplePosition(transform.position, out navHit, 2f, NavMesh.AllAreas))
                this.navMeshAgent.Warp(navHit.position);
        }
    }

    private void ResetAnims() {
        this.animator.SetBool("Idle", false);
        this.animator.SetBool("Walk", false);
        this.animator.SetBool("Sprint", false);
    }

    private void Death() {        
        foreach(Transform child in weaponHolder.transform) {
            child.gameObject.SetActive(false);
        }

        this.ClearAttackLayerForDeath();

        if(this.animator != null)
            this.animator.SetBool("Death", true);

        Invoke("Destroyer", 5f);
    }

    private void ClearAttackLayerForDeath() {
        this.staggerUntil = -1f;
        this.staggerAnimPlaying = false;
        this.isWaitingToAttack = false;

        if(this.attackRoutine != null) {
            StopCoroutine(this.attackRoutine);
            this.attackRoutine = null;
        }

        if(this.animator == null) return;

        int emptyHash = Animator.StringToHash("Empty");

        if(this.animator.HasState(ATTACK_LAYER, emptyHash))
            this.animator.Play(emptyHash, ATTACK_LAYER, 0f);
    }

    private void StopMovement() {
        this.canMove = false;
        this.canAttack = false;
        this.isAlive = false;
    }
    private void StartMovement() {
        this.canMove = true;
        this.canAttack = true;
        this.isAlive = true;
    }

    private void StartPhaseTwo() {
        this.tag = "Untagged";
        this.firstPhasePassed = true;
        this.isInTransition = true;

        this.animator.SetBool("Walk", false);
        this.walkMethod = "Sprint";

        this.LoadBossStats();
        this.animator.SetBool("TransitionSecondPhase", true);

        Invoke("Camouflage", 2.5f);
        Invoke("ChangeSkin", 5f);
        Invoke("PhaseTwo", 10f);

    }

    private void PhaseTwo() {
        this.tag = "Boss";
        this.isInTransition = false;
        this.bossRegen = false;
        
        this.audio.StopBossThemeSFX();
        this.audio.PlayBossThemeSFX(1);

        this.StartMovement();

        this.spawnersController.SetMaxEntities(15);
    }

    private void ChangeSkin() {
        this.animator.SetBool("TransitionSecondPhase", false);
        this.bossRegen = true;
        this.bossName.text = "Dark Nabil";

        foreach(Transform child in weaponHolder.transform) {
            if(child.gameObject.name == "weapon_3") {
                child.gameObject.SetActive(true);
            } else {
                child.gameObject.SetActive(false);
            }
        }
        GameObject.Find("BossSkin").GetComponent<Renderer>().material = darkSkin;
    }

    private void Camouflage() {
        Instantiate(smokeGO, new Vector3(this.transform.position.x, 0, this.transform.position.z), Quaternion.identity);
        GameObject.FindGameObjectWithTag("Smoke").GetComponent<ParticleSystem>().Play();

        this.SetHealthBarMax(this.maxHealth);
    }

    private void SetHealthBar(int hp) { 
        this.healthBar.value = hp; 
    }

    private void SetHealthBarMax(int max) { 
        this.healthBar.maxValue = max; 
    }

    private void BossRegen() {
        float currentHealth = Mathf.MoveTowards(this.healthBar.value, this.maxHealth, 300 * Time.deltaTime);
        this.healthBar.value = currentHealth;
    }

    public int GetHealth() { 
        return this.health;          
    }
    
    public int GetMaxHealth() {
        return this.maxHealth;
    }   

    private void Destroyer() {
        Destroy(GameObject.FindGameObjectWithTag(Names.MainCharacter));
        Destroy(GameObject.FindGameObjectWithTag("UI"));
        Destroy(GameObject.FindGameObjectWithTag("Difficulty"));
        this.ChangeScene();
    } 

    private void ChangeScene() {
        SceneManager.LoadScene(Names.CinematicScenes[1], LoadSceneMode.Single);
    }
}
