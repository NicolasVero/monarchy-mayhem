using System;
using System.Collections.Generic;
using UnityEngine;

public enum CombatState {
    Ready,
    Charging,
    Windup,
    Active,
    Recovery,
    Dodging,
    Blocking,
    Staggered
}

// Ce qu'il advient d'un coup reçu par le joueur.
public enum DefenseResult {
    Hit,
    Invulnerable,
    Blocked,
    GuardBroken,
    Parried
}

// Machine à états de l'attaque du joueur.
//
// Remplace l'ancien couple canAttack / goingAttack : l'attaque était une fenêtre fixe
// de 0.2 s ouverte après le clic, les dégâts tombaient via un Invoke à part, et le
// cooldown tournait sur un battement libre jamais remis à zéro. Le joueur gardait
// 100 % de sa vitesse pendant le coup, donc reculer en frappant était gratuit.
//
// Ici, un coup engage : Windup -> Active -> Recovery, avec la vitesse bridée et la
// marche arrière verrouillée pendant la frappe. La hitbox est un cône orienté.
//
// Ajouté au runtime par PlayerController : rien à câbler sur le prefab.
public class PlayerCombat : MonoBehaviour {

    // Longueur approximative des clips d'attaque : sert à caler la vitesse
    // d'animation sur la durée réelle du coup.
    // attackSpeed de référence dans PlayerBaseStats.json. C'est un délai :
    // plus il est bas, plus l'attaque est rapide.
    private const float REFERENCE_ATTACK_DELAY = 2.0f;

    private const float INPUT_BUFFER = 0.25f;

    private const int PRIMARY_ATTACK_BUTTON = 0;
    private const int BLOCK_BUTTON = 1;
    private const int TECHNIQUE_BUTTON = 2;

    private const float CHARGE_MAX_HOLD = 1.20f;
    private const float CHARGE_MOVE_MULT = 0.35f;
    private const float CHARGE_DAMAGE_MIN = 1.35f;
    private const float CHARGE_DAMAGE_MAX = 2.40f;
    private const float CHARGE_POISE_MIN = 1.50f;
    private const float CHARGE_POISE_MAX = 3.00f;
    private const float CHARGE_KNOCKBACK_MIN = 1.20f;
    private const float CHARGE_KNOCKBACK_MAX = 2.00f;

    // Bornes de portée, mesurées depuis le centre du joueur. Les ennemis s'immobilisent
    // à environ 1,7 m : sous 2,1 la marge devient trop mince pour être fiable dès qu'ils
    // bougent un peu.
    private const float MIN_REACH = 2.1f;
    private const float MAX_REACH = 4.1f;

    // Roulade : couvre la distance en ralentissant, avec une fenêtre d'invincibilité
    // qui commence après le lancement — esquiver demande d'anticiper, pas de spammer.
    private const float DODGE_DURATION = 0.65f;
    private const float DODGE_DISTANCE = 4.0f;
    private const float DODGE_DISTANCE_SPRINT = 5.5f;
    private const float DODGE_IFRAME_START = 0.10f;
    private const float DODGE_IFRAME_END = 0.45f;
    private const float DODGE_STAMINA = 25f;
    private const float DODGE_RADIUS = 0.4f;

    // Parade : les premières fraction de seconde après l'appui sont une parade
    // parfaite. Au-delà, on encaisse en garde.
    private const float PARRY_WINDOW = 0.20f;
    private const float BLOCK_ANGLE = 120f;
    private const float BLOCK_DAMAGE_REDUCTION = 0.7f;
    private const float BLOCK_STAMINA_PER_DAMAGE = 1.2f;
    private const float BLOCK_MOVE_MULT = 0.45f;
    private const float GUARD_BREAK_DURATION = 1.1f;
    private const float PARRY_STAGGER_DURATION = 1.4f;
    private const float RIPOSTE_WINDOW = 2f;
    private const float RIPOSTE_MULT = 2f;

    private PlayerController player;
    private AudioController audioController;

    private CombatState state = CombatState.Ready;
    private float stateTimer;
    private float stateDuration;

