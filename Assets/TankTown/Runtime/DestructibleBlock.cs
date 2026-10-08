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
        internal bool PreserveShape { get; set; }
        internal bool CameraExposed { get; private set; }
        internal bool RenderSeparately => CameraExposed || Durability01 < .9999f;
        internal void ExposeForCamera(bool value)
        {
            if (CameraExposed == value) return;
            CameraExposed = value; Building?.RegisterDamage();
        }
        public float Durability01 => Detached ? 0 : Mathf.Clamp01(1 - accumulatedDamage / MaxDurability);
        public readonly List<DestructibleBlock> Links = new List<DestructibleBlock>();
        public IReadOnlyList<Rigidbody> DebrisBodies => looseBodies;
        private readonly List<Rigidbody> looseBodies = new List<Rigidbody>();
        private TankTownGame game;
        private float accumulatedDamage;
        private MaterialPropertyBlock damageProperties;
        private float MaxDurability => Building != null && game != null ? Mathf.Max(1, game.Settings.buildingDurability) : 1;
        private static readonly int StructuralDamageId = Shader.PropertyToID("_StructuralDamage");

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
            // Restore real renderers before splitting so detached pieces remain visible.
            if (Building?.Presentation != null)
                foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = true;
            var explosive = GetComponent<ChainExplosive>();
            explosive?.Ignite();
            game.ScoreBlock();
            SplitLargePart(explosive, origin, impulse, forceRadius);
            ConfigureDebris(gameObject, explosive, origin, impulse, forceRadius);
        }

        internal bool DamageFromBlast(Vector3 origin, float damage, float impulse, float forceRadius)
        {
            if (Detached || game == null || !game.IsPlaying || damage <= 0) return false;
            accumulatedDamage += damage;
            if (accumulatedDamage + .0001f >= MaxDurability) Break(origin, impulse, forceRadius);
            else
            {
                Building?.RegisterDamage();
                if (damageProperties == null) damageProperties = new MaterialPropertyBlock();
                foreach (var renderer in GetComponentsInChildren<Renderer>())
                {
                    if (renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(StructuralDamageId)) continue;
                    renderer.GetPropertyBlock(damageProperties);
                    damageProperties.SetFloat(StructuralDamageId, 1 - Durability01);
                    renderer.SetPropertyBlock(damageProperties);
                }
            }
            return true;
        }

        private void SplitLargePart(ChainExplosive explosive, Vector3 origin, float impulse, float forceRadius)
        {
            var mesh = GetComponent<MeshFilter>();
            var renderer = GetComponent<MeshRenderer>();
            var box = GetComponent<BoxCollider>();
            // 柱や車・燃料、木の複合形状は維持し、大きな板・梁・屋根を分割する。
            if (PreserveShape || explosive != null || Kind == BuildingPart.Pillar || mesh == null || renderer == null || box == null || box.center != Vector3.zero || box.size != Vector3.one) return;
            Vector3 size = transform.localScale;
            float target = Mathf.Max(1, game.Settings.fragmentSize);
            var divisions = new Vector3Int(Mathf.Clamp(Mathf.CeilToInt(size.x / target), 1, 4),
                Mathf.Clamp(Mathf.CeilToInt(size.y / target), 1, 4), Mathf.Clamp(Mathf.CeilToInt(size.z / target), 1, 4));
            while (divisions.x * divisions.y * divisions.z > 12)
            {
                if (divisions.x >= divisions.y && divisions.x >= divisions.z) divisions.x--;
                else if (divisions.y >= divisions.z) divisions.y--;
                else divisions.z--;
            }
            if (divisions == Vector3Int.one) return;
            Matrix4x4 original = transform.localToWorldMatrix;
            Quaternion rotation = transform.rotation;
            var parent = transform.parent;
            var decorations = new List<Transform>();
            foreach (Transform child in transform) decorations.Add(child);
            foreach (var child in decorations) child.SetParent(parent, true);
            var pieces = new List<Transform>();
            Vector3 pieceSize = new Vector3(size.x / divisions.x, size.y / divisions.y, size.z / divisions.z);
            var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
            for (int x = 0; x < divisions.x; x++) for (int y = 0; y < divisions.y; y++) for (int z = 0; z < divisions.z; z++)
            {
                Transform piece;
                if (pieces.Count == 0) piece = transform;
                else
                {
                    var item = new GameObject(name + " fragment", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
                    piece = item.transform; piece.SetParent(parent, false);
                    item.GetComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                    var fragmentRenderer = item.GetComponent<MeshRenderer>();
                    fragmentRenderer.sharedMaterials = renderer.sharedMaterials;
                    if (!properties.isEmpty) fragmentRenderer.SetPropertyBlock(properties);
                    game.CameraRig.InheritOcclusion(renderer, fragmentRenderer);
                }
                var localCenter = new Vector3((x + .5f) / divisions.x - .5f, (y + .5f) / divisions.y - .5f, (z + .5f) / divisions.z - .5f);
                piece.SetPositionAndRotation(original.MultiplyPoint3x4(localCenter), rotation);
                piece.localScale = pieceSize;
                pieces.Add(piece);
            }
            // 窓などの装飾は元の世界位置を保って近い破片へ付け替える。
            foreach (var child in decorations)
            {
                Transform nearest = pieces[0]; float distance = float.MaxValue;
                foreach (var piece in pieces)
                {
                    float candidate = (piece.position - child.position).sqrMagnitude;
                    if (candidate < distance) { nearest = piece; distance = candidate; }
                }
                child.SetParent(nearest, true);
            }
            for (int i = 1; i < pieces.Count; i++) ConfigureDebris(pieces[i].gameObject, null, origin, impulse, forceRadius);
        }

        private void ConfigureDebris(GameObject piece, ChainExplosive explosive, Vector3 origin, float impulse, float forceRadius)
        {
            foreach (var part in piece.GetComponentsInChildren<Transform>()) part.gameObject.layer = TownPhysics.DebrisLayer;
            var body = piece.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = piece.AddComponent<Rigidbody>();
                body.mass = explosive != null && explosive.IsFuelTank ? 4 : Kind == BuildingPart.Slab ? .65f : .55f;
            }
            looseBodies.Add(body);
            body.isKinematic = false;
            body.constraints = RigidbodyConstraints.None;
            body.maxAngularVelocity = 12;
            // Speculative continuous collision handles flying slabs without the costly CCD sweep for every shard.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            if (explosive == null) { body.solverIterations = 2; body.solverVelocityIterations = 1; body.sleepThreshold = .05f; }
            body.AddExplosionForce(impulse, origin, forceRadius > 0 ? forceRadius : game.Stats.BlastRadius * 2, 2.8f, ForceMode.Impulse);
            body.AddTorque(Random.insideUnitSphere * impulse * 0.3f, ForceMode.Impulse);
            // A primed car/tank must finish its short fuse even when a huge collapse
            // evicts ordinary debris. It destroys itself on detonation after 0.18s.
            float lifetime = game.Settings.debrisLifetime;
            if (explosive == null || !explosive.Primed)
            {
                lifetime = game.NextDebrisLifetime();
                var fragment = piece.AddComponent<FlyingDebris>(); fragment.Initialize(game, this);
                fragment.Fade = piece.AddComponent<DebrisFade>(); fragment.Fade.Initialize(lifetime, fragment.Renderers);
                game.TrackDebris(piece);
            }
            Destroy(piece, lifetime);
        }

        internal void TipDebris(Vector3 direction)
        {
            foreach (var body in looseBodies)
            {
                if (body == null || !body.gameObject.activeInHierarchy) continue;
                body.AddForce(direction * 3.5f + Vector3.down, ForceMode.VelocityChange);
                body.AddTorque(Vector3.Cross(Vector3.up, direction) * 2.2f, ForceMode.VelocityChange);
            }
        }
    }

    // Only detached pieces need a per-frame fade; intact city blocks have no Update.
    internal sealed class DebrisFade : MonoBehaviour
    {
        private Renderer[] debrisRenderers;
        private MaterialPropertyBlock debrisProperties;
        private float debrisBornAt, debrisLifetime;
        internal float Lifetime => debrisLifetime;
        private static readonly int DebrisVisibilityId = Shader.PropertyToID("_DebrisVisibility");

        public void Initialize(float lifetime, Renderer[] renderers = null)
        {
            debrisBornAt = Time.time;
            debrisLifetime = lifetime;
            debrisRenderers = renderers ?? GetComponentsInChildren<Renderer>();
            enabled = false; // The owner's registry ticks all debris; no per-piece Unity Update.
        }

        internal void Tick()
        {
            if (debrisRenderers == null) return;
            float age = Time.time - debrisBornAt;
            if (age < debrisLifetime - .45f) return;
            if (debrisProperties == null) debrisProperties = new MaterialPropertyBlock();
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
