using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    // Shared render meshes preserve the original box colliders and part dimensions.
    internal sealed class ToyGeometry
    {
        private readonly Dictionary<(Vector3, bool), Mesh> boxes = new Dictionary<(Vector3, bool), Mesh>();
        private Mesh head;
        public Mesh LowPolyHead()
        {
            if (head != null) return head;
            var points = new List<Vector3>(); var triangles = new List<int>();
            Vector3 Point(int ring, int sector)
            {
                float y = ring * Mathf.PI / 3, a = sector * Mathf.PI / 4;
                return new Vector3(Mathf.Sin(y) * Mathf.Sin(a), Mathf.Cos(y), Mathf.Sin(y) * Mathf.Cos(a)) * .5f;
            }
            void Face(Vector3 a, Vector3 b, Vector3 c) { int n = points.Count; points.Add(a); points.Add(b); points.Add(c); triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2); }
            for (int r = 0; r < 3; r++) for (int s = 0; s < 8; s++)
            {
                var a = Point(r, s); var b = Point(r + 1, s); var c = Point(r + 1, s + 1); var d = Point(r, s + 1);
                if (r != 2) Face(a, b, c); if (r != 0) Face(a, c, d);
            }
            head = new Mesh { name = "32 face low poly head" }; head.SetVertices(points); head.SetTriangles(triangles, 0); head.RecalculateNormals(); head.RecalculateBounds(); return head;
        }

        public Mesh RoundedBox(Vector3 size, bool tapered = false)
        {
            var key = (size, tapered);
            if (boxes.TryGetValue(key, out var cached)) return cached;
            float radius = Mathf.Min(.16f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * .22f);
            Vector3 half = size * .5f, inner = half - Vector3.one * radius;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            Vector3[] directions = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var direction in directions)
            {
                Vector3 u = Mathf.Abs(direction.x) > .5f ? Vector3.up : Vector3.right;
                Vector3 v = Vector3.Cross(direction, u);
                float du = Vector3.Dot(half, Abs(u)), dv = Vector3.Dot(half, Abs(v));
                float[] xs = Grid(du, radius), ys = Grid(dv, radius);
                int start = vertices.Count;
                for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++)
                {
                    Vector3 point = Vector3.Scale(direction, half) + u * xs[x] + v * ys[y];
                    Vector3 core = new Vector3(Mathf.Clamp(point.x, -inner.x, inner.x), Mathf.Clamp(point.y, -inner.y, inner.y), Mathf.Clamp(point.z, -inner.z, inner.z));
                    Vector3 normal = (point - core).normalized;
                    Vector3 rounded = core + normal * radius;
                    if (tapered)
                    {
                        float height = rounded.y / size.y + .5f;
                        float sx = Mathf.Lerp(1, .8f, height), sz = Mathf.Lerp(1, .85f, height);
                        normal = new Vector3(normal.x / sx, normal.y + rounded.x * .2f / size.y * normal.x / sx + rounded.z * .15f / size.y * normal.z / sz, normal.z / sz).normalized;
                        rounded.x *= sx; rounded.z *= sz;
                    }
                    vertices.Add(new Vector3(rounded.x / size.x, rounded.y / size.y, rounded.z / size.z));
                    // Inverse-transpose scaling then recovers the physical bevel normal.
                    normals.Add(Vector3.Scale(normal, size).normalized);
                }
                for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
                {
                    int a = start + y * 4 + x, b = a + 1, c = a + 4, d = c + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(d);
                    triangles.Add(a); triangles.Add(d); triangles.Add(c);
                }
            }
            var mesh = new Mesh { name = (tapered ? "Toy sloped armor " : "Toy bevel ") + size };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0);
            // Keep shared geometry readable for per-building batching. One bevel strip
            // retains rounded highlights with 108 triangles instead of 300 per box.
            mesh.RecalculateBounds();
            boxes.Add(key, mesh);
            return mesh;
        }

        private static Vector3 Abs(Vector3 p) => new Vector3(Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z));
        private static float[] Grid(float half, float radius) => new[] { -half, -half + radius, half - radius, half };
        public void Dispose() { foreach (var mesh in boxes.Values) Object.Destroy(mesh); boxes.Clear(); if (head != null) Object.Destroy(head); }
    }
}