    private WeaponFamilyStat family;
    private float timingMult = 1f;

    private int comboIndex;
    private int queuedComboSteps;
    private float dodgeBufferedAt = -1f;
    private float currentActiveDuration;
    private float currentRecoveryDuration;

    private float lungeSpeed;
    private bool swingPlayedSFX;
    private bool projectileFired;
    private float attackPressedAt = -1f;
    private float chargeNormalized;
    private bool techniqueSwing;

    private readonly HashSet<Transform> hitThisSwing = new HashSet<Transform>();
    private readonly List<HitCandidate> results = new List<HitCandidate>(16);

    private Stamina stamina;
    private Vector3 dodgeDirection;
    private float dodgeDistance;

    private bool blockHeld;
    private float riposteUntil = -1f;

    public CombatState State { get { return this.state; } }
    public bool IsDodging { get { return this.state == CombatState.Dodging; } }
    public bool IsCharging { get { return this.state == CombatState.Charging; } }
    public float ChargeNormalized { get { return this.chargeNormalized; } }
    public event Action<float, bool> ChargeChanged;

    public bool IsAttacking {
        get { return this.state == CombatState.Windup || this.state == CombatState.Active || this.state == CombatState.Recovery; }
    }

    // Fenêtre d'invincibilité de la roulade : elle s'ouvre après le lancement,
    // donc esquiver demande d'anticiper le coup et non de spammer la touche.
    public bool IsInvulnerable {
        get {
            return this.state == CombatState.Dodging
                && this.stateTimer >= DODGE_IFRAME_START
                && this.stateTimer <= DODGE_IFRAME_END;
        }
    }

    public bool IsBlocking { get { return this.state == CombatState.Blocking; } }
    public bool IsStaggered { get { return this.state == CombatState.Staggered; } }

    // Part de la vitesse de déplacement conservée : c'est l'engagement.
    public float MoveSpeedMultiplier {
        get {
            if(this.state == CombatState.Dodging)   return 0f;
            if(this.state == CombatState.Staggered) return 0f;
            if(this.state == CombatState.Blocking)  return BLOCK_MOVE_MULT;
            if(this.state == CombatState.Charging)  return CHARGE_MOVE_MULT;
            if(this.family == null) return 1f;

            switch(this.state) {
                case CombatState.Windup:   return this.family.moveMultWindup;
                case CombatState.Active:   return this.family.moveMultActive;
                case CombatState.Recovery: return this.family.moveMultRecovery;
                default:                   return 1f;
            }
        }
    }

    // Pendant la frappe, on ne recule plus : c'est ce qui tue le kiting.
    public bool LockBackward {
        get { return this.state == CombatState.Windup || this.state == CombatState.Active; }
    }

    public void Initialize(PlayerController owner, AudioController audioSource, Stamina staminaPool) {
        this.player = owner;
        this.audioController = audioSource;
        this.stamina = staminaPool;
    }

    private void Update() {
        if(this.player == null) return;

        if(!this.player.IsAlive()) {
            this.EndChargeDisplay();
            this.state = CombatState.Ready;
            return;
        }

        this.ReadInput();
        this.TickState();
    }

    private void ReadInput() {
        if(GameController.GameIsFreeze() || !this.player.GetCanResume()) return;
        if(this.player.GetIsDancing()) return;

        if(Input.GetMouseButtonDown(PRIMARY_ATTACK_BUTTON)) {
            if(this.state == CombatState.Charging) {
                this.CancelAttack();
                this.TryBeginSwing();
            } else if(this.IsAttacking) {
                this.QueueComboStep();
            } else if(this.state == CombatState.Ready) {
                this.comboIndex = 0;
                this.queuedComboSteps = 0;
                this.TryBeginSwing();
            }
        }

        if(Input.GetMouseButtonDown(TECHNIQUE_BUTTON) && this.state == CombatState.Ready)
            this.TryBeginCharge();

        if(Input.GetKeyDown(KeyCode.Space))
            this.dodgeBufferedAt = Time.time;

        this.blockHeld = Input.GetMouseButton(BLOCK_BUTTON);
    }

