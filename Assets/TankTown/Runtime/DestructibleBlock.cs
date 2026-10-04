using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    public enum BuildingPart { Pillar, Beam, Slab, Wall, Prop }
    public sealed class DestructibleBlock : MonoBehaviour
    {
        public BlockBuilding Building { get; set; }
        public bool CanCrush { get; set; }
        public bool Detached { get; private set; }
        public int Column { get; set; }
        public int Level { get; set; }
        public BuildingPart Kind { get; set; }
        public bool Foundation { get; set; }
        public readonly List<DestructibleBlock> Links = new List<DestructibleBlock>();
        private TankTownGame game;

        public void Initialize(TankTownGame owner)
        {
            game = owner;
            game.RegisterBlock();
        }

        public void Break(Vector3 origin, float impulse, float forceRadius = 0)
        {
            if (Detached || game == null || !game.IsPlaying) return;
            Detached = true;
            Building?.RegisterDetached();
            var explosive = GetComponent<ChainExplosive>();
            explosive?.Ignite();
            game.ScoreBlock();
            foreach (var part in GetComponentsInChildren<Transform>()) part.gameObject.layer = TownPhysics.DebrisLayer;
            var body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
                body.mass = explosive != null && explosive.IsFuelTank ? 4 : Kind == BuildingPart.Slab ? 1.4f : .55f;
            }
            body.isKinematic = false;
            body.constraints = RigidbodyConstraints.None;
            body.maxAngularVelocity = 12;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.AddExplosionForce(impulse, origin, forceRadius > 0 ? forceRadius : game.Stats.BlastRadius * 2, 1.5f, ForceMode.Impulse);
            body.AddTorque(Random.insideUnitSphere * impulse * 0.3f, ForceMode.Impulse);
            // A primed car/tank must finish its short fuse even when a huge collapse
            // evicts ordinary debris. It destroys itself on detonation after 0.18s.
            if (explosive == null || !explosive.Primed)
            {
                gameObject.AddComponent<DebrisFade>().Initialize(game.Settings.debrisLifetime);
                game.TrackDebris(gameObject);
            }
            Destroy(gameObject, game.Settings.debrisLifetime);
        }
    }

    // Only detached pieces need a per-frame fade; intact city blocks have no Update.
    internal sealed class DebrisFade : MonoBehaviour
    {
        private Renderer[] debrisRenderers;
        private MaterialPropertyBlock debrisProperties;
        private float debrisBornAt, debrisLifetime;
        private static readonly int DebrisVisibilityId = Shader.PropertyToID("_DebrisVisibility");

        public void Initialize(float lifetime)
        {
            debrisBornAt = Time.time;
            debrisLifetime = lifetime;
            debrisRenderers = GetComponentsInChildren<Renderer>();
            debrisProperties = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (debrisRenderers == null) return;
            float age = Time.time - debrisBornAt;
            if (age < debrisLifetime - .45f) return;
            float visibility = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(debrisLifetime - .45f, debrisLifetime, age));
            foreach (var renderer in debrisRenderers)
            {
                if (renderer == null || renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(DebrisVisibilityId)) continue;
                // Preserve the camera's separate occlusion fade and any material tint.
                renderer.GetPropertyBlock(debrisProperties);
                debrisProperties.SetFloat(DebrisVisibilityId, visibility);
                renderer.SetPropertyBlock(debrisProperties);
            }
        }
    }
}
