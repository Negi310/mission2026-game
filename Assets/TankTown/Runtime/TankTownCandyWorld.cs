using UnityEngine;

namespace TankTown
{
    public sealed partial class TankTownWorld
    {
        internal bool CandyTheme => game.BuiltStage == TownStage.CandyTown;
        private CandyGeometry candyGeometry;
        private Material[] candyLollipops;
        private Material candyCream, candySponge, candyWafer, candyChocolate, candyChocolateTile, candyStrawberry, candyLeaf, candyPink;
        private void InitializeCandyPalette()
        {
            candyGeometry = new CandyGeometry();
            candyCream = MakeMaterial(new Color(1, .98f, .9f), ToySurface.Organic);
            candySponge = MakeMaterial(new Color(1, .74f, .36f), ToySurface.Organic);
            candyWafer = MakeMaterial(new Color(.91f, .60f, .28f), ToySurface.Wood);
            candyChocolate = MakeMaterial(new Color(.24f, .065f, .04f));
            candyChocolateTile = MakeMaterial(new Color(.40f, .13f, .075f));
            candyStrawberry = MakeMaterial(new Color(1, .045f, .17f));
            candyLeaf = MakeMaterial(new Color(.13f, .77f, .25f), ToySurface.Organic);
            candyPink = MakeMaterial(ItemIcons.Color(ItemKind.Magnet));
            road.SetColor("_BaseColor", new Color(.18f, .09f, .10f));
            grass.SetColor("_BaseColor", new Color(.48f, .93f, .71f));
            cream.SetColor("_BaseColor", new Color(1, .88f, .59f));
        }
        private Material CandyWallMaterial(int style) => style == 0 ? candySponge : style == 1 ? candyChocolate : candyWafer;

