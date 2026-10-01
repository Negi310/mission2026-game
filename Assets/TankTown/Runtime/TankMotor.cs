using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    public sealed class TankMotor : MonoBehaviour
    {
        public Transform Muzzle { get; set; }
        public Transform Visual { get; set; }
        public float Cooldown01 => Mathf.Clamp01((nextShot - Time.time) / Mathf.Max(0.01f, game.Stats.ShotCooldown));
        public bool Respawning => respawnAt > 0;
        public float ActualSpeed => body == null ? 0 : new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude;
        private TankTownGame game;
        private Rigidbody body;
        private float nextShot, respawnAt, nextSafeSample, protectedUntil;
        private readonly List<Pose> safe = new List<Pose>();
        private Pose initial;
        private Vector3 commandedVelocity;
        private Vector3 groundUp = Vector3.up;
        private float steering;
        private readonly Collider[] nearby = new Collider[64];

        public void Initialize(TankTownGame owner)
        {
            game = owner;
            body = gameObject.AddComponent<Rigidbody>();
            body.mass = 100;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            initial = new Pose(transform.position, transform.rotation);
            safe.Add(initial);
        }

        public void Fire(int pad)
        {
            // Provisional: both pads fire the same cannon; pad identity stays available at the input boundary.
            if (!game.IsPlaying || Respawning || Time.time < nextShot) return;
            nextShot = Time.time + game.Stats.ShotCooldown;
            var shot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shot.name = "Cannonball";
            shot.transform.position = Muzzle.position;
            shot.transform.localScale = Vector3.one * 0.48f;
            shot.GetComponent<Collider>().enabled = false;
            Destroy(shot.GetComponent<Collider>());
            shot.GetComponent<Renderer>().sharedMaterial = game.World.ShotMaterial;
            shot.AddComponent<TankShell>().Initialize(game, transform.forward);
            game.Effects.Trail(shot);
            game.Effects.Muzzle(Muzzle.position, transform.forward);
            game.CameraRig.Kick(.055f);
            game.PlayTone(130, 0.12f, 0.22f);
        }

        private void FixedUpdate()
        {
            bool grounded = Physics.Raycast(body.position + Vector3.up * 4, Vector3.down, out var ground, 12, TownPhysics.GroundMask, QueryTriggerInteraction.Ignore);
            float verticalCorrection = grounded ? Mathf.Clamp((ground.point.y + game.Settings.rideHeight - body.position.y) * 12, -12, 12) : body.linearVelocity.y;
            if (grounded)
            {
                var slopeUp = Vector3.RotateTowards(Vector3.up, ground.normal, 30 * Mathf.Deg2Rad, 0);
                groundUp = Vector3.Slerp(groundUp, slopeUp, 1 - Mathf.Exp(-10 * Time.fixedDeltaTime));
                body.AddForce(-Physics.gravity, ForceMode.Acceleration);
            }
            if (!game.IsPlaying || Respawning)
            {
                body.linearVelocity = Vector3.up * verticalCorrection;
                body.angularVelocity = Vector3.zero;
                steering = 0;
                return;
            }
            // Bicycle steering preserves momentum. Collision torque still cannot steer the tank.
            body.angularVelocity = Vector3.zero;
            float dt = Time.fixedDeltaTime;
            steering = Mathf.MoveTowards(steering, game.Input.Steering, game.Settings.steeringResponse * dt);
            var velocity = new Vector3(body.linearVelocity.x, 0, body.linearVelocity.z);
            var oldForward = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, Vector3.up).normalized;
            float currentSpeed = Mathf.Max(0, Vector3.Dot(velocity, oldForward));
            float speed = Mathf.MoveTowards(currentSpeed, game.Stats.Speed, game.Settings.acceleration * dt);
            float yawRate = currentSpeed / game.Settings.wheelbase * Mathf.Tan(steering * game.Settings.steeringAngle * Mathf.Deg2Rad) * Mathf.Rad2Deg * (game.Stats.TurnSpeed / 85);
            var heading = Quaternion.Euler(0, body.rotation.eulerAngles.y + yawRate * dt, 0);
            var forward = heading * Vector3.forward;
            var right = heading * Vector3.right;
            float slip = Vector3.Dot(velocity, right) * Mathf.Exp(-game.Settings.tireGrip * dt);
            commandedVelocity = forward * speed + right * slip;
            float slopeSpeed = grounded ? -(ground.normal.x * commandedVelocity.x + ground.normal.z * commandedVelocity.z) / Mathf.Max(.35f, ground.normal.y) : 0;
            commandedVelocity.y = grounded ? slopeSpeed + verticalCorrection : body.linearVelocity.y;
            var slopeForward = new Vector3(forward.x, -(groundUp.x * forward.x + groundUp.z * forward.z) / Mathf.Max(.35f, groundUp.y), forward.z).normalized;
            body.MoveRotation(Quaternion.LookRotation(slopeForward, groundUp));
            body.linearVelocity = commandedVelocity;
            Visual.localRotation = Quaternion.Slerp(Visual.localRotation, Quaternion.Euler(-(speed - currentSpeed) / dt * .12f, 0, -steering * Mathf.Clamp01(speed / 9) * 3), 1 - Mathf.Exp(-8 * dt));
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
            if (Mathf.Abs(body.position.x) > game.World.HalfExtent - 1 || Mathf.Abs(body.position.z) > game.World.HalfExtent - 1) Explode();
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
            if (!game.IsPlaying || !Respawning || Time.time < respawnAt) return;
            var pose = initial;
            // Prefer positions a few seconds behind, then fall back to the guaranteed clear spawn road.
            for (int i = Mathf.Max(0, safe.Count - 4); i >= 0; i--)
                if (IsSafe(safe[i])) { pose = safe[i]; break; }
            body.position = pose.position;
            body.rotation = pose.rotation;
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            Physics.SyncTransforms();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            steering = 0;
            groundUp = Vector3.RotateTowards(Vector3.up, game.World.Terrain.NormalAt(pose.position), 30 * Mathf.Deg2Rad, 0);
            Visual.localRotation = Quaternion.identity;
            Visual.gameObject.SetActive(true);
            respawnAt = 0;
            protectedUntil = Time.time + 1.5f;
            game.CameraRig.Snap();
        }

        private void OnCollisionEnter(Collision hit) => HandleCollision(hit);
        private void OnCollisionStay(Collision hit) => HandleCollision(hit);

        private void HandleCollision(Collision hit)
        {
            if (!game.IsPlaying || Respawning) return;
            var block = hit.collider.GetComponentInParent<DestructibleBlock>();
            if (block == null) return;
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

        private void PushAside(DestructibleBlock block)
        {
            var fragment = block.GetComponent<Rigidbody>();
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
            if (!game.IsPlaying || Respawning) return;
            game.Effects.Crash(transform.position + Vector3.up);
            game.CameraRig.Kick(.2f);
            game.PlayTone(60, 0.3f, 0.25f);
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
                game.Blast(hit.point, radius, impulse);
                Destroy(gameObject);
                return;
            }
            transform.position += direction * distance;
            life += Time.deltaTime;
            if (life > 3) Destroy(gameObject);
        }
    }
}
