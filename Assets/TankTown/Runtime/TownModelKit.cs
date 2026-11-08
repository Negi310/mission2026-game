using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    // Street furniture, building trim and cars made by scripts/blender/models/city_kit.py, and candy house
    // decorations made by candy_kit.py. Each piece is a group of one-material meshes named "<piece>|<object>",
    // in metres around the piece's anchor.
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
        private readonly HashSet<Mesh> roundMeshes = new HashSet<Mesh>();
        private readonly HashSet<Mesh> hiddenFar = new HashSet<Mesh>();
        private Mesh hiddenShape;
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

        internal static TownModelKit Load(Dictionary<string, Material> palette, string resource = "Models/CityKit")
        {
            var source = Resources.Load<GameObject>(resource);
            if (source == null) { Debug.LogWarning(resource + " is missing; using the box models."); return null; }
            return new TownModelKit(source, palette);
        }

        // Rounded pieces, such as candy roof toppers, fade into an ellipsoid instead of a box in the distance.
        internal void RoundInDistance(params string[] names)
        {
            foreach (var name in names)
                if (pieces.TryGetValue(name, out var parts)) foreach (var part in parts) roundMeshes.Add(part.Mesh);
        }

        // Small trim, such as bulbs, flags and rings of seats, is left out far away: its bounding box would draw as a solid block.
        internal void HideInDistance(params string[] roles)
        {
            var names = new HashSet<string>(roles);
            foreach (var parts in pieces.Values)
                foreach (var part in parts) if (names.Contains(part.Name)) hiddenFar.Add(part.Mesh);
        }

        internal bool Has(string piece) => pieces.ContainsKey(piece);

        // Places the piece's meshes directly under parent. The anchor is in the parent's metre-sized local frame;
        // the parent's scale is cancelled so the model keeps metre units, then stretched to fit its slot.
        internal void Attach(Transform parent, string piece, Vector3 anchor, Vector3 stretch, Dictionary<string, Material> paints = null)
            => Attach(parent, piece, anchor, stretch, Quaternion.identity, paints);

        // The rotation turns the piece about its anchor. Cancelling the parent's scale stays exact for turns about Y
        // when the parent's X and Z scales match, and for half turns on any parent.
        internal void Attach(Transform parent, string piece, Vector3 anchor, Vector3 stretch, Quaternion rotation, Dictionary<string, Material> paints = null)
        {
            if (!pieces.TryGetValue(piece, out var parts)) return;
            Vector3 scale = parent.lossyScale;
            foreach (var part in parts)
            {
                var item = new GameObject(part.Name, typeof(MeshFilter), typeof(MeshRenderer));
                item.transform.SetParent(parent, false);
                var point = anchor + rotation * Vector3.Scale(part.Position, stretch);
                item.transform.localPosition = new Vector3(point.x / scale.x, point.y / scale.y, point.z / scale.z);
                item.transform.localRotation = rotation;
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

        // Adds the piece to per-material combine lists, for backdrops drawn as a few batched meshes.
        // The placement scales the piece about its anchor, like Attach's stretch.
        internal void Combine(Dictionary<Material, List<CombineInstance>> groups, string piece, Matrix4x4 placement, Dictionary<string, Material> paints = null)
        {
            if (!pieces.TryGetValue(piece, out var parts)) return;
            foreach (var part in parts)
            {
                var material = Paint(part.Material, paints);
                if (!groups.TryGetValue(material, out var list)) groups[material] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = part.Mesh, transform = placement * Matrix4x4.Translate(part.Position) });
            }
        }

        // Adds the piece to a shared mesh being combined, such as one traffic car shape for every car.
        // Parts painted with firstMaterial go first, so that material becomes submesh 0.
        internal void Combine(TownMeshWorkspace workspace, string piece, Matrix4x4 placement, Dictionary<string, Material> paints = null, string firstMaterial = null)
        {
            if (!pieces.TryGetValue(piece, out var parts)) return;
            foreach (bool first in new[] { true, false })
                foreach (var part in parts)
                    if ((part.Material == firstMaterial) == first)
                        workspace.Add(Paint(part.Material, paints), part.Mesh, placement * Matrix4x4.Translate(part.Position));
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
            if (hiddenFar.Contains(mesh))
            {
                // One zero-area triangle keeps the batching input valid while drawing nothing.
                if (hiddenShape == null) { hiddenShape = new Mesh { name = "Hidden far away" }; hiddenShape.SetVertices(new[] { Vector3.zero, Vector3.zero, Vector3.zero }); hiddenShape.SetTriangles(new[] { 0, 1, 2 }, 0); }
                return hiddenShape;
            }
            if (distant.TryGetValue(mesh, out var shape)) return shape;
            shape = roundMeshes.Contains(mesh) ? Ellipsoid(mesh.bounds) : BoundsBox(mesh.bounds); shape.name = "Distant " + mesh.name;
            distant.Add(mesh, shape);
            return shape;
        }

        // Both distant shapes carry metre-sized UVs so textured candy materials keep their tone far away.
        private static Mesh BoundsBox(Bounds bounds)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>(); var triangles = new List<int>();
            Vector3[] directions = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var normal in directions)
            {
                Vector3 u = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.up, v = Vector3.Cross(normal, u);
                int start = vertices.Count;
                foreach (var (a, b) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                {
                    var point = bounds.center + Vector3.Scale(normal + u * a + v * b, bounds.extents);
                    vertices.Add(point); normals.Add(normal); uvs.Add(new Vector2(Vector3.Dot(point, u), Vector3.Dot(point, v)));
                }
                // Clockwise when seen from outside, Unity's front face.
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh Ellipsoid(Bounds bounds, int around = 12, int rings = 6)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>(); var triangles = new List<int>();
            Vector3 e = bounds.extents;
            for (int r = 0; r <= rings; r++)
            {
                float polar = Mathf.PI * r / rings;
                for (int a = 0; a <= around; a++)
                {
                    float azimuth = 2 * Mathf.PI * a / around;
                    var unit = new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(azimuth));
                    vertices.Add(bounds.center + Vector3.Scale(unit, e));
                    normals.Add(new Vector3(unit.x / Mathf.Max(e.x, 1e-4f), unit.y / Mathf.Max(e.y, 1e-4f), unit.z / Mathf.Max(e.z, 1e-4f)).normalized);
                    uvs.Add(new Vector2(azimuth * Mathf.Max(e.x, e.z), polar * e.y));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int a = 0; a < around; a++)
                {
                    int i = r * (around + 1) + a, below = i + around + 1;
                    // Clockwise when seen from outside, Unity's front face.
                    triangles.AddRange(new[] { i, i + 1, below, i + 1, below + 1, below });
                }
            var mesh = new Mesh();
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return mesh;
        }

        internal void Dispose()
        {
            foreach (var mesh in distant.Values) Object.Destroy(mesh);
            distant.Clear();
            if (hiddenShape != null) Object.Destroy(hiddenShape);
        }
    }
}
