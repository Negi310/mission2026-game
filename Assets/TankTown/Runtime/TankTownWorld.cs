using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    public sealed class TankTownWorld : MonoBehaviour
    {
        public Material ShotMaterial { get; private set; }
        private readonly List<Material> materials = new List<Material>();
        private TankTownGame game;
        private Material road, cream, dark, glass, lime, grass;
        private readonly Color[] palette = { new Color(1, .46f, .3f), new Color(.22f, .7f, .81f), new Color(1, .73f, .25f), new Color(.62f, .49f, .86f), new Color(.94f, .57f, .68f) };

        public Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var template = Resources.Load<Material>("TankTownSurface");
            var material = template != null ? new Material(template) : new Material(shader);
            material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .18f);
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
            if (!solid) Destroy(item.GetComponent<Collider>());
            return item;
        }

        public void Build(TankTownGame owner)
        {
            game = owner;
            road = MakeMaterial(new Color(.18f, .25f, .31f));
            cream = MakeMaterial(new Color(1, .95f, .8f));
            dark = MakeMaterial(new Color(.1f, .17f, .21f));
            glass = MakeMaterial(new Color(.12f, .32f, .43f));
            lime = MakeMaterial(new Color(.73f, .9f, .19f));
            grass = MakeMaterial(new Color(.38f, .7f, .51f));
            ShotMaterial = MakeMaterial(new Color(1, .72f, .14f));
            Box("Island", new Vector3(0, -.9f, 0), new Vector3(120, 1.8f, 120), grass);
            Box("Town paving", new Vector3(0, .015f, 0), new Vector3(112, .03f, 112), cream, solid: false);
            for (int i = -2; i <= 2; i++)
            {
                float lane = i * 24;
                Box("North south avenue", new Vector3(lane, .04f, 0), new Vector3(10, .04f, 112), road, solid: false);
                Box("East west avenue", new Vector3(0, .045f, lane), new Vector3(112, .04f, 10), road, solid: false);
                for (int d = -52; d <= 52; d += 4)
                {
                    if (Mathf.Abs(d % 24) < 6) continue;
                    Box("Lane dash", new Vector3(lane, .075f, d), new Vector3(.16f, .02f, 1.7f), cream, solid: false);
                    Box("Lane dash", new Vector3(d, .075f, lane), new Vector3(1.7f, .02f, .16f), cream, solid: false);
                }
            }
            var random = new System.Random(game.Settings.citySeed);
            for (int x = -2; x < 2; x++)
                for (int z = -2; z < 2; z++)
                {
                    var center = new Vector3(x * 24 + 12, 0, z * 24 + 12);
                    CreateBuilding(center, random.Next(3, 7), MakeMaterial(palette[random.Next(palette.Length)]));
                    CreateProp(center + new Vector3(-5.8f, 0, -6), "Street sign", ShotMaterial, true);
                    CreateProp(center + new Vector3(5.6f, 0, 5.6f), "Tree", grass, false);
                    CreateProp(center + new Vector3(-5.6f, 0, 5.6f), "Street lamp", dark, true);
                    var crate = Box("Crushable crate", center + new Vector3(5.8f, .6f, -5.8f), Vector3.one * 1.2f, lime);
                    AddBlock(crate, true);
                }
            // A readable ring around the island, without an invisible wall to get stuck against.
            for (int i = -56; i <= 56; i += 4)
            {
                Box("Edge stripe", new Vector3(i, .09f, 56), new Vector3(2, .1f, .6f), ShotMaterial, solid: false);
                Box("Edge stripe", new Vector3(i, .09f, -56), new Vector3(2, .1f, .6f), ShotMaterial, solid: false);
                Box("Edge stripe", new Vector3(56, .09f, i), new Vector3(.6f, .1f, 2), ShotMaterial, solid: false);
                Box("Edge stripe", new Vector3(-56, .09f, i), new Vector3(.6f, .1f, 2), ShotMaterial, solid: false);
            }
        }

        private DestructibleBlock AddBlock(GameObject item, bool crushable)
        {
            var block = item.AddComponent<DestructibleBlock>();
            block.CanCrush = crushable;
            block.Initialize(game);
            return block;
        }

        private void CreateBuilding(Vector3 center, int floors, Material color)
        {
            var root = new GameObject("Block building");
            root.transform.SetParent(transform);
            root.transform.position = center;
            var building = root.AddComponent<BlockBuilding>();
            const float cell = 1.7f;
            for (int y = 0; y < floors; y++)
                for (int x = 0; x < 4; x++)
                    for (int z = 0; z < 4; z++)
                    {
                        var item = Box("Structural block", new Vector3((x - 1.5f) * cell, .85f + y * cell, (z - 1.5f) * cell), Vector3.one * 1.66f, y == floors - 1 ? cream : color, root.transform);
                        var block = AddBlock(item, false);
                        block.Building = building;
                        block.Column = x * 4 + z;
                        block.Level = y;
                        building.Blocks.Add(block);
                        if (y == floors - 1) continue;
                        // Windows belong to their blocks and travel with the debris.
                        if (z == 0 || z == 3)
                            Box("Window", new Vector3(0, .04f, z == 0 ? -.505f : .505f), new Vector3(.56f, .5f, .015f), glass, item.transform, false);
                        if (x == 0 || x == 3)
                            Box("Window", new Vector3(x == 0 ? -.505f : .505f, .04f, 0), new Vector3(.015f, .5f, .56f), glass, item.transform, false);
                    }
        }

        private void CreateProp(Vector3 position, string label, Material color, bool sign)
        {
            var item = Box(label, position + Vector3.up * 1.25f, new Vector3(.22f, 2.5f, .22f), dark);
            AddBlock(item, true);
            Box("Top", new Vector3(0, .44f, 0), sign ? new Vector3(7, .35f, 2) : new Vector3(9, .65f, 9), color, item.transform, false);
        }

        public TankMotor CreateTank()
        {
            var tank = new GameObject("Player Tank");
            tank.layer = 2;
            tank.transform.position = new Vector3(0, 1, -42);
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
                for (int w = -2; w <= 2; w++)
                    Box("Track pad", new Vector3(side * 1.54f, -.35f, w * .62f), new Vector3(.07f, .56f, .32f), cream, visual, false);
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
        }
    }
}