    private void TickState() {

        if(this.state == CombatState.Charging
        && (GameController.GameIsFreeze() || !this.player.GetCanResume() || this.player.GetIsDancing())) {
            this.CancelAttack();
            return;
        }

        bool wantsDodge = this.dodgeBufferedAt >= 0f && (Time.time - this.dodgeBufferedAt) <= INPUT_BUFFER;

        // La roulade prime sur l'attaque et annule la récupération d'un coup
        // (dodge-cancel) ou la garde, mais jamais la frappe elle-même.
        bool dodgeAllowed = this.state == CombatState.Ready
                         || this.state == CombatState.Charging
                         || this.state == CombatState.Recovery
                         || this.state == CombatState.Blocking;

        if(wantsDodge && dodgeAllowed) {
            this.dodgeBufferedAt = -1f;
            if(this.TryStartDodge()) return;
        }

        switch(this.state) {

            case CombatState.Ready:
                if(this.blockHeld) {
                    this.EnterBlock();
                }
                break;

            case CombatState.Charging:
                if(this.blockHeld) {
                    this.EndChargeDisplay();
                    this.EnterBlock();
                    break;
                }

                float heldFor = Mathf.Max(Time.time - this.attackPressedAt, 0f);
                this.chargeNormalized = Mathf.Clamp01(heldFor / CHARGE_MAX_HOLD);
                this.NotifyCharge(this.chargeNormalized, true);

                if(!Input.GetMouseButton(TECHNIQUE_BUTTON)) {
                    this.EndChargeDisplay();
                    this.EnterState(CombatState.Ready, 0f);
                    this.TryBeginTechnique(this.chargeNormalized);
                }
                break;

            case CombatState.Blocking:
                // Le timer sert de fenêtre de parade : il court depuis l'appui.
                this.stateTimer += Time.deltaTime;

                if(!this.blockHeld) {
                    this.EnterState(CombatState.Ready, 0f);
                    this.player.SetAttackIcon(true);
                    this.player.StopAttackAnimation();
                }
                break;

            case CombatState.Staggered:
                if(this.Advance()) {
                    this.EnterState(CombatState.Ready, 0f);
                    this.player.SetAttackIcon(true);
                    this.player.StopAttackAnimation();
                }
                break;

            case CombatState.Dodging:
                this.TickDodge();
                break;

            case CombatState.Windup:
                this.ApplyLunge();

                if(this.Advance()) {
                    this.hitThisSwing.Clear();
                    this.swingPlayedSFX = false;
                    this.projectileFired = false;
                    this.player.SetWeaponTrail(true);
                    this.EnterState(CombatState.Active, this.currentActiveDuration);
                }
                break;

            case CombatState.Active:
                this.ResolveHits();

                if(this.Advance()) {
                    this.player.SetWeaponTrail(false);
                    this.EnterState(CombatState.Recovery, this.currentRecoveryDuration);
                }
                break;

            case CombatState.Recovery:
                if(this.Advance()) {
                    if(this.queuedComboSteps > 0 && this.comboIndex + 1 < Mathf.Max(this.family.comboLength, 1)) {
                        this.queuedComboSteps--;
                        this.comboIndex++;

                        if(this.TryBeginSwing()) break;

                        // Plus d'endurance : le combo s'arrête sans rejouer le clip.
                        this.comboIndex--;
                        this.queuedComboSteps = 0;
                    }

                    this.queuedComboSteps = 0;
                    this.comboIndex = 0;
                    this.techniqueSwing = false;
                    this.EnterState(CombatState.Ready, 0f);
                    this.player.SetAttackIcon(true);
                    this.player.StopAttackAnimation();
                }
                break;
        }
    }

    private void QueueComboStep() {
        int comboLength = (this.family != null) ? Mathf.Max(this.family.comboLength, 1) : 1;
        int remainingSteps = Mathf.Max(comboLength - this.comboIndex - 1, 0);
        this.queuedComboSteps = Mathf.Min(this.queuedComboSteps + 1, remainingSteps);
    }

