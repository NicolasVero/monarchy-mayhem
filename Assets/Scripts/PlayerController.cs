using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;


public class PlayerController : MonoBehaviour, IDamageable {
    
    [Header("HUD")] 
    [SerializeField] private Slider xpBar;
    [SerializeField] private Slider healthBar;
    [SerializeField] private Slider staminaBar;
    [SerializeField] private GameObject levelUpPanel;
    private GameObject chargeIndicator;
    private Image chargeFill;

    [Header("Scripts")]
    [SerializeField] private LevelUpChoice levelUpChoice;
    [SerializeField] private HUDStats hudStats;
    [SerializeField] private new CameraController camera;
    [SerializeField] private new AudioController audio;

    [Header("Canvas")]
    [SerializeField] private Canvas deathScreen;
    [SerializeField] private Canvas hudScreen;
    [SerializeField] private Canvas questScreen;
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private GameObject questMenu;
    [SerializeField] private ProgressiveDarkeningController progressiveDarkening;
    [SerializeField] private GameObject retry;

    [Header("Weapons")]
    [SerializeField] GameObject weapon;
    [SerializeField] private WeaponsDropper weaponsDropper;
    [SerializeField] private GameObject weaponHolder;


    private float sensitivity = 10;
    private int enemyKillCounter, sprint = 0, danceCounter = 0;
    private bool canResume = true, isAlive = true, inPause = false, isSprinting = false, inDanseMenu = false, isDancing;

    private float timeSinceLastRegeneration = 0f;
    private float regenerationDelay = 10.0f;

    // attributs
    private int totalXP;
    private int xp;
    private int xpToNext;
    private int level;
    private int health;
    private int maxActualHealth;
    private int resistance;
    private int attack;
    private float attackSpeed;
    private float range;
    private float speed;
    private float knockback;
    private int regeneration;

    // controlleur attributs
    private int xpRequired = 0;

    private int maxLevel;

    private int[] increaseHealth;
    private int[] increaseResistance;
    private int[] increaseAttack;
    private float[] increaseAttackSpeed;
    private float[] increaseRange;
    private float[] increaseSpeed;
    private float[] increaseKnockback;
    private int[] increaseRegeneration;
    private float[] increaseStamina;
    private float[] increaseStaminaRegen;


    private int healthLevel = 1;
    private int resistanceLevel = 1;
    private int attackLevel = 1;
    private int attackSpeedLevel = 1;
    private int rangeLevel = 1;
    private int speedLevel = 1;
    private int regenerationLevel = 1;
    private int staminaLevel = 1;
    private int staminaRegenLevel = 1;

    private int weaponID;
    private string weaponName = "";
    private string weaponFamily = "";
    private int weaponAmmo;
    private Camera playerCamera;
    private int   weaponAttack;
    private float weaponAttackSpeed;
    private float weaponKnockback;
    private float weaponRange;
    private int weaponRegeneration;
    private float weaponSpeed;

    private SphereCollider rangeCollider;
    private PlayerCombat combat;
    private CameraRig cameraRig;
    private Stamina stamina;
    private Animator animator;

    // Valeurs de base et bonus gagnés via les deux améliorations d'endurance.
    private float baseStamina = 100f;
    private float baseStaminaRegen = 22f;
    private float staminaRegenDelay = 0.8f;
    private float staminaBonus;
    private float staminaRegenBonus;

    // Coût du sprint, par seconde.
    private const float SPRINT_STAMINA_PER_SECOND = 12f;
    private const float BACKWARD_SPEED_MULTIPLIER = 0.75f;

    // Répit après un coup encaissé, indispensable face à une horde.
    private const float HURT_INVULNERABILITY = 0.4f;
    private float hurtInvulnerableUntil = -1f;
    private Vector3 moveDirection;
    private SceneController sceneController;
    private Canvas bossCanvas;
    private Dictionary<KeyCode, Action> keyActions = new Dictionary<KeyCode, Action>();

    private string currentAnimation;
    private string[] secondLayerAnimations = { "Attack", "Ibreakyou", "Wave" };

    private const int UPPER_BODY_LAYER = 1;



