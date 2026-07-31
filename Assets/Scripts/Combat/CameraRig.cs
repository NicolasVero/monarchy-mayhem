using UnityEngine;

// Rig de caméra.
//
// La caméra était un simple enfant du joueur, à offset figé, sans pitch ni collision :
// elle traversait les murs et ne permettait pas de viser en hauteur. La rotation du
// corps s'écrivait en plus dans FixedUpdate, donc à la fréquence physique.
//
// Le GameObject reste dans le prefab — toutes les références sérialisées, dont le
// post-process noir & blanc de la mort, restent valides — mais il est déparenté au
// runtime et piloté ici.
//
// Le cadrage ne se déduit PAS de la position d'origine dans le prefab : la caméra n'y
// est pas forcément fille directe du transform du joueur, et le décalage latéral
// d'origine (plus de 4 mètres sur la gauche) est inexploitable pour du combat visé.
// Tout vient de Data/CameraSettings.json, réglable sans recompiler.
public class CameraRig : MonoBehaviour {

    private const float FOV_RETURN_SPEED = 6f;

    private Transform target;
    private Camera cam;
    private CameraSettings settings;

    private float baseFov;
    private float yaw;
    private float pitch;
    private bool inputEnabled = true;

    private Vector3 velocity;

    private float shakeAmplitude;
    private float shakeDecay;
    private float fovKick;

    public float Yaw { get { return this.yaw; } }

    public void Initialize(Transform followTarget) {

        this.target = followTarget;
        this.cam = GetComponent<Camera>();
        this.settings = LoadSettings();

        this.baseFov = (this.cam != null) ? this.cam.fieldOfView : 60f;

        this.yaw = (followTarget != null) ? followTarget.eulerAngles.y : 0f;
        this.pitch = 10f;

        // Déparentée : la caméra ne doit plus hériter des à-coups du personnage,
        // et elle survit aux changements de scène comme le joueur.
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);

        this.SnapToTarget();
    }

    private static CameraSettings LoadSettings() {

        TextAsset json = Resources.Load<TextAsset>("Data/CameraSettings");

        CameraSettings loaded = (json != null) ? JsonUtility.FromJson<CameraSettings>(json.text) : null;

        if(loaded == null) {
            Debug.LogWarning("[CameraRig] Data/CameraSettings.json introuvable : valeurs par défaut utilisées.");
            loaded = new CameraSettings();
        }

        // Garde-fous : un réglage à zéro collerait la caméra dans le personnage.
        if(loaded.distance <= 0.1f)        loaded.distance = 5f;
        if(loaded.height <= 0f)            loaded.height = 1.8f;
        if(loaded.sensitivity <= 0f)       loaded.sensitivity = 3f;
        if(loaded.smooth < 0f)             loaded.smooth = 0.05f;
        if(loaded.collisionRadius <= 0f)   loaded.collisionRadius = 0.28f;
        if(loaded.pitchMax <= loaded.pitchMin) { loaded.pitchMin = -30f; loaded.pitchMax = 55f; }

        return loaded;
    }

    public void SetInputEnabled(bool enabled) {
        this.inputEnabled = enabled;
    }

    public void Shake(float amplitude, float duration) {
        if(duration <= 0f) return;

        this.shakeAmplitude = Mathf.Max(this.shakeAmplitude, amplitude);
        this.shakeDecay = amplitude / duration;
    }

    public void KickFov(float amount) {
        this.fovKick = Mathf.Max(this.fovKick, amount);
    }

    // La visée est lue en Update et le corps est orienté dans la foulée, pour que le
    // corps, la caméra et le cône de frappe partagent la même orientation dans la frame.
    private void Update() {

        if(this.target == null) return;

        this.ReadInput();

        this.target.eulerAngles = new Vector3(0f, this.yaw, 0f);
    }

    private void LateUpdate() {

        if(this.target == null) return;

        this.UpdatePosition(false);
        this.UpdateFov();
    }

    private void ReadInput() {

        if(!this.inputEnabled || GameController.GameIsFreeze()) return;

        this.yaw += Input.GetAxis("Mouse X") * this.settings.sensitivity;
        this.pitch = Mathf.Clamp(
            this.pitch - Input.GetAxis("Mouse Y") * this.settings.sensitivity * 0.7f,
            this.settings.pitchMin,
            this.settings.pitchMax);
    }

    private void SnapToTarget() {
        this.UpdatePosition(true);
    }

    private void UpdatePosition(bool instant) {

        Vector3 pivot = this.target.position + Vector3.up * this.settings.height;
        Quaternion rotation = Quaternion.Euler(this.pitch, this.yaw, 0f);

        Vector3 offset = new Vector3(this.settings.shoulder, 0f, -this.settings.distance);
        Vector3 desired = pivot + rotation * offset;

        // Bras télescopique : la caméra se rapproche au lieu de traverser le décor.
        Vector3 toDesired = desired - pivot;
        float distance = toDesired.magnitude;

        if(distance > 0.01f) {
            Vector3 direction = toDesired / distance;
            RaycastHit hit;

            if(Physics.SphereCast(pivot, this.settings.collisionRadius, direction, out hit, distance, CombatLayers.ObstacleMask, QueryTriggerInteraction.Ignore))
                desired = pivot + direction * Mathf.Max(hit.distance - 0.1f, 0.5f);
        }

        transform.position = instant
                           ? desired
                           : Vector3.SmoothDamp(transform.position, desired, ref this.velocity, this.settings.smooth, Mathf.Infinity, Time.unscaledDeltaTime);

        // Même rotation que celle qui place la caméra : le centre de l'écran pointe
        // exactement là où AimDirection lance son rayon.
        transform.rotation = rotation;

        this.ApplyShake();
    }

    private void ApplyShake() {

        if(this.shakeAmplitude <= 0f) return;

        // Bruit de Perlin plutôt que du random : la secousse reste continue.
        float time = Time.unscaledTime * 28f;

        Vector3 noise = new Vector3(
            Mathf.PerlinNoise(time, 0f) - 0.5f,
            Mathf.PerlinNoise(0f, time) - 0.5f,
            0f);

        transform.position += transform.rotation * noise * (this.shakeAmplitude * 2f);

        this.shakeAmplitude = Mathf.Max(this.shakeAmplitude - this.shakeDecay * Time.unscaledDeltaTime, 0f);
    }

    private void UpdateFov() {

        if(this.cam == null) return;

        this.cam.fieldOfView = this.baseFov + this.fovKick;
        this.fovKick = Mathf.Max(this.fovKick - this.fovKick * FOV_RETURN_SPEED * Time.unscaledDeltaTime - 0.01f, 0f);
    }
}
