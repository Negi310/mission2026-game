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
        private TankTownGame game;
        private Rigidbody body;
        private float nextShot, respawnAt, nextSafeSample, protectedUntil;
        private readonly List<Pose> safe = new List<Pose>();
        private Pose initial;
        private Vector3 commandedVelocity;
        private readonly Collider[] nearby = new Collider[64];

        public void Initialize(TankTownGame owner)
        {
            game = owner;
            body = gameObject.AddComponent<Rigidbody>();
            body.mass = 100;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;
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
            if (!game.IsPlaying || Respawning)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                return;
            }
            body.MoveRotation(body.rotation * Quaternion.Euler(0, game.Input.Steering * game.Stats.TurnSpeed * Time.fixedDeltaTime, 0));
            commandedVelocity = transform.forward * game.Stats.Speed;
            body.linearVelocity = commandedVelocity;
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
            int count = Physics.OverlapBoxNonAlloc(pose.position + pose.rotation * Vector3.forward * 2, new Vector3(2, 1.4f, 4), nearby, pose.rotation, TownPhysics.StructureMask, QueryTriggerInteraction.Ignore);
            if (count == nearby.Length) return false;
            for (int i = 0; i < count; i++)
                if (nearby[i].GetComponent<DestructibleBlock>() is DestructibleBlock block && !block.Detached && !block.CanCrush) return false;
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
            var block = hit.collider.GetComponent<DestructibleBlock>();
            if (block == null) return;
            if (block.Detached)
            {
                var fragment = block.GetComponent<Rigidbody>();
                if (fragment != null)
                {
                    var away = fragment.worldCenterOfMass - body.position;
                    away.y = 0;
                    if (away.sqrMagnitude < .01f) away = transform.right;
                    var push = away.normalized * 9 + transform.forward * game.Stats.Speed * .7f + Vector3.up * 3;
                    fragment.linearVelocity = Vector3.Lerp(fragment.linearVelocity, push, .85f);
                    body.linearVelocity = commandedVelocity;
                }
                return;
            }
            if (block.CanCrush)
            {
                block.Break(transform.position, game.Stats.BlastImpulse * 0.7f);
                block.Building?.ReleaseUnsupported(transform.position, game.Stats.BlastImpulse);
                return;
            }
            float impact = 0;
            for (int i = 0; i < hit.contactCount; i++)
                impact = Mathf.Max(impact, Vector3.Dot(commandedVelocity, -hit.GetContact(i).normal));
            if (Time.time >= protectedUntil && impact >= game.Settings.fatalImpactSpeed) Explode();
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