    // Avance le timer de l'état ; vrai quand il est écoulé.
    private bool Advance() {
        this.stateTimer += Time.deltaTime;
        return this.stateTimer >= this.stateDuration;
    }

    private void EnterState(CombatState next, float duration) {
        this.state = next;
        this.stateTimer = 0f;
        this.stateDuration = Mathf.Max(duration, 0.01f);
    }

    private bool TryBeginSwing() {

        WeaponFamilyStat next = WeaponFamilyLibrary.Get(this.player.GetWeaponFamily());

        // Arme de jet vide : on retombe aux poings plutôt que de frapper dans le vide.
        if(next.ranged && this.player.GetWeaponAmmo() <= 0)
            next = WeaponFamilyLibrary.Get("unarmed");

        // Sans endurance, pas de coup : c'est ce qui empêche le matraquage du clic.
        if(this.stamina != null && !this.stamina.TrySpend(next.staminaCost))
            return false;

        this.family = next;
        this.techniqueSwing = false;
        this.chargeNormalized = 0f;

        // Le délai d'attaque (stat + arme) module les durées de la famille, mais de
        // façon compressée : la famille porte déjà l'identité de cadence. Un rapport
        // direct pénalisait deux fois les armes lourdes — timings longs ET malus
        // d'attackSpeed — au point de les rendre moins efficaces qu'une épée.
        float delay = Mathf.Max(this.player.GetAttackSpeed() + this.player.GetWeaponAttackSpeed(), 0.2f);
        this.timingMult = Mathf.Clamp(1f + (delay - REFERENCE_ATTACK_DELAY) * 0.25f, 0.5f, 1.6f);

        float windup = this.family.windup * this.timingMult;
        float active = this.family.active * this.timingMult;
        float recovery = this.family.recovery * this.timingMult;
        float configuredDuration = windup + active + recovery;

        this.lungeSpeed = (windup > 0.01f) ? this.family.lunge / windup : 0f;
        this.currentActiveDuration = active;

        this.player.SetAttackIcon(false);
        float animationDuration = this.PlayAttackAnimation(configuredDuration);
        this.currentRecoveryDuration = Mathf.Max(recovery, animationDuration - windup - active);

        this.EnterState(CombatState.Windup, windup);
        return true;
    }

    private bool TryBeginCharge() {

        WeaponFamilyStat next = WeaponFamilyLibrary.Get(this.player.GetWeaponFamily());

        if(next.ranged && this.player.GetWeaponAmmo() <= 0)
            next = WeaponFamilyLibrary.Get("unarmed");

        if(next.technique == null) return false;
        if(this.stamina != null && !this.stamina.Has(next.technique.staminaCost)) return false;

        this.family = next;
        this.techniqueSwing = false;
        this.comboIndex = 0;
        this.queuedComboSteps = 0;
        this.attackPressedAt = Time.time;
        this.chargeNormalized = 0f;

        this.player.SetAttackIcon(false);
        this.NotifyCharge(0f, true);
        this.EnterState(CombatState.Charging, float.MaxValue);
        return true;
    }

    private bool TryBeginTechnique(float power) {

        WeaponTechniqueStat technique = (this.family != null) ? this.family.technique : null;

        if(technique == null) return this.TryBeginSwing();
        if(this.stamina != null && !this.stamina.TrySpend(technique.staminaCost)) {
            this.player.SetAttackIcon(true);
            return false;
        }

        this.techniqueSwing = true;
        this.chargeNormalized = Mathf.Clamp01(power);
        this.comboIndex = 0;
        this.queuedComboSteps = 0;

        float delay = Mathf.Max(this.player.GetAttackSpeed() + this.player.GetWeaponAttackSpeed(), 0.2f);
        this.timingMult = Mathf.Clamp(1f + (delay - REFERENCE_ATTACK_DELAY) * 0.25f, 0.5f, 1.6f);

        float windup = Mathf.Max(technique.windup, 0.01f) * this.timingMult;
        float active = Mathf.Max(technique.active, 0.01f) * this.timingMult;
        float recovery = Mathf.Max(technique.recovery, 0.01f) * this.timingMult;
        float configuredDuration = windup + active + recovery;

        this.lungeSpeed = (windup > 0.01f) ? technique.lunge / windup : 0f;
        this.currentActiveDuration = active;

        float animationDuration = this.player.PlayAttackAnimation(technique.clip, configuredDuration);
        this.currentRecoveryDuration = Mathf.Max(recovery, animationDuration - windup - active);

        this.EnterState(CombatState.Windup, windup);
        return true;
    }

