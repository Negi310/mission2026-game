using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    // Street furniture and building trim made by scripts/blender/models/city_kit.py.
    // Each piece is a group of one-material meshes named "<piece>|<object>", in metres around the piece's anchor.
    // One material per mesh keeps building batching (one material per renderer) and per-part fading working.
    internal sealed class TownModelKit
    {
        private readonly struct Part
        {
            internal readonly string Name, Material;
            internal readonly Mesh Mesh;
            internal readonly Vector3 Position;
            internal Part(string name, Mesh mesh, string material, Vector3 position) { Name = name; Mesh = mesh; Material = material; Position = position; }
        }

        private readonly Dictionary<string, Part[]> pieces = new Dictionary<string, Part[]>();
        private readonly Dictionary<string, Material> palette;
        private readonly HashSet<Mesh> kitMeshes = new HashSet<Mesh>();
        private readonly Dictionary<Mesh, Mesh> distant = new Dictionary<Mesh, Mesh>();
        private readonly HashSet<string> missingPaints = new HashSet<string>();

        private TownModelKit(GameObject source, Dictionary<string, Material> palette)
        {
            this.palette = palette;
            foreach (Transform group in source.transform)
            {
                var parts = new List<Part>();
                foreach (Transform child in group)
                {
                    var filter = child.GetComponent<MeshFilter>(); var renderer = child.GetComponent<MeshRenderer>();
                    if (filter == null || renderer == null || renderer.sharedMaterial == null) continue;
                    int bar = child.name.IndexOf('|');
                    parts.Add(new Part(child.name.Substring(bar + 1), filter.sharedMesh, renderer.sharedMaterial.name, child.localPosition));
                    kitMeshes.Add(filter.sharedMesh);
                }
                pieces[group.name] = parts.ToArray();
            }
        }

        internal static TownModelKit Load(Dictionary<string, Material> palette)
        {
            var source = Resources.Load<GameObject>("Models/CityKit");
            if (source == null) { Debug.LogWarning("Models/CityKit is missing; using the box street furniture."); return null; }
            return new TownModelKit(source, palette);
        }

        internal bool Has(string piece) => pieces.ContainsKey(piece);

        // Places the piece's meshes directly under parent. The anchor is in the parent's metre-sized local frame;
        // the parent's scale is cancelled so the model keeps metre units, then stretched to fit its slot.
        internal void Attach(Transform parent, string piece, Vector3 anchor, Vector3 stretch, Dictionary<string, Material> paints = null)
        {
            if (!pieces.TryGetValue(piece, out var parts)) return;
            Vector3 scale = parent.lossyScale;
            foreach (var part in parts)
            {
                var item = new GameObject(part.Name, typeof(MeshFilter), typeof(MeshRenderer));
                item.transform.SetParent(parent, false);
                var point = anchor + Vector3.Scale(part.Position, stretch);
                item.transform.localPosition = new Vector3(point.x / scale.x, point.y / scale.y, point.z / scale.z);
                item.transform.localScale = new Vector3(stretch.x / scale.x, stretch.y / scale.y, stretch.z / scale.z);
                item.GetComponent<MeshFilter>().sharedMesh = part.Mesh;
                item.GetComponent<MeshRenderer>().sharedMaterial = Paint(part.Material, paints);
            }
        }

        internal void Attach(Transform parent, string piece, Dictionary<string, Material> paints = null)
            => Attach(parent, piece, Vector3.zero, Vector3.one, paints);

        // Road marks and other single-part pieces centred on their anchor draw on the item itself,
        // saving one GameObject for each of the thousands of marks.
        internal void Draw(GameObject item, string piece, Dictionary<string, Material> paints = null)
        {
            if (!pieces.TryGetValue(piece, out var parts)) return;
            if (parts.Length != 1 || parts[0].Position.sqrMagnitude > 1e-6f) { Attach(item.transform, piece, paints); return; }
            item.AddComponent<MeshFilter>().sharedMesh = parts[0].Mesh;
            item.AddComponent<MeshRenderer>().sharedMaterial = Paint(parts[0].Material, paints);
        }

        // The mesh of a single-material piece, such as a unit-sized tree canopy.
        internal Mesh Mesh(string piece) => pieces.TryGetValue(piece, out var parts) && parts.Length > 0 ? parts[0].Mesh : null;

        private Material Paint(string name, Dictionary<string, Material> paints)
        {
            if (paints != null && paints.TryGetValue(name, out var paint)) return paint;
            if (palette.TryGetValue(name, out paint)) return paint;
            if (missingPaints.Add(name)) Debug.LogWarning($"City kit material {name} has no game material.");
            return palette["CityFrame"];
        }

        // Distant buildings draw one box per renderer, sized by its transform. Kit meshes are not unit boxes,
        // so they get a box matching their own bounds instead.
        internal Mesh DistantShape(Mesh mesh)
        {
            if (mesh == null || !kitMeshes.Contains(mesh)) return null;
            if (distant.TryGetValue(mesh, out var shape)) return shape;
            shape = BoundsBox(mesh.bounds); shape.name = "Distant " + mesh.name;
            distant.Add(mesh, shape);
            return shape;
        }

        private static Mesh BoundsBox(Bounds bounds)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
            Vector3[] directions = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var normal in directions)
            {
                Vector3 u = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.up, v = Vector3.Cross(normal, u);
                int start = vertices.Count;
                foreach (var (a, b) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                {
                    vertices.Add(bounds.center + Vector3.Scale(normal + u * a + v * b, bounds.extents));
                    normals.Add(normal);
                }
                // Clockwise when seen from outside, Unity's front face.
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return mesh;
        }

        internal void Dispose()
        {
            foreach (var mesh in distant.Values) Object.Destroy(mesh);
            distant.Clear();
        }
    }
}
