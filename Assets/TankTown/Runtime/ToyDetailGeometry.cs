using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    internal sealed partial class ToyGeometry
    {
        private readonly Mesh[] canopies = new Mesh[3];
        private readonly Dictionary<Vector3, Mesh> windowFrames = new Dictionary<Vector3, Mesh>();
        private Mesh trunk;

        internal Mesh WindowFrame(Vector3 size)
        {
            if (windowFrames.TryGetValue(size, out var existing)) return existing;
            float border = Mathf.Min(.16f, Mathf.Min(size.x, size.y) * .10f);
            var pieces = new List<CombineInstance>();
            foreach (int side in new[] { -1, 1 })
            {
                var upright = new Vector3(border, size.y, size.z);
                pieces.Add(new CombineInstance { mesh = RoundedBox(upright, false, .045f), transform = Matrix4x4.TRS(new Vector3(side * (size.x - border) / 2, 0, 0), Quaternion.identity, upright) });
                var sill = new Vector3(size.x - border * 2, border, size.z);
                pieces.Add(new CombineInstance { mesh = RoundedBox(sill, false, .045f), transform = Matrix4x4.TRS(new Vector3(0, side * (size.y - border) / 2, 0), Quaternion.identity, sill) });
            }
            var mesh = new Mesh { name = "Hollow bevel window frame " + size }; mesh.CombineMeshes(pieces.ToArray(), true, true);
            var points = mesh.vertices; var normals = mesh.normals;
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new Vector3(points[i].x / size.x, points[i].y / size.y, points[i].z / size.z);
                normals[i] = Vector3.Scale(normals[i], size).normalized;
            }
            mesh.vertices = points; mesh.normals = normals; mesh.RecalculateBounds(); windowFrames.Add(size, mesh); return mesh;
        }

        internal Mesh TreeTrunk()
        {
            if (trunk != null) return trunk;
            var pieces = new List<CombineInstance>();
            void Branch(Vector3 bottom, Vector3 top, float width)
            {
                Vector3 delta = top - bottom, size = new Vector3(width, delta.magnitude, width);
                pieces.Add(new CombineInstance { mesh = RoundedBox(size, true, .12f), transform = Matrix4x4.TRS((top + bottom) / 2, Quaternion.FromToRotation(Vector3.up, delta.normalized), size) });
            }
            Branch(Vector3.zero, new Vector3(.08f, 3.25f, 0), .46f);
            Branch(new Vector3(0, 1.7f, 0), new Vector3(-.85f, 2.8f, .20f), .22f);
            Branch(new Vector3(0, 1.9f, 0), new Vector3(.75f, 3.0f, -.25f), .20f);
            Branch(new Vector3(0, 1.7f, 0), new Vector3(.12f, 2.7f, .76f), .18f);
            trunk = new Mesh { name = "Shared tapered trunk and branches" }; trunk.CombineMeshes(pieces.ToArray(), true, true); trunk.RecalculateBounds(); return trunk;
        }

        internal Mesh TreeCanopy(int variant)
        {
            variant %= canopies.Length;
            if (canopies[variant] != null) return canopies[variant];
            var points = new List<Vector3>(); var normals = new List<Vector3>(); var colors = new List<Color>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            const int sides = 10, rings = 5;
            for (int lobe = 0; lobe < 7; lobe++)
            {
                float angle = (lobe - 1) * Mathf.PI / 3 + variant * .42f;
                var center = lobe == 0 ? new Vector3(.02f, .16f, -.02f) : new Vector3(Mathf.Cos(angle) * .22f, -.025f + Mathf.Sin(lobe * 2.3f + variant) * .08f, Mathf.Sin(angle) * .22f);
                var scale = new Vector3(.28f + lobe % 3 * .025f, .30f + (lobe + variant) % 3 * .045f, .29f);
                if (variant == 1) { center.y *= 1.5f; scale.y *= 1.16f; scale.x *= .85f; }
                if (variant == 2) { center.y *= .7f; scale.y *= .76f; scale.x *= 1.1f; }
                int first = points.Count;
                for (int y = 0; y <= rings; y++) for (int x = 0; x <= sides; x++)
                {
                    float a = x * Mathf.PI * 2 / sides, b = y * Mathf.PI / rings;
                    var direction = new Vector3(Mathf.Sin(b) * Mathf.Cos(a), Mathf.Cos(b), Mathf.Sin(b) * Mathf.Sin(a));
                    var point = center + Vector3.Scale(direction, scale); points.Add(point);
                    normals.Add(new Vector3(direction.x / scale.x, direction.y / scale.y, direction.z / scale.z).normalized);
                    float shade = .84f + Mathf.Clamp01(point.y + .45f) * .24f + Mathf.Sin(lobe * 2.4f + variant) * .055f;
                    colors.Add(new Color(shade * .92f, shade, shade * .90f, 1)); uv.Add(new Vector2(x / (float)sides, y / (float)rings));
                    if (y == rings || x == sides) continue;
                    int n = first + y * (sides + 1) + x;
                    triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + sides + 1);
                    triangles.Add(n + 1); triangles.Add(n + sides + 2); triangles.Add(n + sides + 1);
                }
            }
            var mesh = new Mesh { name = "Shared clustered tree canopy " + variant };
            mesh.SetVertices(points); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            canopies[variant] = mesh; return mesh;
        }

        private void DisposeDetails()
        {
            foreach (var mesh in canopies) if (mesh != null) Object.Destroy(mesh);
            foreach (var mesh in windowFrames.Values) Object.Destroy(mesh);
            if (trunk != null) Object.Destroy(trunk);
        }
    }
}
