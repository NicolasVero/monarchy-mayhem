using UnityEngine;

// Contrat commun au joueur, aux ennemis et au boss.
// Permet au resolver de hitbox de frapper n'importe quelle cible sans connaître son type.
public interface IDamageable {

    void TakeHit(DamageInfo info);
    bool IsAlive();
    Transform GetTransform();
}
