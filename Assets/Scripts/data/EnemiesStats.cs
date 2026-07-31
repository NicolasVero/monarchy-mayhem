using System;

[Serializable]
public class EnemiesStats {
    public DifficultyStats easy;
    public DifficultyStats medium;
    public DifficultyStats hard;
}

[Serializable]
public class DifficultyStats {
    public EnemyStats[] enemiesStat;
}

[Serializable]
public class EnemyStats {
    public string type;
    public float chanceToDrop;
    public int health;
    public int attack;
    public float attackSpeed;
    public float speed;
    public float range;
    public int xp;

    // Résistance à l'interruption : en dessous du seuil, l'ennemi encaisse sans
    // broncher. C'est ce qui rend une arme lourde utile face à un chevalier.
    public float poise;

    // Durée d'anticipation avant le coup. Elle était figée à 0.5 s dans le code
    // et surtout invisible : l'animation ne partait qu'à l'impact.
    public float windup;

    // Ouverture du cône de frappe : au-delà, l'attaque rate.
    public float attackAngle;
}



// using System;

// [Serializable]
// public class EnemiesStats {
//     public EnemyStats[] enemiesStat;
// }

// [Serializable]
// public class EnemyStats {
//     public string type; 
//     public float chanceToDrop; 
//     public int health;
//     public int attack; 
//     public float attackSpeed;
//     public float speed;
//     public float range; 
//     public int xp;
// }