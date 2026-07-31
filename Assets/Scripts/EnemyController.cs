using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;

public class EnemyController : MonoBehaviour, IDamageable, IStaggerable {
    
    private Transform playerPosition;
    private Transform enemyPosition;
    private NavMeshAgent navMeshAgent;
    private Animator animator;
    private ParticleSystem attackParticle;
    private PlayerController playerController;
    private WeaponsDropper weaponsDropper;
    private QuestController questController;
    private Difficulty difficultyController;

    public Material damageSkin;
    private Renderer renderer;
    private MaterialPropertyBlock propertyBlock;
    private Color baseColor = Color.white;
    private Color damageColor = Color.red;
    private static readonly int COLOR_ID = Shader.PropertyToID("_Color");

    private string enemyType;
    private string enemyFamily;
    private string enemyRole;
    private int attack, health, xp;
    private float chanceToDrop, attackSpeed, range, speed, timeSinceLastAttack;
    private bool canMove = true, canAttack = true, isAlive = true, deathCount = false, isWaitingToAttack = false;

    private float staggerUntil = -1f;
    private bool staggerAnimPlaying;
    private Coroutine attackRoutine;
    private EnemyRoleBehaviour roleBehaviour;
    private bool roleActionActive;
    private GameObject roleWeaponVisual;
    private const int ATTACK_LAYER = 1;

    // --- Combat ---

    private float windup = 0.5f;
    private float attackAngle = 100f;
    private Poise poise;

    private const float POISE_DECAY_DURATION = 2.5f;
    private const float STAGGER_DURATION = 0.8f;

    // La télégraphie suit le joueur au début du geste puis se fige : esquiver sur le
    // côté au bon moment fait rater le coup.
    private const float TRACKING_CUTOFF = 0.6f;

    // Couleur d'anticipation, distincte du rouge de dégât pour rester lisible.
    private static readonly Color TELEGRAPH_COLOR = new Color(1f, 0.55f, 0.1f);
    private float flashUntil = -1f;

    // Longueur approximative du clip d'attaque ennemi, pour caler sa vitesse.
    private const float ATTACK_CLIP_REFERENCE = 1f;
    private bool hasAttackSpeedParam;

    // Créneau d'approche : évite que tous les ennemis convergent vers le même point.
    private float slotAngle;
    private static int spawnCounter;

    // Chevalier : charge quand le joueur prend ses distances, pour punir le recul.
    private bool canCharge;
    private float chargeSpeed;
    private float farFromPlayerSince = -1f;
    private float chargeUntil = -1f;

    private const float CHARGE_TRIGGER_DISTANCE = 6f;
    private const float CHARGE_TRIGGER_DELAY = 2f;
    private const float CHARGE_DURATION = 1.2f;
    private const float CHARGE_COOLDOWN = 5f;
    private float nextChargeAllowedAt = -1f;

    // Bourgeois : tourne autour du joueur au lieu de foncer droit dessus.
    private bool circles;

    // Bonus du support : ils ne se cumulent pas, mais un nouveau sort rafraichit
    // leur duree. Les valeurs reviennent a 1 a l'expiration.
    private float battleDamageMultiplier = 1f;
    private float battleSpeedMultiplier = 1f;
    private float battleBuffUntil = -1f;
    private Light battleBuffLight;

