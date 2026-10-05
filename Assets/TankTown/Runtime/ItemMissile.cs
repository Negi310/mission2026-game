using UnityEngine;

namespace TankTown
{
    public sealed class ItemMissile : MonoBehaviour
    {
        private TankTownGame game;
        private TankItems owner;
        private int lane, wave;
        private float age;
        private Vector3 launch, apex, aimPoint, previous, flightDirection;
        private TrailRenderer trail;
        public TownMovingTarget TrackingTarget { get; private set; }
        public Vector3 AimPoint => aimPoint;
        public int Retargets { get; private set; }
        public void Initialize(TankTownGame game, TankItems owner, int lane)
        {
            this.game = game; this.owner = owner; this.lane = lane;
            var white = owner.MissileWhite;
            var orange = owner.MissileOrange;
            game.World.Box("Missile body", Vector3.zero, new Vector3(.55f, .55f, 1.6f), white, transform, false);
            var nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nose.GetComponent<Collider>().enabled = false; Destroy(nose.GetComponent<Collider>());
            nose.transform.SetParent(transform, false); nose.transform.localPosition = Vector3.forward * .78f; nose.transform.localScale = Vector3.one * .58f;
            nose.GetComponent<Renderer>().sharedMaterial = orange;
            for (int side = -1; side <= 1; side += 2)
                game.World.Box("Rocket fin", new Vector3(side * .42f, 0, -.55f), new Vector3(.45f, .12f, .5f), orange, transform, false);
            game.Effects.Trail(gameObject); trail = GetComponent<TrailRenderer>(); trail.startWidth = .8f; trail.time = .3f;
            Launch();
        }
        private void Launch()
        {
            age = -lane * .065f;
            var forward = Vector3.ProjectOnPlane(game.Tank.Muzzle.forward, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, forward);
            launch = game.Tank.Muzzle.position + right * (lane - 2) * .7f + Vector3.up * .6f;
            apex = launch + Vector3.up * (17 + lane * 1.2f);
            var offset = forward * (28 + lane * 3) + right * ((lane - 2) * 3);
            var aim = game.Tank.transform.position + offset;
            aim.x = Mathf.Clamp(aim.x, -game.World.HalfExtent + 6, game.World.HalfExtent - 6);
            aim.z = Mathf.Clamp(aim.z, -game.World.HalfExtent + 6, game.World.HalfExtent - 6);
            aimPoint = game.World.GroundPoint(aim, .45f);
            TrackingTarget = null; AcquireTarget(forward);
            flightDirection = Vector3.up;
            previous = transform.position = launch;
            transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            trail.Clear();
        }
        private void AcquireTarget(Vector3 forward)
        {
            TownMovingTarget best = null; float bestScore = float.PositiveInfinity;
            float range = game.Settings.missileTargetRange;
            foreach (var candidate in game.World.MovingTargets)
            {
                if (candidate == null || !candidate.Available) continue;
                var delta = candidate.AimPoint - game.Tank.transform.position;
                if (delta.sqrMagnitude > range * range) continue;
                if (!VisibleAhead(game, candidate, forward)) continue;
                float score = delta.sqrMagnitude + owner.MissilesTracking(candidate) * 1000 +
                    (candidate.IsVehicle || candidate.IsExplosive ? 0 : range * range * 2);
                if (score < bestScore) { bestScore = score; best = candidate; }
            }
            TrackingTarget = best;
            if (best != null) { aimPoint = best.AimPoint; Retargets++; }
        }
        internal static bool VisibleAhead(TankTownGame game, TownMovingTarget candidate, Vector3 forward)
        {
            var delta = Vector3.ProjectOnPlane(candidate.AimPoint - game.Tank.Muzzle.position, Vector3.up);
            if (delta.sqrMagnitude < .01f || Vector3.Dot(delta.normalized, forward) < Mathf.Cos(50 * Mathf.Deg2Rad)) return false;
            var camera = game.CameraRig.GetComponent<Camera>();
            var viewport = camera.WorldToViewportPoint(candidate.AimPoint);
            if (viewport.z <= camera.nearClipPlane || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) return false;
            if (Physics.Linecast(camera.transform.position, candidate.AimPoint, out var hit, TownPhysics.StructureMask, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<TownMovingTarget>() == candidate;
            return true;
        }
        private void Update()
        {
            if (!game.IsPlaying || !owner.MissilesActive) return;
            age += Time.deltaTime;
            if (TrackingTarget != null && !TrackingTarget.Available) TrackingTarget = null;
            if (TrackingTarget == null) AcquireTarget(Vector3.ProjectOnPlane(game.Tank.Muzzle.forward, Vector3.up).normalized);
            if (TrackingTarget != null) aimPoint = TrackingTarget.AimPoint + TrackingTarget.Velocity * .12f;
            Vector3 next;
            if (age < .45f) next = Vector3.Lerp(launch, apex, Mathf.Clamp01(age / .45f));
            else
            {
                var desired = (aimPoint - previous).normalized;
                flightDirection = Vector3.RotateTowards(flightDirection, desired, game.Settings.missileTurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0);
                next = previous + flightDirection * game.Settings.missileSpeed * Time.deltaTime;
            }
            var movement = next - previous;
            if (age >= .45f && movement.sqrMagnitude > .0001f)
            {
                bool solid = Physics.SphereCast(previous, .4f, movement.normalized, out var hit, movement.magnitude, TownPhysics.StructureMask, QueryTriggerInteraction.Ignore);
                if (Physics.SphereCast(previous, .4f, movement.normalized, out var personHit, movement.magnitude, TownPhysics.PedestrianMask, QueryTriggerInteraction.Collide) && (!solid || personHit.distance < hit.distance))
                { hit = personHit; solid = true; }
                if (solid) { Strike(hit.point, hit.collider.GetComponentInParent<TownMovingTarget>() == TrackingTarget && TrackingTarget != null); return; }
            }
            if (age >= .45f && TrackingTarget != null)
            {
                var segment = next - previous;
                float t = segment.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector3.Dot(TrackingTarget.AimPoint - previous, segment) / segment.sqrMagnitude) : 0;
                if ((TrackingTarget.AimPoint - (previous + segment * t)).sqrMagnitude < TrackingTarget.HitRadius * TrackingTarget.HitRadius)
                { Strike(TrackingTarget.AimPoint, true); return; }
            }
            transform.position = next;
            if (movement.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(movement.normalized);
            previous = next;
            if ((next - aimPoint).sqrMagnitude < 1 || age >= 3.2f) Strike(next);
        }
        private void Strike(Vector3 point, bool trackedHit = false)
        {
            owner.CountMissileStrike(trackedHit);
            game.MissileBlast(point);
            wave++; Launch();
        }
    }

    public sealed class GigantBomb : MonoBehaviour
    {
        public int Contacts { get; private set; }
        public bool Detonated { get; private set; }
        public bool GroundImpact { get; private set; }
        public bool StructureImpact { get; private set; }
        public Vector3 DetonationPoint { get; private set; }
        private TankTownGame game;
        private TankItems owner;
        private Rigidbody body;
        private Renderer visual;
        private MaterialPropertyBlock properties;
        public void Initialize(TankTownGame game, TankItems owner)
        {
            this.game = game; this.owner = owner;
            visual = GetComponent<Renderer>(); properties = new MaterialPropertyBlock();
            body = gameObject.AddComponent<Rigidbody>(); body.mass = 30;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            Vector3 forward = Vector3.ProjectOnPlane(game.Tank.Muzzle.forward, Vector3.up).normalized;
            body.linearVelocity = forward * game.Settings.gigantBombThrowSpeed + Vector3.up * game.Settings.gigantBombUpSpeed;
            body.angularVelocity = new Vector3(2, 1, 0);
            Physics.IgnoreCollision(GetComponent<Collider>(), game.Tank.HullCollider);
            var fuse = game.World.Box("Gigant bomb fuse", Vector3.up * .53f, new Vector3(.07f, .2f, .07f), game.World.ShotMaterial, transform, false);
            fuse.transform.localRotation = Quaternion.Euler(0, 0, -20);
            game.Effects.Trail(gameObject); GetComponent<TrailRenderer>().startWidth = 1.3f;
        }
        private void Update()
        {
            if (!game.IsPlaying) return;
            float flash = Mathf.Sin(Time.time * (owner.Remaining(ItemKind.GigantBomb) < 1 ? 28 : 12)) * .5f + .5f;
            properties.SetColor("_BaseColor", Color.Lerp(new Color(.12f, .15f, .22f), new Color(1, .18f, .04f), flash));
            visual.SetPropertyBlock(properties);
        }
        private void FixedUpdate()
        {
            if (!game.IsPlaying) return;
            var p = body.position;
            float edge = game.World.HalfExtent - 5;
            if (Mathf.Abs(p.x) > edge || Mathf.Abs(p.z) > edge)
            {
                p.x = Mathf.Clamp(p.x, -edge, edge); p.z = Mathf.Clamp(p.z, -edge, edge);
                body.position = p; body.linearVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up) * .1f + Vector3.up * body.linearVelocity.y;
            }
        }
        private void OnCollisionEnter(Collision hit) => Impact(hit);
        private void OnCollisionStay(Collision hit) => Impact(hit);
        private void Impact(Collision hit)
        {
            if (!game.IsPlaying || Detonated) return;
            if (hit.collider.gameObject.layer == TownPhysics.GroundLayer) { GroundImpact = true; Detonate(); return; }
            var block = hit.collider.GetComponentInParent<DestructibleBlock>();
            if (block == null || block.Detached) return;
            Contacts++; StructureImpact = true; Detonate();
        }
        public void Detonate()
        {
            if (Detonated) return;
            Detonated = true;
            DetonationPoint = body.position;
            if (game.IsPlaying)
            {
                game.DamageSphere(body.position, game.Settings.gigantBombRadius, game.Stats.BlastImpulse * game.Settings.gigantBombImpulseMultiplier);
                game.Effects.SuperImpact(body.position, game.Settings.gigantBombRadius);
                game.Sound.BigExplosion(body.position);
            }
            owner.BombDetonated(this);
            Destroy(gameObject);
        }
    }
}
