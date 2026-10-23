using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankTown
{
    // A fixed large-town backdrop with the same seeded plots and terrain as gameplay.
    // It contains render meshes only: no destructible parts, traffic, scripts or colliders.
    // With the Blender city kit it shows the same trim, street furniture, road marks and scenery as
    // the ordinary city; detailed kit windows are built only for buildings near the orbiting camera.
    internal sealed class TownTitlePreview
    {
        internal const int Layer = 13;
        private const float DetailDistance = 230, SimpleDistance = 250;
        internal GameObject Root { get; }
        internal float HalfExtent { get; }
        internal float HighestRoof { get; private set; }
        internal TownLayout Layout { get; }
        internal int DetailedWindowSets { get { int n = 0; foreach (var set in windowSets) if (set.Near != null && set.Near.enabled) n++; return n; } }
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly TownTerrain terrain;
        private readonly TankTownSettings settings;
        private readonly Mesh cube;
        private readonly TownModelKit kit;
        private readonly Dictionary<string, Material> kitPaints = new Dictionary<string, Material>();
        private readonly List<WindowSet> windowSets = new List<WindowSet>();
        private bool warmed;

        // One building's windows: cheap glass boxes in the distance, kit windows when the camera is near.
        private sealed class WindowSet
        {
            internal Vector3 Center;
            internal Material Paint;
            internal readonly List<(string piece, Matrix4x4 placement)> Windows = new List<(string, Matrix4x4)>();
            internal MeshRenderer Far, Near;
        }

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
            if (settings.importedCityModels)
            {
                var lamp = world.MakeMaterial(new Color(1, .85f, .54f), ToySurface.Glass); lamp.SetColor("_EmissionColor", new Color(1.8f, 1.25f, .55f));
                var white = world.MakeMaterial(new Color(.97f, .99f, 1));
                kit = TownModelKit.Load(new Dictionary<string, Material>
                {
                    ["CityGlass"] = glass, ["CityFrame"] = frame, ["CityCream"] = ground, ["CityDark"] = dark, ["CityPaint"] = frame,
                    ["CityLamp"] = lamp, ["CityYellow"] = world.MakeMaterial(new Color(1, .83f, .07f)),
                    ["CityWood"] = world.MakeMaterial(new Color(.3f, .19f, .105f), ToySurface.Wood),
                    ["CityLeaf"] = grass, ["CityGreen"] = grass, ["CityLime"] = world.MakeMaterial(new Color(.64f, 1, .04f)),
                    ["CityRed"] = world.MakeMaterial(new Color(1, .2f, .15f)), ["CityWhite"] = white, ["CityCloud"] = white,
                    ["CitySand"] = sand, ["CityRock"] = world.MakeMaterial(new Color(.76f, .57f, .35f), ToySurface.Concrete),
                    ["CityIsland"] = world.MakeMaterial(new Color(.15f, .73f, .48f), ToySurface.Organic)
                });
            }
            terrain.Surface("Preview ground", Vector2.one * -HalfExtent, Vector2.one * HalfExtent * 2, 0, grass, sampleSpacing: 4);
            float paving = HalfExtent * 2 - 8;
            terrain.Surface("Preview paving", Vector2.one * -paving / 2, Vector2.one * paving, .015f, ground, sampleSpacing: 4);
            var streets = new Dictionary<Material, List<CombineInstance>>();
            float spacing = settings.blockSpacing;
            // Kit crosswalks occupy 8.3–10.9m from each junction, as in the ordinary city.
            float clear = roadWidth / 2 + (kit != null ? 4.5f : 1);
            for (int i = -settings.cityHalfBlocks; i <= settings.cityHalfBlocks; i++)
            {
                float lane = i * spacing;
                terrain.Surface("Preview avenue", new Vector2(lane - roadWidth / 2, -paving / 2), new Vector2(roadWidth, paving), .045f, road, sampleSpacing: 4);
                terrain.Surface("Preview avenue", new Vector2(-paving / 2, lane - roadWidth / 2), new Vector2(paving, roadWidth), .05f, road, sampleSpacing: 4);
                for (float d = -paving / 2 + 2; d < paving / 2; d += 4)
                {
                    if (Mathf.Abs(d - Mathf.Round(d / spacing) * spacing) < clear) continue;
                    if (kit != null) { RoadMark(streets, "Lane dash", new Vector3(lane, 0, d), 0); RoadMark(streets, "Lane dash", new Vector3(d, 0, lane), 90); continue; }
                    Box(streets, ground, At(new Vector3(lane, 0, d), .08f), new Vector3(.32f, .02f, 2.1f));
                    Box(streets, ground, At(new Vector3(d, 0, lane), .08f), new Vector3(2.1f, .02f, .32f));
                }
            }
            if (kit != null) RoadMarkings(streets, roadWidth);
            Emit("Preview lane markings", streets, false);
            var furniture = new Dictionary<Material, List<CombineInstance>>();
            foreach (var plot in Layout.Plots)
            {
                Vector3 center = At(plot.Center); float width = plot.Width, height = settings.floorHeight;
                var parts = new Dictionary<Material, List<CombineInstance>>();
                if (kit != null) StreetCorner(furniture, new Vector3((plot.X + .5f) * spacing, 0, (plot.Z + .5f) * spacing), settings.buildingWidth / 2 + 1.5f);
                if (plot.Park)
                {
                    terrain.Surface("Preview park", new Vector2(center.x - 10, center.z - 10), Vector2.one * 20, .04f, grass, sampleSpacing: 4);
                    if (kit != null) Park(furniture, At(new Vector3((plot.X + .5f) * spacing, 0, (plot.Z + .5f) * spacing)));
                    continue;
                }
                var paint = paints[(int)plot.Reward];
                var set = kit != null ? new WindowSet { Center = center, Paint = paint } : null;
                var farWindows = new Dictionary<Material, List<CombineInstance>>();
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
                            float x = panel * (width - 1.6f) / 4, panelWidth = (width - 1.6f) / 2 - .08f, panelHeight = height - .65f;
                            var pos = new Vector3(x, y + height / 2 - .12f, width / 2 - .2f);
                            Box(parts, paint, center + rotation * pos, new Vector3(panelWidth, panelHeight, .28f), rotation);
                            foreach (int window in new[] { -1, 1 })
                            {
                                if (level == 0 && side == 2 && window == -panel) continue;
                                var pane = new Vector3(window * panelWidth * .24f, panelHeight * .03f, 0);
                                Box(kit != null ? farWindows : parts, glass, center + rotation * (pos + pane + Vector3.forward * .1442f), new Vector3(panelWidth * .32f, panelHeight * .55f, .007f), rotation);
                                if (kit == null) continue;
                                // Same pieces and nominal pane sizes as TownFacadeDetails.CityWindow.
                                string piece = plot.Style == 0 ? "Tower window" : plot.Style == 1 ? "Apartment window" : level == 0 ? "Shop storefront" : "Shop window";
                                float nominal = plot.Style == 0 ? 2 : plot.Style == 1 ? 2.9f : 2.5f;
                                set.Windows.Add((piece, Matrix4x4.TRS(center + rotation * (pos + pane + Vector3.forward * .14f), rotation,
                                    new Vector3(panelWidth * .32f / nominal, panelHeight * .55f / 1.6f, 1))));
                            }
                            if (kit != null && plot.Style == 1 && level > 0 && side % 2 == 0)
                                kit.Combine(parts, "Apartment balcony", Matrix4x4.TRS(center + rotation * (pos + new Vector3(0, -panelHeight / 2, .14f)), rotation,
                                    new Vector3(panelWidth / 9.12f, 1, 1)), Paints(paint));
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
                    {
                        var plate = center + new Vector3(0, plot.Floors * height + .65f, side * width / 4);
                        Box(parts, paint, plate, new Vector3(width + .3f, .3f, width * .54f), Quaternion.Euler(side * 15, 0, 0));
                        kit?.Combine(parts, "Shop roof tiles", Matrix4x4.TRS(plate, Quaternion.Euler(side * 15, 0, 0), new Vector3((width + .3f) / 17.9f, 1, width * .54f / 9.5f)));
                    }
                }
                else
                {
                    float crown = plot.Style == 0 ? 3 : 1.2f, top = width * .3f;
                    var roof = center + Vector3.up * (plot.Floors * height + crown / 2);
                    Box(parts, plot.Style == 0 ? paint : dark, roof, new Vector3(top, crown, top));
                    // Kit sizes: tower crown 4.32×3×4.32, rooftop plant base 6×1.2×6.
                    if (plot.Style == 0) kit?.Combine(parts, "Tower crown trim", Matrix4x4.TRS(roof, Quaternion.identity, new Vector3(top / 4.32f, 1, top / 4.32f)), Paints(paint));
                    else kit?.Combine(parts, "Roof utility top", Matrix4x4.TRS(roof, Quaternion.identity, new Vector3(top / 6, 1, top / 6)));
                }
                HighestRoof = Mathf.Max(HighestRoof, center.y + plot.Floors * height + 3);
                Emit("Preview building", parts);
                // Window shadows on a flat facade are not visible from the orbit, so the backdrop skips them.
                if (set != null) { set.Far = Emit("Preview distant windows", farWindows, false); windowSets.Add(set); }
            }
            if (kit != null) Emit("Preview street furniture", furniture);
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
            if (kit != null) Scenery();
            else foreach (var renderer in world.GetComponentsInChildren<MeshRenderer>())
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

        // Swaps building windows between glass boxes and kit windows by distance from the title camera.
        // The first call builds every nearby set at once; later calls add at most one per frame.
        internal void Tick(Vector3 view)
        {
            int budget = warmed ? 1 : int.MaxValue; warmed = true;
            foreach (var set in windowSets)
            {
                float distance = Vector2.Distance(new Vector2(view.x, view.z), new Vector2(set.Center.x, set.Center.z));
                bool near = set.Near != null && set.Near.enabled ? distance < SimpleDistance : distance < DetailDistance;
                if (near && set.Near == null)
                {
                    if (budget-- <= 0) continue;
                    var groups = new Dictionary<Material, List<CombineInstance>>();
                    foreach (var (piece, placement) in set.Windows) kit.Combine(groups, piece, placement, Paints(set.Paint));
                    set.Near = Emit("Preview near windows", groups, false);
                    set.Near.gameObject.layer = Layer;
                }
                if (set.Near != null) set.Near.enabled = near;
                set.Far.enabled = !(near && set.Near != null);
            }
        }

        private Dictionary<string, Material> Paints(Material paint) { kitPaints.Clear(); kitPaints["CityPaint"] = paint; return kitPaints; }

        private void RoadMark(Dictionary<Material, List<CombineInstance>> groups, string piece, Vector3 p, float yaw)
            => kit.Combine(groups, piece, Matrix4x4.TRS(At(p, .08f), Quaternion.FromToRotation(Vector3.up, terrain.NormalAt(p)) * Quaternion.Euler(0, yaw, 0), Vector3.one));

        // Same crosswalks and manholes as TankTownWorld.CreateRoadMarkings.
        private void RoadMarkings(Dictionary<Material, List<CombineInstance>> groups, float roadWidth)
        {
            int half = settings.cityHalfBlocks; float spacing = settings.blockSpacing, band = roadWidth / 2 + 1.6f;
            for (int x = -half; x <= half; x++) for (int z = -half; z <= half; z++)
            {
                var junction = new Vector3(x * spacing, 0, z * spacing);
                if (x < half) RoadMark(groups, "Crosswalk", junction + Vector3.right * band, 90);
                if (x > -half) RoadMark(groups, "Crosswalk", junction + Vector3.left * band, 90);
                if (z < half) RoadMark(groups, "Crosswalk", junction + Vector3.forward * band, 0);
                if (z > -half) RoadMark(groups, "Crosswalk", junction + Vector3.back * band, 0);
            }
            for (int i = -half; i <= half; i++) for (int k = -half; k < half; k++)
            {
                float lane = i * spacing, middle = (k + .5f) * spacing;
                RoadMark(groups, "Manhole", new Vector3(lane + roadWidth * .2f, 0, middle), 0);
                RoadMark(groups, "Manhole", new Vector3(middle, 0, lane - roadWidth * .2f), 0);
            }
        }

        // The ordinary city's corner tree, lamp and sign around each plot (crates are left out to keep the backdrop light).
        private void StreetCorner(Dictionary<Material, List<CombineInstance>> groups, Vector3 cell, float sidewalk)
        {
            Tree(groups, At(cell + new Vector3(sidewalk, 0, sidewalk)));
            kit.Combine(groups, "Street lamp", Matrix4x4.Translate(At(cell + new Vector3(-sidewalk, 0, sidewalk))));
            kit.Combine(groups, "Street sign", Matrix4x4.Translate(At(cell + new Vector3(-sidewalk, 0, -sidewalk))));
        }

        // Same layout as TankTownWorld.CreatePark.
        private void Park(Dictionary<Material, List<CombineInstance>> groups, Vector3 center)
        {
            kit.Combine(groups, "Fountain", Matrix4x4.Translate(center));
            for (int i = 0; i < 4; i++)
            {
                var rotation = Quaternion.Euler(0, i * 90, 0);
                kit.Combine(groups, "Bench", Matrix4x4.TRS(At(center + rotation * Vector3.forward * 7, .45f), rotation, Vector3.one));
                Tree(groups, At(center + Quaternion.Euler(0, i * 90 + 45, 0) * Vector3.forward * 8));
            }
        }

        private void Tree(Dictionary<Material, List<CombineInstance>> groups, Vector3 ground)
        {
            int variant = Mathf.Abs(Mathf.RoundToInt(ground.x * 3 + ground.z * 7)) % 3;
            kit.Combine(groups, "Tree trunk", Matrix4x4.Translate(ground));
            kit.Combine(groups, $"Tree canopy {variant}", Matrix4x4.TRS(ground + Vector3.up * 3.05f, Quaternion.identity, new Vector3(3.25f, 3.05f, 3.25f)));
        }

        // Islands, sailboats and clouds laid out like TownScenery for the large town, as static meshes.
        private void Scenery()
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.PI * 2 / 10;
                var position = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (Mathf.Max(240, HalfExtent * Mathf.Sqrt(2) + 50) + i % 3 * 32);
                kit.Combine(groups, $"Island {i % 3}", Matrix4x4.TRS(new Vector3(position.x, -3.8f, position.z), Quaternion.Euler(0, i * 47, 0), Vector3.one * (1 + i % 3 * .15f)));
            }
            for (int i = 0; i < 5; i++)
            {
                float angle = (i + .5f) * Mathf.PI * 2 / 5, distance = Mathf.Max(HalfExtent + 40, 150) + i % 2 * 28;
                kit.Combine(groups, "Sailboat", Matrix4x4.TRS(new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance + Vector3.up * -4.1f,
                    Quaternion.Euler(0, -angle * Mathf.Rad2Deg + i % 2 * 180, 0), Vector3.one));
            }
            for (int i = 0; i < 16; i++)
            {
                float angle = (i + .3f) * Mathf.PI * 2 / 16, radius = (150 + i % 4 * 31) * 2;
                kit.Combine(groups, $"Cloud {i % 3}", Matrix4x4.TRS(new Vector3(Mathf.Cos(angle) * radius, 48 + i % 5 * 4, Mathf.Sin(angle) * radius), Quaternion.identity,
                    Vector3.one * (.85f + i % 4 * .12f)));
            }
            Emit("Preview scenery", groups);
        }

        private Vector3 At(Vector3 point, float lift = 0) { point.y = terrain.HeightAt(point) + lift; return point; }
        private void Box(Dictionary<Material, List<CombineInstance>> groups, Material material, Vector3 position, Vector3 size, Quaternion rotation = default)
        {
            if (!groups.TryGetValue(material, out var list)) groups[material] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(position, Quaternion.Dot(rotation, rotation) < .5f ? Quaternion.identity : rotation, size) });
        }
        private MeshRenderer Emit(string label, Dictionary<Material, List<CombineInstance>> groups, bool shadows = true)
        {
            var combinations = new List<CombineInstance>(); var materials = new List<Material>();
            var temporary = new List<Mesh>();
            foreach (var group in groups)
            {
                var piece = new Mesh { indexFormat = IndexFormat.UInt32 }; piece.CombineMeshes(group.Value.ToArray(), true, true);
                combinations.Add(new CombineInstance { mesh = piece, transform = Matrix4x4.identity }); materials.Add(group.Key); temporary.Add(piece);
            }
            var mesh = new Mesh { name = label, indexFormat = IndexFormat.UInt32 }; mesh.CombineMeshes(combinations.ToArray(), false, true); mesh.UploadMeshData(true); meshes.Add(mesh);
            var renderer = MeshObject(label, mesh, materials.ToArray());
            if (!shadows) renderer.shadowCastingMode = ShadowCastingMode.Off;
            foreach (var piece in temporary) Object.Destroy(piece);
            return renderer;
        }
        private MeshRenderer MeshObject(string label, Mesh mesh, Material[] materials)
        {
            var item = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer)); item.transform.SetParent(Root.transform, false);
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.GetComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
            return renderer;
        }
        internal void Dispose() { terrain.Dispose(); kit?.Dispose(); foreach (var mesh in meshes) Object.Destroy(mesh); Object.Destroy(settings); Object.Destroy(Root); }
    }
}