    private void Awake() {

        this.enemyFamily = EnemyController.GetEnemyFamily(gameObject.name);
        this.enemyRole = EnemyController.GetEnemyRole(gameObject.name);
        this.enemyType = string.IsNullOrEmpty(this.enemyRole) ? this.enemyFamily : this.enemyRole;
        this.navMeshAgent = GetComponent<NavMeshAgent>();

        this.difficultyController = FindObjectOfType<Difficulty>();
        string difficulty = (this.difficultyController != null) ? this.difficultyController.GetDifficulty() : Difficulty.Default;

        // Le JSON était rechargé et reparsé par ennemi, à chaque spawn.
        EnemyStats enemy = EnemyStatsCache.Get(this.enemyType, difficulty);

        if(enemy != null) {
            this.chanceToDrop = enemy.chanceToDrop;
            this.attack       = enemy.attack;
            this.attackSpeed  = enemy.attackSpeed;
            this.health       = enemy.health;
            this.range        = enemy.range;
            this.speed        = enemy.speed;
            this.xp           = enemy.xp;

            // Valeurs par défaut si le JSON précède la refonte du combat.
            this.windup      = (enemy.windup > 0f) ? enemy.windup : 0.5f;
            this.attackAngle = (enemy.attackAngle > 0f) ? enemy.attackAngle : 100f;
            this.poise       = new Poise((enemy.poise > 0f) ? enemy.poise : 30f, POISE_DECAY_DURATION);

            // stoppingDistance s'applique à la DESTINATION de l'agent, or celle-ci est
            // désormais un point d'encerclement placé à ~0.9 × range du joueur, pas le
            // joueur lui-même. Y mettre la portée d'attaque faisait donc stopper
            // l'ennemi jusqu'à deux fois trop loin, hors d'allonge et sans jamais se
            // rapprocher. La portée d'attaque est testée séparément, sur la distance
            // réelle au joueur.
            this.navMeshAgent.stoppingDistance = 0.2f;
        } else {
            this.windup = 0.5f;
            this.attackAngle = 100f;
            this.poise = new Poise(30f, POISE_DECAY_DURATION);
        }

        this.ConfigureArchetype();

        this.playerPosition = GameObject.FindGameObjectWithTag(Names.MainCharacter).transform;
        this.enemyPosition = this.navMeshAgent.transform;
        this.questController = GameObject.FindGameObjectWithTag("QuestCanvas").GetComponent<QuestController>();

        this.attackParticle = this.GetComponentInChildren<ParticleSystem>();
        this.animator = GetComponentInChildren<Animator>();

        // Navigation owns world movement; animation root motion would fight the
        // NavMeshAgent and can lift the rig during full-body attack clips.
        if(this.animator != null) this.animator.applyRootMotion = false;

        // Le paramètre n'existe qu'une fois le controller enrichi (Tools/Monarchy/Combat) :
        // l'écrire sans vérifier produirait un avertissement à chaque attaque.
        if(this.animator != null)
            foreach(AnimatorControllerParameter parameter in this.animator.parameters)
                if(parameter.name == "AttackSpeed" && parameter.type == AnimatorControllerParameterType.Float)
                    this.hasAttackSpeedParam = true;
        
        this.playerController = GameObject.FindGameObjectWithTag(Names.MainCharacter).GetComponent<PlayerController>();
        this.weaponsDropper = GameObject.FindGameObjectWithTag("WeaponsDropper").GetComponent<WeaponsDropper>();
        this.navMeshAgent.speed = this.CurrentMoveSpeed;

        // Les requêtes de hitbox du joueur filtrent par layer : sans ça, l'ennemi
        // reste sur Default et n'est jamais ramené par l'OverlapSphere.
        CombatLayers.ApplyEnemyLayer(gameObject);

        Transform enemySkin = transform.Find("EnemySkin");
        if(enemySkin != null) {
            this.renderer = enemySkin.GetComponent<Renderer>();
            this.propertyBlock = new MaterialPropertyBlock();

            // sharedMaterial et non material : lire .material instancie déjà une copie
            // par ennemi, ce qui cassait le batching dès le spawn.
            Material shared = this.renderer.sharedMaterial;
            this.baseColor = (shared != null && shared.HasProperty(COLOR_ID)) ? shared.GetColor(COLOR_ID) : Color.white;
        }

        this.damageSkin = Resources.Load<Material>("Materials/DamageSkin");
        this.damageColor = (this.damageSkin != null && this.damageSkin.HasProperty(COLOR_ID))
                         ? this.damageSkin.GetColor(COLOR_ID)
                         : Color.red;

        this.roleBehaviour = EnemyRoleBehaviour.Create(this.enemyRole, gameObject);
        if(this.roleBehaviour != null) this.roleBehaviour.Initialize(this);
    }

    // Le flash de dégât passait par renderer.material = damageSkin, qui instancie
    // un matériau à chaque coup reçu : fuite mémoire continue en horde.
    private void SetSkinColor(Color color) {
        if(this.renderer == null || this.propertyBlock == null) return;

        this.renderer.GetPropertyBlock(this.propertyBlock);
        this.propertyBlock.SetColor(COLOR_ID, color);
        this.renderer.SetPropertyBlock(this.propertyBlock);
    }