    void Awake() {

        this.keyActions.Add(KeyCode.E, TakeWeapon);
        this.keyActions.Add(KeyCode.R, ToggleQuestMenu);
        this.keyActions.Add(KeyCode.P, TogglePauseMenu);
        this.keyActions.Add(KeyCode.Escape, TogglePauseMenu);

        this.audio.PlayThemeSFX();
        DontDestroyOnLoad(this.gameObject);
        this.camera.DisableBlackAndWhiteEffect();

        GameController.SetCanvasVisibility(deathScreen, false);
        GameController.HidePauseMenu(pauseMenu);
        GameController.SetPanelVisibility(this.levelUpPanel, false);
        GameController.SetPanelVisibility(this.retry, false);

        this.SetHealthBarMax(this.health);
        this.rangeCollider = GetComponent<SphereCollider>();
        this.animator = GetComponentInChildren<Animator>();

        // Movement, dodges and attack lunges are translated by gameplay code.
        // Imported root curves must not add vertical drift or make the feet slide.
        if(this.animator != null) this.animator.applyRootMotion = false;

        this.LoadAttributes();

        // La sphère de portée servait de hitbox : elle touchait tout autour du joueur,
        // dos compris. La frappe passe maintenant par un cône (PlayerCombat), donc on
        // coupe le collider plutôt que de le laisser générer des triggers inutiles.
        if(this.rangeCollider != null) this.rangeCollider.enabled = false;

        CombatLayers.ApplyPlayerLayer(gameObject);

        this.stamina = gameObject.AddComponent<Stamina>();
        this.stamina.Configure(this.baseStamina, this.baseStaminaRegen, this.staminaRegenDelay);

        this.combat = gameObject.AddComponent<PlayerCombat>();
        this.combat.Initialize(this, this.audio, this.stamina);

        this.SetupCameraAndFeedback();

        // maxValue était figé à 1 : la jauge se remplissait dès le premier point d'XP
        // alors que le passage de niveau en demande xpToNext (5). On la cale sur le
        // vrai palier, comme le fait déjà SetXPBarMax après chaque montée.
        this.xpBar.maxValue = Mathf.Max(this.xpToNext, 1);
        this.xpBar.value = 0;

        this.healthBar.maxValue = this.GetHealth();
        this.healthBar.value = this.GetHealth();

        this.AddWeaponsDropper();
        this.DisableWeapons();
        this.SetAttackIcon(true);
        this.BindStaminaBar();
        this.BuildChargeIndicator();
        this.combat.ChargeChanged += this.SetChargeDisplay;

        // La barre d'XP n'est plus touchée par le code : c'est ta mise en page, et
        // la désactiver depuis un script rend son réglage impossible dans la scène.
        // Si son libellé te gêne, supprime-le directement dans la hiérarchie.
    }

    void Update() {

        if(!this.isAlive) return;


        foreach (var kvp in keyActions) {
            if (Input.GetKeyDown(kvp.Key)) {
                kvp.Value.Invoke();
            }
        }

        // Le sprint était gratuit et illimité : il permettait de distancer les ennemis
        // indéfiniment. Il coûte maintenant de l'endurance et s'arrête à sec.
        bool wantsSprint = Input.GetKey(KeyCode.LeftShift)
                        && Input.GetAxisRaw("Vertical") >= 0f
                        && (this.combat == null || !this.combat.IsCharging)
                        && (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f);

        if(wantsSprint && this.stamina != null)
            wantsSprint = this.stamina.Drain(SPRINT_STAMINA_PER_SECOND);

        this.isSprinting = wantsSprint;
        this.sprint = wantsSprint ? 2 : 0;

        // L'input d'attaque est lu par PlayerCombat, qui possède la machine à états.
    }

    public void AddWeaponsDropper() {
        this.weaponsDropper = GameObject.FindGameObjectWithTag("WeaponsDropper").GetComponent<WeaponsDropper>();
    }

    private void ShowRetryMenu() {
        GameController.SetCursorVisibility(true);
        GameController.SetGameState(false);
        GameController.SetPanelVisibility(this.retry, true);
    }

    public void ConfigureQuestCanvas() {
        this.questScreen = GameObject.FindGameObjectWithTag("QuestCanvas").GetComponent<Canvas>();
        this.questMenu = GameObject.FindGameObjectWithTag("QuestCanvas");
    }

    private void TogglePauseMenu() {
        if(this.canResume && this.isAlive) {
            GameController.SetGameState(false);
            this.SetInPause(true);
            this.ManagePauseMenu();
        }
    }

    private void ToggleQuestMenu() {
        if(this.canResume) {
            if(GameController.GetGameObjectAlpha(this.questMenu) > 0f) {
                GameController.SetMenuAlpha(this.questMenu, 0f);
            } else {
                GameController.SetMenuAlpha(this.questMenu, 1f);
            }
        }
    }

    public void ManagePauseMenu() {
        
        if(this.inPause) {
            GameController.ShowPauseMenu(pauseMenu);
            this.audio.PlayPauseMenuSFX();
            this.audio.StopThemeSFX();
            this.audio.StopBossThemeSFX();
        } else {
            GameController.HidePauseMenu(pauseMenu);
            this.audio.StopPauseMenuSFX();

            if(this.sceneController.GetSceneName() != Names.Scenes[3])
                this.audio.PlayThemeSFX();
            else
                this.audio.PlayBossThemeSFX(0);
        }
    }

    void FixedUpdate() {

        if(!this.isAlive) return;
        
        this.Move();

        // L'orientation du corps est pilotée par CameraRig, dans Update : la faire ici
        // la calait sur la fréquence physique et produisait une visée saccadée.

        this.TimerRegeneration();
    }

