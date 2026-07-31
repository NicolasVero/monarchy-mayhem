using System.Collections;
using UnityEngine;

public sealed class ThrowerEnemyRole : EnemyRoleBehaviour {

    private const float PreferredMin = 6f;
    private const float PreferredMax = 10f;
    private const float ProjectileSpeed = 16f;
    private const int ProjectileWeaponId = 18;

    private static readonly Color ThrowColor = new Color(1f, 0.65f, 0.08f, 0.8f);

    public override void Initialize(EnemyController controller) {
        base.Initialize(controller);
        this.owner.AttachRoleWeapon(18);
    }

    public override void TickRole(float distanceToPlayer) {
        if(this.owner.RoleActionActive) return;

        if(distanceToPlayer < PreferredMin) {
            this.owner.RoleMoveTo(this.AwayFromPlayer(4f));
            return;
        }

        if(distanceToPlayer > PreferredMax) {
            this.owner.RoleMoveTo(this.owner.PlayerTransform.position);
            return;
        }

        this.owner.RoleStop();
        this.owner.RoleFacePlayer();

        if(this.owner.CanStartRoleAction)
            StartCoroutine(this.Throw());
    }

    private IEnumerator Throw() {
        float windup = Mathf.Max(this.owner.RoleWindup, 0.4f);
        this.owner.SetRoleWeaponVisible(true);
        this.owner.BeginRoleAction("OverhandThrow", windup);

        float elapsed = 0f;
        Vector3 aimPoint = this.owner.PlayerTransform.position + Vector3.up;
        LineRenderer line = this.BeginRoleTelegraph(
            "Rock trajectory",
            new Color(ThrowColor.r, ThrowColor.g, ThrowColor.b, 0.15f),
            0.045f,
            2);

        while(elapsed < windup) {
            elapsed += Time.deltaTime;

            if(elapsed < windup * TrackingCutoff) {
                aimPoint = this.owner.PlayerTransform.position + Vector3.up;
                this.owner.RoleFacePlayer();
            }

            float progress = Mathf.Clamp01(elapsed / windup);
            Vector3 previewOrigin = transform.position + Vector3.up * 1.35f + transform.forward * 0.55f;
            line.SetPosition(0, previewOrigin);
            line.SetPosition(1, aimPoint);

            Color lineColor = ThrowColor;
            lineColor.a = Mathf.Lerp(0.15f, 0.9f, progress);
            this.SetRoleTelegraphColor(line, lineColor);
            this.owner.UpdateRoleTelegraph(progress, ThrowColor);

            if(this.ActionInterrupted) {
                this.EndRoleTelegraph();
                this.owner.SetRoleWeaponVisible(true);
                this.owner.EndRoleAction(false);
                yield break;
            }

            yield return null;
        }

        Vector3 origin = transform.position + Vector3.up * 1.35f + transform.forward * 0.55f;
        Vector3 direction = (aimPoint - origin).normalized;
        this.EndRoleTelegraph();
        this.owner.SetRoleWeaponVisible(false);
        this.owner.LaunchRoleProjectile(direction, ProjectileSpeed, ProjectileWeaponId);
        this.owner.EndRoleAction();
    }
}