    private void FixedUpdate() {

        if(!this.isAlive) return;

        this.UpdateBattleBuff();

        // La pression accumulée retombe si on cesse de frapper.
        if(this.poise != null) this.poise.Decay(Time.fixedDeltaTime);

        // Sortie de stagger : il FAUT rendre la couche d'attaque à Empty. L'état
        // GetHit n'a aucune transition sortante et la couche est masquée sur le haut
        // du corps avec Write Defaults à 1 : sans ce retour, le buste de l'ennemi
        // restait figé pour toujours sur la dernière frame de l'encaissement.
        if(this.staggerAnimPlaying && !this.IsStaggered) {
            this.staggerAnimPlaying = false;
            this.ClearStaggerAnimation();
        }

        // Staggeré : ni déplacement ni attaque, et l'animation d'encaissement
        // n'est pas écrasée par ResetAnims.
        if(this.IsStaggered) return;

        ResetAnims();

        this.timeSinceLastAttack += Time.fixedDeltaTime;

        if(!this.playerPosition || !this.canMove) return;

        float distance = this.FlatDistanceToPlayer();

        if(this.roleBehaviour != null) {
            this.roleBehaviour.TickRole(distance);
            return;
        }

        this.UpdateCharge(distance);

        if(distance <= this.range) {
            this.animator.SetBool("Idle", true);

            // Pendant l'anticipation, c'est la coroutine qui gère l'orientation.
            if(!this.isWaitingToAttack) this.FacePlayer();

            if(this.timeSinceLastAttack >= this.attackSpeed && this.canAttack && !this.isWaitingToAttack)
                this.attackRoutine = StartCoroutine(WaitAndAttack());
        } else {
            this.Move(distance);
        }
    }

    private IEnumerator WaitAndAttack() {

        this.isWaitingToAttack = true;

        // L'animation et la particule ne partaient qu'à l'IMPACT : l'anticipation de
        // 0.5 s existait mais restait totalement invisible, ce qui rendait le combat
        // injuste alors qu'il était mécaniquement esquivable. Elles partent maintenant
        // au début du geste, étirées sur toute sa durée.
        this.StartTelegraph();

        float elapsed = 0f;

        while(elapsed < this.windup) {
            elapsed += Time.deltaTime;

            // On suit le joueur au début du geste, puis on s'engage : rouler sur le
            // côté au bon moment fait rater le coup.
            if(elapsed < this.windup * TRACKING_CUTOFF) this.FacePlayer();

            this.UpdateTelegraphTint(elapsed / this.windup);

            if(this.IsStaggered || !this.isAlive) {
                this.EndTelegraph();
                this.isWaitingToAttack = false;
                this.attackRoutine = null;
                yield break;
            }

            yield return null;
        }

        this.EndTelegraph();

        if(this.CanReachPlayer()) this.Attack();

        // Le compteur n'était remis à zéro que si le coup aboutissait : sortir de
        // portée pendant l'anticipation laissait l'ennemi prêt à frapper aussitôt
        // qu'on revenait. La cadence repart maintenant dans tous les cas.
        this.timeSinceLastAttack = 0f;

        this.isWaitingToAttack = false;
        this.attackRoutine = null;
    }

    private void Move(float distance) {
        this.animator.SetBool("Walk", true);

        // L'agent est désactivé pendant le recul pour ne pas lutter contre lui, et
        // FixedUpdate continue de tourner : écrire destination à ce moment-là lève
        // "SetDestination can only be called on an active agent".
        if(!this.CanNavigate()) return;

        this.navMeshAgent.destination = this.DesiredDestination(distance);
    }

    private bool CanNavigate() {
        return this.navMeshAgent != null
            && this.navMeshAgent.isActiveAndEnabled
            && this.navMeshAgent.isOnNavMesh;
    }

    // Tous les ennemis visaient exactement la position du joueur et s'entonnaient au
    // même point. Chacun vise maintenant son créneau sur un anneau autour de la cible.
    private Vector3 DesiredDestination(float distance) {

        Vector3 playerPos = this.playerPosition.position;

        // De loin, on fonce ; l'écartement ne sert qu'à l'approche finale.
        if(distance > this.range * 3f) return playerPos;

        Vector3 fromPlayer = transform.position - playerPos;
        fromPlayer.y = 0f;

        if(fromPlayer.sqrMagnitude < 0.01f) return playerPos;

        float angle = this.slotAngle;

        // Le bourgeois glisse en continu autour de sa cible au lieu de foncer droit.
        if(this.circles) angle += Mathf.Sin(Time.time * 0.8f + this.slotAngle) * 35f;

        Vector3 direction = Quaternion.Euler(0f, angle, 0f) * fromPlayer.normalized;

        return playerPos + direction * (this.range * 0.85f);
    }

