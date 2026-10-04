using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankTown
{
    // Junctions are flat; smooth ramps connect them and each building plot has a flat pad.
    public sealed class TownTerrain
    {
        private readonly TankTownSettings settings;
        private readonly Transform parent;
        private readonly float roadWidth;
        private readonly TownLayout layout;
        private readonly List<Mesh> meshes = new List<Mesh>();
        public TownTerrain(TankTownSettings settings, Transform parent, float roadWidth, TownLayout layout = null)
        { this.settings = settings; this.parent = parent; this.roadWidth = roadWidth; this.layout = layout ?? new TownLayout(settings, settings.citySeed, roadWidth); }

        private float Junction(int x, int z)
            => layout.JunctionHeight(x, z);
        private float Ramp(float local)
        {
            float t = Mathf.Clamp01((local - roadWidth / 2) / (settings.blockSpacing - roadWidth));
            return t * t * (3 - 2 * t);
        }
        public float HeightAt(Vector3 position)
        {
            float spacing = settings.blockSpacing;
            int x = Mathf.FloorToInt(position.x / spacing), z = Mathf.FloorToInt(position.z / spacing);
            float lx = position.x - x * spacing, lz = position.z - z * spacing;
            float a = Junction(x, z), b = Junction(x + 1, z), c = Junction(x, z + 1), d = Junction(x + 1, z + 1);
            float road = Mathf.Lerp(Mathf.Lerp(a, b, Ramp(lx)), Mathf.Lerp(c, d, Ramp(lx)), Ramp(lz));
            float distance = Mathf.Min(Mathf.Min(lx, spacing - lx), Mathf.Min(lz, spacing - lz));
            float pad = Mathf.SmoothStep(0, 1, Mathf.Clamp01((distance - roadWidth / 2) / 2));
            return Mathf.Lerp(road, (a + b + c + d) / 4, pad);
        }
        public Vector3 NormalAt(Vector3 p)
        {
            const float step = .25f;
            float dx = HeightAt(p + Vector3.right * step) - HeightAt(p - Vector3.right * step);
            float dz = HeightAt(p + Vector3.forward * step) - HeightAt(p - Vector3.forward * step);
            return new Vector3(-dx, step * 2, -dz).normalized;
        }
        public void Surface(string label, Vector2 minimum, Vector2 size, float lift, Material material, bool solid = false)
        {
            int nx = Mathf.CeilToInt(size.x / 2), nz = Mathf.CeilToInt(size.y / 2);
            var vertices = new Vector3[(nx + 1) * (nz + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[nx * nz * 6];
            for (int z = 0; z <= nz; z++) for (int x = 0; x <= nx; x++)
            {
                int i = z * (nx + 1) + x;
                var p = new Vector3(minimum.x + size.x * x / nx, 0, minimum.y + size.y * z / nz);
                p.y = HeightAt(p) + lift;
                vertices[i] = p; uv[i] = new Vector2(p.x, p.z) * .05f;
            }
            for (int z = 0; z < nz; z++) for (int x = 0; x < nx; x++)
            {
                int i = z * (nx + 1) + x, t = (z * nx + x) * 6;
                triangles[t] = i; triangles[t + 1] = i + nx + 1; triangles[t + 2] = i + 1;
                triangles[t + 3] = i + 1; triangles[t + 4] = i + nx + 1; triangles[t + 5] = i + nx + 2;
            }
            var mesh = new Mesh { name = label, indexFormat = IndexFormat.UInt32, vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
            var item = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(parent, false);
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (solid) { item.layer = TownPhysics.GroundLayer; item.AddComponent<MeshCollider>().sharedMesh = mesh; }
        }
        public void Dispose() { foreach (var mesh in meshes) Object.Destroy(mesh); }
    }
}
