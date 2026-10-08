using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    internal sealed class CandyGeometry
    {
        public Mesh Strawberry { get; } = Berry();
        public Mesh DoughnutSlice { get; } = RingSlice();
        private static Mesh Build(string name, System.Action<System.Action<Vector3, Vector3, Vector3>> populate)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            void Face(Vector3 a, Vector3 b, Vector3 c) { int n = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2); }
            populate(Face);
            var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        private static Mesh Berry() => Build("Faceted strawberry", face =>
        {
            float[] ys = { -.5f, -.28f, .16f, .5f }, radii = { .03f, .28f, .48f, .22f };
            Vector3 Point(int r, int s) { float a = s * Mathf.PI / 4; return new Vector3(Mathf.Sin(a) * radii[r], ys[r], Mathf.Cos(a) * radii[r]); }
            for (int r = 0; r < 3; r++) for (int s = 0; s < 8; s++)
            { var a = Point(r, s); var b = Point(r + 1, s); var c = Point(r + 1, s + 1); var d = Point(r, s + 1); face(a, c, b); face(a, d, c); }
            for (int s = 0; s < 8; s++) { face(Vector3.up * .5f, Point(3, s), Point(3, s + 1)); face(Vector3.down * .5f, Point(0, s + 1), Point(0, s)); }
        });
        private static Mesh RingSlice() => Build("Faceted doughnut segment", face =>
        {
            Vector3 Point(int a, int b)
            {
                float angle = (-7.5f + a * 7.5f) * Mathf.Deg2Rad, around = b * Mathf.PI / 4;
                float radius = 12 + Mathf.Cos(around) * 2;
                return new Vector3(Mathf.Sin(angle) * radius / 3.8f, (Mathf.Cos(angle) * radius - 12) / 4.2f, Mathf.Sin(around) * 2 / 4.2f);
            }
            for (int a = 0; a < 2; a++) for (int b = 0; b < 8; b++)
            { var p = Point(a, b); var q = Point(a + 1, b); var r = Point(a + 1, b + 1); var s = Point(a, b + 1); face(p, r, q); face(p, s, r); }
            foreach (int a in new[] { 0, 2 })
            {
                Vector3 center = Vector3.zero; for (int b = 0; b < 8; b++) center += Point(a, b) / 8;
                for (int b = 0; b < 8; b++) { if (a == 0) face(center, Point(a, b + 1), Point(a, b)); else face(center, Point(a, b), Point(a, b + 1)); }
            }
        });
        public void Dispose() { Object.Destroy(Strawberry); Object.Destroy(DoughnutSlice); }
    }
}