    // Le chevalier est lent, donc facile à distancer. S'il perd le joueur de vue trop
    // longtemps, il charge : reculer indéfiniment cesse d'être une stratégie.
    private void UpdateCharge(float distance) {

        if(!this.canCharge) return;

        if(Time.time < this.chargeUntil) {
            this.navMeshAgent.speed = this.chargeSpeed * this.battleSpeedMultiplier;
            return;
        }

        this.navMeshAgent.speed = this.CurrentMoveSpeed;

        if(distance <= CHARGE_TRIGGER_DISTANCE) {
            this.farFromPlayerSince = -1f;
            return;
        }

        if(this.farFromPlayerSince < 0f) this.farFromPlayerSince = Time.time;

        bool waitedLongEnough = Time.time - this.farFromPlayerSince >= CHARGE_TRIGGER_DELAY;

        if(waitedLongEnough && Time.time >= this.nextChargeAllowedAt) {
            this.chargeUntil = Time.time + CHARGE_DURATION;
            this.nextChargeAllowedAt = Time.time + CHARGE_DURATION + CHARGE_COOLDOWN;
            this.farFromPlayerSince = -1f;
        }
    }

    private float FlatDistanceToPlayer() {
        Vector3 delta = this.playerPosition.position - this.enemyPosition.position;
        delta.y = 0f;

        return delta.magnitude;
    }