    private void LoadAttributes() {
        TextAsset baseStats     = Resources.Load<TextAsset>("Data/PlayerBaseStats");
        TextAsset increaseStats = Resources.Load<TextAsset>("Data/PlayerIncreaseStats");

        if(baseStats != null && increaseStats != null) {
            PlayerBaseStats playerBaseStats = JsonUtility.FromJson<PlayerBaseStats>(baseStats.text);
            PlayerIncreaseStats playerIncreaseStats = JsonUtility.FromJson<PlayerIncreaseStats>(increaseStats.text);


            this.totalXP              = playerBaseStats.totalXP;
            this.xp                   = playerBaseStats.xp;
            this.xpToNext             = playerBaseStats.xpToNext;
            this.level                = playerBaseStats.level;
            this.health               = playerBaseStats.health;
            this.maxActualHealth      = playerBaseStats.health;
            this.resistance           = playerBaseStats.resistance;
            this.attack               = playerBaseStats.attack;
            this.attackSpeed          = playerBaseStats.attackSpeed;
            this.range                = playerBaseStats.range;
            this.speed                = playerBaseStats.speed;
            this.knockback            = playerBaseStats.knockback;
            this.regeneration         = playerBaseStats.regeneration;

            // Valeurs par défaut si le JSON n'a pas encore les champs d'endurance.
            if(playerBaseStats.stamina > 0f)           this.baseStamina        = playerBaseStats.stamina;
            if(playerBaseStats.staminaRegen > 0f)      this.baseStaminaRegen   = playerBaseStats.staminaRegen;
            if(playerBaseStats.staminaRegenDelay > 0f) this.staminaRegenDelay  = playerBaseStats.staminaRegenDelay;

            this.maxLevel             = playerBaseStats.maxLevel;
            this.xpRequired           = playerBaseStats.xpToNext;

            this.increaseHealth       = playerIncreaseStats.increaseHealth;
            this.increaseResistance   = playerIncreaseStats.increaseResistance;
            this.increaseAttack       = playerIncreaseStats.increaseAttack;
            this.increaseAttackSpeed  = playerIncreaseStats.increaseAttackSpeed;
            this.increaseRange        = playerIncreaseStats.increaseRange;
            this.increaseSpeed        = playerIncreaseStats.increaseSpeed;
            this.increaseKnockback    = playerIncreaseStats.increaseKnockback;
            this.increaseRegeneration = playerIncreaseStats.increaseRegeneration;
            this.increaseStamina      = playerIncreaseStats.increaseStamina;
            this.increaseStaminaRegen = playerIncreaseStats.increaseStaminaRegen;
        }
    }

    public void TimerRegeneration() {
        if(this.timeSinceLastRegeneration >= this.regenerationDelay) {  
            this.timeSinceLastRegeneration = 0f;
            this.Heal(this.regeneration + this.weaponRegeneration);
        }

        this.timeSinceLastRegeneration += Time.fixedDeltaTime;
    }

    // Point d'entrée unique des dégâts subis. Il faut connaître l'attaquant pour
    // savoir si le coup arrive de face (parade) ou dans le dos, et pour pouvoir
    // le stagger sur une parade réussie.
    public void TakeHit(DamageInfo info) {

        if(!this.isAlive) return;

        int damage = info.amount;
        DefenseResult result = DefenseResult.Hit;

        if(this.combat != null)
            result = this.combat.ResolveIncoming(info, out damage);

        Vector3 point = (info.hitPoint != Vector3.zero) ? info.hitPoint : transform.position + Vector3.up;

        if(CombatFeedback.Instance != null) {
            switch(result) {
                case DefenseResult.Parried:     CombatFeedback.Instance.OnParried(point); break;
                case DefenseResult.Blocked:     CombatFeedback.Instance.OnBlocked(point); break;
                case DefenseResult.GuardBroken: CombatFeedback.Instance.OnGuardBroken(); break;
            }
        }

        if(result == DefenseResult.Invulnerable || result == DefenseResult.Parried)
            return;

        // Brèves i-frames après un coup encaissé : sans elles, une horde de 40 ennemis
        // vidait la barre de vie en une poignée de frames, sans laisser réagir.
        if(Time.time < this.hurtInvulnerableUntil) return;

        if(result != DefenseResult.Blocked) {
            this.hurtInvulnerableUntil = Time.time + HURT_INVULNERABILITY;

            if(CombatFeedback.Instance != null)
                CombatFeedback.Instance.OnPlayerHurt(damage);
        }

        this.ApplyDamage(damage);
    }

    // Conservé pour les appels qui n'ont pas d'attaquant à fournir.
    public void TakeDamage(int damage) {
        this.TakeHit(new DamageInfo(damage, 0f, 0f, null));
    }

    private void ApplyDamage(int damage) {

        if(damage <= 0) return;

        this.health -= Mathf.RoundToInt(damage * (1.0f - (float) this.resistance / 100.0f));
        this.hudStats.UpdateHealth();

        if(this.health <= 0) {
            this.Death();
        } else {
            this.SetHealthBar(this.health);
        }
    }

    public Transform GetTransform() {
        return transform;
    }

    private void Death() {
        this.isAlive = false;
        Invoke("CameraDeathAnimation", 0.5f);
        this.audio.PlayDeathSFX();
        this.audio.StopThemeSFX();
        this.audio.StopBossThemeSFX();
        this.DanceTriggered();
    }

    private void CameraDeathAnimation() {
        GameController.SetCanvasVisibility(new Canvas[] { this.hudScreen, this.questScreen }, false);
        
        if(this.sceneController.GetSceneName() == Names.Scenes[3])
            GameController.SetCanvasVisibility(this.bossCanvas, false);
                
        string deathRnd = "Death_" + GameController.Random(1, 3);
        this.ChangeAnimationState(deathRnd);
        this.camera.EnableBlackAndWhiteEffect();
        GameController.SetGameState(0.3f);
        Invoke("DeathScreen", 0.55f);
    }

    private void DeathScreen() {
        GameController.SetCanvasVisibility(deathScreen, true);
        progressiveDarkening.StartFading();
        Invoke("ShowRetryMenu", 1f);
    }

    private void DanceTriggered() {
        GameObject enemiesParent = GameObject.Find("Enemies");
        EnemyController[] enemies = enemiesParent.GetComponentsInChildren<EnemyController>();
        
        foreach (EnemyController enemy in enemies)
            enemy.Dance();
    }

    public void SetCanResume(bool statut) {
        this.canResume = statut;
    }

