using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    public sealed class TankTownWorld : MonoBehaviour
    {
        public Material ShotMaterial { get; private set; }
        public UpgradeIcons Icons { get; private set; }
        public float HalfExtent { get; private set; }
        public float RoadWidth => Mathf.Clamp(game.Settings.roadWidth, 8, game.Settings.blockSpacing - 14);
        public float BuildingWidth => Mathf.Clamp(game.Settings.buildingWidth, 10, game.Settings.blockSpacing - RoadWidth - 4);
        public Vector3 SpawnPosition => GroundPoint(new Vector3(0, 0, -HalfExtent + 20), game.Settings.rideHeight);
        public TownTerrain Terrain { get; private set; }
        public int ParkCount { get; private set; }
        public Vector3 GroundPoint(Vector3 p, float lift = 0) { p.y = Terrain.HeightAt(p) + lift; return p; }
        public readonly List<BlockBuilding> Buildings = new List<BlockBuilding>();
        public readonly List<TankPickup> Pickups = new List<TankPickup>();
        private readonly List<Material> materials = new List<Material>();
        private TankTownGame game;
        private Material road, cream, dark, glass, lime, grass, frame;
        private readonly Color[] palette = { new Color(1, .26f, .18f), new Color(.04f, .78f, 1), new Color(1, .68f, .04f), new Color(.62f, .27f, 1), new Color(1, .27f, .62f) };

        public Material MakeMaterial(Color color)
        {
            var template = Resources.Load<Material>("TankTownSurface");
            var material = template != null ? new Material(template) : new Material(Shader.Find("TankTown/Saturated Toon"));
            material.SetColor("_BaseColor", color);
            material.enableInstancing = true;
            materials.Add(material);
            return material;
        }

        public GameObject Box(string label, Vector3 position, Vector3 size, Material material, Transform parent = null, bool solid = true)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = label;
            item.transform.SetParent(parent == null ? transform : parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = size;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) { item.GetComponent<Collider>().enabled = false; Destroy(item.GetComponent<Collider>()); }
            return item;
        }

        public void Build(TankTownGame owner)
        {
            game = owner;
            HalfExtent = game.Settings.cityHalfBlocks * game.Settings.blockSpacing + 14;
            road = MakeMaterial(new Color(.13f, .2f, .32f));
            cream = MakeMaterial(new Color(1, .94f, .77f));
            dark = MakeMaterial(new Color(.055f, .09f, .16f));
            glass = MakeMaterial(new Color(.04f, .34f, .59f));
            lime = MakeMaterial(new Color(.64f, 1, .04f));
            grass = MakeMaterial(new Color(.12f, .84f, .42f));
            frame = MakeMaterial(new Color(.85f, .91f, 1));
            ShotMaterial = MakeMaterial(new Color(1, .83f, .07f));
            Icons = new UpgradeIcons();
            Terrain = new TownTerrain(game.Settings, transform, RoadWidth);
            Box("Island", new Vector3(0, -.9f, 0), new Vector3(HalfExtent * 2, 1.8f, HalfExtent * 2), grass, solid: false);
            float paving = HalfExtent * 2 - 8;
            Terrain.Surface("Terraced ground", new Vector2(-HalfExtent, -HalfExtent), Vector2.one * HalfExtent * 2, 0, grass, true);
            Terrain.Surface("Town paving", Vector2.one * -paving / 2, Vector2.one * paving, .015f, cream);
            float spacing = game.Settings.blockSpacing;
            int half = game.Settings.cityHalfBlocks;
            for (int i = -half; i <= half; i++)
            {
                float lane = i * spacing;
                Terrain.Surface("North south ramp avenue", new Vector2(lane - RoadWidth / 2, -paving / 2), new Vector2(RoadWidth, paving), .045f, road);
                Terrain.Surface("East west ramp avenue", new Vector2(-paving / 2, lane - RoadWidth / 2), new Vector2(paving, RoadWidth), .05f, road);
                for (float d = -paving / 2 + 2; d < paving / 2; d += 4)
                {
                    if (Mathf.Abs(d - Mathf.Round(d / spacing) * spacing) < RoadWidth / 2 + 1) continue;
                    SurfaceBox("Lane dash", new Vector3(lane, 0, d), new Vector3(.18f, .02f, 1.8f), cream);
                    SurfaceBox("Lane dash", new Vector3(d, 0, lane), new Vector3(1.8f, .02f, .18f), cream);
                }
            }
            var random = new System.Random(game.Settings.citySeed);
            for (int x = -half; x < half; x++)
                for (int z = -half; z < half; z++)
                {
                    var center = GroundPoint(new Vector3((x + .5f) * spacing, 0, (z + .5f) * spacing));
                    bool park = (x == 0 && z == 0) || (x == -2 && z == -1) || (x == 1 && z == -2);
                    int style = (x - z + half * 3 + 6) % 3;
                    if (x == -half && z == -half) style = 1;
                    int floors = style == 0 ? random.Next(6, 9) : style == 1 ? random.Next(4, 7) : random.Next(2, 4);
                    float width = Mathf.Max(10, BuildingWidth * (style == 0 ? .72f : style == 2 ? .88f : 1));
                    if (park) CreatePark(center);
                    else CreateBuilding(center, floors, MakeMaterial(palette[random.Next(palette.Length)]), width, style);
                    float sidewalk = BuildingWidth / 2 + 1.5f;
                    CreateProp(center + new Vector3(-sidewalk, 0, -sidewalk), "Street sign", ShotMaterial, true);
                    CreateProp(center + new Vector3(sidewalk, 0, sidewalk), "Tree", grass, false);
                    CreateProp(center + new Vector3(-sidewalk, 0, sidewalk), "Street lamp", cream, true);
                    var crate = Box("Crushable crate", GroundPoint(center + new Vector3(sidewalk, 0, -sidewalk), .6f), Vector3.one * 1.2f, lime);
                    AddBlock(crate, true);
                    var pickupPosition = x == 0 && z == -half ? new Vector3(spacing * .375f, 1.2f, z * spacing) : new Vector3(x * spacing, 1.2f, (z + .45f) * spacing);
                    CreatePickup(pickupPosition, (UpgradeKind)random.Next(4));
                    CreatePickup(new Vector3((x + .6f) * spacing, 1.2f, z * spacing), (UpgradeKind)random.Next(4));
                }
            // The first straight has all four upgrades, so their effect is easy to try.
            for (int i = 0; i < 4; i++) CreatePickup(SpawnPosition + new Vector3(0, .2f, 8 + i * 7), (UpgradeKind)i);
            float edge = HalfExtent - 4;
            for (float i = -edge; i <= edge; i += 4)
            {
                SurfaceBox("Edge stripe", new Vector3(i, 0, edge), new Vector3(2, .1f, .6f), ShotMaterial);
                SurfaceBox("Edge stripe", new Vector3(i, 0, -edge), new Vector3(2, .1f, .6f), ShotMaterial);
                SurfaceBox("Edge stripe", new Vector3(edge, 0, i), new Vector3(.6f, .1f, 2), ShotMaterial);
                SurfaceBox("Edge stripe", new Vector3(-edge, 0, i), new Vector3(.6f, .1f, 2), ShotMaterial);
                foreach (var p in new[] { new Vector3(i, 0, HalfExtent), new Vector3(i, 0, -HalfExtent), new Vector3(HalfExtent, 0, i), new Vector3(-HalfExtent, 0, i) })
                {
                    float height = Terrain.HeightAt(p) + 1.8f;
                    Box("Island retaining edge", new Vector3(p.x, height / 2 - 1.8f, p.z), Mathf.Abs(p.x) == HalfExtent ? new Vector3(.3f, height, 4) : new Vector3(4, height, .3f), grass, solid: false);
                }
            }
        }

        private void SurfaceBox(string label, Vector3 p, Vector3 size, Material material)
        {
            var item = Box(label, GroundPoint(p, .08f), size, material, solid: false);
            item.transform.rotation = Quaternion.FromToRotation(Vector3.up, Terrain.NormalAt(p));
        }
        private void CreatePark(Vector3 center)
        {
            ParkCount++;
            float width = BuildingWidth;
            Terrain.Surface("Garden plaza", new Vector2(center.x - width / 2, center.z - width / 2), Vector2.one * width, .04f, grass);
            var fountain = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fountain.name = "Crushable fountain";
            fountain.transform.SetParent(transform, false);
            fountain.transform.position = center + Vector3.up * .5f;
            fountain.transform.localScale = new Vector3(4, .5f, 4);
            fountain.GetComponent<Renderer>().sharedMaterial = cream;
            fountain.GetComponent<Collider>().enabled = false; Destroy(fountain.GetComponent<Collider>());
            var fountainCollider = fountain.AddComponent<MeshCollider>();
            fountainCollider.sharedMesh = fountain.GetComponent<MeshFilter>().sharedMesh; fountainCollider.convex = true;
            AddBlock(fountain, true);
            var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water.name = "Fountain water"; water.transform.SetParent(fountain.transform, false);
            water.transform.localPosition = Vector3.up * 1.03f; water.transform.localScale = new Vector3(.78f, .04f, .78f);
            water.GetComponent<Collider>().enabled = false; Destroy(water.GetComponent<Collider>());
            water.GetComponent<Renderer>().sharedMaterial = glass;
            for (int i = 0; i < 4; i++)
            {
                var direction = Quaternion.Euler(0, i * 90, 0) * Vector3.forward;
                var bench = Box("Crushable bench", GroundPoint(center + direction * 7, .45f), new Vector3(2.6f, .9f, .8f), ShotMaterial);
                bench.transform.rotation = Quaternion.Euler(0, i * 90, 0); AddBlock(bench, true);
                CreateProp(center + Quaternion.Euler(0, i * 90 + 45, 0) * Vector3.forward * 8, "Tree", grass, false);
            }
        }

        private void CreatePickup(Vector3 position, UpgradeKind kind)
        {
            position = GroundPoint(position, 1.2f);
            var item = new GameObject(TankStats.Label(kind) + " UP");
            item.transform.SetParent(transform, false);
            item.transform.position = position;
            var pickup = item.AddComponent<TankPickup>();
            pickup.Initialize(game, kind, Icons.Material(kind));
            Pickups.Add(pickup);
        }

        private DestructibleBlock AddBlock(GameObject item, bool crushable)
        {
            var block = item.AddComponent<DestructibleBlock>();
            block.CanCrush = crushable;
            block.Kind = BuildingPart.Prop;
            block.Initialize(game);
            return block;
        }

        private void CreateBuilding(Vector3 center, int floors, Material color, float width, int style)
        {
            var root = new GameObject(style == 0 ? "Tower frame" : style == 1 ? "Apartment frame" : "Shop frame");
            root.transform.SetParent(transform);
            root.transform.position = center;
            var building = root.AddComponent<BlockBuilding>();
            building.Initialize(game);
            building.Width = width; building.Floors = floors;
            Buildings.Add(building);
            float pillar = Mathf.Clamp(game.Settings.pillarWidth, .6f, width / 5);
            float height = game.Settings.floorHeight, h = (width - pillar) / 2;
            var corners = new[] { new Vector3(-h, 0, -h), new Vector3(h, 0, -h), new Vector3(h, 0, h), new Vector3(-h, 0, h) };
            var previous = new DestructibleBlock[4];
            for (int level = 0; level < floors; level++)
            {
                float y = level * height;
                var pillars = new DestructibleBlock[4];
                var beams = new DestructibleBlock[4];
                var slabs = new DestructibleBlock[4];
                for (int i = 0; i < 4; i++)
                {
                    pillars[i] = Part("Pillar", corners[i] + Vector3.up * (y + height / 2), new Vector3(pillar, height - .12f, pillar), frame, BuildingPart.Pillar, level, i, building);
                    pillars[i].Foundation = level == 0;
                    if (level > 0) BlockBuilding.Connect(pillars[i], previous[i]);
                    var middle = (corners[i] + corners[(i + 1) % 4]) / 2;
                    beams[i] = Part("Beam", middle + Vector3.up * (y + height - .25f), i % 2 == 0 ? new Vector3(width - pillar, .5f, .65f) : new Vector3(.65f, .5f, width - pillar), frame, BuildingPart.Beam, level, i, building);
                    float q = (width - pillar) / 4;
                    slabs[i] = Part("Floor slab", new Vector3(Mathf.Sign(corners[i].x) * q, y + height - .12f, Mathf.Sign(corners[i].z) * q), new Vector3(q * 2 - .05f, .24f, q * 2 - .05f), level == floors - 1 ? cream : frame, BuildingPart.Slab, level, i, building);
                }
                for (int i = 0; i < 4; i++)
                {
                    BlockBuilding.Connect(pillars[i], beams[i]);
                    BlockBuilding.Connect(pillars[(i + 1) % 4], beams[i]);
                    BlockBuilding.Connect(beams[i], slabs[i]);
                    BlockBuilding.Connect(beams[i], slabs[(i + 1) % 4]);
                    BlockBuilding.Connect(slabs[i], slabs[(i + 1) % 4]);
                    float yaw = i == 0 ? 180 : i == 1 ? 90 : i == 2 ? 0 : 270;
                    var rotation = Quaternion.Euler(0, yaw, 0);
                    for (int panel = -1; panel <= 1; panel += 2)
                    {
                        var wall = Part("Facade panel", rotation * new Vector3(panel * (width - 1.6f) / 4, 0, width / 2 - .2f) + Vector3.up * (y + height / 2 - .12f), new Vector3((width - 1.6f) / 2 - .08f, height - .65f, .28f), color, BuildingPart.Wall, level, i, building);
                        wall.transform.localRotation = rotation;
                        BlockBuilding.Connect(wall, beams[i]);
                        // Normalized local sizes keep these windows attached to thin facade panels.
                        for (int window = -1; window <= 1; window += 2)
                            Box("Window", new Vector3(window * .24f, .03f, .515f), new Vector3(.32f, .55f, .025f), glass, wall.transform, false);
                    }
                }
                previous = pillars;
            }
            if (style == 2)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var roof = Part("Shop gable roof", new Vector3(0, floors * height + .65f, side * width / 4), new Vector3(width + .3f, .3f, width * .54f), color, BuildingPart.Prop, floors - 1, -1, building);
                    roof.transform.localRotation = Quaternion.Euler(side * 15, 0, 0);
                    foreach (var post in previous) BlockBuilding.Connect(roof, post);
                }
            }
            else
            {
                float roofHeight = style == 0 ? 3 : 1.2f;
                var roof = Part(style == 0 ? "Tower crown" : "Roof utility", new Vector3(0, floors * height + roofHeight / 2), new Vector3(width * .3f, roofHeight, width * .3f), style == 0 ? color : dark, BuildingPart.Prop, floors - 1, -1, building);
                foreach (var post in previous) BlockBuilding.Connect(roof, post);
            }
        }

        private DestructibleBlock Part(string label, Vector3 position, Vector3 size, Material material, BuildingPart kind, int level, int column, BlockBuilding building)
        {
            var block = AddBlock(Box(label, position, size, material, building.transform), false);
            block.Building = building;
            block.Kind = kind;
            block.Level = level;
            block.Column = column;
            building.Blocks.Add(block);
            return block;
        }

        private void CreateProp(Vector3 position, string label, Material color, bool sign)
        {
            position = GroundPoint(position);
            var item = new GameObject(label);
            item.transform.SetParent(transform, false);
            item.transform.position = position;
            var trunk = item.AddComponent<BoxCollider>();
            trunk.center = Vector3.up * 1.4f;
            trunk.size = new Vector3(sign ? .3f : .45f, 2.8f, sign ? .3f : .45f);
            AddBlock(item, true);
            Box("Post", trunk.center, trunk.size, dark, item.transform, false);
            if (!sign)
            {
                var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.name = "Tree crown";
                crown.transform.SetParent(item.transform, false);
                crown.transform.localPosition = Vector3.up * 2.65f;
                crown.transform.localScale = new Vector3(2.8f, 2.5f, 2.8f);
                crown.GetComponent<Renderer>().sharedMaterial = color;
                // The visible crown joins the trunk as a compound physics collider.
            }
            else
            {
                Box("Top", new Vector3(0, 2.7f, 0), label == "Street lamp" ? new Vector3(.9f, .5f, .9f) : new Vector3(1.8f, .9f, .25f), color, item.transform, true);
            }
        }

        public TankMotor CreateTank()
        {
            var tank = new GameObject("Player Tank");
            tank.layer = TownPhysics.TankLayer;
            tank.transform.position = SpawnPosition;
            var collider = tank.AddComponent<BoxCollider>();
            collider.size = new Vector3(2.6f, 1.6f, 3.4f);
            var visual = new GameObject("Tank visual").transform;
            visual.SetParent(tank.transform, false);
            Box("Hull", Vector3.zero, new Vector3(2.4f, .85f, 3.1f), lime, visual, false);
            Box("Turret", new Vector3(0, .68f, -.15f), new Vector3(1.5f, .75f, 1.6f), lime, visual, false);
            Box("Fixed cannon", new Vector3(0, .72f, 1.65f), new Vector3(.4f, .4f, 2.5f), cream, visual, false);
            Box("Muzzle", new Vector3(0, .72f, 2.88f), new Vector3(.6f, .6f, .3f), dark, visual, false);
            foreach (int side in new[] { -1, 1 })
            {
                Box("Track", new Vector3(side * 1.2f, -.35f, 0), new Vector3(.65f, .9f, 3.5f), dark, visual, false);
                for (int w = -2; w <= 2; w++) Box("Track pad", new Vector3(side * 1.54f, -.35f, w * .62f), new Vector3(.07f, .56f, .32f), cream, visual, false);
            }
            var muzzle = new GameObject("Shot origin").transform;
            muzzle.SetParent(tank.transform, false);
            muzzle.localPosition = new Vector3(0, .72f, 3.2f);
            var motor = tank.AddComponent<TankMotor>();
            motor.Visual = visual;
            motor.Muzzle = muzzle;
            motor.Initialize(game);
            return motor;
        }

        private void OnDestroy()
        {
            Terrain?.Dispose();
            foreach (var material in materials) if (material != null) Destroy(material);
            Icons?.Dispose();
        }
    }
}
