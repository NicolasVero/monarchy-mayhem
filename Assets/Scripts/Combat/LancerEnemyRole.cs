using System.Collections;
using UnityEngine;

public sealed class LancerEnemyRole : EnemyRoleBehaviour {

    private const float CloseAttackDistance = 2.6f;
    private const float PreferredMin = 3.2f;
    private const float PreferredMax = 4.2f;
    private const float LongThrustRange = 4.4f;
    private const float CloseThrustRange = 2.8f;
    private const float LongThrustAngle = 30f;
    private const float CloseThrustAngle = 55f;
    private const float LongRecovery = 0.34f;
    private const float CloseRecovery = 0.22f;
    private const float CloseRetreatTimeout = 1f;

    private static readonly Color ThrustColor = new Color(1f, 0.15f, 0.04f, 0.85f);
    private static readonly Color CloseThrustColor = new Color(1f, 0.55f, 0.05f, 0.85f);

    private bool retreatingAfterCloseAttack;
    private float closeRetreatDeadline;

    public override void Initialize(EnemyController controller) {
        base.Initialize(controller);
        this.owner.AttachRoleWeapon(this.owner.EnemyFamily == "peasant" ? 31 : 32);
    }

    public override void TickRole(float distanceToPlayer) {
        if(this.owner.RoleActionActive) return;

        if(this.retreatingAfterCloseAttack) {
            if(distanceToPlayer < PreferredMin && Time.time < this.closeRetreatDeadline) {
                this.owner.RoleMoveTo(this.AwayFromPlayer(3f));
                return;
            }

            this.retreatingAfterCloseAttack = false;
        }

        if(this.owner.CanStartRoleAction) {
            if(distanceToPlayer <= CloseAttackDistance) {
                this.owner.RoleStop();
                this.owner.RoleFacePlayer();
                StartCoroutine(this.Thrust(true));
                return;
            }

            if(distanceToPlayer <= PreferredMax) {
                this.owner.RoleStop();
                this.owner.RoleFacePlayer();
                StartCoroutine(this.Thrust(false));
                return;
            }
        }

        if(distanceToPlayer < PreferredMin) {
            this.owner.RoleMoveTo(this.AwayFromPlayer(3f));
            return;
        }

        if(distanceToPlayer > PreferredMax) {
            this.owner.RoleMoveTo(this.owner.PlayerTransform.position);
            return;
        }

        this.owner.RoleStop();
        this.owner.RoleFacePlayer();
    }

    private IEnumerator Thrust(bool closeAttack) {
        float windup = closeAttack
            ? Mathf.Max(this.owner.RoleWindup * 0.65f, 0.32f)
            : Mathf.Max(this.owner.RoleWindup, 0.45f);
        float recovery = closeAttack ? CloseRecovery : LongRecovery;
        float attackRange = closeAttack ? CloseThrustRange : LongThrustRange;
        float attackAngle = closeAttack ? CloseThrustAngle : LongThrustAngle;
        Color telegraphColor = closeAttack ? CloseThrustColor : ThrustColor;

        // The clip keeps playing through recovery instead of being cut on impact.
        this.owner.BeginRoleAction("PolearmThrust_1", windup + recovery, "AttackPolearm01");

        float elapsed = 0f;
        LineRenderer line = this.BeginRoleTelegraph(
            closeAttack ? "Short pike lane" : "Pike lane",
            new Color(telegraphColor.r, telegraphColor.g, telegraphColor.b, 0.15f),
            0.10f,
            2);
        line.endWidth = closeAttack ? 0.30f : 0.24f;

        while(elapsed < windup) {
            elapsed += Time.deltaTime;

            if(elapsed < windup * TrackingCutoff)
                this.owner.RoleFacePlayer();

            float progress = Mathf.Clamp01(elapsed / windup);
            Vector3 ground = transform.position + Vector3.up * 0.08f;
            line.SetPosition(0, ground + transform.forward * 0.65f);
            line.SetPosition(1, ground + transform.forward * attackRange);

            Color lineColor = telegraphColor;
            lineColor.a = Mathf.Lerp(0.15f, 0.95f, progress);
            this.SetRoleTelegraphColor(line, lineColor);
            this.owner.UpdateRoleTelegraph(progress, telegraphColor);

            if(this.ActionInterrupted) {
                this.EndRoleTelegraph();
                this.owner.EndRoleAction(false);
                yield break;
            }

            yield return null;
        }

        this.EndRoleTelegraph();

        if(this.owner.RoleCanHitPlayer(attackRange, attackAngle)) {
            if(closeAttack)
                this.owner.DealRoleDamage(0.7f, 12f, 1.2f, HitWeight.Medium);
            else
                this.owner.DealRoleDamage(1f, 20f, 0.8f, HitWeight.Heavy);
        }

        elapsed = 0f;

        while(elapsed < recovery) {
            elapsed += Time.deltaTime;

            if(this.ActionInterrupted) {
                this.owner.EndRoleAction(false);
                yield break;
            }

            yield return null;
        }

        this.owner.EndRoleAction();

        if(closeAttack) {
            this.retreatingAfterCloseAttack = true;
            this.closeRetreatDeadline = Time.time + CloseRetreatTimeout;
        }
    }
}