    private int XPRequired() {
        return (int)(5 * Math.Pow(1.1, this.level - 1));
    }

    // Updates / Increments

    // Les tableaux d'increase n'ont que 5 paliers. L'UI masque une stat au-delà,
    // mais rien ne le garantissait côté code : un palier de plus levait un
    // IndexOutOfRange. On borne sur la dernière valeur.
    private static int Step(int[] table, int level) {
        if(table == null || table.Length == 0) return 0;
        return table[Mathf.Clamp(level - 1, 0, table.Length - 1)];
    }

    private static float Step(float[] table, int level) {
        if(table == null || table.Length == 0) return 0f;
        return table[Mathf.Clamp(level - 1, 0, table.Length - 1)];
    }

    public void UpdateResistance() {
        this.resistance += Step(this.increaseResistance, this.resistanceLevel);
        this.resistanceLevel++;
        if(this.resistanceLevel > 5) this.hudStats.MaxResistance();
    }

    public void UpdateAttackSpeed() {
        this.attackSpeed += Step(this.increaseAttackSpeed, this.attackSpeedLevel);
        this.attackSpeedLevel++;
        if(this.attackSpeedLevel > 5) this.hudStats.MaxAttackSpeed();
    }

    public void UpdateRange() {
        this.range += Step(this.increaseRange, this.rangeLevel);
        this.rangeLevel++;
        if(this.rangeLevel > 5) this.hudStats.MaxRange();
    }

    public void UpdateHealth() {
        int gain = Step(this.increaseHealth, this.healthLevel);

        this.health += gain;
        this.maxActualHealth += gain;
        this.SetMaxHealthBar(this.maxActualHealth);

        this.healthLevel++;
        this.SetHealthBar(this.health);
        this.hudStats.UpdateHealth();
    }

    public void UpdateAttack() {
        this.attack += Step(this.increaseAttack, this.attackLevel);
        this.knockback += Step(this.increaseKnockback, this.attackLevel);
        this.attackLevel++;
        if(this.attackLevel > 5) {
            this.hudStats.MaxAttack();
            this.hudStats.MaxKnockback();
        }
    }

    public void UpdateSpeed() {
        this.speed += Step(this.increaseSpeed, this.speedLevel);
        this.speedLevel++;
        if(this.speedLevel > 5) this.hudStats.MaxSpeed();
    }

    public void UpdateRegeneration() {
        this.regeneration += Step(this.increaseRegeneration, this.regenerationLevel);
        this.regenerationLevel++;
        if(this.regenerationLevel > 5) this.hudStats.MaxRegeneration();
    }

    public void UpdateStamina() {
        this.staminaBonus += Step(this.increaseStamina, this.staminaLevel);
        this.staminaLevel++;
        this.RefreshStamina();
    }

    public void UpdateStaminaRegen() {
        this.staminaRegenBonus += Step(this.increaseStaminaRegen, this.staminaRegenLevel);
        this.staminaRegenLevel++;
        this.RefreshStamina();
    }

    public void IncrementKillCounter() {
        this.enemyKillCounter++;
    }

    public void IncrementStatCounter() {
        this.hudStats.UpdateStats();
    }


    // Animations
    private void Move() {

        // Pendant la roulade, le déplacement et l'animation sont pilotés par
        // PlayerCombat : MoveAnims écraserait sinon le clip de roulade dès la frame
        // suivante, la couche de déplacement étant réévaluée à chaque FixedUpdate.
        if(this.combat != null && this.combat.IsDodging) return;

        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        // Engagement : pendant un coup, la vitesse s'effondre et la marche arrière est
        // coupée. C'est ce qui empêche de reculer indéfiniment en frappant.
        float combatMultiplier = 1f;

        if(this.combat != null) {
            combatMultiplier = this.combat.MoveSpeedMultiplier;

            if(this.combat.LockBackward && verticalInput < 0f)
                verticalInput = 0f;
        }

        Vector3 movement = new Vector3(horizontalInput, 0f, verticalInput).normalized;
        float directionMultiplier = (verticalInput < -0.01f) ? BACKWARD_SPEED_MULTIPLIER : 1f;
        float moveSpeed = (this.GetSpeed() + this.GetWeaponSpeed() + this.sprint)
                        * combatMultiplier
                        * directionMultiplier;

        transform.Translate(movement * moveSpeed * Time.fixedDeltaTime * 2f);

        MoveAnims(horizontalInput, verticalInput);
    }


    private void MoveAnims(float horizontalInput, float verticalInput) {

        string animationState = (this.GetIsDancing()) ? null : "Idle";

        if(verticalInput > 0) {
            if(horizontalInput > 0 ) animationState = this.isSprinting ? "Sprint_Forward_Right" : "Strafe_Forward_Right";
            if(horizontalInput < 0 ) animationState = this.isSprinting ? "Sprint_Forward_Left"  : "Strafe_Forward_Left";
            if(horizontalInput == 0) animationState = this.isSprinting ? "Sprint_Forward"       : "Walk_Forward";
        }

        if(verticalInput < 0) {
            if(horizontalInput > 0 ) animationState = "Strafe_Back_Right";
            if(horizontalInput < 0 ) animationState = "Strafe_Back_Left";
            if(horizontalInput == 0) animationState = "Walk_Back";
        }

        if(verticalInput == 0) {
            if(horizontalInput > 0) animationState = this.isSprinting ? "Sprint_Right" : "Strafe_Right";
            if(horizontalInput < 0) animationState = this.isSprinting ? "Sprint_Left"  : "Strafe_Left";
        }

        if (animationState != null) this.ChangeAnimationState(animationState);
    }

