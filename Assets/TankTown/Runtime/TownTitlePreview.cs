using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankTown
{
    // A fixed large-town backdrop with the same seeded plots and terrain as gameplay.
    // It contains render meshes only: no destructible parts, traffic, scripts or colliders.
    internal sealed class TownTitlePreview
    {
        internal const int Layer = 13;
        internal GameObject Root { get; }
        internal float HalfExtent { get; }
        internal float HighestRoof { get; private set; }
        internal TownLayout Layout { get; }
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly TownTerrain terrain;
        private readonly TankTownSettings settings;
        private readonly Mesh cube;

        internal TownTitlePreview(TankTownGame game)
        {
            Root = new GameObject("Large town title preview");
            var sourceSettings = Resources.Load<TankTownSettings>("TankTownSettings");
            settings = sourceSettings != null ? Object.Instantiate(sourceSettings) : ScriptableObject.CreateInstance<TankTownSettings>();
            settings.cityHalfBlocks *= 2; settings.cityParks *= 4;
            HalfExtent = settings.cityHalfBlocks * settings.blockSpacing + 28;
            float roadWidth = Mathf.Clamp(settings.roadWidth, 8, settings.blockSpacing - 14);
            Layout = new TownLayout(settings, game.CitySeed, roadWidth);
            terrain = new TownTerrain(settings, Root.transform, roadWidth, Layout);
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = source.GetComponent<MeshFilter>().sharedMesh;
            source.SetActive(false); Object.Destroy(source);
            var world = game.World;
            var ground = world.MakeMaterial(new Color(1, .94f, .77f), ToySurface.Concrete);
            var road = world.MakeMaterial(new Color(.13f, .2f, .32f), ToySurface.Concrete);
            road.SetFloat("_GroundDetail", 1); road.SetFloat("_Gloss", .035f);
            var frame = world.MakeMaterial(new Color(.85f, .91f, 1), ToySurface.Concrete);
            var glass = world.MakeMaterial(new Color(.04f, .34f, .59f), ToySurface.Glass);
            var dark = world.MakeMaterial(new Color(.055f, .09f, .16f), ToySurface.Metal);
            var grass = world.MakeMaterial(new Color(.12f, .84f, .42f), ToySurface.Organic);
            var sand = world.MakeMaterial(new Color(1, .81f, .45f), ToySurface.Concrete);
            var sea = world.MakeMaterial(new Color(.035f, .62f, .82f), ToySurface.Glass);
            var paints = new Material[6];
            for (int i = 0; i < paints.Length; i++) paints[i] = world.MakeMaterial(ItemIcons.Color((ItemKind)i), ToySurface.Concrete);
            terrain.Surface("Preview ground", Vector2.one * -HalfExtent, Vector2.one * HalfExtent * 2, 0, grass, sampleSpacing: 4);
            float paving = HalfExtent * 2 - 8;
            terrain.Surface("Preview paving", Vector2.one * -paving / 2, Vector2.one * paving, .015f, ground, sampleSpacing: 4);
            var streets = new Dictionary<Material, List<CombineInstance>>();
            float spacing = settings.blockSpacing;
            for (int i = -settings.cityHalfBlocks; i <= settings.cityHalfBlocks; i++)
            {
                float lane = i * spacing;
                terrain.Surface("Preview avenue", new Vector2(lane - roadWidth / 2, -paving / 2), new Vector2(roadWidth, paving), .045f, road, sampleSpacing: 4);
                terrain.Surface("Preview avenue", new Vector2(-paving / 2, lane - roadWidth / 2), new Vector2(paving, roadWidth), .05f, road, sampleSpacing: 4);
                for (float d = -paving / 2 + 2; d < paving / 2; d += 4)
                {
                    if (Mathf.Abs(d - Mathf.Round(d / spacing) * spacing) < roadWidth / 2 + 1) continue;
                    Box(streets, ground, At(new Vector3(lane, 0, d), .08f), new Vector3(.32f, .02f, 2.1f));
                    Box(streets, ground, At(new Vector3(d, 0, lane), .08f), new Vector3(2.1f, .02f, .32f));
                }
            }
            Emit("Preview lane markings", streets);
            foreach (var plot in Layout.Plots)
            {
                Vector3 center = At(plot.Center); float width = plot.Width, height = settings.floorHeight;
                var parts = new Dictionary<Material, List<CombineInstance>>();
                if (plot.Park)
                {
                    terrain.Surface("Preview park", new Vector2(center.x - 10, center.z - 10), Vector2.one * 20, .04f, grass, sampleSpacing: 4);
                    continue;
                }
                var paint = paints[(int)plot.Reward];
                float pillar = Mathf.Clamp(settings.pillarWidth, .6f, width / 5), h = (width - pillar) / 2;
                foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                    Box(parts, frame, center + new Vector3(x * h, plot.Floors * height / 2, z * h), new Vector3(pillar, plot.Floors * height, pillar));
                for (int level = 0; level < plot.Floors; level++)
                {
                    float y = level * height;
                    Box(parts, level == plot.Floors - 1 ? ground : frame, center + Vector3.up * (y + height - .12f), new Vector3(width - pillar, .24f, width - pillar));
                    for (int side = 0; side < 4; side++)
                    {
                        Quaternion rotation = Quaternion.Euler(0, side * 90, 0);
                        Box(parts, frame, center + rotation * new Vector3(0, y + height - .25f, h), new Vector3(width - pillar, .5f, .65f), rotation);
                        foreach (int panel in new[] { -1, 1 })
                        {
                            float x = panel * (width - 1.6f) / 4, panelWidth = (width - 1.6f) / 2 - .08f;
                            var pos = new Vector3(x, y + height / 2 - .12f, width / 2 - .2f);
                            Box(parts, paint, center + rotation * pos, new Vector3(panelWidth, height - .65f, .28f), rotation);
                            foreach (int window in new[] { -1, 1 })
                            {
                                if (level == 0 && side == 2 && window == -panel) continue;
                                Box(parts, glass, center + rotation * (pos + new Vector3(window * panelWidth * .24f, (height - .65f) * .03f, .1442f)), new Vector3(panelWidth * .32f, (height - .65f) * .55f, .007f), rotation);
                            }
                        }
                    }
                }
                // Match the main south-facing doorway without extra objects or physics.
                float doorWidth = plot.Style == 0 ? 3.4f : 2.9f, doorHeight = 2.45f;
                Vector3 doorway = center + Vector3.back * (width / 2 + .16f);
                Box(parts, dark, doorway + Vector3.up * doorHeight / 2, new Vector3(doorWidth, doorHeight, .12f));
                foreach (int side in new[] { -1, 1 })
                {
                    Box(parts, frame, doorway + new Vector3(side * (doorWidth / 2 + .1f), doorHeight / 2, -.08f), new Vector3(.24f, doorHeight, .25f));
                    Box(parts, glass, doorway + new Vector3(side * doorWidth / 4, doorHeight * .65f, -.13f), new Vector3(doorWidth / 2 - .24f, doorHeight * .58f, .05f));
                }
                Box(parts, frame, doorway + new Vector3(0, doorHeight + .08f, -.08f), new Vector3(doorWidth + .45f, .3f, .3f));
                Box(parts, paint, doorway + new Vector3(0, doorHeight + .42f, -.45f), new Vector3(doorWidth + .95f, .16f, 1.4f));
                Box(parts, ground, doorway + new Vector3(0, .08f, -.35f), new Vector3(doorWidth + .6f, .16f, 1.0f));
                if (plot.Style == 2)
                {
                    foreach (int side in new[] { -1, 1 })
                        Box(parts, paint, center + new Vector3(0, plot.Floors * height + .65f, side * width / 4), new Vector3(width + .3f, .3f, width * .54f), Quaternion.Euler(side * 15, 0, 0));
                }
                else
                {
                    float crown = plot.Style == 0 ? 3 : 1.2f;
                    Box(parts, plot.Style == 0 ? paint : dark, center + Vector3.up * (plot.Floors * height + crown / 2), new Vector3(width * .3f, crown, width * .3f));
                }
                HighestRoof = Mathf.Max(HighestRoof, center.y + plot.Floors * height + 3);
                Emit("Preview building", parts);
            }
            var ocean = new Dictionary<Material, List<CombineInstance>>();
            Box(ocean, sea, new Vector3(0, -3.8f, 0), new Vector3(4000, .12f, 4000)); Emit("Preview ocean", ocean);
            // Use a continuous sloped shoreline rather than a vertical cut at the edge.
            var coastVertices = new List<Vector3>(); var coastIndices = new List<int>();
            for (int side = 0; side < 4; side++) for (int n = 0; n < 68; n++)
            {
                var rotation = Quaternion.Euler(0, side * 90, 0);
                float a = -HalfExtent + HalfExtent * 2 * n / 68, b = -HalfExtent + HalfExtent * 2 * (n + 1) / 68;
                int start = coastVertices.Count;
                coastVertices.Add(At(rotation * new Vector3(a, 0, HalfExtent)));
                coastVertices.Add(At(rotation * new Vector3(b, 0, HalfExtent)));
                coastVertices.Add(rotation * new Vector3(a * 1.06f, -3.65f, HalfExtent + 16));
                coastVertices.Add(rotation * new Vector3(b * 1.06f, -3.65f, HalfExtent + 16));
                coastIndices.AddRange(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3 });
            }
            var coast = new Mesh { name = "Preview coast" }; coast.SetVertices(coastVertices); coast.SetTriangles(coastIndices, 0); coast.RecalculateNormals();
            MeshObject("Preview coast", coast, new[] { sand }); meshes.Add(coast);
            foreach (var renderer in world.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.gameObject.name != "Distant island" && renderer.transform.parent?.name != "Soft toy cloud") continue;
                var filter = renderer.GetComponent<MeshFilter>(); if (filter == null) continue;
                var copy = new GameObject("Preview scenery", typeof(MeshFilter), typeof(MeshRenderer)); copy.transform.SetParent(Root.transform);
                Vector3 pos = renderer.transform.position;
                var center = renderer.transform.parent?.name == "Soft toy cloud" ? renderer.transform.parent.position : pos;
                pos += new Vector3(center.x, 0, center.z) * (2f / game.StageScale - 1);
                copy.transform.SetPositionAndRotation(pos, renderer.transform.rotation); copy.transform.localScale = renderer.transform.lossyScale;
                copy.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh; copy.GetComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }
            foreach (Transform item in Root.GetComponentsInChildren<Transform>()) item.gameObject.layer = Layer;
        }

        private Vector3 At(Vector3 point, float lift = 0) { point.y = terrain.HeightAt(point) + lift; return point; }
        private void Box(Dictionary<Material, List<CombineInstance>> groups, Material material, Vector3 position, Vector3 size, Quaternion rotation = default)
        {
            if (!groups.TryGetValue(material, out var list)) groups[material] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(position, Quaternion.Dot(rotation, rotation) < .5f ? Quaternion.identity : rotation, size) });
        }
        private void Emit(string label, Dictionary<Material, List<CombineInstance>> groups)
        {
            var combinations = new List<CombineInstance>(); var materials = new List<Material>();
            var temporary = new List<Mesh>();
            foreach (var group in groups)
            {
                var piece = new Mesh { indexFormat = IndexFormat.UInt32 }; piece.CombineMeshes(group.Value.ToArray(), true, true);
                combinations.Add(new CombineInstance { mesh = piece, transform = Matrix4x4.identity }); materials.Add(group.Key); temporary.Add(piece);
            }
            var mesh = new Mesh { name = label, indexFormat = IndexFormat.UInt32 }; mesh.CombineMeshes(combinations.ToArray(), false, true); mesh.UploadMeshData(true); meshes.Add(mesh);
            MeshObject(label, mesh, materials.ToArray());
            foreach (var piece in temporary) Object.Destroy(piece);
        }
        private void MeshObject(string label, Mesh mesh, Material[] materials)
        {
            var item = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer)); item.transform.SetParent(Root.transform, false);
            item.GetComponent<MeshFilter>().sharedMesh = mesh; item.GetComponent<MeshRenderer>().sharedMaterials = materials;
        }
        internal void Dispose() { terrain.Dispose(); foreach (var mesh in meshes) Object.Destroy(mesh); Object.Destroy(settings); Object.Destroy(Root); }
    }
}
