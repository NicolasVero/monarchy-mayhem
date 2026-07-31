using System.Collections.Generic;
using UnityEngine;

public static class EncounterPoolLibrary {

    private static Dictionary<string, EncounterPool> pools;
    private static Dictionary<string, List<GameObject>> prefabs;

    public static bool HasPool(string sceneName) {
        Load();
        return !string.IsNullOrEmpty(sceneName) && pools.ContainsKey(sceneName);
    }

    public static GameObject Pick(string sceneName) {
        Load();

        EncounterPool pool;
        if(string.IsNullOrEmpty(sceneName) || !pools.TryGetValue(sceneName, out pool))
            return null;

        if(pool.enemies == null || pool.enemies.Length == 0)
            return null;

        float total = 0f;

        for(int i = 0; i < pool.enemies.Length; i++) {
            EncounterWeight entry = pool.enemies[i];
            if(entry == null || entry.weight <= 0f || !HasPrefabs(entry.type)) continue;
            total += entry.weight;
        }

        if(total <= 0f) return null;

        float roll = GameController.RandomFloat() * total;

        for(int i = 0; i < pool.enemies.Length; i++) {
            EncounterWeight entry = pool.enemies[i];
            if(entry == null || entry.weight <= 0f || !HasPrefabs(entry.type)) continue;

            roll -= entry.weight;
            if(roll <= 0f) return PickPrefab(entry.type);
        }

        return null;
    }

    public static GameObject PickType(string type) {
        Load();
        return PickPrefab(type);
    }

    public static GameObject PickSpecial(string sceneName, int sequenceIndex) {
        Load();

        EncounterPool pool;
        if(string.IsNullOrEmpty(sceneName) || !pools.TryGetValue(sceneName, out pool)
            || pool.enemies == null || pool.enemies.Length == 0)
            return null;

        List<string> available = new List<string>();

        for(int i = 0; i < pool.enemies.Length; i++) {
            EncounterWeight entry = pool.enemies[i];

            if(entry != null
                && entry.type != null
                && entry.type.Contains("_")
                && HasPrefabs(entry.type))
                available.Add(entry.type);
        }

        if(available.Count == 0) return null;

        int index = Mathf.Abs(sequenceIndex) % available.Count;
        return PickPrefab(available[index]);
    }

    private static bool HasPrefabs(string type) {
        List<GameObject> found;
        return !string.IsNullOrEmpty(type)
            && prefabs.TryGetValue(type, out found)
            && found.Count > 0;
    }

    private static GameObject PickPrefab(string type) {
        List<GameObject> found;
        if(!prefabs.TryGetValue(type, out found) || found.Count == 0) return null;

        return found[GameController.Random(0, found.Count - 1)];
    }

    private static void Load() {
        if(pools != null) return;

        pools = new Dictionary<string, EncounterPool>();
        prefabs = new Dictionary<string, List<GameObject>>();

        TextAsset json = Resources.Load<TextAsset>("Data/EncounterPools");

        if(json != null) {
            EncounterPools parsed = JsonUtility.FromJson<EncounterPools>(json.text);

            if(parsed != null && parsed.pools != null) {
                for(int i = 0; i < parsed.pools.Length; i++) {
                    EncounterPool pool = parsed.pools[i];
                    if(pool != null && !string.IsNullOrEmpty(pool.scene))
                        pools[pool.scene] = pool;
                }
            }
        } else {
            Debug.LogWarning("[EncounterPoolLibrary] Data/EncounterPools.json introuvable, utilisation des reglages de scene.");
        }

        GameObject[] all = Resources.LoadAll<GameObject>("Characters/Prefab");

        for(int i = 0; i < all.Length; i++) {
            GameObject prefab = all[i];
            string type = TypeForPrefab(prefab != null ? prefab.name : "");
            if(string.IsNullOrEmpty(type)) continue;

            List<GameObject> group;
            if(!prefabs.TryGetValue(type, out group)) {
                group = new List<GameObject>();
                prefabs[type] = group;
            }

            group.Add(prefab);
        }
    }

    private static string TypeForPrefab(string prefabName) {
        switch(prefabName) {
            case "peasant_4":   return "peasant_lancer";
            case "peasant_5":   return "peasant_thrower";
            case "bourgeois_4": return "bourgeois_support";
            case "knight_2":    return "knight_lancer";
            case "peasant_1":
            case "peasant_2":
            case "peasant_3":   return "peasant";
            case "bourgeois_1":
            case "bourgeois_2":
            case "bourgeois_3": return "bourgeois";
            case "knight_1":    return "knight";
            default:            return null;
        }
    }
}