    public void SetIsDancing(bool state) {
        this.isDancing = state;
    }

    public void DisableIsDancing() {
        this.isDancing = false;
    }
    
    public bool GetIsDancing() {
        return this.isDancing;
    }

    public void ChangeAnimationState(string newAnimation) {
        if(this.currentAnimation == newAnimation) return;

        if(Array.IndexOf(this.secondLayerAnimations, newAnimation) != -1)
            this.animator.CrossFade(newAnimation, 0.2f, 1);
        else
            this.animator.CrossFade(newAnimation, 0.2f, 0);

        this.currentAnimation = newAnimation;
    }


    public void WeaponAppearance() {
        GameObject.Find("WeaponHolder").transform.localScale = new Vector3(0.1295791f, 0.1295791f, 0.1295791f);
    }


    // La détection de touche vit désormais dans PlayerCombat (cône orienté).

    public void DisableAttack() {
        if(this.combat != null) this.combat.CancelAttack();
    }

    // Joue un état d'attaque sur l'UpperBody Layer, en calant sa vitesse sur la durée
    // réelle du coup. On ne passe pas par ChangeAnimationState : celui-ci partage un
    // seul champ currentAnimation entre les deux layers et refuserait de rejouer le
    // même état deux fois de suite, ce qui bloquait les enchaînements.
    public float PlayAttackAnimation(string stateName, float targetDuration) {
        if(this.animator == null) return Mathf.Max(targetDuration, 0.01f);

        int hash = Animator.StringToHash(stateName);
        string resolvedStateName = stateName;

        // Les clips par famille n'existeront dans le controller qu'une fois les états
        // ajoutés : tant qu'ils manquent, on retombe sur l'attaque d'origine.
        if(!this.animator.HasState(UPPER_BODY_LAYER, hash)) {
            resolvedStateName = "Attack";
            hash = Animator.StringToHash(resolvedStateName);
            if(!this.animator.HasState(UPPER_BODY_LAYER, hash))
                return Mathf.Max(targetDuration, 0.01f);
        }

        AnimationClip clip = this.FindAttackClip(resolvedStateName);
        float clipLength = (clip != null) ? clip.length : Mathf.Max(targetDuration, 0.01f);
        float speed = Mathf.Clamp(clipLength / Mathf.Max(targetDuration, 0.01f), 0.5f, 1.8f);

        this.animator.SetFloat("AttackSpeed", speed);
        this.animator.CrossFade(hash, 0.08f, UPPER_BODY_LAYER, 0f);
        return clipLength / speed;
    }

    private AnimationClip FindAttackClip(string stateName) {
        if(this.animator == null || this.animator.runtimeAnimatorController == null) return null;

        AnimationClip[] clips = this.animator.runtimeAnimatorController.animationClips;
        string normalizedState = NormalizeAnimationName(stateName);

        for(int i = 0; i < clips.Length; i++) {
            AnimationClip clip = clips[i];
            if(clip != null && NormalizeAnimationName(clip.name) == normalizedState)
                return clip;
        }

        // Les trois etats de combo polearm peuvent partager le clip officiel importe,
        // dont le nom de sous-asset reste HumanM@AttackPolearm01.
        if(normalizedState.StartsWith("polearmthrust")) {
            for(int i = 0; i < clips.Length; i++) {
                AnimationClip clip = clips[i];

                if(clip != null && NormalizeAnimationName(clip.name).Contains("attackpolearm01"))
                    return clip;
            }
        }

        // L'etat historique "Attack" pointe vers ce clip dans KingMovement.controller.
        if(stateName == "Attack") {
            for(int i = 0; i < clips.Length; i++) {
                AnimationClip clip = clips[i];
                if(clip != null && clip.name == "WK_heavy_infantry_08_attack_B")
                    return clip;
            }
        }

        return null;
    }

    private static string NormalizeAnimationName(string value) {
        return string.IsNullOrEmpty(value)
            ? ""
            : value.Replace("_", "").Replace(" ", "").ToLowerInvariant();
    }

    // Rend la main à la couche de déplacement : l'état Attack n'a aucune transition
    // sortante, sans ça le haut du corps reste figé sur la dernière frame du coup.
    public void StopAttackAnimation() {
        if(this.animator == null) return;

        int hash = Animator.StringToHash("Empty");

        if(this.animator.HasState(UPPER_BODY_LAYER, hash))
            this.animator.CrossFade(hash, 0.12f, UPPER_BODY_LAYER, 0f);

        if(Array.IndexOf(this.secondLayerAnimations, this.currentAnimation) != -1)
            this.currentAnimation = null;
    }

    // Roulade : joue sur la couche de déplacement, corps entier.
    public void PlayRollAnimation(string stateName) {
        if(this.animator == null) return;

        int hash = Animator.StringToHash(stateName);

        // Les états de roulade n'existent qu'une fois le controller enrichi
        // (Tools/Monarchy/Combat). Sans eux, la roulade reste jouable, sans animation.
        if(!this.animator.HasState(0, hash)) return;

        this.animator.CrossFade(hash, 0.05f, 0, 0f);
        this.currentAnimation = stateName;
    }

    public void PlayBlockAnimation() {
        this.PlayUpperBodyState("Block", 1f);
    }

    public void PlayStunAnimation() {
        this.PlayUpperBodyState("Stunned", 1f);
    }

