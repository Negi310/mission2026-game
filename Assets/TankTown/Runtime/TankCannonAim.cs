using UnityEngine;

namespace TankTown
{
    internal readonly struct CannonAimSolution
    {
        public readonly Vector3 Direction, Point;
        public readonly DestructibleBlock Target;
        public CannonAimSolution(Vector3 direction, Vector3 point, DestructibleBlock target = null)
        { Direction = direction; Point = point; Target = target; }
    }

    // One bounded physics query for the preview, shared with the real cannon at fire time.
    // No steering, camera rotation, homing or changes to the special weapons.
    internal sealed class TankCannonAim
    {
        private readonly TankTownGame game;
        private Collider[] candidates = new Collider[256];
        private CannonAimSolution preview;
        private float refreshAt;
        private const float ShellRadius = .25f;
        private const int CandidateMask = TownPhysics.StructureMask & ~TownPhysics.GroundMask & ~TownPhysics.PedestrianMask;

        public TankCannonAim(TankTownGame owner) { game = owner; }

        public CannonAimSolution Resolve(Camera view, bool force = false)
        {
            var muzzle = game.Tank.Muzzle;
            var straight = new CannonAimSolution(muzzle.forward, muzzle.position + muzzle.forward * 70);
            if (!game.IsPlaying || game.Tank.Relocating || game.Items.StopsForward ||
                (game.Items.Held != ItemKind.None && game.Items.Weapon == ItemKind.None))
                return straight;
            if (!force && Time.time < refreshAt && (preview.Target == null || IsTarget(preview.Target))) return preview;
            refreshAt = Time.time + .05f;
            preview = Find(muzzle.position, muzzle.forward, view, straight);
            return preview;
        }

        private CannonAimSolution Find(Vector3 origin, Vector3 forward, Camera view, CannonAimSolution straight)
        {
            float range = Mathf.Clamp(game.Settings.cannonAimAssistRange, 10, 150);
            // Never divert a shot that already hits a destructible object.
            if (Physics.SphereCast(origin, ShellRadius, forward, out var direct, range, TownPhysics.StructureMask, QueryTriggerInteraction.Ignore))
            {
                var target = direct.collider.GetComponentInParent<DestructibleBlock>();
                if (IsTarget(target)) return new CannonAimSolution(forward, direct.point, target);
            }
            float angle = Mathf.Clamp(game.Settings.cannonAimAssistAngle, 0, 12);
            if (!game.Settings.cannonAimAssist || angle <= 0 || view == null) return straight;
            float radius = Mathf.Tan(angle * Mathf.Deg2Rad) * range;
            var end = origin + forward * range;
            int count = Physics.OverlapCapsuleNonAlloc(origin, end, radius, candidates, CandidateMask, QueryTriggerInteraction.Ignore);
            if (count == candidates.Length && candidates.Length < 2048)
            {
                // Dense facades can fill the initial buffer. Grow once, retain it, and keep a hard cap.
                candidates = new Collider[2048];
                count = Physics.OverlapCapsuleNonAlloc(origin, end, radius, candidates, CandidateMask, QueryTriggerInteraction.Ignore);
            }
            float bestDot = Mathf.Cos(angle * Mathf.Deg2Rad), bestDistance = float.PositiveInfinity;
            var best = straight;
            for (int i = 0; i < count; i++)
            {
                var collider = candidates[i];
                var target = collider.GetComponentInParent<DestructibleBlock>();
                if (!IsTarget(target)) continue;
                float along = Vector3.Dot(collider.bounds.center - origin, forward);
                if (along <= .5f) continue;
                // Aim near the edge closest to the crosshair, then slightly inside the shape.
                var surface = collider.ClosestPoint(origin + forward * along);
                var point = Vector3.Lerp(surface, collider.bounds.center, .12f);
                var offset = point - origin;
                float distance = offset.magnitude;
                if (distance < .5f || distance > range) continue;
                var direction = offset / distance;
                float dot = Vector3.Dot(direction, forward);
                if (dot < bestDot || (Mathf.Abs(dot - bestDot) < .000001f && distance >= bestDistance)) continue;
                var viewport = view.WorldToViewportPoint(point);
                if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) continue;
                // The actual swept shell and the camera must both see the same piece.
                if (!Physics.SphereCast(origin, ShellRadius, direction, out var hit, distance + ShellRadius, TownPhysics.StructureMask, QueryTriggerInteraction.Ignore) ||
                    hit.collider.GetComponentInParent<DestructibleBlock>() != target) continue;
                var fromCamera = point - view.transform.position;
                if (Physics.Raycast(view.transform.position, fromCamera.normalized, out var obstruction, fromCamera.magnitude, TownPhysics.StructureMask, QueryTriggerInteraction.Ignore) &&
                    obstruction.collider.GetComponentInParent<DestructibleBlock>() != target) continue;
                bestDot = dot; bestDistance = distance;
                best = new CannonAimSolution(direction, hit.point, target);
            }
            return best;
        }

        private static bool IsTarget(DestructibleBlock target)
            => target != null && target.isActiveAndEnabled && !target.Detached;
    }
}
