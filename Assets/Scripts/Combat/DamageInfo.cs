using UnityEngine;

// Poids d'un coup : pilote le hitstop, le screenshake et le VFX d'impact.
public enum HitWeight {
    Light,
    Medium,
    Heavy,
    Kill,
    Parry
}

// Tout ce qu'un coup transporte, de l'attaquant vers la cible.
// Remplace les anciens ApplyDamage()/TakeDamage() sans paramètre, qui obligeaient
// la cible à aller relire les stats du joueur elle-même.
public struct DamageInfo {

    public int amount;
    public float poiseDamage;
    public float knockback;

    public Vector3 hitPoint;
    public Vector3 hitNormal;

    // Position de l'attaquant : sert à orienter le knockback et à savoir
    // si le coup arrive de face (parade) ou dans le dos (backstab).
    public Vector3 sourcePosition;
    public Transform source;

    public bool isBackstab;
    public HitWeight weight;

    public DamageInfo(int amount, float poiseDamage, float knockback, Transform source) {
        this.amount         = amount;
        this.poiseDamage    = poiseDamage;
        this.knockback      = knockback;
        this.source         = source;
        this.sourcePosition = (source != null) ? source.position : Vector3.zero;
        this.hitPoint       = this.sourcePosition;
        this.hitNormal      = Vector3.up;
        this.isBackstab     = false;
        this.weight         = HitWeight.Light;
    }

    // Direction horizontale attaquant -> cible, normalisée.
    // L'ancien knockback utilisait -transform.forward de la cible, ce qui projetait
    // l'ennemi n'importe où dès qu'il ne regardait pas le joueur.
    public Vector3 KnockbackDirection(Vector3 targetPosition) {
        Vector3 direction = targetPosition - this.sourcePosition;
        direction.y = 0f;

        return (direction.sqrMagnitude > 0.0001f) ? direction.normalized : Vector3.forward;
    }
}