    private void PlayUpperBodyState(string stateName, float speed) {
        if(this.animator == null) return;

        int hash = Animator.StringToHash(stateName);
        if(!this.animator.HasState(UPPER_BODY_LAYER, hash)) return;

        this.animator.CrossFade(hash, 0.1f, UPPER_BODY_LAYER, 0f);
    }

    // Traînée sur la lame, active uniquement pendant la fenêtre de frappe. C'est le
    // repère le plus lisible pour savoir quand le coup porte réellement, et ça ne
    // coûte qu'un TrailRenderer créé à la volée.
    public void SetWeaponTrail(bool emitting) {

        Transform weapon = this.GetActiveWeapon();
        if(weapon == null) return;

        TrailRenderer trail = weapon.GetComponentInChildren<TrailRenderer>(true);
        if(trail == null) trail = this.CreateWeaponTrail(weapon);
        if(trail == null) return;

        trail.emitting = emitting;
        if(!emitting) trail.Clear();
    }

    private Transform GetActiveWeapon() {

        if(this.weaponHolder == null || this.weaponID <= 0) return null;

        string expected = "weapon_" + this.weaponID;

        foreach(Transform child in this.weaponHolder.transform)
            if(child.gameObject.activeSelf && child.gameObject.name == expected)
                return child;

        return null;
    }

    private TrailRenderer CreateWeaponTrail(Transform weapon) {

        Renderer renderer = weapon.GetComponentInChildren<Renderer>();
        if(renderer == null) return null;

        // Placé sur la pointe de l'arme plutôt qu'à sa racine, sinon la traînée
        // part du poing.
        GameObject tip = new GameObject("TrailTip");
        tip.transform.SetParent(weapon, false);
        tip.transform.position = renderer.bounds.center + Vector3.up * renderer.bounds.extents.magnitude * 0.8f;

        TrailRenderer trail = tip.AddComponent<TrailRenderer>();
        trail.time = 0.16f;
        trail.startWidth = 0.22f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.03f;
        trail.autodestruct = false;
        trail.emitting = false;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = new Color(1f, 0.95f, 0.8f, 0.55f);
        trail.endColor = new Color(1f, 0.85f, 0.5f, 0f);

        return trail;
    }

    // Appelée depuis Awake : une exception ici interromprait toute l'initialisation
    // du joueur, et le HUD resterait figé sur ses valeurs d'auteur.
    public void SetAttackIcon(bool ready) {
        if(this.hudStats != null) this.hudStats.ChangeEnableAttackIcon(ready);
    }

    public bool IsSprinting() {
        return this.isSprinting;
    }

    public Stamina GetStamina() {
        return this.stamina;
    }

    // La caméra reste dans le prefab (toutes les références sérialisées, dont le
    // post-process de mort, restent valides) mais CameraRig la déparente au runtime
    // pour lui donner un bras télescopique, du pitch et de la collision.
    private void SetupCameraAndFeedback() {

        if(this.camera != null) {
            this.cameraRig = this.camera.gameObject.AddComponent<CameraRig>();
            this.cameraRig.Initialize(transform);
        }

        CombatFeedback feedback = gameObject.AddComponent<CombatFeedback>();
        feedback.Initialize(this.cameraRig, this.hudScreen);
    }

    // La barre d'endurance est un vrai élément du HUD, placé et stylé dans la scène.
    // Le clonage au runtime n'était qu'un contournement pour éviter d'éditer les
    // scènes : il héritait des libellés de la barre de vie et de sa couleur, donc
    // se confondait avec elle. On se contente maintenant de la piloter.
    private void BindStaminaBar() {

        // Non assignée dans l'inspecteur : on la retrouve par son nom, à côté de la
        // barre de vie, pour que ça marche sans câblage manuel.
        if(this.staminaBar == null && this.healthBar != null && this.healthBar.transform.parent != null) {

            Transform parent = this.healthBar.transform.parent;
            Transform found = parent.Find("Stamina Bar");

            if(found == null) found = parent.Find("StaminaBar");
            if(found != null) this.staminaBar = found.GetComponent<Slider>();
        }

        if(this.staminaBar == null) {
            Debug.LogWarning("[Stamina] Aucune barre d'endurance trouvée. Assigne le Slider dans l'inspecteur du PlayerController, ou nomme-le \"Stamina Bar\" à côté de la barre de vie.");
            return;
        }

        // Héritée de la barre de vie, qui travaille en entiers : sur une plage 0..1,
        // wholeNumbers ne laisse passer que 0 ou 1, d'où une jauge en tout ou rien.
        this.staminaBar.wholeNumbers = false;
        this.staminaBar.minValue = 0f;
        this.staminaBar.maxValue = 1f;
        this.staminaBar.value = 1f;

        this.stamina.Changed += this.RefreshStaminaBar;
        this.RefreshStaminaBar();
    }

    private void RefreshStaminaBar() {
        if(this.staminaBar != null && this.stamina != null)
            this.staminaBar.value = this.stamina.Normalized;
    }

    private void OnDestroy() {
        if(this.combat != null)
            this.combat.ChargeChanged -= this.SetChargeDisplay;

        if(this.stamina != null)
            this.stamina.Changed -= this.RefreshStaminaBar;
    }

