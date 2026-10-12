using UnityEngine;
using UnityEngine.Rendering;

namespace TankTown
{
    // The collection volume stays at the demolished building, independently of the magnetized item.
    public sealed class SpecialItemArea : MonoBehaviour
    {
        public float Radius { get; private set; }
        public Vector3 Center => transform.position;
        private TankTownGame game;
        private ItemPickup item;
        private Mesh curtain, floor;
        private const int Segments = 64;

        internal void Initialize(TankTownGame owner, ItemPickup reward, BlockBuilding building)
        {
            game = owner; item = reward;
            gameObject.layer = TownPhysics.RewardLayer;
            transform.position = game.World.GroundPoint(building.transform.position);
            Radius = Mathf.Max(game.Settings.specialAreaMinRadius, building.Width * game.Settings.specialAreaWidthRatio);
            var trigger = gameObject.AddComponent<SphereCollider>(); trigger.isTrigger = true;
            trigger.center = Vector3.up * .75f; trigger.radius = Radius;
            var color = ItemIcons.Color(item.Kind);
            var vertices = new Vector3[(Segments + 1) * 2]; var uv = new Vector2[vertices.Length];
            var triangles = new int[Segments * 6];
            var fillVertices = new Vector3[Segments + 2]; var fillUv = new Vector2[fillVertices.Length];
            var fillColors = new Color[fillVertices.Length]; var fillTriangles = new int[Segments * 3];
            fillVertices[0] = Vector3.up * .13f; fillUv[0] = Vector2.one * .5f; fillColors[0] = new Color(color.r, color.g, color.b, .05f);
            var ringRoot = new GameObject("Reward area glowing rim"); ringRoot.layer = TownPhysics.RewardLayer; ringRoot.transform.SetParent(transform, false);
            var ring = ringRoot.AddComponent<LineRenderer>(); ring.sharedMaterial = game.Effects.Glow;
            ring.useWorldSpace = false; ring.loop = true; ring.positionCount = Segments;
            ring.startWidth = ring.endWidth = .72f;
            ring.startColor = ring.endColor = Color.Lerp(color, Color.white, .55f) * 1.35f;
            ring.shadowCastingMode = ShadowCastingMode.Off; ring.receiveShadows = false;
            var haloRoot = new GameObject("Reward area rim halo"); haloRoot.layer = TownPhysics.RewardLayer; haloRoot.transform.SetParent(transform, false);
            var halo = haloRoot.AddComponent<LineRenderer>(); halo.sharedMaterial = game.Effects.Glow;
            halo.useWorldSpace = false; halo.loop = true; halo.positionCount = Segments;
            halo.startWidth = halo.endWidth = 2.1f;
            halo.startColor = halo.endColor = new Color(color.r, color.g, color.b, .2f);
            halo.shadowCastingMode = ShadowCastingMode.Off; halo.receiveShadows = false;
            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments, angle = t * Mathf.PI * 2;
                var point = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * Radius;
                point.y = game.World.Terrain.HeightAt(Center + point) - Center.y + .14f;
                vertices[i * 2] = point; vertices[i * 2 + 1] = point + Vector3.up * game.Settings.specialAreaLightHeight;
                uv[i * 2] = new Vector2(t, 0); uv[i * 2 + 1] = new Vector2(t, 1);
                fillVertices[i + 1] = point; fillUv[i + 1] = Vector2.one * .5f; fillColors[i + 1] = new Color(color.r, color.g, color.b, .15f);
                if (i == Segments) continue;
                ring.SetPosition(i, point); halo.SetPosition(i, point);
                int n = i * 2, index = i * 6;
                triangles[index] = n; triangles[index + 1] = n + 1; triangles[index + 2] = n + 2;
                triangles[index + 3] = n + 2; triangles[index + 4] = n + 1; triangles[index + 5] = n + 3;
                fillTriangles[i * 3] = 0; fillTriangles[i * 3 + 1] = i + 2; fillTriangles[i * 3 + 2] = i + 1;
            }
            curtain = new Mesh { name = "Rising light around reward area", vertices = vertices, uv = uv, triangles = triangles };
            curtain.RecalculateBounds(); curtain.UploadMeshData(true);
            var glow = MeshObject("Reward area rising light", curtain, game.Effects.RewardAreaLight);
            var tint = new MaterialPropertyBlock(); tint.SetColor("_Color", new Color(color.r, color.g, color.b, .8f)); glow.SetPropertyBlock(tint);
            floor = new Mesh { name = "Reward area ground circle", vertices = fillVertices, uv = fillUv, colors = fillColors, triangles = fillTriangles };
            floor.RecalculateBounds(); floor.UploadMeshData(true);
            MeshObject("Reward area ground circle", floor, game.Effects.Glow);
        }

        private MeshRenderer MeshObject(string label, Mesh mesh, Material material)
        {
            var root = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer)); root.layer = TownPhysics.RewardLayer; root.transform.SetParent(transform, false);
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return renderer;
        }
        internal bool ContainsHorizontal(Vector3 point)
        {
            Vector3 offset = point - Center; return offset.x * offset.x + offset.z * offset.z <= Radius * Radius;
        }
        internal bool Contains(Vector3 point) => ContainsHorizontal(point) && Mathf.Abs(point.y - Center.y) <= 5;
        private void Touch(Collider other)
        {
            if (other.gameObject.layer == TownPhysics.TankLayer && other.GetComponentInParent<TankMotor>() == game.Tank && Contains(game.Tank.transform.position)) item.Collect();
        }
        private void OnTriggerEnter(Collider other) => Touch(other);
        private void OnTriggerStay(Collider other) => Touch(other);
        private void OnDestroy() { if (curtain != null) Destroy(curtain); if (floor != null) Destroy(floor); }
    }
}
