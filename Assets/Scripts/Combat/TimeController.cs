using UnityEngine;

// Propriétaire unique de Time.timeScale.
//
// Avant, deux systèmes écrivaient dessus sans se concerter : la pause (0)
// et le ralenti de mort (0.3). Le hitstop s'y ajoute et doit pouvoir figer
// l'image sans jamais tomber à 0, sinon GameController.GameIsFreeze()
// considère le jeu en pause et bloque l'input d'attaque.
//
// L'échelle appliquée est toujours : min(baseScale, hitstopScale).
// Le hitstop est ignoré tant que le jeu est en pause.
public static class TimeController {

    private static float baseScale = 1f;
    private static float hitstopScale = 1f;
    private static float hitstopEndsAt = -1f;

    public static bool IsPaused { get { return baseScale <= 0f; } }
    public static float BaseScale { get { return baseScale; } }
    public static bool HitstopActive { get { return Time.unscaledTime < hitstopEndsAt; } }

    // Échelle "normale" du jeu : 1 en jeu, 0 en pause, 0.3 pendant la mort.
    public static void SetBaseScale(float scale) {
        baseScale = Mathf.Max(0f, scale);

        // Une pause annule un hitstop en cours, sinon il reprendrait au dégel.
        if(baseScale <= 0f) {
            hitstopEndsAt = -1f;
            hitstopScale = 1f;
        }

        Apply();
    }

    public static void TogglePause() {
        SetBaseScale(IsPaused ? 1f : 0f);
    }

    // duration est en temps réel : un hitstop ne doit pas se ralentir lui-même.
    public static void Hitstop(float duration, float scale) {
        if(IsPaused || duration <= 0f) return;

        bool wasActive = HitstopActive;
        float end = Time.unscaledTime + duration;

        // Un hitstop plus fort ou plus long écrase le précédent, jamais l'inverse.
        if(end > hitstopEndsAt) hitstopEndsAt = end;
        if(!wasActive || scale < hitstopScale) hitstopScale = Mathf.Clamp(scale, 0.01f, 1f);

        Apply();
    }

    private static void Apply() {
        Time.timeScale = HitstopActive ? Mathf.Min(baseScale, hitstopScale) : baseScale;
    }

    // Appelé chaque frame par le runner : rend la main quand le hitstop expire.
    public static void Tick() {
        if(!HitstopActive && hitstopScale != 1f) {
            hitstopScale = 1f;
            Apply();
        }
    }

    // Les valeurs statiques survivent aux changements de scène et, en Editor,
    // aux sorties de Play Mode si le domain reload est désactivé : on repart propre.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() {
        baseScale = 1f;
        hitstopScale = 1f;
        hitstopEndsAt = -1f;
        Apply();

        GameObject runner = new GameObject("~TimeController");
        runner.hideFlags = HideFlags.HideAndDontSave;
        runner.AddComponent<TimeControllerRunner>();
        Object.DontDestroyOnLoad(runner);
    }
}