    private void BuildChargeIndicator() {

        if(this.chargeIndicator != null || this.staminaBar == null) return;

        RectTransform staminaRect = this.staminaBar.transform as RectTransform;
        RectTransform parent = (staminaRect != null) ? staminaRect.parent as RectTransform : null;
        if(staminaRect == null || parent == null) return;

        this.chargeIndicator = new GameObject("Charge Indicator", typeof(RectTransform), typeof(Image));
        RectTransform backgroundRect = this.chargeIndicator.GetComponent<RectTransform>();
        backgroundRect.SetParent(parent, false);
        backgroundRect.anchorMin = staminaRect.anchorMin;
        backgroundRect.anchorMax = staminaRect.anchorMax;
        backgroundRect.pivot = staminaRect.pivot;
        backgroundRect.anchoredPosition = staminaRect.anchoredPosition
                                        + Vector2.up * (Mathf.Max(staminaRect.rect.height, 8f) + 6f);
        backgroundRect.sizeDelta = new Vector2(staminaRect.sizeDelta.x, 6f);

        Image background = this.chargeIndicator.GetComponent<Image>();
        background.color = new Color(0.06f, 0.05f, 0.03f, 0.82f);
        background.raycastTarget = false;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.SetParent(backgroundRect, false);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(1f, 1f);
        fillRect.offsetMax = new Vector2(-1f, -1f);

        this.chargeFill = fillObject.GetComponent<Image>();
        Image staminaFillImage = (this.staminaBar.fillRect != null)
                               ? this.staminaBar.fillRect.GetComponent<Image>()
                               : null;

        if(staminaFillImage != null)
            this.chargeFill.sprite = staminaFillImage.sprite;

        this.chargeFill.color = new Color(1f, 0.72f, 0.12f, 0.95f);
        this.chargeFill.type = Image.Type.Filled;
        this.chargeFill.fillMethod = Image.FillMethod.Horizontal;
        this.chargeFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        this.chargeFill.fillAmount = 0f;
        this.chargeFill.raycastTarget = false;

        this.chargeIndicator.SetActive(false);
    }

    private void SetChargeDisplay(float normalized, bool visible) {
        if(this.chargeIndicator == null || this.chargeFill == null) return;

        this.chargeFill.fillAmount = Mathf.Clamp01(normalized);
        if(this.chargeIndicator.activeSelf != visible)
            this.chargeIndicator.SetActive(visible);
    }

    // Les améliorations de capacité et de régénération sont indépendantes.
    private void RefreshStamina() {
        if(this.stamina == null) return;

        this.stamina.SetMax(this.baseStamina + this.staminaBonus);
        this.stamina.SetRegen(this.baseStaminaRegen + this.staminaRegenBonus);
    }

    private void TakeWeapon() {
        var weapon = GetTheNearestWeapon();
        if(weapon != null && this.combat != null) this.combat.CancelAttack();
        GetTheWeaponDatas(weapon);
    }

    private void GetTheWeaponDatas(Weapon weapon) {

        if(weapon != null) {

            if(this.weaponName != "") {
                this.weaponsDropper.CreateWeapon(this.weaponID - 1, transform.position);
            }

            this.weaponID = weapon.id;
            this.weaponName = weapon.weaponName;
            this.weaponFamily = weapon.family;
            this.weaponAmmo = WeaponFamilyLibrary.Get(weapon.family).ammo;
            this.weaponAttack = weapon.attack;
            this.weaponAttackSpeed = weapon.attackSpeed;
            this.weaponKnockback = weapon.knockback;
            this.weaponRange = weapon.range;
            this.weaponRegeneration = weapon.regeneration;
            this.weaponSpeed = weapon.speed;

            GameController.DestroyWeapon(weapon);
            this.hudStats.UpdateStats();

            string weaponNameInHand = "weapon_" + this.weaponID;

            foreach(Transform child in weaponHolder.transform) {
                if(child.gameObject.name == weaponNameInHand) {
                    child.gameObject.SetActive(true);
                } else {
                    child.gameObject.SetActive(false);
                }
            }
        }
    }

    private void DisableWeapons() {
        foreach(Transform child in weaponHolder.transform) {
            child.gameObject.SetActive(false);
        }
    }

    private Weapon GetTheNearestWeapon() {

        GameObject weaponsObject = GameObject.Find("Weapons");

        if(weaponsObject != null) {

            Weapon[] weapons = weaponsObject.GetComponentsInChildren<Weapon>();
            
            float minimalDistance = 5.0f;
            Weapon nearestWeapon = null;

            foreach(var weapon in weapons) {
                float distance = Vector3.Distance(transform.position, weapon.transform.position);

                if(distance < minimalDistance) {
                    minimalDistance = distance;
                    nearestWeapon = weapon;
                }
            }

            if(nearestWeapon != null) 
                return nearestWeapon;
        }
        
        return null;
    }


    // Getters
    public bool IsAlive()                { return this.isAlive;            }
    public bool GetCanResume()           { return this.canResume && this.isAlive; }
    public int GetKillCounter()          { return this.enemyKillCounter;   }
 
    public int GetResistance()           { return this.resistance;         }
    public int GetAttack()               { return this.attack;             }
    public int GetHealth()               { return this.health;             }
    public int GetMaxActualHealth()      { return this.maxActualHealth;    }
    public int GetLevel()                { return this.level;              }
    public float GetAttackSpeed()        { return this.attackSpeed;        }
    public float GetRange()              { return this.range;              }
    public float GetSpeed()              { return this.speed;              }
    public float GetRegeneration()       { return this.regeneration;       }
    public float GetKnockback()          { return this.knockback;          }
    
