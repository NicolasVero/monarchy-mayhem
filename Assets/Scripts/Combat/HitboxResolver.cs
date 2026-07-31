using System.Collections.Generic;
using UnityEngine;

public struct HitCandidate {
    public Collider collider;
    public IDamageable target;
    public Vector3 point;
    public float distance;
}

// Résolution des frappes en cône.
//
// Remplace l'ancien OnTriggerStay + SphereCollider de rayon (range + weaponRange),
// qui touchait tout ce qui entrait dans la sphère — y compris dans le dos, le test
// d'angle étant resté commenté. Ici la direction compte, la portée est celle de
// l'arme, et un mur bloque le coup.
public static class HitboxResolver {

    private const int BUFFER_SIZE = 48;
    private const float VERTICAL_TOLERANCE = 2.5f;

    private static readonly Collider[] buffer = new Collider[BUFFER_SIZE];
    private static readonly RaycastHit[] obstructionBuffer = new RaycastHit[32];
    private static readonly List<HitCandidate> candidates = new List<HitCandidate>(BUFFER_SIZE);
    private static readonly HashSet<Transform> candidateTargets = new HashSet<Transform>();

    // Remplit "results" avec les cibles touchées, les plus proches d'abord.
    // alreadyHit garantit qu'une cible n'encaisse qu'une fois par coup porté.
    public static void ResolveCone(
        Vector3 origin,
        Vector3 forward,
        float range,
        float arcAngle,
        int layerMask,
        int maxTargets,
        Transform attacker,
        HashSet<Transform> alreadyHit,
        List<HitCandidate> results) {

        results.Clear();
        candidates.Clear();
        candidateTargets.Clear();

        if(range <= 0f) return;

        // Le joueur et les NavMeshAgent déplacent leurs Transform hors du moteur
        // physique. Sans synchronisation, une requête lancée juste après un mouvement
        // peut encore voir les capsules à leur position précédente, surtout au contact.
        Physics.SyncTransforms();

        int count = Physics.OverlapSphereNonAlloc(origin, range, buffer, layerMask, QueryTriggerInteraction.Collide);

        forward.y = 0f;
        if(forward.sqrMagnitude < 0.0001f) return;
        forward.Normalize();

        float halfArc = Mathf.Max(arcAngle, 1f) * 0.5f;

        for(int i = 0; i < count; i++) {
            Collider collider = buffer[i];
            if(collider == null) continue;

            // La portée et l'angle se mesurent jusqu'à la surface réellement frappable,
            // pas jusqu'au centre du personnage. Sinon un ennemi visiblement à portée
            // pouvait être rejeté à cause du rayon de sa capsule.
            Vector3 aimPoint = collider.ClosestPoint(origin);

            // Écart vertical : évite de frapper un ennemi sur un toit ou en contrebas.
            if(Mathf.Abs(aimPoint.y - origin.y) > VERTICAL_TOLERANCE) continue;

            Vector3 flat = aimPoint - origin;
            flat.y = 0f;

            float distance = flat.magnitude;
            if(distance > range) continue;

            // Une cible collée au joueur n'a pas de direction fiable : on l'accepte.
            if(distance > 0.15f && Vector3.Angle(forward, flat) > halfArc) continue;

            IDamageable target = collider.GetComponentInParent<IDamageable>();
            if(target == null || !target.IsAlive()) continue;

            Transform targetTransform = target.GetTransform();
            if(targetTransform == null) continue;
            if(alreadyHit != null && alreadyHit.Contains(targetTransform)) continue;
            if(!candidateTargets.Add(targetTransform)) continue;

            // Ignore les colliders appartenant au joueur ou à la cible, mais conserve
            // les vrais obstacles situés entre les deux.
            if(IsObstructed(origin, aimPoint, attacker, targetTransform)) {
                candidateTargets.Remove(targetTransform);
                continue;
            }

            candidates.Add(new HitCandidate {
                collider = collider,
                target   = target,
                point    = aimPoint,
                distance = distance
            });
        }

        if(candidates.Count == 0) return;

        candidates.Sort(CompareByDistance);

        int limit = (maxTargets > 0) ? Mathf.Min(maxTargets, candidates.Count) : candidates.Count;

        for(int i = 0; i < limit; i++) {
            results.Add(candidates[i]);
            if(alreadyHit != null) alreadyHit.Add(candidates[i].target.GetTransform());
        }
    }

    private static bool IsObstructed(Vector3 origin, Vector3 aimPoint, Transform attacker, Transform target) {
        Vector3 direction = aimPoint - origin;
        float distance = direction.magnitude;
        if(distance <= 0.001f) return false;

        int count = Physics.RaycastNonAlloc(
            origin,
            direction / distance,
            obstructionBuffer,
            distance,
            CombatLayers.ObstacleMask,
            QueryTriggerInteraction.Ignore);

        for(int i = 0; i < count; i++) {
            Transform hit = obstructionBuffer[i].transform;
            if(hit == null) continue;

            if(attacker != null && (hit == attacker || hit.IsChildOf(attacker))) continue;
            if(target != null && (hit == target || hit.IsChildOf(target))) continue;

            return true;
        }

        return false;
    }

    private static int CompareByDistance(HitCandidate a, HitCandidate b) {
        return a.distance.CompareTo(b.distance);
    }

    // Vrai si "attacker" frappe "target" par l'arrière (backstab des dagues,
    // et côté ennemi : coup encaissé hors de la garde).
    public static bool IsFromBehind(Transform attacker, Transform target) {
        if(attacker == null || target == null) return false;

        Vector3 toAttacker = attacker.position - target.position;
        toAttacker.y = 0f;

        if(toAttacker.sqrMagnitude < 0.0001f) return false;

        return Vector3.Angle(target.forward, toAttacker.normalized) > 120f;
    }
}
