using System;
using UnityEngine;

// Cache des stats ennemies.
//
// Chaque ennemi rechargeait et reparsait EnemiesStats.json dans son Awake. Avec un
// plafond de 10 à 40 entités et un respawn continu, ce parse revenait à chaque
// apparition et provoquait des à-coups. Il est fait une fois par session.
public static class EnemyStatsCache {

    private static EnemiesStats data;
    private static bool loaded;

    public static EnemyStats Get(string type, string difficulty) {

        Load();

        if(data == null) return null;

        DifficultyStats stats = Resolve(difficulty);
        if(stats == null || stats.enemiesStat == null) return null;

        EnemyStats found = Array.Find(stats.enemiesStat, e => e.type == type);

        if(found == null)
            Debug.LogError("[EnemyStatsCache] Type d'ennemi \"" + type + "\" absent de EnemiesStats.json (difficulté " + difficulty + ").");

        return found;
    }

    private static DifficultyStats Resolve(string difficulty) {
        switch(difficulty) {
            case "medium": return data.medium;
            case "hard":   return data.hard;
            default:       return data.easy;
        }
    }

    private static void Load() {

        if(loaded) return;
        loaded = true;

        TextAsset json = Resources.Load<TextAsset>("Data/EnemiesStats");

        if(json == null) {
            Debug.LogError("[EnemyStatsCache] Data/EnemiesStats.json introuvable.");
            return;
        }

        data = JsonUtility.FromJson<EnemiesStats>(json.text);
    }
}
