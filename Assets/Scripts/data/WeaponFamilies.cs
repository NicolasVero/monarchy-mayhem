using System;

[Serializable]
public class WeaponFamilies {
    public WeaponFamilyStat[] families;
}

// Profil de maniement d'une famille d'armes.
//
// Les 34 armes n'étaient que des deltas de stats et jouaient toutes le même clip
// avec la même hitbox : rien ne les distinguait en jeu. La famille porte tout ce
// qui change le ressenti — forme de la frappe, timings, engagement, stagger.
//
// Les durées sont exprimées pour un attackSpeed de référence de 2.0 ; elles sont
// mises à l'échelle au runtime par la vitesse d'attaque réelle du joueur.
[Serializable]
public class WeaponFamilyStat {

    public string id;

    // Forme de la frappe
    public float arcAngle;      // ouverture totale du cône, en degrés
    public float rangeMult;     // multiplie (range joueur + range arme)
    public int   maxTargets;
    public bool  pierce;        // traverse les cibles alignées au lieu de balayer

    // Timings (secondes)
    public float windup;
    public float active;
    public float recovery;

    // Engagement : part de la vitesse de déplacement conservée par phase
    public float moveMultWindup;
    public float moveMultActive;
    public float moveMultRecovery;
    public float lunge;         // pas en avant sur le windup, en mètres

    // Combat
    public int   comboLength;
    public float comboHandoff;
    public float staminaCost;
    public float poiseDamage;
    public float knockbackMult;
    public float backstabMult;  // 1 = pas de bonus dans le dos
    public string hitWeight;    // Light | Medium | Heavy

    // Armes de jet
    public bool  ranged;
    public float projectileSpeed;
    public int   ammo;

    // États d'animation par coup de la chaîne ; repli sur "Attack" si absent
    public string[] clips;

    // Coup charge propre a la famille. Les multiplicateurs de puissance progressifs
    // restent communs a toutes les armes ; cette donnee definit sa forme et son cout.
    public WeaponTechniqueStat technique;
}

[Serializable]
public class WeaponTechniqueStat {

    public string clip;

    // Timings une fois le bouton relache.
    public float windup;
    public float active;
    public float recovery;

    // Forme et engagement du coup.
    public float arcAngle;
    public float rangeMult;
    public int maxTargets;
    public bool pierce;
    public float lunge;

    public float staminaCost;
    public float poiseBonus;

    // Armes de jet : vitesse du projectile entre charge minimale et maximale.
    public float projectileSpeedMultMin;
    public float projectileSpeedMultMax;
}
