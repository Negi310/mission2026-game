using System;
using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    // Shared silhouettes only: seeds, waffle grooves and candy patterns live in textures.
    internal sealed class CandyGeometry
    {
        public Mesh Strawberry { get; } = Berry();
        public Mesh DoughnutSlice { get; } = RingSlice();
        public Mesh Sphere { get; } = Ball();
        public Mesh SugarStick { get; } = Cylinder();
        public Mesh CaneHook { get; } = Hook();
        public Mesh CreamRibbon { get; } = Ribbon();
        public Mesh DistantStrawberry { get; } = Berry(12, 6);
        public Mesh DistantSphere { get; } = Ball(12, 6);
        public Mesh TierStrawberry { get; } = Berry(16, 8);
        private readonly Dictionary<Vector3, Mesh> chocolateBars = new Dictionary<Vector3, Mesh>();
        private readonly Dictionary<Vector3, Mesh> chocolateBackings = new Dictionary<Vector3, Mesh>();
        internal Mesh DistantSilhouette(Mesh source)
            => source == null ? null : source == Strawberry || source == TierStrawberry ? DistantStrawberry : source == Sphere ? DistantSphere
                : source == DoughnutSlice || source.name.StartsWith("Chocolate backing") || source.name.StartsWith("Embossed chocolate") ? source : null;

        internal Mesh ChocolateBacking(Vector3 size, Mesh box)
        {
            if (chocolateBackings.TryGetValue(size, out var cached)) return cached;
            var mesh = UnityEngine.Object.Instantiate(box); mesh.name = "Chocolate backing " + size;
            var points = mesh.vertices; var normals = mesh.normals;
            for (int i = 0; i < points.Length; i++)
            { points[i].z = points[i].z * .25f - .35f; normals[i] = new Vector3(normals[i].x, normals[i].y, normals[i].z * 4).normalized; }
            mesh.vertices = points; mesh.normals = normals; mesh.RecalculateBounds(); chocolateBackings[size] = mesh; return mesh;
        }
        internal Mesh ChocolateBar(Vector3 size)
        {
            if (chocolateBars.TryGetValue(size, out var cached)) return cached;
            const int corners = 4, steps = 4, count = corners * (steps + 1);
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            // Thick rounded shoulders, a raised lip, then a shallow recessed face.
            float[] inset = { 0, 0, .06f, .15f, .25f, .31f };
            float[] depth = { -.5f, -.12f, .30f, .5f, .5f, .29f };
            float bevel = Mathf.Min(size.x, size.y) * .10f;
            for (int ring = 0; ring < inset.Length; ring++)
            {
                float hx = size.x * .5f - inset[ring], hy = size.y * .5f - inset[ring];
                float radius = Mathf.Min(bevel, Mathf.Min(hx, hy) * .35f);
                for (int corner = 0; corner < corners; corner++) for (int step = 0; step <= steps; step++)
                {
                    float angle = (corner + (float)step / steps) * Mathf.PI / 2;
                    float centerAngle = (corner + .5f) * Mathf.PI / 2;
                    float x = Mathf.Sign(Mathf.Cos(centerAngle)) * (hx - radius) + Mathf.Cos(angle) * radius;
                    float y = Mathf.Sign(Mathf.Sin(centerAngle)) * (hy - radius) + Mathf.Sin(angle) * radius;
                    vertices.Add(new Vector3(x / size.x, y / size.y, depth[ring])); uv.Add(new Vector2(x + size.x / 2, y + size.y / 2));
                }
            }
            for (int ring = 0; ring < inset.Length - 1; ring++) for (int i = 0; i < count; i++)
            {
                int a = ring * count + i, b = ring * count + (i + 1) % count, c = a + count, d = b + count;
                triangles.Add(a); triangles.Add(b); triangles.Add(d); triangles.Add(a); triangles.Add(d); triangles.Add(c);
            }
            for (int cap = 0; cap < 2; cap++)
            {
                int ring = cap == 0 ? 0 : inset.Length - 1, center = vertices.Count;
                vertices.Add(new Vector3(0, 0, depth[ring])); uv.Add(new Vector2(size.x / 2, size.y / 2));
                for (int i = 0; i < count; i++)
                { int a = ring * count + i, b = ring * count + (i + 1) % count; triangles.Add(center); triangles.Add(cap == 0 ? b : a); triangles.Add(cap == 0 ? a : b); }
            }
            var mesh = new Mesh { name = "Embossed chocolate " + size }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); chocolateBars[size] = mesh; return mesh;
        }

        private static Mesh Grid(string name, int columns, int rows, Func<int, int, Vector3> point,
            Func<int, int, Vector3> normal, Func<int, int, Vector2> uv)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var texcoords = new List<Vector2>();
            var triangles = new List<int>();
            for (int y = 0; y <= rows; y++) for (int x = 0; x <= columns; x++)
            { vertices.Add(point(x, y)); normals.Add(normal(x, y).normalized); texcoords.Add(uv(x, y)); }
            for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
            {
                int a = y * (columns + 1) + x, b = a + 1, c = a + columns + 1, d = c + 1;
                triangles.Add(a); triangles.Add(c); triangles.Add(b); triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }
            var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetNormals(normals);
            mesh.SetUVs(0, texcoords); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); return mesh;
        }
        private static Mesh Ball(int sides = 24, int rings = 12)
        {
            Vector3 Point(int x, int y)
            {
                float a = x * Mathf.PI * 2 / sides, b = y * Mathf.PI / rings;
                return new Vector3(Mathf.Sin(a) * Mathf.Sin(b), Mathf.Cos(b), Mathf.Cos(a) * Mathf.Sin(b)) * .5f;
            }
            return Grid("Smooth sugar sphere", sides, rings, Point, (x, y) => Point(x, y),
                (x, y) => new Vector2(Point(x, y).x + .5f, Point(x, y).y + .5f));
        }
        private static Mesh Berry(int sides = 24, int rings = 10)
        {
            float Radius(float t) => Mathf.Sin(t * Mathf.PI) * Mathf.Lerp(.32f, .56f, t);
            Vector3 Point(int x, int y)
            {
                float a = x * Mathf.PI * 2 / sides, t = (float)y / rings;
                return new Vector3(Mathf.Sin(a) * Radius(t), .5f - t, Mathf.Cos(a) * Radius(t));
            }
            Vector3 Normal(int x, int y)
            {
                float t = (float)y / rings, a = x * Mathf.PI * 2 / sides;
                float slope = (Radius(Mathf.Min(1, t + .001f)) - Radius(Mathf.Max(0, t - .001f))) / .002f;
                return new Vector3(Mathf.Sin(a), slope, Mathf.Cos(a));
            }
            return Grid("Smooth textured strawberry", sides, rings, Point, Normal,
                (x, y) => new Vector2((float)x / sides * 2, 1 - (float)y / rings));
        }
        private static Mesh Cylinder()
        {
            const int sides = 16;
            Vector3 Normal(int x, int y) { float a = x * Mathf.PI * 2 / sides; return new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)); }
            var mesh = Grid("Smooth sugar stick", sides, 1, (x, y) => Normal(x, y) * .5f + Vector3.up * (.5f - y), Normal,
                (x, y) => new Vector2((float)x / sides, y));
            var vertices = new List<Vector3>(mesh.vertices); var normals = new List<Vector3>(mesh.normals);
            var uv = new List<Vector2>(mesh.uv); var triangles = new List<int>(mesh.triangles);
            for (int cap = 0; cap < 2; cap++)
            {
                int start = vertices.Count; var center = Vector3.up * (.5f - cap); var capNormal = cap == 0 ? Vector3.up : Vector3.down;
                vertices.Add(center); normals.Add(capNormal); uv.Add(Vector2.one * .5f);
                for (int x = 0; x <= sides; x++) { var point = Normal(x, 0) * .5f; vertices.Add(center + point); normals.Add(capNormal); uv.Add(new Vector2(point.x + .5f, point.z + .5f)); }
                for (int x = 0; x < sides; x++) { triangles.Add(start); triangles.Add(start + x + (cap == 0 ? 1 : 2)); triangles.Add(start + x + (cap == 0 ? 2 : 1)); }
            }
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); return mesh;
        }
        private static Mesh Hook()
        {
            const int along = 20, around = 12;
            Vector3 Normal(int x, int y)
            {
                float a = Mathf.PI - x * Mathf.PI / along, b = y * Mathf.PI * 2 / around;
                return new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(a) * Mathf.Cos(b), Mathf.Sin(b));
            }
            Vector3 Point(int x, int y)
            {
                float a = Mathf.PI - x * Mathf.PI / along;
                return new Vector3(Mathf.Cos(a) * .5f, Mathf.Sin(a) * .5f, 0) + Vector3.Scale(Normal(x, y), new Vector3(.17f, .17f, .5f));
            }
            return Grid("Smooth candy cane bend", along, around, Point, (x, y) => Vector3.Scale(Normal(x, y), new Vector3(1 / .17f, 1 / .17f, 2)),
                (x, y) => new Vector2((float)y / around, (float)x / along));
        }
        private static Mesh RingSlice()
        {
            const int along = 4, around = 24;
            Vector3 Point(int x, int y)
            {
                float a = (-7.5f + x * 15f / along) * Mathf.Deg2Rad, b = y * Mathf.PI * 2 / around;
                float r = 12 + Mathf.Cos(b) * 2;
                return new Vector3(Mathf.Sin(a) * r / 3.8f, (Mathf.Cos(a) * r - 12) / 4.2f, Mathf.Sin(b) * 2 / 4.2f);
            }
            Vector3 Normal(int x, int y)
            {
                float a = (-7.5f + x * 15f / along) * Mathf.Deg2Rad, b = y * Mathf.PI * 2 / around;
                return Vector3.Scale(new Vector3(Mathf.Sin(a) * Mathf.Cos(b), Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(b)), new Vector3(3.8f, 4.2f, 4.2f));
            }
            var mesh = Grid("Smooth doughnut segment", along, around, Point, Normal,
                (x, y) => new Vector2((float)x / along, (float)y / around));
            // Close exposed cut ends, with independent cap normals.
            var vertices = new List<Vector3>(mesh.vertices); var normals = new List<Vector3>(mesh.normals);
            var uv = new List<Vector2>(mesh.uv); var triangles = new List<int>(mesh.triangles);
            foreach (int x in new[] { 0, along })
            {
                float a = (-7.5f + x * 15f / along) * Mathf.Deg2Rad;
                var capNormal = Vector3.Scale(new Vector3(Mathf.Cos(a), -Mathf.Sin(a), 0) * (x == 0 ? -1 : 1), new Vector3(3.8f, 4.2f, 4.2f)).normalized;
                Vector3 center = Vector3.zero; for (int y = 0; y < around; y++) center += Point(x, y) / around;
                int start = vertices.Count; vertices.Add(center); normals.Add(capNormal); uv.Add(Vector2.one * .5f);
                for (int y = 0; y <= around; y++) { vertices.Add(Point(x, y)); normals.Add(capNormal); float b = y * Mathf.PI * 2 / around; uv.Add(new Vector2(.5f + Mathf.Cos(b) * .5f, .5f + Mathf.Sin(b) * .5f)); }
                for (int y = 0; y < around; y++)
                { triangles.Add(start); triangles.Add(start + y + (x == 0 ? 2 : 1)); triangles.Add(start + y + (x == 0 ? 1 : 2)); }
            }
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); return mesh;
        }
        private static Mesh Ribbon()
        {
            const int columns = 24;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            float Bottom(int x) => -.27f - .23f * (.5f + .5f * Mathf.Cos(x * Mathf.PI * 6 / columns));
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
            {
                int start = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                for (int i = 0; i < 4; i++) { normals.Add(n); var p = vertices[start + i]; uv.Add(new Vector2(p.x + .5f, p.y + .5f)); }
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2); triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
            for (int x = 0; x < columns; x++)
            {
                float a = (float)x / columns - .5f, b = (float)(x + 1) / columns - .5f;
                float ya = Bottom(x), yb = Bottom(x + 1);
                Quad(new Vector3(a,ya,.42f), new Vector3(b,yb,.42f), new Vector3(b,.42f,.42f), new Vector3(a,.42f,.42f), Vector3.forward);
                Quad(new Vector3(b,yb,-.42f), new Vector3(a,ya,-.42f), new Vector3(a,.42f,-.42f), new Vector3(b,.42f,-.42f), Vector3.back);
                Quad(new Vector3(a,.42f,.42f), new Vector3(b,.42f,.42f), new Vector3(b,.5f,0), new Vector3(a,.5f,0), new Vector3(0,1,1).normalized);
                Quad(new Vector3(a,.5f,0), new Vector3(b,.5f,0), new Vector3(b,.42f,-.42f), new Vector3(a,.42f,-.42f), new Vector3(0,1,-1).normalized);
                Quad(new Vector3(b,yb,.42f), new Vector3(a,ya,.42f), new Vector3(a,ya,-.42f), new Vector3(b,yb,-.42f), new Vector3(yb-ya,a-b,0).normalized);
            }
            foreach (int end in new[] { 0, columns })
            { float x = (float)end / columns - .5f; float y = Bottom(end); var n = end == 0 ? Vector3.left : Vector3.right;
                if (end == 0) Quad(new Vector3(x,y,-.42f), new Vector3(x,y,.42f), new Vector3(x,.42f,.42f), new Vector3(x,.42f,-.42f), n);
                else Quad(new Vector3(x,y,.42f), new Vector3(x,y,-.42f), new Vector3(x,.42f,-.42f), new Vector3(x,.42f,.42f), n);
                var front = new Vector3(x,.42f,.42f); var back = new Vector3(x,.42f,-.42f); var peak = new Vector3(x,.5f,0);
                if (end == 0) Quad(front, peak, back, back, n); else Quad(back, peak, front, front, n); }
            var mesh = new Mesh { name = "Scalloped cream edge" }; mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); return mesh;
        }
        public void Dispose()
        {
            foreach (var mesh in new[] { Strawberry, DoughnutSlice, Sphere, SugarStick, CaneHook, CreamRibbon, DistantStrawberry, DistantSphere, TierStrawberry }) UnityEngine.Object.Destroy(mesh);
            foreach (var mesh in chocolateBars.Values) UnityEngine.Object.Destroy(mesh);
            foreach (var mesh in chocolateBackings.Values) UnityEngine.Object.Destroy(mesh);
        }
    }
}
