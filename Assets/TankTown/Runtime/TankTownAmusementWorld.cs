using UnityEngine;

namespace TankTown
{
    public sealed partial class TankTownWorld
    {
        internal bool AmusementTheme => game.BuiltStage == TownStage.AmusementPark;
        private AmusementGeometry amusementGeometry;
        private Material parkYellow, parkRed, parkBlue, parkPink, parkWhite;
        private Material[] parkRewardPaints;
        private readonly string[] amusementNames = { "Ferris wheel", "Roller coaster", "Carousel", "Drop tower", "Ticket booth", "Snack booth", "Amusement entrance gate" };

        private void InitializeAmusementPalette()
        {
            amusementGeometry = new AmusementGeometry();
            parkYellow = MakeMaterial(new Color(1, .77f, .025f), ToySurface.Metal);
            parkRed = MakeMaterial(new Color(1, .13f, .045f), ToySurface.Metal);
            parkBlue = MakeMaterial(new Color(.015f, .68f, 1), ToySurface.Metal);
            parkPink = MakeMaterial(new Color(1, .045f, .5f));
            parkWhite = MakeMaterial(new Color(1, .96f, .81f), ToySurface.Metal);
            parkRewardPaints = new Material[6];
            for (int i = 0; i < 6; i++) parkRewardPaints[i] = MakeMaterial(ItemIcons.Color((ItemKind)i));
        }

        private BlockBuilding AmusementRoot(Vector3 center, float width, int style, ItemKind reward)
        {
            var root = new GameObject(amusementNames[style]); root.transform.SetParent(transform, false); root.transform.position = center;
            var building = root.AddComponent<BlockBuilding>(); building.Initialize(game);
            building.Width = width; building.Floors = 1; building.Style = style; building.RewardKind = reward;
            building.FacadeColor = ItemIcons.Color(reward); Buildings.Add(building); return building;
        }

        private DestructibleBlock ParkPart(BlockBuilding building, string label, Vector3 point, Vector3 size, Material material, BuildingPart kind = BuildingPart.Beam, Mesh mesh = null)
        {
            var block = Part(label, point, size, material, kind, 0, -1, building);
            if (mesh != null) { block.GetComponent<MeshFilter>().sharedMesh = mesh; block.PreserveShape = true; }
            return block;
        }
        private DestructibleBlock ParkStroke(BlockBuilding building, string label, Vector3 a, Vector3 b, float thickness, Material material)
        {
            var block = ParkPart(building, label, (a + b) * .5f, new Vector3(thickness, (b - a).magnitude, thickness), material);
            block.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a); return block;
        }
        private DestructibleBlock ParkFoot(BlockBuilding building, Vector3 point, float height = 1.8f)
        {
            float ground = GroundPoint(building.transform.position + point).y - building.transform.position.y;
            var foot = ParkPart(building, "Amusement foundation", new Vector3(point.x, ground + height / 2, point.z), new Vector3(1.8f, height, 1.8f), parkWhite, BuildingPart.Pillar);
            foot.Foundation = true; return foot;
        }
        private GameObject ParkDetail(Transform parent, string label, Vector3 point, Vector3 size, Material material, Mesh mesh = null)
        {
            var item = Box(label, Vector3.zero, size, material, parent, false);
            var scale = parent.localScale;
            item.transform.localPosition = new Vector3(point.x / scale.x, point.y / scale.y, point.z / scale.z);
            item.transform.localScale = new Vector3(size.x / scale.x, size.y / scale.y, size.z / scale.z);
            if (mesh != null) item.GetComponent<MeshFilter>().sharedMesh = mesh;
            return item;
        }
        private void ParkRewardBanner(BlockBuilding building, DestructibleBlock parent)
        {
            ParkDetail(parent.transform, "Reward banner pole", new Vector3(-1.25f, parent.transform.localScale.y * .5f + 1.7f, -.3f), new Vector3(.18f, 3.4f, .18f), dark);
            ParkDetail(parent.transform, "Amusement reward banner", new Vector3(0, parent.transform.localScale.y * .5f + 1.5f, -.3f), new Vector3(2.4f, 2.7f, .18f), parkRewardPaints[(int)building.RewardKind]);
            ParkDetail(parent.transform, "Reward banner star", new Vector3(0, parent.transform.localScale.y * .5f + 1.7f, -.44f), new Vector3(1.1f, 1.1f, .12f), parkWhite, amusementGeometry.Star);
        }

