using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    internal sealed class AmusementGeometry
    {
        internal Mesh Sphere { get; } = CreateSphere();
        internal Mesh Star { get; } = CreateStar();
        internal Mesh RoofPanel { get; } = CreateRoofPanel();
        internal Mesh FloorPanel { get; } = CreateFloorPanel();
        internal Mesh DistantSilhouette(Mesh source)
            => source == Sphere || source == Star || source == RoofPanel || source == FloorPanel ? source : null;

        private static Mesh CreateSphere()
        {
            const int sides = 16, rings = 8;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int r = 0; r <= rings; r++) for (int s = 0; s <= sides; s++)
            {
                float a = s * Mathf.PI * 2 / sides, b = r * Mathf.PI / rings;
                vertices.Add(new Vector3(Mathf.Sin(b) * Mathf.Cos(a), Mathf.Cos(b), Mathf.Sin(b) * Mathf.Sin(a)) * .5f);
            }
            for (int r = 0; r < rings; r++) for (int s = 0; s < sides; s++)
            {
                int a = r * (sides + 1) + s, b = a + sides + 1;
                triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
            }
            var mesh = new Mesh { name = "Amusement smooth balloon" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < vertices.Count; i++) normals[i] = vertices[i].normalized;
            mesh.normals = normals; mesh.RecalculateBounds(); return mesh;
        }

        private static Mesh CreateStar()
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            void Face(Vector3 a, Vector3 b, Vector3 c)
            { int index = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2); }
            Vector3 Point(int index, float z)
            {
                float angle = index * Mathf.PI / 5, radius = index % 2 == 0 ? .5f : .23f;
                return new Vector3(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius, z);
            }
            for (int i = 0; i < 10; i++)
            {
                var a = Point(i, -.5f); var b = Point(i + 1, -.5f);
                var c = Point(i, .5f); var d = Point(i + 1, .5f);
                Face(new Vector3(0, 0, -.5f), a, b); Face(new Vector3(0, 0, .5f), d, c);
                Face(a, c, d); Face(a, d, b);
            }
            var mesh = new Mesh { name = "Amusement extruded star" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private static Mesh CreateRoofPanel()
        {
            var mesh = new Mesh { name = "Amusement canopy wedge" };
            mesh.vertices = new[]
            {
                new Vector3(0, .5f, -.5f), new Vector3(-.5f, -.5f, .5f), new Vector3(.5f, -.5f, .5f),
                new Vector3(0, .43f, -.5f), new Vector3(-.5f, -.57f, .5f), new Vector3(.5f, -.57f, .5f)
            };
            mesh.triangles = new[] { 0, 1, 2, 3, 5, 4, 0, 3, 4, 0, 4, 1, 1, 4, 5, 1, 5, 2, 2, 5, 3, 2, 3, 0 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        private static Mesh CreateFloorPanel()
        {
            var mesh = CreateRoofPanel(); mesh.name = "Amusement circular platform slice";
            var points = mesh.vertices;
            for (int i = 0; i < points.Length; i++) points[i].y = i < 3 ? .5f : -.5f;
            mesh.vertices = points; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        internal void Dispose() { Object.Destroy(Sphere); Object.Destroy(Star); Object.Destroy(RoofPanel); Object.Destroy(FloorPanel); }
    }
}