    private void NotifyCharge(float normalized, bool visible) {
        if(this.ChargeChanged != null)
            this.ChargeChanged(Mathf.Clamp01(normalized), visible);
    }

    private void EndChargeDisplay() {
        this.NotifyCharge(0f, false);
    }

    // --- Armes de jet ---

    private void FireProjectile() {

        int weaponId = this.player.GetWeaponID();

        Vector3 origin = transform.position + Vector3.up * 1.4f + transform.forward * 0.6f;
        Vector3 direction = this.AimDirection(origin);

        int damage = Mathf.Max(Mathf.RoundToInt(
            (this.player.GetAttack() + this.player.GetWeaponAttack()) * this.TechniqueDamageMultiplier()), 1);

        DamageInfo info = new DamageInfo(
            damage,
            this.TechniquePoiseDamage(),
            (this.player.GetKnockback() + this.player.GetWeaponKnockback()) * this.TechniqueKnockbackMultiplier(),
            transform);

        info.weight = WeaponFamilyLibrary.ParseWeight(this.family.hitWeight);

        GameObject go = new GameObject("Projectile_" + weaponId);
        Projectile projectile = go.AddComponent<Projectile>();

        // CreateWeapon indexe les modèles à partir de 0, les id du JSON à partir de 1.
        float projectileSpeed = this.family.projectileSpeed;

        if(this.techniqueSwing && this.family.technique != null)
            projectileSpeed *= Mathf.Lerp(
                Mathf.Max(this.family.technique.projectileSpeedMultMin, 1f),
                Mathf.Max(this.family.technique.projectileSpeedMultMax, 1f),
                this.chargeNormalized);

        projectile.Launch(origin, direction, projectileSpeed, info, weaponId - 1, null, 0, false);

        this.AttachVisual(go, weaponId);
        this.player.ConsumeWeaponAmmo();

        if(this.audioController != null) this.audioController.PlaySlashSFX();
    }

