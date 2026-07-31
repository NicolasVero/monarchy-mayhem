using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class SupportEnemyRole : EnemyRoleBehaviour {

    private const float PreferredDistance = 7f;
    private const float RetreatDistance = 5.5f;
    private const float ApproachDistance = 8.5f;
    private const float CastDuration = 1.2f;
    private const float BuffRadius = 9f;
    private const float BuffDuration = 6f;
    private const float DamageMultiplier = 1.20f;
    private const float SpeedMultiplier = 1.15f;
    private const int RingSegments = 48;

    private static readonly Color SupportColor = new Color(0.15f, 0.75f, 1f, 0.85f);

    private static readonly Collider[] AllyBuffer = new Collider[64];
    private readonly HashSet<EnemyController> allies = new HashSet<EnemyController>();

    public override void Initialize(EnemyController controller) {
        base.Initialize(controller);
        this.owner.AttachRoleWeapon(7);
    }

    public override void TickRole(float distanceToPlayer) {
        if(this.owner.RoleActionActive) return;

        if(distanceToPlayer < RetreatDistance) {
            this.owner.RoleMoveTo(this.AwayFromPlayer(PreferredDistance - distanceToPlayer + 2f));
            return;
        }

        if(distanceToPlayer > ApproachDistance) {
            this.owner.RoleMoveTo(this.owner.PlayerTransform.position);
            return;
        }

        this.owner.RoleStop();
        this.owner.RoleFacePlayer();

        if(this.owner.CanStartRoleAction)
            StartCoroutine(this.CastBuff());
    }

    private IEnumerator CastBuff() {
        this.owner.BeginRoleAction("Buff", CastDuration);

        float elapsed = 0f;
        LineRenderer ring = this.BeginRoleTelegraph(
            "Support radius",
            new Color(SupportColor.r, SupportColor.g, SupportColor.b, 0.15f),
            0.07f,
            RingSegments,
            true);

        while(elapsed < CastDuration) {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / CastDuration);
            float radius = Mathf.Lerp(1.2f, BuffRadius, progress);

            for(int i = 0; i < RingSegments; i++) {
                float angle = (Mathf.PI * 2f * i) / RingSegments;
                Vector3 point = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                point.y += 0.08f;
                ring.SetPosition(i, point);
            }

            Color ringColor = SupportColor;
            ringColor.a = Mathf.Lerp(0.15f, 0.95f, progress);
            this.SetRoleTelegraphColor(ring, ringColor);
            this.owner.UpdateRoleTelegraph(progress, SupportColor);

            if(this.ActionInterrupted) {
                this.EndRoleTelegraph();
                this.owner.EndRoleAction(false);
                yield break;
            }

            yield return null;
        }

        this.EndRoleTelegraph();
        this.BuffNearbyAllies();
        this.owner.EndRoleAction();
    }

    private void BuffNearbyAllies() {
        this.allies.Clear();

        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            BuffRadius,
            AllyBuffer,
            CombatLayers.EnemyMask,
            QueryTriggerInteraction.Collide);

        for(int i = 0; i < count; i++) {
            Collider collider = AllyBuffer[i];
            if(collider == null) continue;

            EnemyController ally = collider.GetComponentInParent<EnemyController>();
            if(ally == null || ally == this.owner || !ally.IsAlive() || !this.allies.Add(ally))
                continue;

            ally.ApplyBattleBuff(DamageMultiplier, SpeedMultiplier, BuffDuration);
        }
    }
}
