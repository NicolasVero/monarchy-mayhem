using System.Collections.Generic;
using UnityEngine;

// Charge et met en cache WeaponFamilies.json.
//
// Le reste du projet reparse ses JSON à chaque instanciation (EnemyController le fait
// par ennemi, à chaque spawn). Ici le parse est fait une fois pour toute la session.
public static class WeaponFamilyLibrary {

    public const string DefaultFamily = "sword1h";

    private static Dictionary<string, WeaponFamilyStat> families;
    private static WeaponFamilyStat fallback;

    public static WeaponFamilyStat Get(string id) {
        Load();

        if(!string.IsNullOrEmpty(id)) {
            WeaponFamilyStat found;
            if(families.TryGetValue(id, out found)) return found;

            Debug.LogWarning("[WeaponFamilyLibrary] Famille \"" + id + "\" inconnue, repli sur " + DefaultFamily + ".");
        }

        WeaponFamilyStat def;
        if(families.TryGetValue(DefaultFamily, out def)) return def;

        return fallback;
    }

    public static HitWeight ParseWeight(string weight) {
        switch(weight) {
            case "Heavy":  return HitWeight.Heavy;
            case "Medium": return HitWeight.Medium;
            default:       return HitWeight.Light;
        }
    }

    private static void Load() {
        if(families != null) return;

        families = new Dictionary<string, WeaponFamilyStat>();
        fallback = BuildFallback();

        TextAsset json = Resources.Load<TextAsset>("Data/WeaponFamilies");

        if(json == null) {
            Debug.LogError("[WeaponFamilyLibrary] Data/WeaponFamilies.json introuvable : toutes les armes se joueront à l'identique.");
            families[DefaultFamily] = fallback;
            return;
        }

        WeaponFamilies parsed = JsonUtility.FromJson<WeaponFamilies>(json.text);

        if(parsed == null || parsed.families == null || parsed.families.Length == 0) {
            Debug.LogError("[WeaponFamilyLibrary] WeaponFamilies.json vide ou illisible.");
            families[DefaultFamily] = fallback;
            return;
        }

        foreach(WeaponFamilyStat family in parsed.families)
            if(family != null && !string.IsNullOrEmpty(family.id))
                families[family.id] = family;
    }

    // Profil neutre : garantit que le combat reste jouable même si la donnée manque.
    private static WeaponFamilyStat BuildFallback() {
        return new WeaponFamilyStat {
            id               = DefaultFamily,
            arcAngle         = 100f,
            rangeMult        = 1f,
            maxTargets       = 2,
            pierce           = false,
            windup           = 0.18f,
            active           = 0.12f,
            recovery         = 0.28f,
            moveMultWindup   = 0.4f,
            moveMultActive   = 0.15f,
            moveMultRecovery = 0.6f,
            lunge            = 0.6f,
            comboLength      = 3,
            comboHandoff     = 0.92f,
            staminaCost      = 14f,
            poiseDamage      = 20f,
            knockbackMult    = 1f,
            backstabMult     = 1f,
            hitWeight        = "Light",
            ranged           = false,
            projectileSpeed  = 0f,
            ammo             = 0,
            clips            = new string[] { "Attack" },
            technique        = new WeaponTechniqueStat {
                clip                   = "SwordCombo_3",
                windup                 = 0.38f,
                active                 = 0.16f,
                recovery               = 0.5f,
                arcAngle               = 140f,
                rangeMult              = 1.1f,
                maxTargets             = 3,
                pierce                 = false,
                lunge                  = 0.8f,
                staminaCost            = 28f,
                poiseBonus             = 1f,
                projectileSpeedMultMin = 1.2f,
                projectileSpeedMultMax = 1.8f
            }
        };
    }
}