    // Vise le point sous le réticule (centre écran) plutôt que transform.forward brut :
    // c'est ce qui rend le tir réellement précis, en hauteur comme en contrebas.
    private Vector3 AimDirection(Vector3 origin) {

        Camera camera = this.player.GetPlayerCamera();

        if(camera == null) return transform.forward;

        Ray ray = camera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));

        RaycastHit hit;
        Vector3 aimPoint = Physics.Raycast(ray, out hit, 120f, CombatLayers.EnemyMask | CombatLayers.ObstacleMask, QueryTriggerInteraction.Collide)
                         ? hit.point
                         : ray.origin + ray.direction * 60f;

        Vector3 direction = aimPoint - origin;

        // Cible trop proche du canon : la direction devient instable.
        return (direction.sqrMagnitude > 0.25f) ? direction.normalized : transform.forward;
    }

    // Reprend le modèle de l'arme comme visuel de vol. Le composant Weapon et les
    // colliders sont désactivés avant leur Start : sinon l'arme se comporterait comme
    // un ramassable en plein vol.
    private void AttachVisual(GameObject projectile, int weaponId) {

        GameObject prefab = Resources.Load<GameObject>("Weapons/Prefabs/weapon_" + weaponId);
        if(prefab == null) return;

        GameObject visual = Instantiate(prefab, projectile.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        Weapon weaponScript = visual.GetComponent<Weapon>();
        if(weaponScript != null) weaponScript.enabled = false;

        foreach(Collider collider in visual.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
    }

    // --- Garde et parade ---

    private void EnterBlock() {
        this.queuedComboSteps = 0;
        this.comboIndex = 0;
        this.techniqueSwing = false;
        this.chargeNormalized = 0f;
        this.attackPressedAt = -1f;
        this.EndChargeDisplay();

        this.player.SetAttackIcon(false);
        this.player.PlayBlockAnimation();
        this.EnterState(CombatState.Blocking, float.MaxValue);
    }

    // Appelé par PlayerController quand un coup arrive. Décide de ce qui passe.
    public DefenseResult ResolveIncoming(DamageInfo info, out int finalDamage) {

        finalDamage = Mathf.Max(info.amount, 0);

        if(this.IsInvulnerable) {
            finalDamage = 0;
            return DefenseResult.Invulnerable;
        }

        if(this.state != CombatState.Blocking) return DefenseResult.Hit;

        // Sans attaquant identifié (dégâts scriptés, chute…), la garde ne s'applique pas.
        if(info.source == null) return DefenseResult.Hit;

        // La garde ne couvre que l'avant : se faire prendre à revers reste puni.
        Vector3 toAttacker = info.sourcePosition - transform.position;
        toAttacker.y = 0f;

        if(toAttacker.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, toAttacker) > BLOCK_ANGLE * 0.5f)
            return DefenseResult.Hit;

        // Parade parfaite : relâcher au bon moment ouvre une riposte.
        if(this.stateTimer <= PARRY_WINDOW) {
            finalDamage = 0;
            this.riposteUntil = Time.time + RIPOSTE_WINDOW;
            this.StaggerAttacker(info.source, PARRY_STAGGER_DURATION);

            if(this.audioController != null) this.audioController.PlayParrySFX();

            return DefenseResult.Parried;
        }

        finalDamage = Mathf.RoundToInt(info.amount * (1f - BLOCK_DAMAGE_REDUCTION));

        // Bloquer coûte de l'endurance ; à sec, la garde cède.
        float cost = info.amount * BLOCK_STAMINA_PER_DAMAGE;

        if(this.stamina != null && !this.stamina.TrySpend(cost)) {
            this.stamina.Empty();
            this.BreakGuard();

            return DefenseResult.GuardBroken;
        }

        return DefenseResult.Blocked;
    }

    private void BreakGuard() {
        this.player.PlayStunAnimation();
        this.EnterState(CombatState.Staggered, GUARD_BREAK_DURATION);
    }

    private void StaggerAttacker(Transform source, float duration) {
        if(source == null) return;

        IStaggerable target = source.GetComponentInParent<IStaggerable>();
        if(target != null) target.Stagger(duration);
    }

    // --- Roulade ---

    private bool TryStartDodge() {

        if(this.stamina != null && !this.stamina.TrySpend(DODGE_STAMINA))
            return false;

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical   = Input.GetAxisRaw("Vertical");

        Vector3 direction = transform.forward * vertical + transform.right * horizontal;
        direction.y = 0f;

        // Sans direction, on esquive vers l'arrière.
        if(direction.sqrMagnitude < 0.01f) direction = -transform.forward;

        this.dodgeDirection = direction.normalized;
        this.dodgeDistance = this.player.IsSprinting() ? DODGE_DISTANCE_SPRINT : DODGE_DISTANCE;

        this.hitThisSwing.Clear();
        this.comboIndex = 0;
        this.queuedComboSteps = 0;
        this.techniqueSwing = false;
        this.chargeNormalized = 0f;
        this.attackPressedAt = -1f;
        this.EndChargeDisplay();

        this.player.StopAttackAnimation();
        this.player.PlayRollAnimation(RollStateName(horizontal, vertical));

        if(this.audioController != null) this.audioController.PlayDodgeSFX();

        this.EnterState(CombatState.Dodging, DODGE_DURATION);
        return true;
    }

    private void TickDodge() {

        float before = Mathf.Clamp01(this.stateTimer / DODGE_DURATION);
        this.stateTimer += Time.deltaTime;
        float after = Mathf.Clamp01(this.stateTimer / DODGE_DURATION);

        this.MoveSafely(this.dodgeDirection, (DodgeEase(after) - DodgeEase(before)) * this.dodgeDistance);

        if(this.stateTimer >= this.stateDuration) {
            this.EnterState(CombatState.Ready, 0f);
            this.player.SetAttackIcon(true);
        }
    }

    // Départ vif puis freinage : donne le poids d'une roulade plutôt qu'un glissement.
    private static float DodgeEase(float t) {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static string RollStateName(float horizontal, float vertical) {
        if(Mathf.Abs(vertical) >= Mathf.Abs(horizontal))
            return (vertical > 0.1f) ? "Roll_Forward" : "Roll_Back";

        return (horizontal > 0f) ? "Roll_Right" : "Roll_Left";
    }

    // Déplacement qui ne traverse pas le décor.
    private void MoveSafely(Vector3 direction, float distance) {
        if(distance <= 0f) return;

        RaycastHit obstacle;
        Vector3 origin = transform.position + Vector3.up;

        if(Physics.SphereCast(origin, DODGE_RADIUS, direction, out obstacle, distance + 0.1f, CombatLayers.ObstacleMask, QueryTriggerInteraction.Ignore))
            distance = Mathf.Max(obstacle.distance - 0.1f, 0f);

        transform.position += direction * distance;
    }

    private float PlayAttackAnimation(float totalDuration) {
        string[] clips = this.family.clips;

        string clip = (clips != null && clips.Length > 0)
                    ? clips[Mathf.Clamp(this.comboIndex, 0, clips.Length - 1)]
                    : "Attack";

        return this.player.PlayAttackAnimation(clip, totalDuration);
    }

    // Petit pas en avant sur le windup : on avance en frappant au lieu de reculer.
    private void ApplyLunge() {
        if(this.lungeSpeed <= 0f) return;

        transform.position += transform.forward * (this.lungeSpeed * Time.deltaTime);
    }

    private void ResolveHits() {

        // Une arme de jet lâche un seul projectile, au début de la fenêtre active.
        if(this.family.ranged) {
            if(!this.projectileFired) {
                this.FireProjectile();
                this.projectileFired = true;
            }

            return;
        }

        // Plancher et plafond sur la portée. Les bonus d'arme (jusqu'à +2) multipliés
        // par le coefficient de famille (jusqu'à 1.6) donnaient une hallebarde à 6 m
        // — intouchable — et une petite dague à 1 m, incapable d'atteindre un ennemi
        // qui s'immobilise à 1,7 m. Le plancher garantit que chaque arme touche, le
        // plafond garde les armes d'hast longues sans les rendre absurdes.
        float rangeMult = this.family.rangeMult;
        float arcAngle = this.family.arcAngle;
        int maxTargets = this.family.maxTargets;

        if(this.techniqueSwing && this.family.technique != null) {
            rangeMult *= Mathf.Max(this.family.technique.rangeMult, 0.1f);
            arcAngle = this.family.technique.arcAngle;
            maxTargets = this.family.technique.maxTargets;
        }

        float range = Mathf.Clamp(
            (this.player.GetRange() + this.player.GetWeaponRange()) * rangeMult,
            MIN_REACH,
            this.techniqueSwing ? MAX_REACH * 1.35f : MAX_REACH);

        // L'origine part du centre du joueur, sans décalage vers l'avant. Le pousser
        // de 0,5 m plaçait une cible collée au joueur DERRIÈRE ce point : la direction
        // vers elle pointait alors vers l'arrière, l'angle dépassait le demi-cône et le
        // coup était rejeté. C'est ce qui faisait rater les attaques au corps à corps.
        Vector3 origin = transform.position + Vector3.up * 1.0f;

        HitboxResolver.ResolveCone(
            origin,
            transform.forward,
            range,
            arcAngle,
            CombatLayers.EnemyMask,
            maxTargets,
            transform,
            this.hitThisSwing,
            this.results);

        for(int i = 0; i < this.results.Count; i++)
            this.ApplyHit(this.results[i]);
    }

    private void ApplyHit(HitCandidate hit) {

        int damage = Mathf.RoundToInt(
            (this.player.GetAttack() + this.player.GetWeaponAttack()) * this.TechniqueDamageMultiplier());

        bool backstab = this.family.backstabMult > 1f
                     && HitboxResolver.IsFromBehind(transform, hit.target.GetTransform());

        if(backstab)
            damage = Mathf.RoundToInt(damage * this.family.backstabMult);

        // Riposte : le coup qui suit une parade réussie fait double.
        bool riposte = Time.time <= this.riposteUntil;

        if(riposte) {
            damage = Mathf.RoundToInt(damage * RIPOSTE_MULT);
            this.riposteUntil = -1f;
        }

        // Le dernier coup du combo projette davantage.
        bool finisher = !this.techniqueSwing && this.comboIndex + 1 >= Mathf.Max(this.family.comboLength, 1);
        float knockbackMult = this.TechniqueKnockbackMultiplier() * (finisher ? 1.5f : 1f);

        DamageInfo info = new DamageInfo(
            Mathf.Max(damage, 1),
            this.TechniquePoiseDamage(),
            (this.player.GetKnockback() + this.player.GetWeaponKnockback()) * knockbackMult,
            transform);

        info.hitPoint  = hit.point;
        info.hitNormal = (hit.point - transform.position).normalized;
        info.isBackstab = backstab;
        info.weight = (backstab || riposte) ? HitWeight.Heavy : WeaponFamilyLibrary.ParseWeight(this.family.hitWeight);

        // Une riposte casse la garde de n'importe quel adversaire.
        if(riposte) info.poiseDamage *= 3f;

        hit.target.TakeHit(info);

        if(CombatFeedback.Instance != null)
            CombatFeedback.Instance.OnHitDealt(info, !hit.target.IsAlive());

        // Un seul son par coup porté, même quand le balayage touche toute la foule.
        if(!this.swingPlayedSFX && this.audioController != null) {
            this.audioController.PlaySlashSFX(this.SwingPitch());
            this.swingPlayedSFX = true;
        }
    }

    private float TechniqueDamageMultiplier() {
        return this.techniqueSwing
            ? Mathf.Lerp(CHARGE_DAMAGE_MIN, CHARGE_DAMAGE_MAX, this.chargeNormalized)
            : 1f;
    }

    private float TechniquePoiseDamage() {
        if(!this.techniqueSwing) return this.family.poiseDamage;

        float familyBonus = (this.family.technique != null)
                          ? Mathf.Max(this.family.technique.poiseBonus, 1f)
                          : 1f;

        return this.family.poiseDamage
             * Mathf.Lerp(CHARGE_POISE_MIN, CHARGE_POISE_MAX, this.chargeNormalized)
             * familyBonus;
    }

    private float TechniqueKnockbackMultiplier() {
        float multiplier = this.family.knockbackMult;

        if(this.techniqueSwing)
            multiplier *= Mathf.Lerp(CHARGE_KNOCKBACK_MIN, CHARGE_KNOCKBACK_MAX, this.chargeNormalized);

        return multiplier;
    }

    // Grave pour les armes lourdes, aigu pour les dagues : de quoi distinguer les
    // familles à l'oreille avec les 3 seuls sons de combat existants.
    private float SwingPitch() {
        switch(WeaponFamilyLibrary.ParseWeight(this.family.hitWeight)) {
            case HitWeight.Heavy:  return 0.75f;
            case HitWeight.Medium: return 0.92f;
            default:               return 1.12f;
        }
    }

    // Annule le coup en cours (roue des danses, pause, mort).
    public void CancelAttack() {
        // Une roulade en cours n'est pas interrompue : elle porte des i-frames.
        if(this.state == CombatState.Dodging) return;

        bool wasAttacking = this.state != CombatState.Ready;

        this.dodgeBufferedAt = -1f;
        this.queuedComboSteps = 0;
        this.comboIndex = 0;
        this.hitThisSwing.Clear();
        this.techniqueSwing = false;
        this.chargeNormalized = 0f;
        this.attackPressedAt = -1f;
        this.EndChargeDisplay();
        this.EnterState(CombatState.Ready, 0f);

        if(wasAttacking && this.player != null) {
            this.player.SetAttackIcon(true);
            this.player.StopAttackAnimation();
        }
    }
}
