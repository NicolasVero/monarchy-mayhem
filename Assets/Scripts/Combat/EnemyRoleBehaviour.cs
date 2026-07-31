using UnityEngine;

// Les roles tactiques remplacent uniquement la decision de mouvement et d'action.
// EnemyController reste proprietaire de la vie, de la poise, des drops et de la mort.
public abstract class EnemyRoleBehaviour : MonoBehaviour {

    protected const float TrackingCutoff = 0.60f;

    protected EnemyController owner;
    private GameObject telegraphObject;

    private static Material telegraphMaterial;

    public static EnemyRoleBehaviour Create(string enemyType, GameObject target) {
        switch(enemyType) {
            case "lancer":  return target.AddComponent<LancerEnemyRole>();
            case "thrower": return target.AddComponent<ThrowerEnemyRole>();
            case "support": return target.AddComponent<SupportEnemyRole>();
            default:        return null;
        }
    }

    public virtual void Initialize(EnemyController controller) {
        this.owner = controller;
    }

    public abstract void TickRole(float distanceToPlayer);

    public virtual void CancelAction() {
        StopAllCoroutines();
        this.EndRoleTelegraph();

        if(this.owner != null) {
            this.owner.SetRoleWeaponVisible(true);

            if(this.owner.RoleActionActive)
                this.owner.EndRoleAction(false);
        }
    }

    protected bool ActionInterrupted {
        get {
            return this.owner == null
                || !this.owner.IsAlive()
                || this.owner.IsStaggered;
        }
    }

    protected Vector3 AwayFromPlayer(float distance) {
        Vector3 away = transform.position - this.owner.PlayerTransform.position;
        away.y = 0f;

        if(away.sqrMagnitude < 0.01f) away = -transform.forward;
        return transform.position + away.normalized * distance;
    }

    protected LineRenderer BeginRoleTelegraph(
        string telegraphName,
        Color color,
        float width,
        int pointCount,
        bool loop = false) {

        this.EndRoleTelegraph();

        this.telegraphObject = new GameObject(telegraphName);
        this.telegraphObject.transform.SetParent(transform, false);

        LineRenderer line = this.telegraphObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = Mathf.Max(pointCount, 2);
        line.loop = loop;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = GetTelegraphMaterial();
        line.startColor = color;
        line.endColor = color;

        return line;
    }

    protected void SetRoleTelegraphColor(LineRenderer line, Color color) {
        if(line == null) return;

        line.startColor = color;
        line.endColor = color;
    }

    protected void EndRoleTelegraph() {
        if(this.telegraphObject == null) return;

        Destroy(this.telegraphObject);
        this.telegraphObject = null;
    }

    private static Material GetTelegraphMaterial() {
        if(telegraphMaterial != null) return telegraphMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if(shader == null) shader = Shader.Find("Unlit/Color");

        telegraphMaterial = new Material(shader);
        telegraphMaterial.name = "Role Telegraph";
        telegraphMaterial.hideFlags = HideFlags.HideAndDontSave;
        return telegraphMaterial;
    }
}