    public int GetHealthLevel()          { return this.healthLevel;        }
    public int GetResistanceLevel()      { return this.resistanceLevel;    }
    public int GetAttackLevel()          { return this.attackLevel;        }
    public int GetAttackSpeedLevel()     { return this.attackSpeedLevel;   }
    public int GetRangeLevel()           { return this.rangeLevel;         }
    public int GetSpeedLevel()           { return this.speedLevel;         }
    public int GetRegenerationLevel()    { return this.regenerationLevel;  }
    public int GetStaminaLevel()         { return this.staminaLevel;       }
    public int GetStaminaRegenLevel()    { return this.staminaRegenLevel;  }
    public float GetMaxStamina()         { return (this.stamina != null) ? this.stamina.Max : this.baseStamina; }
    public float GetStaminaRegen()       { return (this.stamina != null) ? this.stamina.RegenPerSecond : this.baseStaminaRegen; }
    
    public int GetWeaponAttack()         { return this.weaponAttack;       }
    // Jamais null : Weapon.family et Weapon.weaponName ne sont renseignés que dans
    // le Start() de l'arme, et un ramassage plus tôt les laisserait à null — le HUD
    // afficherait alors littéralement "null".
    public string GetWeaponName()        { return this.weaponName ?? "";    }
    public int GetWeaponID()             { return this.weaponID;           }
    public int GetWeaponAmmo()           { return this.weaponAmmo;         }
    public WeaponsDropper GetWeaponsDropper() { return this.weaponsDropper; }

    // Mains nues quand rien n'est équipé : coups de poing plutôt qu'une épée invisible.
    public string GetWeaponFamily() {
        return string.IsNullOrEmpty(this.weaponName) ? "unarmed" : this.weaponFamily;
    }

    // La caméra est un enfant du joueur et n'est pas taguée MainCamera :
    // Camera.main renverrait null.
    public Camera GetPlayerCamera() {
        if(this.playerCamera == null && this.camera != null)
            this.playerCamera = this.camera.GetComponent<Camera>();

        return this.playerCamera;
    }

    // Une arme de jet lancée quitte la main : à court de munitions, on se retrouve
    // désarmé, ce qui est plus lisible qu'une arme vide qui reste équipée.
    public void ConsumeWeaponAmmo() {
        if(this.weaponAmmo <= 0) return;

        this.weaponAmmo--;

        if(this.weaponAmmo <= 0) {
            this.weaponID = 0;
            this.weaponName = "";
            this.weaponFamily = "";
            this.weaponAttack = 0;
            this.weaponAttackSpeed = 0f;
            this.weaponKnockback = 0f;
            this.weaponRange = 0f;
            this.weaponRegeneration = 0;
            this.weaponSpeed = 0f;

            this.DisableWeapons();
        }

        this.hudStats.UpdateStats();
    }
    public float GetWeaponRange()        { return this.weaponRange;        }
    public float GetWeaponAttackSpeed()  { return this.weaponAttackSpeed;  }
    public float GetWeaponKnockback()    { return this.weaponKnockback;    }
    public float GetWeaponSpeed()        { return this.weaponSpeed;        }
    public float GetWeaponRegeneration() { return this.weaponRegeneration; }
    public Animator GetAnimator()        { return this.animator;           }

    public Canvas GetQuestCanvas() { return this.questScreen; }
    public bool IsQuestCanvasVisible() { return this.questMenu.activeSelf; }

    // Setters

    public void SetInPause(bool state) {
        this.inPause = state;
    } 

    private void SetXPBar(int xp) {
        this.xpBar.value = xp;
    }

    private void AddXPBar(int xp) {
        this.xpBar.value += xp;
    } 

    private void SetXPBarMax(int max) {
        this.xpBar.maxValue = max;
    }

    private void SetHealthBar(int hp) {
        this.healthBar.value = hp;
    }

    private void SetMaxHealthBar(int hpMax) {
        this.healthBar.maxValue = hpMax;
    }

    private void SetHealthBarMax(int max) {
        this.healthBar.maxValue = max;
    }

    public void SetRotation(bool state) {
        this.sensitivity = state ? 10 : 0;

        if(this.cameraRig != null) this.cameraRig.SetInputEnabled(state);
    }

    public void Heal(int healAmount) {
        this.health += healAmount;
        if(this.health >= this.maxActualHealth) 
            this.health = this.maxActualHealth;
        
        this.SetHealthBar(this.health);
        this.hudStats.UpdateHealth();
    }

    public void XPGain(int xpAmount) {
        this.totalXP += xpAmount;
        this.AddXPBar(xpAmount);
        
        if(this.totalXP >= this.xpRequired && this.level < this.maxLevel) {
            this.level++;
            this.xpToNext = XPRequired();
            this.xpRequired += this.xpToNext;
            this.xp = 0;

            this.SetXPBarMax(this.xpToNext);
            this.SetXPBar(0);

            this.SetCanResume(false);
            GameController.SetCanvasVisibility(this.questScreen, false);
            this.levelUpChoice.UpdateStatsDisplay();
        }
    }

    public void IncrementDanceCounter() {
        this.danceCounter++;
    }

    public int GetDanceCounter() { 
        return this.danceCounter;   
    }

    public void InitSceneController() {
        this.sceneController = GameObject.Find("Out").GetComponent<SceneController>();
    }
    
    public void InitBossCanvas() {
        this.bossCanvas = GameObject.FindGameObjectWithTag("BossCanvas").GetComponent<Canvas>();
    }
}