        private void CreateAmusementBuilding(Vector3 center, float width, int style, ItemKind reward)
        {
            var building = AmusementRoot(center, width, style, reward);
            switch (style)
            {
                case 0: CreateFerrisWheel(building, width); break;
                case 1: CreateRollerCoaster(building, width); break;
                case 2: CreateCarousel(building, width); break;
                case 3: CreateDropTower(building); break;
                default: CreateParkBooth(building, width, style == 5); break;
            }
            CreateBuildingReward(building);
            CreateAmusementFences(center, width);
            if ((Buildings.Count - 1) % 3 == 0) CreateExplosiveCluster(building);
        }

        private void CreateFerrisWheel(BlockBuilding building, float width)
        {
            float radius = width * .56f, hubY = radius + 3.8f;
            var center = Vector3.up * hubY;
            var hub = ParkPart(building, "Ferris red hub", center, new Vector3(3.8f, 3.8f, 2.5f), parkRed, BuildingPart.Beam, amusementGeometry.Sphere);
            ParkDetail(hub.transform, "Ferris hub star", new Vector3(0, 0, -1.35f), new Vector3(2.8f, 2.8f, .3f), parkYellow, amusementGeometry.Star);
            for (int i = 0; i < 4; i++)
            {
                var point = new Vector3(i % 2 == 0 ? -width * .39f : width * .39f, 0, i < 2 ? -2.7f : 2.7f);
                var foot = ParkFoot(building, point);
                var arm = ParkStroke(building, "Ferris A-frame", point + Vector3.up * 1.8f, center + Vector3.forward * point.z * .25f, 1.15f, parkYellow);
                BlockBuilding.Connect(foot, arm); BlockBuilding.Connect(arm, hub);
                if (i == 0) ParkRewardBanner(building, foot);
            }
            const int count = 16;
            var rims = new DestructibleBlock[count];
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count, next = (i + 1) * Mathf.PI * 2 / count;
                var a = center + new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0) * radius;
                var b = center + new Vector3(Mathf.Sin(next), Mathf.Cos(next), 0) * radius;
                rims[i] = ParkStroke(building, "Ferris rim segment", a, b, .68f, parkYellow);
                var spoke = ParkStroke(building, "Ferris spoke", center, a, .30f, i % 4 == 0 ? parkYellow : parkWhite);
                BlockBuilding.Connect(hub, spoke); BlockBuilding.Connect(spoke, rims[i]);
                if (i > 0) BlockBuilding.Connect(rims[i - 1], rims[i]);
                if (i % 2 != 0) continue;
                var color = i % 8 == 0 ? parkBlue : i % 8 == 2 ? parkPink : i % 8 == 4 ? parkYellow : parkRed;
                var cabin = ParkPart(building, "Ferris gondola", a + new Vector3(0, -1.15f, -1.1f), new Vector3(2.8f, 1.9f, 2), color, BuildingPart.Prop);
                cabin.PreserveShape = true; BlockBuilding.Connect(rims[i], cabin);
                ParkDetail(cabin.transform, "Gondola roof", new Vector3(0, 1.2f, 0), new Vector3(3, .32f, 2.15f), color);
                ParkDetail(cabin.transform, "Gondola front glass", new Vector3(0, .25f, -1.04f), new Vector3(2.15f, .9f, .08f), glass);
                ParkDetail(cabin.transform, "Gondola rear glass", new Vector3(0, .25f, 1.04f), new Vector3(2.15f, .9f, .08f), glass);
            }
            BlockBuilding.Connect(rims[count - 1], rims[0]);
        }

        private void CreateRollerCoaster(BlockBuilding building, float width)
        {
            const int count = 24;
            Vector3 Track(float angle, float side)
                => new Vector3(Mathf.Cos(angle) * (width * .47f + side), 9 + 6 * Mathf.Sin(angle * 2 + .5f), Mathf.Sin(angle) * (width * .40f + side));
            var outer = new DestructibleBlock[count]; var inner = new DestructibleBlock[count];
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2 / count, b = (i + 1) * Mathf.PI * 2 / count;
                outer[i] = ParkStroke(building, "Coaster outer rail", Track(a, .6f), Track(b, .6f), .48f, parkRed);
                inner[i] = ParkStroke(building, "Coaster inner rail", Track(a, -.6f), Track(b, -.6f), .48f, parkRed);
                var tie = ParkStroke(building, "Coaster cross tie", Track(a, -.85f), Track(a, .85f), .30f, parkYellow);
                BlockBuilding.Connect(outer[i], tie); BlockBuilding.Connect(inner[i], tie);
                if (i > 0) { BlockBuilding.Connect(outer[i - 1], outer[i]); BlockBuilding.Connect(inner[i - 1], inner[i]); }
                if (i % 3 != 0) continue;
                var at = Track(a, 0); var foot = ParkFoot(building, new Vector3(at.x, 0, at.z), 1.3f);
                var post = ParkStroke(building, "Coaster support column", new Vector3(at.x, 1.3f, at.z), at, 1.1f, parkWhite);
                BlockBuilding.Connect(foot, post); BlockBuilding.Connect(post, tie);
                if (i == 0) ParkRewardBanner(building, foot);
            }
            BlockBuilding.Connect(outer[count - 1], outer[0]); BlockBuilding.Connect(inner[count - 1], inner[0]);
        }

        private void CreateCarousel(BlockBuilding building, float width)
        {
            float radius = width * .47f;
            var hub = ParkPart(building, "Carousel centre column", Vector3.up * 3.85f, new Vector3(1.5f, 7.2f, 1.5f), parkRed);
            var feet = new DestructibleBlock[4];
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(i % 2 == 0 ? -3 : 3, 0, i < 2 ? -3 : 3);
                var foot = feet[i] = ParkFoot(building, p, .5f); BlockBuilding.Connect(foot, hub);
                if (i == 0) ParkRewardBanner(building, foot);
            }
            for (int i = 0; i < 16; i++)
            {
                float angle = i * 22.5f;
                var point = Quaternion.Euler(0, angle, 0) * Vector3.forward * radius * .5f + Vector3.up * .32f;
                var floor = ParkPart(building, "Carousel circular floor slice", point, new Vector3(radius * .4f, .45f, radius), parkYellow, BuildingPart.Slab, amusementGeometry.FloorPanel);
                floor.transform.localRotation = Quaternion.Euler(0, angle, 0);
                BlockBuilding.Connect(hub, floor); BlockBuilding.Connect(feet[i / 4], floor);
            }
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30, radians = angle * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians));
                var panel = ParkPart(building, "Carousel striped canopy panel", offset * radius * .5f + Vector3.up * 6.2f,
                    new Vector3(radius * .54f, 2.6f, radius), i % 2 == 0 ? parkRed : parkWhite, BuildingPart.Prop, amusementGeometry.RoofPanel);
                panel.transform.localRotation = Quaternion.Euler(0, angle, 0); BlockBuilding.Connect(hub, panel);
                ParkDetail(panel.transform, "Canopy golden bulb", new Vector3(0, -1.4f, radius / 2), Vector3.one * .55f, parkYellow, amusementGeometry.Sphere);
                if (i % 2 != 0) continue;
                var pole = ParkPart(building, "Carousel outer column", offset * radius * .74f + Vector3.up * 2.6f, new Vector3(.35f, 4.4f, .35f), parkWhite);
                BlockBuilding.Connect(hub, pole); BlockBuilding.Connect(pole, panel);
                var seat = ParkPart(building, "Carousel toy seat", offset * radius * .55f + Vector3.up * 1.35f, new Vector3(1.9f, 1.0f, 1.1f), i % 4 == 0 ? parkBlue : parkPink, BuildingPart.Prop);
                BlockBuilding.Connect(pole, seat);
                ParkDetail(seat.transform, "Seat back", new Vector3(0, .65f, .35f), new Vector3(1.9f, .85f, .25f), parkYellow);
            }
        }

        private void CreateDropTower(BlockBuilding building)
        {
            DestructibleBlock previousTop = null;
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(i % 2 == 0 ? -3.6f : 3.6f, 0, i < 2 ? -3.6f : 3.6f);
                var previous = ParkFoot(building, p);
                if (i == 0) ParkRewardBanner(building, previous);
                var mastPoint = p * .45f;
                var brace = ParkStroke(building, "Drop tower base brace", p + Vector3.up * 1.8f, mastPoint + Vector3.up * 3.4f, .8f, parkBlue);
                BlockBuilding.Connect(previous, brace); previous = brace;
                for (int level = 0; level < 5; level++)
                {
                    var shaft = ParkPart(building, "Drop tower mast section", mastPoint + Vector3.up * (3.8f + level * 4), new Vector3(.9f, 4, .9f), parkBlue);
                    BlockBuilding.Connect(previous, shaft); previous = shaft;
                    ParkDetail(shaft.transform, "Yellow drop tower guide", new Vector3(.52f, 0, 0), new Vector3(.18f, 3.95f, .3f), parkYellow);
                }
                if (previousTop != null) BlockBuilding.Connect(previousTop, previous);
                previousTop = previous;
            }
            var crown = ParkPart(building, "Drop tower star crown", Vector3.up * 22.8f, new Vector3(6, 2.5f, 6), parkBlue);
            BlockBuilding.Connect(previousTop, crown);
            ParkDetail(crown.transform, "Drop tower star sign", new Vector3(0, .2f, -3.15f), new Vector3(2.2f, 2.2f, .25f), parkYellow, amusementGeometry.Star);
            var carriage = ParkPart(building, "Drop tower yellow carriage", Vector3.up * 10, new Vector3(7.4f, .7f, 7.4f), parkYellow);
            BlockBuilding.Connect(crown, carriage);
            for (int i = 0; i < 4; i++)
            {
                var seat = ParkPart(building, "Drop tower seat", Quaternion.Euler(0, i * 90, 0) * new Vector3(0, 10.3f, -3.1f), new Vector3(3.7f, 1.1f, 1.2f), dark, BuildingPart.Prop);
                seat.transform.localRotation = Quaternion.Euler(0, i * 90, 0); BlockBuilding.Connect(carriage, seat);
                ParkDetail(seat.transform, "Drop tower safety bar", new Vector3(0, .75f, -.7f), new Vector3(3.8f, .22f, .22f), parkYellow);
            }
        }

        private void CreateParkBooth(BlockBuilding building, float width, bool snack)
        {
            float w = width * .65f, depth = width * .38f;
            var accent = parkRewardPaints[(int)building.RewardKind];
            var posts = new DestructibleBlock[4]; var beams = new DestructibleBlock[4];
            var corners = new[] { new Vector3(-w / 2, 0, -depth / 2), new Vector3(w / 2, 0, -depth / 2), new Vector3(w / 2, 0, depth / 2), new Vector3(-w / 2, 0, depth / 2) };
            for (int i = 0; i < 4; i++)
            {
                posts[i] = ParkFoot(building, corners[i], 4.2f);
                beams[i] = ParkStroke(building, "Booth roof frame", corners[i] + Vector3.up * 4.2f, corners[(i + 1) % 4] + Vector3.up * 4.2f, .45f, parkYellow);
            }
            for (int i = 0; i < 4; i++) { BlockBuilding.Connect(posts[i], beams[i]); BlockBuilding.Connect(posts[(i + 1) % 4], beams[i]); }
            for (int i = 0; i < 4; i++)
            {
                var wall = ParkPart(building, "Booth wall", (corners[i] + corners[(i + 1) % 4]) * .5f + Vector3.up * 2,
                    i % 2 == 0 ? new Vector3(w - 1.5f, 3.5f, .3f) : new Vector3(.3f, 3.5f, depth - 1.5f), accent, BuildingPart.Wall);
                BlockBuilding.Connect(wall, beams[i]);
                if (i != 0) continue;
                ParkDetail(wall.transform, "Booth entrance door", new Vector3(-w * .30f, -.35f, -.24f), new Vector3(1.8f, 2.8f, .18f), dark);
                ParkDetail(wall.transform, "Booth door glass", new Vector3(-w * .30f, .15f, -.36f), new Vector3(1.1f, 1.2f, .10f), glass);
                ParkDetail(wall.transform, "Booth serving window", new Vector3(w * .12f, .35f, -.23f), new Vector3(w * .43f, 1.9f, .2f), glass);
                ParkDetail(wall.transform, "Booth serving counter", new Vector3(w * .12f, -.6f, -.55f), new Vector3(w * .46f, .22f, .8f), parkWhite);
                ParkDetail(wall.transform, "Booth sign backing", new Vector3(0, 2.6f, -.3f), new Vector3(3.6f, 1.5f, .25f), parkWhite);
                if (snack)
                {
                    ParkDetail(wall.transform, "Burger bun", new Vector3(0, 2.85f, -.5f), new Vector3(2.5f, .65f, .35f), parkYellow, amusementGeometry.Sphere);
                    ParkDetail(wall.transform, "Burger filling", new Vector3(0, 2.5f, -.5f), new Vector3(2.3f, .25f, .32f), parkRed);
                    ParkDetail(wall.transform, "Burger lower bun", new Vector3(0, 2.25f, -.5f), new Vector3(2.4f, .35f, .35f), parkYellow);
                }
                else ParkDetail(wall.transform, "Ticket booth star", new Vector3(0, 2.6f, -.5f), new Vector3(1.2f, 1.2f, .15f), parkBlue, amusementGeometry.Star);
            }
            for (int i = 0; i < 8; i++) for (int side = -1; side <= 1; side += 2)
            {
                var roof = ParkPart(building, "Booth striped roof panel", new Vector3((i + .5f) * (w + .8f) / 8 - (w + .8f) / 2, 4.8f, side * depth * .25f),
                    new Vector3((w + .8f) / 8, .28f, depth * .60f), i % 2 == 0 ? accent : parkWhite, BuildingPart.Prop);
                roof.transform.localRotation = Quaternion.Euler(side * 16, 0, 0);
                BlockBuilding.Connect(roof, beams[side < 0 ? 0 : 2]);
            }
        }

        private void CreateAmusementGate()
        {
            var center = GroundPoint(new Vector3(0, 0, -HalfExtent + 20 + game.Settings.blockSpacing * 2.8f));
            var building = AmusementRoot(center, RoadWidth + 12, 6, ItemKind.Magnet);
            var feet = new DestructibleBlock[4];
            float radius = RoadWidth / 2 + 4;
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -radius : radius, z = i % 2 == 0 ? -1.5f : 1.5f;
                var point = new Vector3(x, 0, z);
                float height = 8.8f - (GroundPoint(center + point).y - center.y);
                feet[i] = ParkFoot(building, point, Mathf.Max(2, height));
                ParkDetail(feet[i].transform, "Gate colored tower", new Vector3(0, 0, 0), new Vector3(2.3f, 8.3f, 2.3f), i < 2 ? parkPink : parkBlue);
                ParkDetail(feet[i].transform, "Gate tower crown", new Vector3(0, 4.6f, 0), new Vector3(2.9f, .6f, 2.9f), parkYellow);
                ParkDetail(feet[i].transform, "Gate pennant", new Vector3(.75f, 5.8f, 0), new Vector3(2.5f, .9f, .15f), parkPink);
            }
            DestructibleBlock previous = null, first = null;
            for (int i = 0; i < 12; i++)
            {
                float a = -Mathf.PI / 2 + i * Mathf.PI / 12, b = a + Mathf.PI / 12;
                var left = new Vector3(Mathf.Sin(a) * radius, 8.8f + Mathf.Cos(a) * 5, 0);
                var right = new Vector3(Mathf.Sin(b) * radius, 8.8f + Mathf.Cos(b) * 5, 0);
                var arch = ParkStroke(building, "Entrance arch segment", left, right, 1.2f, parkBlue);
                if (previous != null) BlockBuilding.Connect(previous, arch); else first = arch;
                previous = arch;
                if (i == 5)
                {
                    var star = ParkPart(building, "Entrance golden star", new Vector3(0, 13.7f, -.85f), new Vector3(2.5f, 2.5f, .25f), parkYellow, BuildingPart.Prop, amusementGeometry.Star);
                    BlockBuilding.Connect(arch, star);
                }
            }
            BlockBuilding.Connect(feet[0], first); BlockBuilding.Connect(feet[1], first);
            BlockBuilding.Connect(feet[2], previous); BlockBuilding.Connect(feet[3], previous);
            CreateBuildingReward(building);
        }

        private void CreateAmusementFences(Vector3 center, float width)
        {
            foreach (int side in new[] { -1, 1 })
            {
                var p = center + new Vector3(side * width * .38f, 0, -width * .54f);
                var rail = Box("Park low fence", GroundPoint(p, 1.1f), new Vector3(width * .28f, .18f, .18f), dark);
                AddBlock(rail, true);
                ParkDetail(rail.transform, "Fence lower rail", Vector3.down * .55f, new Vector3(width * .28f, .15f, .15f), dark);
                for (int i = -1; i <= 1; i++) ParkDetail(rail.transform, "Fence upright", new Vector3(i * width * .135f, -.45f, 0), new Vector3(.20f, 1.1f, .20f), dark);
            }
        }

        private void CreateAmusementProp(Vector3 position, string label)
        {
            if (label == "Tree")
            {
                var pot = Box("Park planter tree", GroundPoint(position, .5f), new Vector3(3, 1, 3), lime); AddBlock(pot, true);
                ParkDetail(pot.transform, "Tree trunk", Vector3.up * 1.4f, new Vector3(.55f, 2.1f, .55f), wood);
                ParkDetail(pot.transform, "Round park foliage", Vector3.up * 3.5f, new Vector3(4.2f, 4, 4.2f), grass, amusementGeometry.Sphere); return;
            }
            var post = Box("Balloon street lamp", GroundPoint(position, 2.8f), new Vector3(.50f, 5.6f, .5f), dark); AddBlock(post, true);
            ParkDetail(post.transform, "Park hanging banner", new Vector3(.95f, 1, 0), new Vector3(1.8f, 2.5f, .18f), parkPink);
            for (int i = 0; i < 3; i++) ParkDetail(post.transform, "Park balloon", new Vector3((i - 1) * 1.05f, 3.0f + (i == 1 ? .6f : 0), 0), new Vector3(1.9f, 2.2f, 1.9f), i == 0 ? parkYellow : i == 1 ? parkBlue : parkPink, amusementGeometry.Sphere);
        }
    }
}
