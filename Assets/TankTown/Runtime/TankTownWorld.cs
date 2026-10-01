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
        public Vector3 SpawnPosition => new Vector3(0, 1, -HalfExtent + 20);
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
            Box("Island", new Vector3(0, -.9f, 0), new Vector3(HalfExtent * 2, 1.8f, HalfExtent * 2), grass);
            float paving = HalfExtent * 2 - 8;
            Box("Town paving", new Vector3(0, .015f, 0), new Vector3(paving, .03f, paving), cream, solid: false);
            float spacing = game.Settings.blockSpacing;
            int half = game.Settings.cityHalfBlocks;
            for (int i = -half; i <= half; i++)
            {
                float lane = i * spacing;
                Box("North south avenue", new Vector3(lane, .04f, 0), new Vector3(RoadWidth, .04f, paving), road, solid: false);
                Box("East west avenue", new Vector3(0, .045f, lane), new Vector3(paving, .04f, RoadWidth), road, solid: false);
                for (float d = -paving / 2 + 2; d < paving / 2; d += 4)
                {
                    if (Mathf.Abs(d - Mathf.Round(d / spacing) * spacing) < RoadWidth / 2 + 1) continue;
                    Box("Lane dash", new Vector3(lane, .075f, d), new Vector3(.18f, .02f, 1.8f), cream, solid: false);
                    Box("Lane dash", new Vector3(d, .075f, lane), new Vector3(1.8f, .02f, .18f), cream, solid: false);
                }
            }
            var random = new System.Random(game.Settings.citySeed);
            for (int x = -half; x < half; x++)
                for (int z = -half; z < half; z++)
                {
                    var center = new Vector3((x + .5f) * spacing, 0, (z + .5f) * spacing);
                    CreateBuilding(center, random.Next(4, 8), MakeMaterial(palette[random.Next(palette.Length)]));
                    float sidewalk = BuildingWidth / 2 + 1.5f;
                    CreateProp(center + new Vector3(-sidewalk, 0, -sidewalk), "Street sign", ShotMaterial, true);
                    CreateProp(center + new Vector3(sidewalk, 0, sidewalk), "Tree", grass, false);
                    CreateProp(center + new Vector3(-sidewalk, 0, sidewalk), "Street lamp", cream, true);
                    var crate = Box("Crushable crate", center + new Vector3(sidewalk, .6f, -sidewalk), Vector3.one * 1.2f, lime);
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
                Box("Edge stripe", new Vector3(i, .09f, edge), new Vector3(2, .1f, .6f), ShotMaterial, solid: false);
                Box("Edge stripe", new Vector3(i, .09f, -edge), new Vector3(2, .1f, .6f), ShotMaterial, solid: false);
                Box("Edge stripe", new Vector3(edge, .09f, i), new Vector3(.6f, .1f, 2), ShotMaterial, solid: false);
                Box("Edge stripe", new Vector3(-edge, .09f, i), new Vector3(.6f, .1f, 2), ShotMaterial, solid: false);
            }
        }

        private void CreatePickup(Vector3 position, UpgradeKind kind)
        {
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

        private void CreateBuilding(Vector3 center, int floors, Material color)
        {
            var root = new GameObject("Frame building");
            root.transform.SetParent(transform);
            root.transform.position = center;
            var building = root.AddComponent<BlockBuilding>();
            building.Initialize(game);
            Buildings.Add(building);
            float width = BuildingWidth;
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
            foreach (var material in materials) if (material != null) Destroy(material);
            Icons?.Dispose();
        }
    }
}
