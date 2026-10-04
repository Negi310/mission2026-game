using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    public sealed class TankMotor : MonoBehaviour
    {
        public Transform Muzzle { get; set; }
        public Transform Visual { get; set; }
        public Transform Turret { get; set; }
        public float VehicleScale { get; private set; } = 1;
        public BoxCollider HullCollider { get; private set; }
        public float Cooldown01 => Mathf.Clamp01((nextShot - Time.time) / Mathf.Max(0.01f, game.Stats.ShotCooldown));
        public bool Respawning => respawnAt > 0;
        public bool Relocating => Respawning || returningToSpawn;
        public float ActualSpeed => body == null ? 0 : new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude;
        private TankTownGame game;
        private Rigidbody body;
        private float nextShot, respawnAt, nextSafeSample, protectedUntil;
        private bool returningToSpawn;
        private readonly List<Pose> safe = new List<Pose>();
        private Pose initial;
        private Vector3 commandedVelocity;
        private Vector3 groundUp = Vector3.up;
        private Vector3 hullHalfSize;
        private float steering;
        private float turretYaw, previousTurretYaw, delayedSteering;
        private Quaternion visualRotation = Quaternion.identity, previousVisualRotation = Quaternion.identity;
        private Quaternion hullRotation = Quaternion.identity, previousHullRotation = Quaternion.identity;
        private struct SteeringSample { public float time, value; }
        private readonly Queue<SteeringSample> steeringHistory = new Queue<SteeringSample>();
        private readonly Collider[] nearby = new Collider[64];

        public void Initialize(TankTownGame owner)
        {
            game = owner;
            body = gameObject.AddComponent<Rigidbody>();
            body.mass = 100;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            HullCollider = GetComponent<BoxCollider>();
            hullHalfSize = HullCollider.size * .5f;
            previousTurretYaw = turretYaw = Mathf.DeltaAngle(0, Turret.localEulerAngles.y);
            previousVisualRotation = visualRotation = Visual.localRotation;
            previousHullRotation = hullRotation = body.rotation;
            initial = new Pose(transform.position, transform.rotation);
            safe.Add(initial);
        }

        public void Fire(int pad)
        {
            // Provisional: both pads fire the same cannon; pad identity stays available at the input boundary.
            if (!game.IsPlaying || Relocating || game.Items.StopsForward || Time.time < nextShot) return;
            nextShot = Time.time + game.Stats.ShotCooldown;
            if (game.Items.UseHeld()) return;
            var shot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shot.name = "Cannonball";
            shot.transform.position = Muzzle.position;
            shot.transform.localScale = Vector3.one * 0.48f;
            shot.GetComponent<Collider>().enabled = false;
            Destroy(shot.GetComponent<Collider>());
            shot.GetComponent<Renderer>().sharedMaterial = game.World.ShotMaterial;
            shot.AddComponent<TankShell>().Initialize(game, Muzzle.forward);
            game.Effects.Trail(shot);
            game.Effects.Muzzle(Muzzle.position, Muzzle.forward);
            game.Sound.Cannon();
        }

        private void FixedUpdate()
        {
            // The turret responds first; the hull receives the wheel input after a short delay.
            body.angularVelocity = Vector3.zero;
            float dt = Time.fixedDeltaTime;
            bool driving = game.IsPlaying && !Relocating;
            float wheel = driving ? game.Input.Steering : 0;
            previousTurretYaw = turretYaw;
            previousVisualRotation = visualRotation;
            previousHullRotation = hullRotation;
            turretYaw = Mathf.MoveTowards(turretYaw, wheel * game.Settings.turretMaxYaw, game.Settings.turretTurnSpeed * dt);
            if (driving)
            {
                steeringHistory.Enqueue(new SteeringSample { time = Time.fixedTime, value = wheel });
                while (steeringHistory.Count > 0 && steeringHistory.Peek().time <= Time.fixedTime - game.Settings.hullTurnDelay)
                    delayedSteering = steeringHistory.Dequeue().value;
                steering = Mathf.MoveTowards(steering, delayedSteering, game.Settings.steeringResponse * dt);
            }
            else { steering = delayedSteering = 0; steeringHistory.Clear(); }
            var velocity = new Vector3(body.linearVelocity.x, 0, body.linearVelocity.z);
            var oldForward = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, Vector3.up).normalized;
            float currentSpeed = Mathf.Max(0, Vector3.Dot(velocity, oldForward));
            float multiplier = game.Items.SpeedMultiplier;
            float speed = driving && !game.Items.StopsForward ? Mathf.MoveTowards(currentSpeed, game.Stats.Speed * multiplier, game.Settings.acceleration * Mathf.Max(1, multiplier) * dt) : 0;
            float yawRate = driving && !game.Items.StopsForward ? game.Stats.TurnSpeed * steering : 0;
            var heading = Quaternion.Euler(0, body.rotation.eulerAngles.y + yawRate * dt, 0);
            var forward = heading * Vector3.forward;
            var right = heading * Vector3.right;
            float slip = driving && !game.Items.StopsForward ? Vector3.Dot(velocity, right) * Mathf.Exp(-game.Settings.tireGrip * dt) : 0;
            commandedVelocity = forward * speed + right * slip;
            // Look ahead over the footprint: a centre ray misses the nose at a ramp entrance.
            var nextPosition = body.position + commandedVelocity * dt;
            if (SampleGround(nextPosition, out _) &&
                SampleGround(nextPosition + forward * hullHalfSize.z, out float front) &&
                SampleGround(nextPosition - forward * hullHalfSize.z, out float rear) &&
                SampleGround(nextPosition + right * hullHalfSize.x, out float rightHeight) &&
                SampleGround(nextPosition - right * hullHalfSize.x, out float leftHeight))
            {
                var slopeUp = (Vector3.up - forward * ((front - rear) / (2 * hullHalfSize.z)) - right * ((rightHeight - leftHeight) / (2 * hullHalfSize.x))).normalized;
                slopeUp = Vector3.RotateTowards(Vector3.up, slopeUp, 55 * Mathf.Deg2Rad, 0);
                groundUp = Vector3.Slerp(groundUp, slopeUp, 1 - Mathf.Exp(-10 * dt));
            }
            var slopeForward = new Vector3(forward.x, -(groundUp.x * forward.x + groundUp.z * forward.z) / Mathf.Max(.35f, groundUp.y), forward.z).normalized;
            var rotation = Quaternion.LookRotation(slopeForward, groundUp);
            hullRotation = rotation;
            // Sweep the entire hull onto the real ground mesh, including banks and crests.
            // Only ground is sampled; buildings and flying debris retain physical collisions.
            var castOrigin = nextPosition + Vector3.up * 12;
            if (Physics.BoxCast(castOrigin, hullHalfSize, Vector3.down, out var support, rotation, 32, TownPhysics.GroundMask, QueryTriggerInteraction.Ignore))
            {
                float clearance = Mathf.Max(.05f, game.Settings.rideHeight * VehicleScale - hullHalfSize.y);
                float nextHeight = castOrigin.y - support.distance + clearance;
                commandedVelocity.y = (nextHeight - body.position.y) / dt;
                body.AddForce(-Physics.gravity, ForceMode.Acceleration);
            }
            else commandedVelocity.y = body.linearVelocity.y;
            body.MoveRotation(rotation);
            body.linearVelocity = commandedVelocity;
            if (!driving) return;
            visualRotation = Quaternion.Slerp(visualRotation, Quaternion.Euler(-(speed - currentSpeed) / dt * .12f, 0, -steering * Mathf.Clamp01(speed / game.Settings.driveSpeed) * 3), 1 - Mathf.Exp(-8 * dt));
            if (Time.time >= nextSafeSample)
            {
                nextSafeSample = Time.time + 0.6f;
                var pose = new Pose(body.position, body.rotation);
                if (IsSafe(pose))
                {
                    safe.Add(pose);
                    if (safe.Count > 9) safe.RemoveAt(0);
                }
            }
            if (Mathf.Abs(body.position.x) > game.World.HalfExtent - 1 || Mathf.Abs(body.position.z) > game.World.HalfExtent - 1)
            {
                if (game.Items.Invincible) { returningToSpawn = true; game.CameraRig.FadeOut(); }
                else Explode();
            }
        }

        private void LateUpdate()
        {
            // Child transforms need the same between-step interpolation as the hull Rigidbody.
            // Camera (order 50) and reticle (order 100) then use this exact rendered pose.
            float blend = Mathf.Clamp01((float)((Time.timeAsDouble - Time.fixedTimeAsDouble) / Time.fixedDeltaTime));
            // Set the visual world pose explicitly so parent physics rotation cannot add a step.
            Visual.rotation = Quaternion.Slerp(previousHullRotation, hullRotation, blend) * Quaternion.Slerp(previousVisualRotation, visualRotation, blend);
            Turret.localRotation = Quaternion.Euler(0, Mathf.LerpAngle(previousTurretYaw, turretYaw, blend), 0);
        }

        public void SetVehicleScale(float scale, bool bulldozer)
        {
            VehicleScale = scale;
            Visual.localScale = Vector3.one * scale;
            HullCollider.size = (bulldozer ? new Vector3(3.8f, 1.6f, 4.6f) : new Vector3(2.6f, 1.6f, 3.4f)) * scale;
            hullHalfSize = HullCollider.size * .5f;
            protectedUntil = Mathf.Max(protectedUntil, Time.time + .35f);
        }

        internal void Warp(Pose pose)
        {
            returningToSpawn = false;
            pose.position = game.World.GroundPoint(pose.position, game.Settings.rideHeight * VehicleScale);
            body.position = pose.position; body.rotation = pose.rotation;
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            Physics.SyncTransforms();
            body.linearVelocity = body.angularVelocity = Vector3.zero;
            steering = delayedSteering = previousTurretYaw = turretYaw = 0; steeringHistory.Clear();
            Turret.localRotation = Quaternion.identity;
            groundUp = Vector3.RotateTowards(Vector3.up, game.World.Terrain.NormalAt(pose.position), 30 * Mathf.Deg2Rad, 0);
            Visual.localRotation = Quaternion.identity;
            previousVisualRotation = visualRotation = Quaternion.identity;
            previousHullRotation = hullRotation = pose.rotation;
            protectedUntil = Time.time + 1.5f;
        }

        private bool SampleGround(Vector3 position, out float height)
        {
            bool found = Physics.Raycast(position + Vector3.up * 12, Vector3.down, out var hit, 32, TownPhysics.GroundMask, QueryTriggerInteraction.Ignore);
            height = hit.point.y;
            return found;
        }

        private bool IsSafe(Pose pose)
        {
            if (!Physics.Raycast(pose.position + Vector3.up * 2, Vector3.down, out var ground, 5, TownPhysics.GroundMask, QueryTriggerInteraction.Ignore) || ground.normal.y < Mathf.Cos(30 * Mathf.Deg2Rad)) return false;
            int count = Physics.OverlapBoxNonAlloc(pose.position + pose.rotation * Vector3.forward * 2, new Vector3(2, 1.4f, 4), nearby, pose.rotation, TownPhysics.StructureMask, QueryTriggerInteraction.Ignore);
            if (count == nearby.Length) return false;
            for (int i = 0; i < count; i++)
                if (nearby[i].GetComponentInParent<DestructibleBlock>() is DestructibleBlock block && !block.Detached && !block.CanCrush) return false;
            return Mathf.Abs(pose.position.x) < game.World.HalfExtent - 7 && Mathf.Abs(pose.position.z) < game.World.HalfExtent - 7;
        }

        private void Update()
        {
            if (!game.IsPlaying) return;
            if (returningToSpawn && game.CameraRig.TransitionCovered)
            {
                Warp(initial); game.CameraRig.Snap(); game.CameraRig.FadeIn();
                return;
            }
            if (!Respawning || Time.time < respawnAt || !game.CameraRig.TransitionCovered) return;
            var pose = initial;
            // Prefer positions a few seconds behind, then fall back to the guaranteed clear spawn road.
            for (int i = Mathf.Max(0, safe.Count - 4); i >= 0; i--)
                if (IsSafe(safe[i])) { pose = safe[i]; break; }
            Warp(pose);
            Visual.gameObject.SetActive(true);
            respawnAt = 0;
            protectedUntil = Time.time + 1.5f;
            game.CameraRig.Snap();
            game.CameraRig.FadeIn();
        }

        private void OnCollisionEnter(Collision hit) => HandleCollision(hit);
        private void OnCollisionStay(Collision hit) => HandleCollision(hit);

        private void HandleCollision(Collision hit)
        {
            if (!game.IsPlaying || Relocating || body.isKinematic) return;
            if (hit.collider.GetComponentInParent<FlyingDebris>() != null)
            {
                PushAside(hit.rigidbody);
                return;
            }
            var block = hit.collider.GetComponentInParent<DestructibleBlock>();
            if (block == null) return;
            if (game.Items.Invincible && !block.Detached)
            {
                game.Items.Ram(hit.GetContact(0).point);
                if (block.Detached) PushAside(block);
                return;
            }
            if (block.Detached)
            {
                PushAside(block);
                return;
            }
            if (block.CanCrush)
            {
                block.Break(transform.position, game.Stats.BlastImpulse * 0.7f);
                block.Building?.ReleaseUnsupported(transform.position, game.Stats.BlastImpulse);
                PushAside(block);
                return;
            }
            float impact = 0;
            for (int i = 0; i < hit.contactCount; i++)
                impact = Mathf.Max(impact, Vector3.Dot(commandedVelocity, -hit.GetContact(i).normal));
            if (Time.time >= protectedUntil && impact >= game.Settings.fatalImpactSpeed) Explode();
        }

        private void PushAside(DestructibleBlock block) => PushAside(block.GetComponent<Rigidbody>());

        private void PushAside(Rigidbody fragment)
        {
            if (fragment == null) return;
            var forward = body.rotation * Vector3.forward;
            var away = fragment.worldCenterOfMass - body.position;
            away.y = 0;
            if (away.sqrMagnitude < .01f) away = forward;
            var push = away.normalized * 9 + forward * game.Stats.Speed * .7f + Vector3.up * 3;
            fragment.linearVelocity = Vector3.Lerp(fragment.linearVelocity, push, .85f);
            body.linearVelocity = commandedVelocity;
            body.angularVelocity = Vector3.zero;
        }

        public void Explode()
        {
            if (!game.IsPlaying || Relocating || game.Items.Invincible) return;
            game.Items.OnTankCrash();
            game.Effects.Crash(transform.position + Vector3.up);
            game.CameraRig.FadeOut(Mathf.Max(0, game.Settings.respawnDelay - .35f));
            game.Sound.BigExplosion(transform.position);
            game.ApplyPenalty();
            Visual.gameObject.SetActive(false);
            respawnAt = Time.time + game.Settings.respawnDelay;
        }
    }

    public sealed class TankShell : MonoBehaviour
    {
        private TankTownGame game;
        private Vector3 direction;
        private float life, radius, impulse;
        public void Initialize(TankTownGame owner, Vector3 forward) { game = owner; direction = forward; radius = game.Stats.BlastRadius; impulse = game.Stats.BlastImpulse; }
        private void Update()
        {
            if (!game.IsPlaying) return;
            float distance = 52 * Time.deltaTime;
            if (Physics.SphereCast(transform.position, 0.25f, direction, out RaycastHit hit, distance, TownPhysics.StructureMask, QueryTriggerInteraction.Ignore))
            {
                game.Blast(hit.point, radius, impulse, hit.collider.GetComponentInParent<DestructibleBlock>());
                Destroy(gameObject);
                return;
            }
            transform.position += direction * distance;
            life += Time.deltaTime;
            if (life > 3) Destroy(gameObject);
        }
    }
}
