using UnityEngine;

// Masques de layers du combat.
//
// Les layers Enemy (3), Player (6) et Weapon (7) existent déjà dans le TagManager,
// mais les prefabs d'ennemis sont restés sur Default (0) : la détection passait par
// CompareTag, donc par un OverlapSphere qui ramenait le décor entier avant de filtrer.
// On assigne le layer au runtime (voir ApplyEnemyLayer) pour ne pas avoir à toucher
// aux 11 prefabs, et pour que tout nouvel ennemi soit couvert automatiquement.
public static class CombatLayers {

    public const string EnemyName  = "Enemy";
    public const string PlayerName = "Player";
    public const string WeaponName = "Weapon";

    private static int enemy    = -1;
    private static int player   = -1;
    private static int obstacle = -1;

    public static int EnemyMask {
        get {
            if(enemy < 0) enemy = Mask(EnemyName);
            return enemy;
        }
    }

    public static int PlayerMask {
        get {
            if(player < 0) player = Mask(PlayerName);
            return player;
        }
    }

    // Ce qui bloque la vue et arrête un projectile : tout sauf les combattants,
    // les armes au sol et les layers non physiques.
    public static int ObstacleMask {
        get {
            if(obstacle < 0) {
                int ignored = EnemyMask
                            | PlayerMask
                            | Mask(WeaponName)
                            | (1 << 2)  // Ignore Raycast
                            | (1 << 5); // UI

                obstacle = ~ignored;
            }

            return obstacle;
        }
    }

    private static int Mask(string layerName) {
        int mask = LayerMask.GetMask(layerName);

        if(mask == 0)
            Debug.LogWarning("[CombatLayers] Layer \"" + layerName + "\" introuvable dans le TagManager : les requêtes physiques associées ne toucheront rien.");

        return mask;
    }

    // Place l'ennemi sur le layer Enemy. On ne touche qu'à la racine : c'est là que
    // vivent le CapsuleCollider, le Rigidbody et le NavMeshAgent. Descendre dans les
    // enfants déplacerait aussi l'arme tenue, qui doit rester sur le layer Weapon.
    public static void ApplyEnemyLayer(GameObject target) {
        int layer = LayerMask.NameToLayer(EnemyName);

        if(layer >= 0 && target != null)
            target.layer = layer;
    }

    public static void ApplyPlayerLayer(GameObject target) {
        int layer = LayerMask.NameToLayer(PlayerName);

        if(layer >= 0 && target != null)
            target.layer = layer;
    }
}
