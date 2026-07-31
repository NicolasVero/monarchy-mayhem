public class PlayerBaseStats {
    public int totalXP;
    public int xp;
    public int xpToNext;
    public int level;
    public int maxLevel;
    public int health;
    public int maxActualHealth;
    public int resistance;
    public int attack;
    public float attackSpeed;
    public float range;
    public float speed;
    public float knockback;
    public int regeneration;

    // Endurance : consommée par les attaques, la roulade et le sprint.
    // La capacité et la régénération progressent via leurs améliorations dédiées.
    public float stamina;
    public float staminaRegen;
    public float staminaRegenDelay;
}
