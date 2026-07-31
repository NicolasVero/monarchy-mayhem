using UnityEngine;
using UnityEngine.UI;

// Couche de retour sensoriel du combat.
//
// Le combat n'en avait aucune : ni hitstop, ni secousse, ni VFX d'impact, ni son de
// dégât reçu. On voyait la barre de vie descendre sans jamais sentir le coup, dans un
// sens comme dans l'autre. Tout ce qui suit est du ressenti pur — aucune règle de jeu.
//
// Créé au runtime par PlayerController : rien à câbler dans les scènes.
public class CombatFeedback : MonoBehaviour {

    public static CombatFeedback Instance;

    private const float HITSTOP_SCALE = 0.05f;

    private CameraRig rig;
    private ImpactVfx vfx;
    private Image vignette;

    private static readonly Color HIT_COLOR = new Color(1f, 0.85f, 0.5f);
    private static readonly Color CRIT_COLOR = new Color(1f, 0.35f, 0.25f);
    private static readonly Color PARRY_COLOR = new Color(0.6f, 0.9f, 1f);

    private float vignetteAlpha;

    public void Initialize(CameraRig cameraRig, Canvas hudCanvas) {

        Instance = this;

        this.rig = cameraRig;
        this.vfx = gameObject.AddComponent<ImpactVfx>();

        this.BuildVignette(hudCanvas);
    }

    // --- Coups portés ---

    public void OnHitDealt(DamageInfo info, bool killed) {

        HitWeight weight = killed ? HitWeight.Kill : info.weight;

        this.Hitstop(HitstopDuration(weight));
        this.Shake(weight);

        if(weight == HitWeight.Heavy || weight == HitWeight.Kill)
            this.Kick(5f);

        Color color = info.isBackstab ? CRIT_COLOR : HIT_COLOR;
        float scale = (weight == HitWeight.Heavy || weight == HitWeight.Kill) ? 1.5f : 1f;

        if(this.vfx != null)
            this.vfx.Spawn(info.hitPoint, -info.hitNormal, color, scale);
    }

    // --- Coups subis ---

    public void OnPlayerHurt(int damage) {
        this.Hitstop(0.05f);
        this.Shake(HitWeight.Medium);
        this.FlashVignette(Mathf.Clamp01(0.35f + damage * 0.01f));
    }

    public void OnBlocked(Vector3 point) {
        this.Hitstop(0.06f);
        this.Shake(HitWeight.Light);

        if(this.vfx != null) this.vfx.Spawn(point, Vector3.up, HIT_COLOR, 0.8f);
    }

    // La parade parfaite doit être la sensation la plus marquante du jeu :
    // c'est le geste le plus difficile.
    public void OnParried(Vector3 point) {
        this.Hitstop(0.15f);
        this.Shake(HitWeight.Parry);
        this.Kick(7f);

        if(this.vfx != null) this.vfx.Spawn(point, Vector3.up, PARRY_COLOR, 2f);
    }

    public void OnGuardBroken() {
        this.Hitstop(0.1f);
        this.Shake(HitWeight.Heavy);
        this.FlashVignette(0.6f);
    }

    // --- Primitives ---

    private void Hitstop(float duration) {
        TimeController.Hitstop(duration, HITSTOP_SCALE);
    }

    private void Shake(HitWeight weight) {

        if(this.rig == null) return;

        switch(weight) {
            case HitWeight.Light:  this.rig.Shake(0.06f, 0.10f); break;
            case HitWeight.Medium: this.rig.Shake(0.12f, 0.15f); break;
            case HitWeight.Heavy:  this.rig.Shake(0.22f, 0.22f); break;
            case HitWeight.Kill:   this.rig.Shake(0.28f, 0.25f); break;
            case HitWeight.Parry:  this.rig.Shake(0.30f, 0.28f); break;
        }
    }

    private void Kick(float amount) {
        if(this.rig != null) this.rig.KickFov(amount);
    }

    private static float HitstopDuration(HitWeight weight) {
        switch(weight) {
            case HitWeight.Medium: return 0.055f;
            case HitWeight.Heavy:  return 0.08f;
            case HitWeight.Kill:   return 0.12f;
            case HitWeight.Parry:  return 0.15f;
            default:               return 0.035f;
        }
    }

    // --- Vignette de dégât ---

    // Une Image plein écran sur le HUD plutôt qu'un post-process : le projet est en
    // pipeline Built-in et n'a qu'un seul effet d'écran, réservé à la mort.
    private void BuildVignette(Canvas hudCanvas) {

        if(hudCanvas == null) return;

        GameObject go = new GameObject("DamageVignette", typeof(RectTransform));
        go.transform.SetParent(hudCanvas.transform, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        this.vignette = go.AddComponent<Image>();
        this.vignette.color = new Color(0.7f, 0f, 0f, 0f);
        this.vignette.raycastTarget = false;

        // Toujours au-dessus du reste du HUD.
        go.transform.SetAsLastSibling();
    }

    private void FlashVignette(float alpha) {
        this.vignetteAlpha = Mathf.Max(this.vignetteAlpha, alpha);
    }

    private void Update() {

        if(this.vignette == null || this.vignetteAlpha <= 0f) return;

        this.vignetteAlpha = Mathf.Max(this.vignetteAlpha - Time.unscaledDeltaTime * 1.8f, 0f);

        Color color = this.vignette.color;
        color.a = this.vignetteAlpha;
        this.vignette.color = color;
    }
}