        // Decorations retain metre-sized geometry even when their structural parent is a thin scaled panel.
        private GameObject CandyDetail(Transform parent, string label, Vector3 point, Vector3 size, Material material, Mesh shape = null)
        {
            var item = Box(label, Vector3.zero, size, material, solid: false);
            item.transform.SetParent(parent, false);
            Vector3 scale = parent.lossyScale;
            item.transform.localPosition = new Vector3(point.x / scale.x, point.y / scale.y, point.z / scale.z);
            item.transform.localScale = new Vector3(size.x / scale.x, size.y / scale.y, size.z / scale.z);
            if (shape != null) item.GetComponent<MeshFilter>().sharedMesh = shape;
            return item;
        }
        private void DecorateCandyPanel(DestructibleBlock wall, int style, Material color)
        {
            float w = wall.transform.localScale.x, h = wall.transform.localScale.y;
            CandyDetail(wall.transform, "Reward-colored icing", new Vector3(0, h / 2 - .12f, .20f), new Vector3(w + .10f, .45f, .48f), color);
            if (style == 0)
            {
                CandyDetail(wall.transform, "Cream layer", new Vector3(0, h / 2 - .50f, .24f), new Vector3(w + .18f, .65f, .60f), candyCream);
                foreach (int side in new[] { -1, 1 })
                    CandyDetail(wall.transform, "Cream drip", new Vector3(side * w * .26f, h / 2 - .94f, .28f), new Vector3(1.5f, 1.30f, .65f), candyCream, geometry.LowPolyHead());
                CandyDetail(wall.transform, "Strawberry filling", new Vector3(0, -.35f, .17f), new Vector3(w, .20f, .12f), candyStrawberry);
            }
            else if (style == 1)
            {
                foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 })
                    CandyDetail(wall.transform, "Chocolate square", new Vector3(x * w * .245f, y * h * .20f - .18f, .23f), new Vector3(w * .45f, h * .35f, .27f), candyChocolateTile);
            }
            else
            {
                CandyDetail(wall.transform, "Biscuit window", new Vector3(0, -.18f, .19f), new Vector3(w * .36f, h * .56f, .16f), candyChocolate);
                foreach (int side in new[] { -1, 1 })
                    CandyDetail(wall.transform, "Wafer edge", new Vector3(side * w * .44f, -.1f, .19f), new Vector3(.18f, h * .75f, .15f), candyCream);
                CandyDetail(wall.transform, "Sugar window cross", new Vector3(0, -.18f, .3f), new Vector3(.13f, h * .56f, .11f), color);
                CandyDetail(wall.transform, "Sugar window cross", new Vector3(0, -.18f, .3f), new Vector3(w * .36f, .13f, .11f), color);
            }
        }
        private void DecorateBiscuitRoof(DestructibleBlock roof, Material color)
        {
            Vector3 size = roof.transform.localScale;
            for (int i = -1; i <= 1; i++)
                CandyDetail(roof.transform, "Wafer roof groove", new Vector3(i * size.x * .28f, .19f, 0), new Vector3(.12f, .10f, size.z), candyChocolateTile);
            CandyDetail(roof.transform, "Icing roof edge", new Vector3(0, .20f, -size.z * .45f), new Vector3(size.x, .28f, .36f), color);
        }
        private void DecorateCandyRoof(DestructibleBlock roof, int style, Material color, float width)
        {
            foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
            {
                Vector3 point = new Vector3(x * width * .20f, 1.6f, z * width * .20f);
                if (style == 0)
                {
                    var berry = CandyDetail(roof.transform, "Roof strawberry", point, new Vector3(3.2f, 3.4f, 3.2f), candyStrawberry, candyGeometry.Strawberry);
                    CandyDetail(berry.transform, "Strawberry leaf", new Vector3(0, 1.65f, 0), new Vector3(1.8f, .22f, .7f), candyLeaf);
                    CandyDetail(berry.transform, "Strawberry leaf", new Vector3(0, 1.65f, 0), new Vector3(.7f, .22f, 1.8f), candyLeaf);
                    foreach (int side in new[] { -1, 1 }) CandyDetail(berry.transform, "Strawberry seed", new Vector3(side * .65f, .15f, -1.2f), new Vector3(.15f, .25f, .08f), candyCream);
                }
                else CandyDetail(roof.transform, "Roof candy", point - Vector3.up * .8f, new Vector3(2, 1.2f, 2), color, geometry.LowPolyHead());
            }
        }
        private void CreateCandyProp(Vector3 point, string label, Material color, bool sign)
        {
            var item = new GameObject(sign ? "Candy cane " + label : "Lollipop tree"); item.transform.SetParent(transform, false);
            item.transform.position = GroundPoint(point);
            var post = item.AddComponent<BoxCollider>(); post.center = Vector3.up * 1.5f; post.size = new Vector3(.4f, 3, .4f);
            AddBlock(item, true);
            Box("Sugar stick", post.center, post.size, candyCream, item.transform, false);
            for (int i = 0; i < 4; i++) Box("Candy stripe", new Vector3(0, .4f + i * .65f, 0), new Vector3(.43f, .25f, .43f), candyStrawberry, item.transform, false);
            if (!sign)
            {
                if (color == grass) color = candyLollipops[Mathf.Abs(Mathf.RoundToInt(point.x) + Mathf.RoundToInt(point.z) * 2) % candyLollipops.Length];
                var lollipop = CandyDetail(item.transform, "Lollipop crown", Vector3.up * 3.4f, new Vector3(3.3f, 3.3f, 1.1f), color, geometry.LowPolyHead());
                var sphere = lollipop.AddComponent<SphereCollider>(); sphere.radius = .5f;
                CandyDetail(item.transform, "Lollipop swirl", new Vector3(0, 3.4f, -.6f), new Vector3(1.55f, 1.55f, .15f), candyCream, geometry.LowPolyHead());
            }
            else if (label == "Street lamp")
            {
                for (int i = 0; i < 3; i++) Box("Candy cane hook", new Vector3(.35f + i * .30f, 3.05f - i * i * .12f, 0), new Vector3(.46f, .42f, .44f), i % 2 == 0 ? candyCream : candyStrawberry, item.transform, false);
                CandyDetail(item.transform, "Candy lamp", new Vector3(.95f, 2.45f, 0), new Vector3(.9f, .8f, .9f), color, geometry.LowPolyHead());
            }
            else Box("Biscuit sign", new Vector3(0, 2.8f, 0), new Vector3(1.9f, 1.0f, .3f), candyWafer, item.transform, false);
        }
        private void CreateCandyPark(Vector3 center)
        {
            ParkCount++;
            Terrain.Surface("Candy garden plaza", new Vector2(center.x - BuildingWidth / 2, center.z - BuildingWidth / 2), Vector2.one * BuildingWidth, .04f, grass);
            var muffin = Box("Crushable cupcake", center + Vector3.up * 1.1f, new Vector3(5, 2.2f, 5), candyWafer);
            AddBlock(muffin, true);
            CandyDetail(muffin.transform, "Cupcake frosting", new Vector3(0, 1.9f, 0), new Vector3(6.2f, 2.8f, 6.2f), candyPink, geometry.LowPolyHead());
            CandyDetail(muffin.transform, "Cupcake cherry", new Vector3(0, 3.7f, 0), new Vector3(1.6f, 1.6f, 1.6f), candyStrawberry, geometry.LowPolyHead());
            for (int i = 0; i < 4; i++)
            {
                var direction = Quaternion.Euler(0, i * 90, 0) * Vector3.forward;
                var bench = Box("Crushable biscuit bench", GroundPoint(center + direction * 7, .45f), new Vector3(2.6f, .9f, .8f), candyChocolateTile);
                bench.transform.rotation = Quaternion.Euler(0, i * 90, 0); AddBlock(bench, true);
                CreateCandyProp(center + Quaternion.Euler(0, i * 90 + 45, 0) * Vector3.forward * 8, "Tree", i % 2 == 0 ? candyPink : candyStrawberry, false);
            }
        }
        private void CreateDoughnutGate()
        {
            var root = new GameObject("Doughnut gate"); root.transform.SetParent(transform, false);
            root.transform.position = GroundPoint(SpawnPosition + Vector3.forward * 58);
            var building = root.AddComponent<BlockBuilding>(); building.Initialize(game);
            building.Width = 28; building.Floors = 1; building.Style = 3;
            building.RewardKind = ItemKind.Magnet; building.FacadeColor = ItemIcons.Color(ItemKind.Magnet); Buildings.Add(building);
            var feet = new DestructibleBlock[4];
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -12 : 12, z = i % 2 == 0 ? -1 : 1;
                float floor = Terrain.HeightAt(root.transform.position + new Vector3(x, 0, z)) - root.transform.position.y;
                feet[i] = Part("Doughnut support wafer", new Vector3(x, (floor + 6) / 2, z), new Vector3(1.7f, Mathf.Max(2, 6 - floor), 1.7f), candyWafer, BuildingPart.Pillar, 0, i, building);
                feet[i].Foundation = true;
            }
            var slices = new DestructibleBlock[12];
            for (int i = 0; i < slices.Length; i++)
            {
                float angle = -82.5f + i * 15, radians = angle * Mathf.Deg2Rad;
                var slice = Part("Doughnut slice", new Vector3(Mathf.Sin(radians) * 12, 6 + Mathf.Cos(radians) * 12, 0), new Vector3(3.8f, 4.2f, 4.2f), candyWafer, BuildingPart.Prop, 0, -1, building);
                slice.PreserveShape = true;
                slice.transform.localRotation = Quaternion.Euler(0, 0, -angle);
                slice.GetComponent<MeshFilter>().sharedMesh = candyGeometry.DoughnutSlice;
                CandyDetail(slice.transform, "Pink doughnut icing", new Vector3(0, 0, -1.3f), new Vector3(3.85f, 4.3f, 1.7f), candyPink, candyGeometry.DoughnutSlice);
                CandyDetail(slice.transform, "Doughnut sugar sprinkle", new Vector3(.3f, .45f, -2.2f), new Vector3(.28f, .8f, .20f), i % 2 == 0 ? candyCream : ShotMaterial);
                slices[i] = slice;
                if (i > 0) BlockBuilding.Connect(slices[i - 1], slice);
            }
            BlockBuilding.Connect(feet[0], slices[0]); BlockBuilding.Connect(feet[1], slices[0]);
            BlockBuilding.Connect(feet[2], slices[11]); BlockBuilding.Connect(feet[3], slices[11]);
            CreateBuildingReward(building);
        }
    }
}
