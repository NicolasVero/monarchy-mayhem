using UnityEngine;

// Arme de jet.
//
// Le projet n'avait aucune arme à distance : les 34 armes étaient toutes du
// corps-à-corps et aucun projectile n'existait. Plutôt que d'ajouter des modèles
// d'arc, on lance les armes elles-mêmes — la hache, la dague, la pierre — et on
// les fait disparaître à l'impact pour ne pas remplir la scène de ramassables.
//
// Le déplacement se fait par SphereCast plutôt qu'avec un Rigidbody : à 28 u/s,
// un collider traverserait les cibles minces entre deux pas de physique.
public class Projectile : MonoBehaviour {

    private const float RADIUS = 0.15f;
    private const float LIFETIME = 6f;
    private const float SPIN_DEGREES_PER_SECOND = 720f;

    private Vector3 direction;
    private float speed;
    private float remainingLife = LIFETIME;

    private int damage;
    private float poiseDamage;
    private float knockback;
    private HitWeight weight;
    private Transform owner;

    private int weaponModelIndex = -1;
    private WeaponsDropper dropper;

    private int mask;
    private bool dropsOnLand = true;

    public void Launch(
        Vector3 origin,
        Vector3 aimDirection,
        float projectileSpeed,
        DamageInfo info,
        int modelIndex,
        WeaponsDropper weaponsDropper,
        int collisionMask = 0,
        bool createDropOnLand = true) {

        transform.position = origin;

        this.direction = aimDirection.normalized;
        this.speed = Mathf.Max(projectileSpeed, 1f);

        this.damage = info.amount;
        this.poiseDamage = info.poiseDamage;
        this.knockback = info.knockback;
        this.weight = info.weight;
        this.owner = info.source;

        this.weaponModelIndex = modelIndex;
        this.dropper = weaponsDropper;
        this.dropsOnLand = createDropOnLand;

        this.mask = (collisionMask != 0)
                  ? collisionMask
                  : CombatLayers.EnemyMask | CombatLayers.ObstacleMask;

        if(this.direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(this.direction);
    }

    private void Update() {

        float step = this.speed * Time.deltaTime;

        RaycastHit hit;

        if(Physics.SphereCast(transform.position, RADIUS, this.direction, out hit, step, this.mask, QueryTriggerInteraction.Collide)) {
            this.Impact(hit);
            return;
        }

        transform.position += this.direction * step;
        transform.Rotate(Vector3.right, SPIN_DEGREES_PER_SECOND * Time.deltaTime, Space.Self);

        this.remainingLife -= Time.deltaTime;
        if(this.remainingLife <= 0f) this.Land(transform.position);
    }

    private void Impact(RaycastHit hit) {

        IDamageable target = hit.collider.GetComponentInParent<IDamageable>();

        // On ne se touche pas soi-même si le tir part collé à un obstacle.
        if(target != null && target.GetTransform() == this.owner) return;

        if(target != null && target.IsAlive()) {

            DamageInfo info = new DamageInfo(this.damage, this.poiseDamage, this.knockback, this.owner);
            info.hitPoint = hit.point;
            info.hitNormal = hit.normal;
            info.weight = this.weight;

            // Le knockback doit partir du projectile, pas du lanceur resté au loin.
            info.sourcePosition = transform.position;

            target.TakeHit(info);
        }

        this.Land(hit.point - this.direction * 0.2f);
    }

    // Certains projectiles peuvent encore demander explicitement un drop. Les armes
    // lancées par le joueur et les pierres ennemies désactivent cette option.
    private void Land(Vector3 position) {

        if(this.dropsOnLand && this.dropper != null && this.weaponModelIndex >= 0)
            this.dropper.CreateWeapon(this.weaponModelIndex, position);

        Destroy(gameObject);
    }
}