    private void FacePlayer() {
        Vector3 to = this.playerPosition.position - transform.position;
        to.y = 0f;

        if(to.sqrMagnitude < 0.01f) return;

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 12f * Time.deltaTime);
    }

    // Le coup ne portait qu'à la distance, sans notion d'angle : on prenait des dégâts
    // en étant dans le dos de l'ennemi. Il faut désormais être dans son cône.
    private bool CanReachPlayer() {

        Vector3 to = this.playerPosition.position - transform.position;
        to.y = 0f;

        if(to.magnitude > this.range + 0.3f) return false;

        return Vector3.Angle(transform.forward, to) <= this.attackAngle * 0.5f;
    }

    // --- Télégraphie ---

    private void StartTelegraph() {

        this.ActivateCollectParticle();

        if(this.animator == null) return;

        // Étire le clip d'attaque sur l'anticipation, pour que le coup tombe à la fin.
        if(this.hasAttackSpeedParam)
            this.animator.SetFloat("AttackSpeed", ATTACK_CLIP_REFERENCE / Mathf.Max(this.windup, 0.1f));

        this.animator.SetTrigger("Attack");
    }

    private void UpdateTelegraphTint(float progress) {
        this.UpdateTelegraphTint(progress, TELEGRAPH_COLOR);
    }

    private void UpdateTelegraphTint(float progress, Color targetColor) {
        // Un flash de dégât en cours reste prioritaire : les deux couleurs ne doivent
        // pas se disputer le rendu.
        if(Time.time < this.flashUntil) return;

        this.SetSkinColor(Color.Lerp(this.baseColor, targetColor, Mathf.Clamp01(progress)));
    }

    private void EndTelegraph() {
        if(Time.time < this.flashUntil) return;

        this.SetSkinColor(this.baseColor);
    }

    private void ConfigureArchetype() {

        // Créneau attribué en tourniquet à l'apparition : les ennemis s'étalent en arc
        // autour du joueur plutôt que de se suivre en file vers le même point.
        this.slotAngle = ((spawnCounter++ % 7) - 3) * 18f;

        switch(this.enemyFamily) {

            case "knight":
                this.canCharge = true;
                this.chargeSpeed = this.speed * 2.4f;
                this.navMeshAgent.avoidancePriority = 30;   // force le passage dans la nuée
                break;

            case "bourgeois":
                this.circles = true;
                this.navMeshAgent.avoidancePriority = 50;
                break;

            default:
                this.navMeshAgent.avoidancePriority = 70;   // les paysans cèdent le passage
                break;
        }
    }
    
    // L'animation et la particule sont déjà parties au début de l'anticipation :
    // ici on ne fait plus qu'appliquer le coup.
    private void Attack() {
        if(!this.playerController.IsAlive()) return;

        // L'ennemi se déclare comme source : le joueur a besoin de savoir d'où vient
        // le coup pour décider s'il est paré, encaissé de dos, ou esquivé.
        DamageInfo info = new DamageInfo(this.CurrentAttackDamage, 0f, 0f, transform);
        info.hitPoint = this.playerController.GetTransform().position + Vector3.up;
        info.weight = HitWeight.Medium;

        this.playerController.TakeHit(info);
    }

    // --- Stagger ---

    public bool IsStaggered {
        get { return Time.time < this.staggerUntil; }
    }

    // Interrompt l'ennemi : anticipation annulée, cadence remise à zéro.
    // Sans ça, une parade réussie n'ouvrait aucune fenêtre de riposte.
    public void Stagger(float duration) {

        if(!this.isAlive || duration <= 0f) return;

        this.staggerUntil = Mathf.Max(this.staggerUntil, Time.time + duration);

        if(this.attackRoutine != null) {
            StopCoroutine(this.attackRoutine);
            this.attackRoutine = null;
        }

        if(this.roleBehaviour != null) this.roleBehaviour.CancelAction();

        this.isWaitingToAttack = false;
        this.timeSinceLastAttack = 0f;

        if(this.CanNavigate()) this.navMeshAgent.ResetPath();

        this.PlayStaggerAnimation();
    }

    private void PlayStaggerAnimation() {
        if(this.animator == null) return;

        int hash = Animator.StringToHash("GetHit");

        // L'état n'existe qu'une fois le controller enrichi (Tools/Monarchy/Combat).
        if(!this.animator.HasState(ATTACK_LAYER, hash)) return;

        this.animator.CrossFade(hash, 0.05f, ATTACK_LAYER, 0f);
        this.staggerAnimPlaying = true;
    }

    private void ClearStaggerAnimation() {
        if(this.animator == null) return;

        int hash = Animator.StringToHash("Empty");

        if(this.animator.HasState(ATTACK_LAYER, hash))
            this.animator.CrossFade(hash, 0.12f, ATTACK_LAYER, 0f);
    }

    // Ancien fonctionnement : ApplyDamage() différait un TakeDamage() sans paramètre,
    // qui allait relire les stats du joueur lui-même, puis posait un cooldown
    // d'invulnérabilité égal à l'attackSpeed du joueur — 2 secondes de base. Un ennemi
    // ne pouvait donc être touché qu'une fois toutes les 2 s, quelle que soit l'arme.
    // Le dédoublonnage se fait maintenant par coup porté, côté HitboxResolver.
    public void TakeHit(DamageInfo info) {

        if(!this.isAlive) return;

        this.health -= Mathf.Max(info.amount, 1);
        this.ApplyKnockback(info);

        if(this.health <= 0) {
            this.Death();

            if(!this.deathCount) {
                this.playerController.IncrementKillCounter();
                this.playerController.IncrementStatCounter();
                questController.UpdateQuestText();
                this.deathCount = true;
            }

            return;
        }

        // Un ennemi frappé poursuivait son geste et touchait quand même. La poise
        // laisse passer les coups légers mais cède aux armes lourdes : c'est ce qui
        // fait qu'une masse ouvre un chevalier là où une dague échoue.
        if(this.poise != null && this.poise.Accumulate(info.poiseDamage))
            this.Stagger(STAGGER_DURATION);
    }

    public bool IsAlive() {
        return this.isAlive;
    }

    public Transform GetTransform() {
        return transform;
    }

    private void ResetAnims() {
        this.animator.SetBool("Idle", false);
        this.animator.SetBool("Walk", false);
    }

    private void Death() {
        if(this.roleBehaviour != null) this.roleBehaviour.CancelAction();

        this.canMove = false;
        this.canAttack = false;
        this.isAlive = false;
        this.gameObject.tag = "Untagged";

        this.ClearAttackLayerForDeath();

        if(this.animator != null)
            this.animator.SetInteger("Death", GameController.Random(1, 3));

        Invoke("GiveXP", 1f);
        Invoke("DestroyEnemy", 2f);
    }

    private void ClearAttackLayerForDeath() {
        this.staggerUntil = -1f;
        this.staggerAnimPlaying = false;
        this.roleActionActive = false;
        this.isWaitingToAttack = false;

        if(this.attackRoutine != null) {
            StopCoroutine(this.attackRoutine);
            this.attackRoutine = null;
        }

        if(this.animator == null) return;

        int emptyHash = Animator.StringToHash("Empty");

        // Immediate Play, not CrossFade: no GetHit/attack frame may keep masking
        // the upper body while the death animation starts on the movement layer.
        if(this.animator.HasState(ATTACK_LAYER, emptyHash))
            this.animator.Play(emptyHash, ATTACK_LAYER, 0f);
    }

    private void GiveXP() {
        this.playerController.XPGain(this.xp);
    }

    private void DestroyEnemy() {


        if(WillDropWeapon())
            this.weaponsDropper.CreateWeapon(transform.position);
        
        Destroy(this.gameObject);
    }

    // La direction venait de -transform.forward de l'ENNEMI : dès qu'il ne regardait
    // pas le joueur, il était projeté n'importe où, parfois vers l'attaquant.
    // Elle part maintenant de l'attaquant vers la cible.
    public void ApplyKnockback(DamageInfo info) {
        Vector3 direction = info.KnockbackDirection(transform.position);
        float distance = Mathf.Max(info.knockback, 0f);

        StartCoroutine(KnockbackEffect(direction, distance, 0.2f));
    }

    private IEnumerator KnockbackEffect(Vector3 direction, float distance, float duration) {

        // Le flash de dégât prime sur la teinte d'anticipation pendant sa durée.
        this.flashUntil = Time.time + 0.2f;
        this.SetSkinColor(this.damageColor);
        CancelInvoke(nameof(RestoreBasicSkin));
        Invoke(nameof(RestoreBasicSkin), 0.2f);

        if(distance <= 0.01f || duration <= 0f) yield break;

        // Ne pas projeter l'ennemi à travers un mur.
        RaycastHit obstacle;
        Vector3 castOrigin = transform.position + Vector3.up;

        if(Physics.SphereCast(castOrigin, 0.4f, direction, out obstacle, distance, CombatLayers.ObstacleMask, QueryTriggerInteraction.Ignore))
            distance = Mathf.Max(obstacle.distance - 0.1f, 0f);

        // Le NavMeshAgent recalculait la position à chaque frame et écrasait le recul,
        // en pouvant au passage sortir l'ennemi du NavMesh. On le suspend, puis on le
        // recale proprement avec Warp.
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

    private void RestoreBasicSkin() {
        this.SetSkinColor(this.baseColor);
    }

    private bool WillDropWeapon() {
        return GameController.RandomFloat() < this.chanceToDrop;
    }

    private void ActivateCollectParticle() {
        // Jouée à chaque amorce d'attaque désormais, et non plus à l'impact :
        // un prefab sans ParticleSystem lèverait l'exception bien plus souvent.
        if(this.attackParticle != null) this.attackParticle.Play();
    }

    private static string GetEnemyFamily(string name) {
        string normalized = name.Replace("(Clone)", "").Trim();

        return normalized.Split('_')[0];
    }

    private static string GetEnemyRole(string name) {
        string normalized = name.Replace("(Clone)", "").Trim();

        switch(normalized) {
            case "peasant_4":
            case "knight_2":
                return "lancer";
            case "peasant_5":
                return "thrower";
            case "bourgeois_4":
                return "support";
            default:
                return null;
        }
    }

    public void Dance() {
        this.animator.SetInteger("Dance", GameController.Random(0, 4));
    }

    // --- API etroite utilisee par les roles tactiques ---

    public Transform PlayerTransform { get { return this.playerPosition; } }
    public string EnemyFamily { get { return this.enemyFamily; } }
    public float RoleWindup { get { return this.windup; } }
    public float RoleAttackRange { get { return this.range; } }
    public float RoleAttackDelay { get { return this.attackSpeed; } }
    public bool RoleActionActive { get { return this.roleActionActive; } }

    public bool CanStartRoleAction {
        get {
            return this.isAlive
                && !this.IsStaggered
                && this.canAttack
                && !this.roleActionActive
                && this.timeSinceLastAttack >= this.attackSpeed;
        }
    }

    private int CurrentAttackDamage {
        get { return Mathf.Max(Mathf.RoundToInt(this.attack * this.battleDamageMultiplier), 1); }
    }

    private float CurrentMoveSpeed {
        get { return Mathf.Max(this.speed * this.battleSpeedMultiplier, 0f); }
    }

    public void RoleMoveTo(Vector3 destination) {
        if(this.animator != null) this.animator.SetBool("Walk", true);
        if(!this.CanNavigate()) return;

        this.navMeshAgent.speed = this.CurrentMoveSpeed;
        this.navMeshAgent.destination = destination;
    }

    public void RoleStop() {
        if(this.animator != null) this.animator.SetBool("Idle", true);
        if(this.CanNavigate() && this.navMeshAgent.hasPath) this.navMeshAgent.ResetPath();
    }

    public void RoleFacePlayer() {
        this.FacePlayer();
    }

    public void BeginRoleAction(string animationState, float duration) {
        this.BeginRoleAction(animationState, duration, null);
    }

    public void BeginRoleAction(string animationState, float duration, string clipNameHint) {
        this.roleActionActive = true;
        this.RoleStop();
        this.ActivateCollectParticle();

        if(this.animator == null) return;

        if(this.hasAttackSpeedParam)
            this.animator.SetFloat("AttackSpeed", this.RoleAnimationSpeed(animationState, duration, clipNameHint));

        int hash = Animator.StringToHash(animationState);

        if(this.animator.HasState(ATTACK_LAYER, hash))
            this.animator.CrossFade(hash, 0.08f, ATTACK_LAYER, 0f);
        else
            this.animator.SetTrigger("Attack");
    }

    public void UpdateRoleTelegraph(float progress) {
        this.UpdateTelegraphTint(progress);
    }

    public void UpdateRoleTelegraph(float progress, Color targetColor) {
        this.UpdateTelegraphTint(progress, targetColor);
    }

    private float RoleAnimationSpeed(string animationState, float duration, string clipNameHint) {
        float clipLength = ATTACK_CLIP_REFERENCE;

        if(this.animator != null && this.animator.runtimeAnimatorController != null) {
            string normalizedState = NormalizeAnimationName(animationState);
            string normalizedHint = NormalizeAnimationName(clipNameHint);
            AnimationClip[] clips = this.animator.runtimeAnimatorController.animationClips;

            for(int i = 0; i < clips.Length; i++) {
                AnimationClip clip = clips[i];
                if(clip == null) continue;

                string normalizedClip = NormalizeAnimationName(clip.name);

                if(normalizedClip == normalizedState
                || (!string.IsNullOrEmpty(normalizedHint) && normalizedClip.Contains(normalizedHint))) {
                    clipLength = clip.length;
                    break;
                }
            }
        }

        return Mathf.Clamp(clipLength / Mathf.Max(duration, 0.1f), 0.25f, 4f);
    }

    private static string NormalizeAnimationName(string value) {
        return string.IsNullOrEmpty(value)
            ? ""
            : value.Replace("_", "").Replace(" ", "").ToLowerInvariant();
    }

    public void EndRoleAction(bool resetCooldown = true) {
        this.EndTelegraph();
        this.roleActionActive = false;

        if(resetCooldown) this.timeSinceLastAttack = 0f;

        if(this.animator == null) return;

        int hash = Animator.StringToHash("Empty");
        if(this.animator.HasState(ATTACK_LAYER, hash))
            this.animator.CrossFade(hash, 0.10f, ATTACK_LAYER, 0f);
    }

    public bool RoleCanHitPlayer(float hitRange, float angle) {
        if(this.playerPosition == null || this.playerController == null || !this.playerController.IsAlive())
            return false;

        Vector3 to = this.playerPosition.position - transform.position;
        to.y = 0f;

        return to.magnitude <= hitRange
            && Vector3.Angle(transform.forward, to) <= angle * 0.5f;
    }

    public void DealRoleDamage(float damageMultiplier, float poiseDamage, float knockback, HitWeight weight) {
        if(this.playerController == null || !this.playerController.IsAlive()) return;

        DamageInfo info = new DamageInfo(
            Mathf.Max(Mathf.RoundToInt(this.CurrentAttackDamage * damageMultiplier), 1),
            poiseDamage,
            knockback,
            transform);

        info.hitPoint = this.playerController.GetTransform().position + Vector3.up;
        info.weight = weight;
        this.playerController.TakeHit(info);
    }

    public void LaunchRoleProjectile(Vector3 direction, float projectileSpeed, int weaponId) {
        if(this.playerPosition == null) return;

        Vector3 origin = transform.position + Vector3.up * 1.35f + transform.forward * 0.55f;
        DamageInfo info = new DamageInfo(this.CurrentAttackDamage, 12f, 0.4f, transform);
        info.weight = HitWeight.Medium;

        GameObject projectileObject = new GameObject("EnemyProjectile_" + weaponId);
        Projectile projectile = projectileObject.AddComponent<Projectile>();
        projectile.Launch(
            origin,
            direction,
            projectileSpeed,
            info,
            weaponId - 1,
            null,
            CombatLayers.PlayerMask | CombatLayers.ObstacleMask,
            false);

        this.AttachWeaponVisual(projectileObject.transform, weaponId, false);
    }

    public void AttachRoleWeapon(int weaponId) {
        if(this.animator == null) return;

        Transform hand = this.animator.GetBoneTransform(HumanBodyBones.RightHand);
        if(hand == null) return;

        this.roleWeaponVisual = this.AttachWeaponVisual(hand, weaponId, true);
        if(this.roleWeaponVisual == null) return;

        Transform referenceHolder = this.FindPlayerWeaponTransform("WeaponHolder");
        Transform referenceWeapon = null;

        if(referenceHolder != null) {
            string expectedName = "weapon_" + weaponId;

            foreach(Transform child in referenceHolder)
                if(child.name == expectedName) {
                    referenceWeapon = child;
                    break;
                }
        }

        if(referenceHolder != null && referenceWeapon != null) {
            Transform visual = this.roleWeaponVisual.transform;
            visual.localPosition = referenceHolder.localPosition
                                 + referenceHolder.localRotation
                                 * Vector3.Scale(referenceHolder.localScale, referenceWeapon.localPosition);
            visual.localRotation = referenceHolder.localRotation * referenceWeapon.localRotation;
            visual.localScale = Vector3.Scale(referenceHolder.localScale, referenceWeapon.localScale);
            return;
        }

        // La plupart des armes du joueur sont agrandies x5 dans un holder à 0,13.
        // Ce secours garde notamment la pique visible si le prefab du joueur change.
        this.roleWeaponVisual.transform.localPosition = new Vector3(-0.04f, -0.34f, -0.05f);
        this.roleWeaponVisual.transform.localRotation = Quaternion.Euler(-13f, 76f, -41f);
        this.roleWeaponVisual.transform.localScale = Vector3.one * 0.65f;
    }

    private Transform FindPlayerWeaponTransform(string expectedName) {
        if(this.playerController == null) return null;

        Transform[] children = this.playerController.GetComponentsInChildren<Transform>(true);

        for(int i = 0; i < children.Length; i++)
            if(children[i].name == expectedName)
                return children[i];

        return null;
    }

    public void SetRoleWeaponVisible(bool visible) {
        if(this.roleWeaponVisual != null)
            this.roleWeaponVisual.SetActive(visible);
    }

    private GameObject AttachWeaponVisual(Transform parent, int weaponId, bool held) {
        GameObject prefab = Resources.Load<GameObject>("Weapons/Prefabs/weapon_" + weaponId);
        if(prefab == null || parent == null) return null;

        GameObject visual = Instantiate(prefab, parent);
        visual.name = held ? "RoleWeapon_" + weaponId : "ProjectileVisual_" + weaponId;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = held ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;

        Weapon weaponScript = visual.GetComponent<Weapon>();
        if(weaponScript != null) weaponScript.enabled = false;

        foreach(Collider collider in visual.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        foreach(Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true)) {
            body.isKinematic = true;
            body.useGravity = false;
        }

        foreach(Light light in visual.GetComponentsInChildren<Light>(true))
            light.enabled = false;

        if(held) visual.transform.localScale = Vector3.one * 0.65f;
        return visual;
    }

    public void ApplyBattleBuff(float damageMultiplier, float speedMultiplier, float duration) {
        if(!this.isAlive) return;

        this.battleDamageMultiplier = Mathf.Max(damageMultiplier, 1f);
        this.battleSpeedMultiplier = Mathf.Max(speedMultiplier, 1f);
        this.battleBuffUntil = Mathf.Max(this.battleBuffUntil, Time.time + Mathf.Max(duration, 0f));

        if(this.CanNavigate()) this.navMeshAgent.speed = this.CurrentMoveSpeed;

        if(this.battleBuffLight == null) {
            GameObject lightObject = new GameObject("Battle Buff");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = Vector3.up * 1.35f;

            this.battleBuffLight = lightObject.AddComponent<Light>();
            this.battleBuffLight.type = LightType.Point;
            this.battleBuffLight.color = new Color(0.15f, 0.75f, 1f);
            this.battleBuffLight.range = 2.6f;
            this.battleBuffLight.intensity = 1.8f;
        }

        this.battleBuffLight.enabled = true;
    }

    private void UpdateBattleBuff() {
        if(this.battleBuffUntil < 0f || Time.time < this.battleBuffUntil) return;

        this.battleBuffUntil = -1f;
        this.battleDamageMultiplier = 1f;
        this.battleSpeedMultiplier = 1f;

        if(this.battleBuffLight != null) this.battleBuffLight.enabled = false;
        if(this.CanNavigate()) this.navMeshAgent.speed = this.CurrentMoveSpeed;
    }
}
