using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    public sealed partial class TankTownWorld
    {
        internal bool AmusementTheme => game.BuiltStage == TownStage.AmusementPark;
        private AmusementGeometry amusementGeometry;
        // Ride parts, booth signs and park props made by scripts/blender/models/amusement_kit.py; null keeps the box rides.
        private TownModelKit amusementKit;
        internal TownModelKit AmusementKit => amusementKit;
        private Material parkYellow, parkRed, parkBlue, parkPink, parkWhite, parkMint, parkPurple, parkWood, parkPlaza;
        private Material[] parkRewardPaints, parkRideColors;
        private readonly Dictionary<string, Material> parkPaints = new Dictionary<string, Material>();
        private readonly string[] amusementNames =
        {
            "Ferris wheel", "Roller coaster", "Carousel", "Drop tower", "Ticket booth", "Snack booth", "Amusement entrance gate",
            "Giant roller coaster", "Giant Ferris wheel", "Circus tent", "Pirate ship", "Teacups"
        };

        private void InitializeAmusementPalette()
        {
            amusementGeometry = new AmusementGeometry();
            parkYellow = MakeMaterial(new Color(1, .77f, .025f), ToySurface.Metal);
            parkRed = MakeMaterial(new Color(1, .13f, .045f), ToySurface.Metal);
            parkBlue = MakeMaterial(new Color(.015f, .68f, 1), ToySurface.Metal);
            parkPink = MakeMaterial(new Color(1, .045f, .5f));
            parkWhite = MakeMaterial(new Color(1, .96f, .81f), ToySurface.Metal);
            parkMint = MakeMaterial(new Color(.2f, .9f, .68f));
            parkPurple = MakeMaterial(new Color(.58f, .3f, 1));
            parkWood = MakeMaterial(new Color(.62f, .36f, .18f), ToySurface.Wood);
            // Merged attraction lots are paved in a warm colour, so their size reads on the street and the map.
            parkPlaza = MakeMaterial(new Color(1, .74f, .6f), ToySurface.Concrete);
            parkRewardPaints = new Material[6];
            for (int i = 0; i < 6; i++) parkRewardPaints[i] = MakeMaterial(ItemIcons.Color((ItemKind)i));
            parkRideColors = new[] { parkBlue, parkPink, parkYellow, parkRed, parkMint, parkPurple };
            if (!game.Settings.importedCityModels) return;
            amusementKit = TownModelKit.Load(new Dictionary<string, Material>
            {
                ["ParkTint"] = parkYellow, ["ParkAccent"] = parkBlue, ["ParkPaint"] = parkPink, ["ParkRed"] = parkRed,
                ["ParkYellow"] = parkYellow, ["ParkBlue"] = parkBlue, ["ParkPink"] = parkPink, ["ParkWhite"] = parkWhite,
                ["ParkDark"] = dark, ["ParkGlass"] = glass, ["ParkLamp"] = lampGlow, ["ParkLeaf"] = grass, ["ParkWood"] = parkWood,
                ["ParkLime"] = lime, ["CityFrame"] = parkWhite
            }, "Models/AmusementKit");
            amusementKit?.RoundInDistance("Ferris hub", "Teacup", "Teapot", "Drop tower ring", "Carousel horse");
            amusementKit?.HideInDistance("Wheel rim bulbs", "Wheel rim ties", "Gondola hanger", "Gondola trim", "Ferris star", "Ferris cap",
                "Deck posts", "Deck rail", "Coaster ties", "Coaster car trim", "Coaster headlights", "Station posts and stripes", "Station star",
                "Canopy bulbs", "Horse hooves", "Horse mane and pole", "Horse saddle", "Carousel mirrors", "Carousel spire", "Carousel flag",
                "Crown stars", "Crown bulbs", "Crown spire", "Drop seats", "Drop harnesses", "Ship trim", "Ship stripe", "Ship stars and flags",
                "Ship flagpoles", "Teacup rim", "Teacup wheel", "Teapot gold", "Tent star", "Gate windows", "Gate flagpole", "Gate flag",
                "Gate star bulbs", "Tent flagpole", "Tent flag", "Awning fringe", "Ticket star", "Burger cheese and seeds", "Burger lettuce");
        }

        // Paint overrides for one kit attachment; the shared dictionary is refilled on every call.
        private Dictionary<string, Material> ParkPaints(Material tint, Material accent = null, Material paint = null)
        {
            parkPaints.Clear();
            if (tint != null) parkPaints["ParkTint"] = tint;
            if (accent != null) parkPaints["ParkAccent"] = accent;
            if (paint != null) parkPaints["ParkPaint"] = paint;
            return parkPaints;
        }

        // Draws a part with a kit piece instead of its box; the collider and support links stay, and the part's colour tints the piece.
        // Without a stretch the piece is in metres around the part's centre; a stretch scales a unit-sized piece.
        private bool ParkSkin(DestructibleBlock part, string piece, Vector3? stretch = null, Material accent = null, Material paint = null)
        {
            if (amusementKit == null || !amusementKit.Has(piece)) return false;
            var renderer = part.GetComponent<MeshRenderer>();
            var tint = renderer.sharedMaterial;
            // Removed now, not at the end of the frame: the building batches every renderer it finds when the town is built.
            DestroyImmediate(renderer);
            amusementKit.Attach(part.transform, piece, Vector3.zero, stretch ?? Vector3.one, ParkPaints(tint, accent, paint));
            return true;
        }

        private static string TrussFor(float length, float thickness)
        {
            float bays = length / Mathf.Max(thickness, .1f);
            return bays < 4.5f ? "Truss 3" : bays < 9 ? "Truss 6" : "Truss 12";
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
        // A straight steel member drawn as a lattice truss or a round tube.
        private DestructibleBlock ParkBeam(BlockBuilding building, string label, Vector3 a, Vector3 b, float thickness, Material material, bool lattice)
        {
            var block = ParkStroke(building, label, a, b, thickness, material);
            float length = (b - a).magnitude;
            ParkSkin(block, lattice ? TrussFor(length, thickness) : "Tube", new Vector3(thickness, length, thickness));
            return block;
        }
        // A part between two points whose local Z stays on the ride's axle, as the wheel rim and spokes need.
        private DestructibleBlock ParkSpan(BlockBuilding building, string label, Vector3 a, Vector3 b, Vector2 section, Material material, BuildingPart kind = BuildingPart.Beam)
        {
            var block = ParkPart(building, label, (a + b) * .5f, new Vector3(section.x, (b - a).magnitude, section.y), material, kind);
            block.transform.localRotation = Quaternion.LookRotation(Vector3.forward, b - a); return block;
        }
        private DestructibleBlock ParkFoot(BlockBuilding building, Vector3 point, float height = 1.8f)
        {
            float ground = GroundPoint(building.transform.TransformPoint(point)).y - building.transform.position.y;
            var foot = ParkPart(building, "Amusement foundation", new Vector3(point.x, ground + height / 2, point.z), new Vector3(1.8f, height, 1.8f), parkWhite, BuildingPart.Pillar);
            foot.Foundation = true; return foot;
        }
        private float FootTop(DestructibleBlock foot) => foot.transform.localPosition.y + foot.transform.localScale.y / 2;
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

        // A lot of one block, or a merged lot of 2×1 or 2×2 blocks. Long lots turn the ride so its length follows the lot.
        private void CreateAmusementBuilding(Vector3 center, TownLayout.Plot plot)
        {
            float spacing = game.Settings.blockSpacing;
            float length = BuildingWidth + (Mathf.Max(plot.SpanX, plot.SpanZ) - 1) * spacing;
            float depth = BuildingWidth + (Mathf.Min(plot.SpanX, plot.SpanZ) - 1) * spacing;
            bool alongZ = plot.SpanZ > plot.SpanX;
            // The reward circle follows the narrow side, so a long lot's circle stays on its plaza.
            var building = AmusementRoot(center, depth, plot.Style, plot.Reward);
            if (alongZ) building.transform.rotation = Quaternion.Euler(0, 90, 0);
            switch (plot.Style)
            {
                case 0: CreateFerrisWheel(building, BuildingWidth * .56f, 16, 1, new Vector2(BuildingWidth * .39f, 2.7f)); break;
                case 1: CreateRollerCoaster(building, BuildingWidth); break;
                case 2: CreateCarousel(building, BuildingWidth); break;
                case 3: CreateDropTower(building); break;
                case TownLayout.GiantCoaster: CreateGiantCoaster(building); break;
                case TownLayout.GiantWheel: CreateFerrisWheel(building, 19, 32, 1.2f, new Vector2(14, 4.2f)); break;
                case TownLayout.CircusTent: CreateCircusTent(building); break;
                case TownLayout.PirateShip: CreatePirateShip(building); break;
                case TownLayout.Teacups: CreateTeacups(building, BuildingWidth); break;
                default: CreateParkBooth(building, BuildingWidth, plot.Style == 5); break;
            }
            CreateBuildingReward(building);
            CreateAmusementFences(building, length, depth);
            if ((Buildings.Count - 1) % 3 != 0) return;
            if (length <= BuildingWidth + 1) { CreateExplosiveCluster(building); return; }
            // On a merged lot the barrels line up across one end of its plaza, clear of the ride.
            CreateExplosiveCluster(building, building.transform.TransformPoint(new Vector3(length / 2 - 2.5f, 0, -5.25f)), building.transform.forward * 3.5f);
        }

        private void CreateFerrisWheel(BlockBuilding building, float radius, int count, float gondola, Vector2 legs)
        {
            bool kit = amusementKit != null;
            // The lowest gondola clears the boarding deck.
            float hubY = radius + 1.1f + 2.75f * gondola;
            var center = Vector3.up * hubY;
            var hub = ParkPart(building, "Ferris red hub", center, new Vector3(3.8f, 3.8f, kit ? 2.9f : 2.5f) * gondola, parkRed, BuildingPart.Beam, kit ? null : amusementGeometry.Sphere);
            if (!ParkSkin(hub, "Ferris hub", Vector3.one * gondola))
                ParkDetail(hub.transform, "Ferris hub star", new Vector3(0, 0, -1.35f), new Vector3(2.8f, 2.8f, .3f), parkYellow, amusementGeometry.Star);
            var feet = new DestructibleBlock[4];
            for (int i = 0; i < 4; i++)
            {
                var point = new Vector3(i % 2 == 0 ? -legs.x : legs.x, 0, i < 2 ? -legs.y : legs.y);
                var foot = feet[i] = ParkFoot(building, point);
                var axle = center + Vector3.forward * Mathf.Sign(point.z) * (kit ? 2.3f * gondola : legs.y * .25f);
                var arm = ParkBeam(building, "Ferris A-frame", new Vector3(point.x, FootTop(foot), point.z), axle, 1.15f * gondola, parkYellow, true);
                BlockBuilding.Connect(foot, arm); BlockBuilding.Connect(arm, hub);
                if (i == 0) ParkRewardBanner(building, foot);
            }
            if (kit)
            {
                var deck = ParkPart(building, "Ferris boarding deck", new Vector3(0, FootTop(feet[0]) - 1.8f + .45f, 0), new Vector3(7, .9f, 4.2f), parkBlue, BuildingPart.Prop);
                ParkSkin(deck, "Ferris deck");
                BlockBuilding.Connect(deck, feet[0]); BlockBuilding.Connect(deck, feet[1]);
            }
            var rims = new DestructibleBlock[count];
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count, next = (i + 1) * Mathf.PI * 2 / count;
                var a = center + new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0) * radius;
                var b = center + new Vector3(Mathf.Sin(next), Mathf.Cos(next), 0) * radius;
                // Kit rims and spokes are twin members on the front and back faces, with the gondolas between them.
                rims[i] = ParkSpan(building, "Ferris rim segment", a, b, kit ? new Vector2(.6f, 2.9f) : new Vector2(.68f, .68f), parkYellow);
                ParkSkin(rims[i], "Wheel rim", new Vector3(1, (b - a).magnitude / 4, 1));
                var spoke = ParkSpan(building, "Ferris spoke", center, a, kit ? new Vector2(.3f, 2.7f) : new Vector2(.3f, .3f), i % 4 == 0 ? parkYellow : parkWhite);
                ParkSkin(spoke, "Wheel spoke", new Vector3(1, (a - center).magnitude, 1));
                BlockBuilding.Connect(hub, spoke); BlockBuilding.Connect(spoke, rims[i]);
                if (i > 0) BlockBuilding.Connect(rims[i - 1], rims[i]);
                if (i % 2 != 0) continue;
                var color = parkRideColors[(i / 2) % parkRideColors.Length];
                var cabin = ParkPart(building, "Ferris gondola", a + (kit ? new Vector3(0, -1.75f * gondola, 0) : new Vector3(0, -1.15f, -1.1f)),
                    kit ? new Vector3(2.4f, 2, 2) * gondola : new Vector3(2.8f, 1.9f, 2), color, BuildingPart.Prop);
                cabin.PreserveShape = true; BlockBuilding.Connect(rims[i], cabin);
                if (ParkSkin(cabin, "Ferris gondola", Vector3.one * gondola, color)) continue;
                ParkDetail(cabin.transform, "Gondola roof", new Vector3(0, 1.2f, 0), new Vector3(3, .32f, 2.15f), color);
                ParkDetail(cabin.transform, "Gondola front glass", new Vector3(0, .25f, -1.04f), new Vector3(2.15f, .9f, .08f), glass);
                ParkDetail(cabin.transform, "Gondola rear glass", new Vector3(0, .25f, 1.04f), new Vector3(2.15f, .9f, .08f), glass);
            }
            BlockBuilding.Connect(rims[count - 1], rims[0]);
        }

        // Track up along a smooth path: world up, tilted into horizontal turns.
        private static Vector3[] BankedNormals(Vector3[] points, float bank)
        {
            int n = points.Length; var normals = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 previous = points[(i - 1 + n) % n], here = points[i], next = points[(i + 1) % n];
                var turn = (next - here).normalized - (here - previous).normalized; turn.y = 0;
                float step = ((next - here).magnitude + (here - previous).magnitude) * .5f;
                normals[i] = Vector3.ProjectOnPlane(Vector3.up + turn / Mathf.Max(step, .1f) * bank, next - previous).normalized;
            }
            return normals;
        }

        // A closed coaster circuit, one part per span between consecutive points.
        private DestructibleBlock[] CoasterTrack(BlockBuilding building, Vector3[] points, Vector3[] normals, Material rails)
        {
            int count = points.Length;
            bool kit = amusementKit != null && amusementKit.Has("Coaster track");
            var spans = new DestructibleBlock[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 a = points[i], b = points[(i + 1) % count];
                float length = (b - a).magnitude + .08f;
                var span = ParkPart(building, "Coaster track", (a + b) * .5f, kit ? new Vector3(1.9f, .75f, length) : new Vector3(1.7f, .3f, length), rails);
                span.transform.localRotation = Quaternion.LookRotation(b - a, normals[i] + normals[(i + 1) % count]);
                ParkSkin(span, "Coaster track", new Vector3(1, 1, length / 3));
                if (i > 0) BlockBuilding.Connect(spans[i - 1], span);
                spans[i] = span;
            }
            BlockBuilding.Connect(spans[count - 1], spans[0]);
            return spans;
        }

        private DestructibleBlock CoasterSupport(BlockBuilding building, DestructibleBlock[] spans, int index, Vector3 point, Vector3 normal)
        {
            var top = point - normal * .45f;
            var foot = ParkFoot(building, new Vector3(top.x, 0, top.z), 1.3f);
            var column = ParkBeam(building, "Coaster support column", new Vector3(top.x, FootTop(foot), top.z), top, 1.0f, parkWhite, true);
            BlockBuilding.Connect(foot, column); BlockBuilding.Connect(column, spans[index]);
            BlockBuilding.Connect(column, spans[(index - 1 + spans.Length) % spans.Length]);
            return foot;
        }

        // A train sitting on consecutive spans, its lead car on the last of them.
        private void CoasterTrain(BlockBuilding building, DestructibleBlock[] spans, int first, int cars, Material color)
        {
            for (int k = 0; k < cars; k++)
            {
                var span = spans[(first + k) % spans.Length];
                var car = ParkPart(building, "Coaster car", span.transform.localPosition + span.transform.localRotation * Vector3.up * .85f,
                    new Vector3(1.7f, 1.1f, 2.6f), color, BuildingPart.Prop);
                car.transform.localRotation = span.transform.localRotation;
                car.PreserveShape = true; BlockBuilding.Connect(span, car);
                ParkSkin(car, k == cars - 1 ? "Coaster car front" : "Coaster car", null, color);
            }
        }

        private void CreateRollerCoaster(BlockBuilding building, float width)
        {
            const int count = 24;
            var points = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count;
                points[i] = new Vector3(Mathf.Cos(angle) * width * .47f, 9 + 6 * Mathf.Sin(angle * 2 + .5f), Mathf.Sin(angle) * width * .40f);
            }
            var normals = BankedNormals(points, 5);
            var spans = CoasterTrack(building, points, normals, parkRed);
            for (int i = 0; i < count; i += 3)
            {
                var foot = CoasterSupport(building, spans, i, points[i], normals[i]);
                if (i == 0) ParkRewardBanner(building, foot);
            }
            CoasterTrain(building, spans, 1, 3, parkRewardPaints[(int)building.RewardKind]);
        }

        // The four-block coaster: station, lift hill, first drop, a vertical loop, banked turns and a camelback hill.
        private static readonly Vector3[] giantCoasterPath =
        {
            new Vector3(-16, 2.2f, -25), new Vector3(-4, 2.2f, -25), new Vector3(6, 2.2f, -25), new Vector3(15, 3, -23), new Vector3(21, 5, -18),
            new Vector3(24, 9, -10), new Vector3(24, 17.5f, 0), new Vector3(24, 25.5f, 10), new Vector3(22, 27, 18), new Vector3(15, 26.5f, 23),
            new Vector3(8, 22, 24.5f), new Vector3(2, 12, 25), new Vector3(-3, 4.5f, 25), new Vector3(-7, 3, 25), new Vector3(-7, 3, 21.4f),
            new Vector3(-14, 3.2f, 21.4f), new Vector3(-21, 4.5f, 18), new Vector3(-25, 6, 11), new Vector3(-26, 12, 2), new Vector3(-26, 6.5f, -6),
            new Vector3(-25, 4, -13), new Vector3(-22, 2.8f, -20), new Vector3(-19, 2.3f, -24)
        };
        private const int GiantLoopEntry = 13;
        private const float GiantLoopRadius = 7.5f;

        private void CreateGiantCoaster(BlockBuilding building)
        {
            var path = giantCoasterPath; int n = path.Length;
            var tangents = new Vector3[n];
            for (int i = 0; i < n; i++) tangents[i] = (path[(i + 1) % n] - path[(i - 1 + n) % n]) * .5f;
            // The track runs straight west into and out of the loop.
            tangents[GiantLoopEntry] = tangents[GiantLoopEntry + 1] = Vector3.left * 4;
            var points = new List<Vector3>(); var loop = new List<int>();
            for (int i = 0; i < n; i++)
            {
                Vector3 p0 = path[i], p1 = path[(i + 1) % n];
                if (i == GiantLoopEntry)
                {
                    const int steps = 16;
                    for (int k = 0; k < steps; k++)
                    {
                        float theta = k * Mathf.PI * 2 / steps;
                        loop.Add(points.Count);
                        points.Add(new Vector3(p0.x - GiantLoopRadius * Mathf.Sin(theta), p0.y + GiantLoopRadius * (1 - Mathf.Cos(theta)), Mathf.Lerp(p0.z, p1.z, theta / (Mathf.PI * 2))));
                    }
                    continue;
                }
                int samples = Mathf.Max(1, Mathf.RoundToInt((p1 - p0).magnitude / 3));
                for (int k = 0; k < samples; k++)
                {
                    float t = k / (float)samples, t2 = t * t, t3 = t2 * t;
                    points.Add((2 * t3 - 3 * t2 + 1) * p0 + (t3 - 2 * t2 + t) * tangents[i] + (-2 * t3 + 3 * t2) * p1 + (t3 - t2) * tangents[(i + 1) % n]);
                }
            }
            var track = points.ToArray();
            var normals = BankedNormals(track, 7);
            foreach (int index in loop)
            {
                // Inside the loop the track's up points at the loop's centre.
                var p = track[index];
                normals[index] = (new Vector3(path[GiantLoopEntry].x, path[GiantLoopEntry].y + GiantLoopRadius, p.z) - p).normalized;
            }
            var spans = CoasterTrack(building, track, normals, parkRed);
            DestructibleBlock first = null;
            for (int i = 2; i < track.Length; i += 2)
            {
                if (normals[i].y < .6f || track[i].y < 3.6f || loop.Contains(i)) continue;
                var foot = CoasterSupport(building, spans, i, track[i], normals[i]);
                if (first == null) { first = foot; ParkRewardBanner(building, foot); }
            }
            // The station platform stands beside the first straight and holds up the waiting train.
            var station = ParkPart(building, "Coaster station", new Vector3(-5, .5f, -21.6f), new Vector3(20, 1, 5), parkBlue, BuildingPart.Pillar);
            station.Foundation = true;
            ParkSkin(station, "Coaster station", null, null, parkRewardPaints[(int)building.RewardKind]);
            for (int i = 0; i < 6; i++) BlockBuilding.Connect(station, spans[i]);
            CoasterTrain(building, spans, 0, 4, parkRewardPaints[(int)building.RewardKind]);
            // The infield is a small garden with balloon lamps.
            foreach (var (x, z) in new[] { (-11f, -9f), (11f, -9f), (-11f, 10f), (11f, 10f) })
                CreateAmusementProp(building.transform.TransformPoint(new Vector3(x, 0, z)), "Tree");
            foreach (float z in new[] { -12f, 12f }) CreateAmusementProp(building.transform.TransformPoint(new Vector3(0, 0, z)), "Lamp");
        }

        private void CreateCarousel(BlockBuilding building, float width)
        {
            float radius = width * .47f;
            bool kit = amusementKit != null;
            var reward = parkRewardPaints[(int)building.RewardKind];
            var hub = ParkPart(building, "Carousel centre column", Vector3.up * 3.85f, new Vector3(1.5f, 7.2f, 1.5f), parkRed);
            ParkSkin(hub, "Carousel column", null, null, reward);
            var feet = new DestructibleBlock[4];
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(i % 2 == 0 ? -3 : 3, 0, i < 2 ? -3 : 3);
                var foot = feet[i] = ParkFoot(building, p, .5f); BlockBuilding.Connect(foot, hub);
                if (i == 0) ParkRewardBanner(building, foot);
            }
            var floors = new DestructibleBlock[16];
            for (int i = 0; i < 16; i++)
            {
                float angle = i * 22.5f;
                var point = Quaternion.Euler(0, angle, 0) * Vector3.forward * radius * .5f + Vector3.up * .32f;
                var floor = floors[i] = ParkPart(building, "Carousel circular floor slice", point, new Vector3(radius * .4f, .45f, radius), parkYellow, BuildingPart.Slab, amusementGeometry.FloorPanel);
                floor.transform.localRotation = Quaternion.Euler(0, angle, 0);
                BlockBuilding.Connect(hub, floor); BlockBuilding.Connect(feet[i / 4], floor);
            }
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30, radians = angle * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians));
                // The kit canopy has a reward-coloured and a white stripe on every panel.
                var panel = ParkPart(building, "Carousel striped canopy panel", offset * radius * .5f + Vector3.up * 6.2f,
                    new Vector3(radius * .54f, 2.6f, radius), kit ? reward : i % 2 == 0 ? parkRed : parkWhite, BuildingPart.Prop, amusementGeometry.RoofPanel);
                panel.transform.localRotation = Quaternion.Euler(0, angle, 0); BlockBuilding.Connect(hub, panel);
                if (!ParkSkin(panel, "Carousel canopy"))
                    ParkDetail(panel.transform, "Canopy golden bulb", new Vector3(0, -1.4f, radius / 2), Vector3.one * .55f, parkYellow, amusementGeometry.Sphere);
                if (kit)
                {
                    // A horse on its own golden pole under every panel, alternately raised.
                    var horse = ParkPart(building, "Carousel horse", Quaternion.Euler(0, angle + 15, 0) * Vector3.forward * radius * .58f + Vector3.up * (1.75f + (i % 2) * .25f),
                        new Vector3(.75f, 1.4f, 2), parkWhite, BuildingPart.Prop);
                    horse.transform.localRotation = Quaternion.Euler(0, angle + 15 + 90, 0);
                    ParkSkin(horse, "Carousel horse", null, parkRideColors[i % parkRideColors.Length]);
                    BlockBuilding.Connect(floors[(i * 16 / 12 + 1) % 16], horse); BlockBuilding.Connect(panel, horse);
                }
                if (i % 2 != 0) continue;
                var pole = ParkPart(building, "Carousel outer column", offset * radius * .74f + Vector3.up * 2.6f, new Vector3(.35f, 4.4f, .35f), kit ? parkYellow : parkWhite);
                ParkSkin(pole, "Tube", pole.transform.localScale);
                BlockBuilding.Connect(hub, pole); BlockBuilding.Connect(pole, panel);
                if (kit) continue;
                var seat = ParkPart(building, "Carousel toy seat", offset * radius * .55f + Vector3.up * 1.35f, new Vector3(1.9f, 1.0f, 1.1f), i % 4 == 0 ? parkBlue : parkPink, BuildingPart.Prop);
                BlockBuilding.Connect(pole, seat);
                ParkDetail(seat.transform, "Seat back", new Vector3(0, .65f, .35f), new Vector3(1.9f, .85f, .25f), parkYellow);
            }
        }

        private void CreateDropTower(BlockBuilding building)
        {
            bool kit = amusementKit != null;
            if (!kit) { CreateBoxDropTower(building); return; }
            // One lattice mast on four braced feet, with the seat ring halfway up and the star crown on top.
            var mast = new DestructibleBlock[5];
            for (int level = 0; level < 5; level++)
            {
                mast[level] = ParkPart(building, "Drop tower mast section", Vector3.up * (3.8f + level * 4), new Vector3(2.2f, 4, 2.2f), parkBlue);
                ParkSkin(mast[level], "Truss 3", mast[level].transform.localScale);
                if (level > 0) BlockBuilding.Connect(mast[level - 1], mast[level]);
            }
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(i % 2 == 0 ? -3.6f : 3.6f, 0, i < 2 ? -3.6f : 3.6f);
                var foot = ParkFoot(building, p);
                if (i == 0) ParkRewardBanner(building, foot);
                var brace = ParkBeam(building, "Drop tower base brace", new Vector3(p.x, FootTop(foot), p.z), new Vector3(p.x * .3f, 3.4f, p.z * .3f), .8f, parkBlue, true);
                BlockBuilding.Connect(foot, brace); BlockBuilding.Connect(brace, mast[0]);
            }
            var crown = ParkPart(building, "Drop tower star crown", Vector3.up * 22.8f, new Vector3(6, 2.5f, 6), parkBlue);
            ParkSkin(crown, "Drop tower crown");
            BlockBuilding.Connect(mast[4], crown);
            var carriage = ParkPart(building, "Drop tower yellow carriage", Vector3.up * 10, new Vector3(7.4f, .7f, 7.4f), parkYellow);
            ParkSkin(carriage, "Drop tower ring", null, parkRewardPaints[(int)building.RewardKind]);
            BlockBuilding.Connect(mast[1], carriage); BlockBuilding.Connect(mast[2], carriage);
        }

        private void CreateBoxDropTower(BlockBuilding building)
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

        // The ship hangs from an A-frame and is caught mid-swing, bow up.
        private void CreatePirateShip(BlockBuilding building)
        {
            var pivot = Vector3.up * 12.5f;
            var axle = ParkPart(building, "Pirate ship axle", pivot, new Vector3(.7f, 6.6f, .7f), parkYellow);
            axle.transform.localRotation = Quaternion.Euler(90, 0, 0);
            ParkSkin(axle, "Tube", axle.transform.localScale);
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(i % 2 == 0 ? -6.2f : 6.2f, 0, i < 2 ? -3.6f : 3.6f);
                var foot = ParkFoot(building, p, 1);
                var leg = ParkBeam(building, "Pirate ship A-frame", new Vector3(p.x, FootTop(foot), p.z), pivot + Vector3.forward * Mathf.Sign(p.z) * 3, .9f, parkRed, true);
                BlockBuilding.Connect(foot, leg); BlockBuilding.Connect(leg, axle);
                if (i == 0) ParkRewardBanner(building, foot);
            }
            var swing = Quaternion.Euler(0, 0, 20);
            var hull = ParkPart(building, "Pirate ship hull", pivot + swing * new Vector3(0, -9.2f, 0), new Vector3(11, 2.8f, 3.8f), parkWood, BuildingPart.Prop);
            hull.transform.localRotation = swing; hull.PreserveShape = true;
            if (!ParkSkin(hull, "Pirate ship", null, null, parkRewardPaints[(int)building.RewardKind]))
                ParkDetail(hull.transform, "Pirate ship benches", new Vector3(0, 1.6f, 0), new Vector3(9, .4f, 3), parkBlue);
            foreach (var (x, z) in new[] { (0f, -1.6f), (0f, 1.6f), (-3.4f, -1.6f), (3.4f, 1.6f) })
            {
                var arm = ParkBeam(building, "Pirate ship arm", pivot + new Vector3(0, 0, z), pivot + swing * new Vector3(x, -7.9f, z), .4f, parkYellow, false);
                BlockBuilding.Connect(axle, arm); BlockBuilding.Connect(arm, hull);
            }
        }

        private void CreateTeacups(BlockBuilding building, float width)
        {
            float radius = width * .47f;
            var hub = ParkPart(building, "Teacup teapot", Vector3.up * 2.9f, new Vector3(3.2f, 4.4f, 3.2f), parkPink);
            ParkSkin(hub, "Teapot");
            var feet = new DestructibleBlock[4];
            for (int i = 0; i < 4; i++)
            {
                var foot = feet[i] = ParkFoot(building, new Vector3(i % 2 == 0 ? -3 : 3, 0, i < 2 ? -3 : 3), .5f);
                BlockBuilding.Connect(foot, hub);
                if (i == 0) ParkRewardBanner(building, foot);
            }
            var floors = new DestructibleBlock[16];
            for (int i = 0; i < 16; i++)
            {
                float angle = i * 22.5f;
                var floor = floors[i] = ParkPart(building, "Teacup floor slice", Quaternion.Euler(0, angle, 0) * Vector3.forward * radius * .5f + Vector3.up * .32f,
                    new Vector3(radius * .4f, .45f, radius), i % 2 == 0 ? parkMint : parkWhite, BuildingPart.Slab, amusementGeometry.FloorPanel);
                floor.transform.localRotation = Quaternion.Euler(0, angle, 0);
                BlockBuilding.Connect(hub, floor); BlockBuilding.Connect(feet[i / 4], floor);
            }
            var colors = new[] { parkBlue, parkYellow, parkPurple, parkRed, parkMint, parkRewardPaints[(int)building.RewardKind] };
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60 + 11.25f;
                var cup = ParkPart(building, "Teacup", Quaternion.Euler(0, angle, 0) * Vector3.forward * radius * .62f + Vector3.up * 1.55f,
                    new Vector3(3.4f, 1.6f, 3.4f), colors[i], BuildingPart.Prop, amusementKit != null ? null : amusementGeometry.Sphere);
                cup.transform.localRotation = Quaternion.Euler(0, angle * 2.3f, 0);
                cup.PreserveShape = true;
                ParkSkin(cup, "Teacup", null, colors[i]);
                BlockBuilding.Connect(floors[(i * 16 / 6) % 16], cup);
            }
        }

        // A two-block big top: king poles, conical ends, sloped middle roof, striped walls and an entrance with curtains.
        private const float TentRadius = 10.5f, TentApex = 15.5f, TentEave = 6.5f, TentPole = 13;
        private void CreateCircusTent(BlockBuilding building)
        {
            bool kit = amusementKit != null;
            var stripes = parkRewardPaints[(int)building.RewardKind];
            float pitch = Mathf.Atan2(TentApex - TentEave, TentRadius) * Mathf.Rad2Deg;
            float slant = new Vector2(TentApex - TentEave, TentRadius).magnitude, chord = 2 * TentRadius * Mathf.Sin(15 * Mathf.Deg2Rad);
            var poles = new DestructibleBlock[2];
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -TentPole : TentPole;
                var foot = ParkFoot(building, new Vector3(x, 0, 0), 1.2f);
                poles[side] = ParkBeam(building, "Tent king pole", new Vector3(x, FootTop(foot), 0), new Vector3(x, 17, 0), .7f, parkWhite, false);
                BlockBuilding.Connect(foot, poles[side]);
                if (kit) amusementKit.Attach(poles[side].transform, "Tent pole top", Vector3.up * poles[side].transform.localScale.y / 2, Vector3.one, ParkPaints(null, null, stripes));
                if (side == 0) ParkRewardBanner(building, foot);
            }
            var corners = new DestructibleBlock[4];
            for (int i = 0; i < 4; i++) corners[i] = ParkFoot(building, new Vector3(i % 2 == 0 ? -TentPole : TentPole, 0, i < 2 ? -TentRadius : TentRadius), .8f);
            for (int side = 0; side < 2; side++)
            {
                var pole = poles[side]; float x = side == 0 ? -TentPole : TentPole;
                for (int k = 0; k < 6; k++)
                {
                    float yaw = side == 0 ? 195 + k * 30 : 15 + k * 30;
                    var direction = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                    var roof = ParkPart(building, "Tent roof panel", new Vector3(x, 0, 0) + direction * TentRadius / 2 + Vector3.up * (TentApex + TentEave) / 2,
                        new Vector3(chord, .6f, slant), k % 2 == 0 ? stripes : parkWhite, BuildingPart.Prop);
                    roof.transform.localRotation = Quaternion.Euler(pitch, yaw, 0);
                    if (kit) { roof.GetComponent<Renderer>().sharedMaterial = stripes; ParkSkin(roof, "Tent roof wedge"); }
                    BlockBuilding.Connect(pole, roof);
                    var wall = ParkPart(building, "Tent wall", new Vector3(x, 0, 0) + direction * TentRadius * Mathf.Cos(15 * Mathf.Deg2Rad) + Vector3.up * TentEave / 2,
                        new Vector3(chord, TentEave, .5f), k % 2 == 0 ? parkRed : parkYellow, BuildingPart.Wall);
                    wall.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                    if (kit) { wall.GetComponent<Renderer>().sharedMaterial = stripes; ParkSkin(wall, "Tent wall arc"); }
                    BlockBuilding.Connect(roof, wall);
                    BlockBuilding.Connect(corners[side == 0 ? (k < 3 ? 0 : 2) : (k < 3 ? 3 : 1)], wall);
                }
            }
            var front = new DestructibleBlock[2];
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -TentPole / 2 : TentPole / 2, z = i < 2 ? -1 : 1;
                var roof = ParkPart(building, "Tent roof panel", new Vector3(x, (TentApex + TentEave) / 2, z * TentRadius / 2), new Vector3(TentPole, .6f, slant), stripes, BuildingPart.Prop);
                roof.transform.localRotation = Quaternion.Euler(pitch, z > 0 ? 0 : 180, 0);
                ParkSkin(roof, "Tent roof side");
                BlockBuilding.Connect(poles[i % 2], roof);
                var wall = ParkPart(building, "Tent wall", new Vector3(x, TentEave / 2, z * TentRadius), new Vector3(TentPole, TentEave, .5f), parkRed, BuildingPart.Wall);
                wall.transform.localRotation = Quaternion.Euler(0, z > 0 ? 0 : 180, 0);
                if (kit) { wall.GetComponent<Renderer>().sharedMaterial = stripes; ParkSkin(wall, "Tent wall"); }
                BlockBuilding.Connect(roof, wall); BlockBuilding.Connect(corners[i], wall);
                if (z < 0) front[i % 2] = roof;
            }
            var entrance = ParkPart(building, "Tent entrance", new Vector3(0, 3, -TentRadius - 1), new Vector3(5.6f, 6, 2), parkYellow, BuildingPart.Prop);
            entrance.transform.localRotation = Quaternion.Euler(0, 180, 0);
            ParkSkin(entrance, "Tent entrance", null, null, stripes);
            BlockBuilding.Connect(front[0], entrance); BlockBuilding.Connect(front[1], entrance);
        }

        private void CreateParkBooth(BlockBuilding building, float width, bool snack)
        {
            float w = width * .65f, depth = width * .38f;
            var accent = parkRewardPaints[(int)building.RewardKind];
            bool kit = amusementKit != null;
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
                if (kit)
                {
                    // A striped awning over the window and a 3D sign above the door line.
                    amusementKit.Attach(wall.transform, "Booth awning", new Vector3(w * .12f, 1.45f, -.16f), new Vector3(w * .5f / 6, 1, 1), ParkPaints(null, null, accent));
                    amusementKit.Attach(wall.transform, snack ? "Burger sign" : "Ticket sign", new Vector3(0, 2.75f, -.5f), Vector3.one, ParkPaints(null, null, accent));
                    continue;
                }
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
                var color = i < 2 ? parkPink : parkBlue;
                if (amusementKit != null && amusementKit.Has("Gate tower"))
                {
                    amusementKit.Attach(feet[i].transform, "Gate tower", Vector3.up * feet[i].transform.localScale.y / 2, Vector3.one, ParkPaints(color, null, i < 2 ? parkBlue : parkPink));
                    continue;
                }
                ParkDetail(feet[i].transform, "Gate colored tower", new Vector3(0, 0, 0), new Vector3(2.3f, 8.3f, 2.3f), color);
                ParkDetail(feet[i].transform, "Gate tower crown", new Vector3(0, 4.6f, 0), new Vector3(2.9f, .6f, 2.9f), parkYellow);
                ParkDetail(feet[i].transform, "Gate pennant", new Vector3(.75f, 5.8f, 0), new Vector3(2.5f, .9f, .15f), parkPink);
            }
            DestructibleBlock previous = null, first = null;
            for (int i = 0; i < 12; i++)
            {
                float a = -Mathf.PI / 2 + i * Mathf.PI / 12, b = a + Mathf.PI / 12;
                var left = new Vector3(Mathf.Sin(a) * radius, 8.8f + Mathf.Cos(a) * 5, 0);
                var right = new Vector3(Mathf.Sin(b) * radius, 8.8f + Mathf.Cos(b) * 5, 0);
                var arch = ParkBeam(building, "Entrance arch segment", left, right, 1.2f, parkBlue, true);
                if (previous != null) BlockBuilding.Connect(previous, arch); else first = arch;
                previous = arch;
                if (i == 5)
                {
                    var star = ParkPart(building, "Entrance golden star", new Vector3(0, 13.7f, -.85f), new Vector3(2.5f, 2.5f, .25f), parkYellow, BuildingPart.Prop, amusementGeometry.Star);
                    ParkSkin(star, "Gate star");
                    BlockBuilding.Connect(arch, star);
                }
            }
            BlockBuilding.Connect(feet[0], first); BlockBuilding.Connect(feet[1], first);
            BlockBuilding.Connect(feet[2], previous); BlockBuilding.Connect(feet[3], previous);
            CreateBuildingReward(building);
        }

        // Low fences along the front of the lot, in 5.6 m sections either side of its entrance.
        private void CreateAmusementFences(BlockBuilding building, float length, float depth)
        {
            int sections = Mathf.Max(1, Mathf.RoundToInt(length * .2f / 5.6f));
            foreach (int side in new[] { -1, 1 })
                for (int k = 0; k < sections; k++)
                {
                    float x = side * (length * .38f + (k - (sections - 1) * .5f) * 5.6f);
                    var point = building.transform.TransformPoint(new Vector3(x, 0, -depth / 2 - .8f));
                    var rail = Box("Park low fence", GroundPoint(point, 1.1f), new Vector3(5.6f, .18f, .18f), dark);
                    rail.transform.rotation = building.transform.rotation;
                    AddBlock(rail, true);
                    if (amusementKit != null && amusementKit.Has("Park fence"))
                    {
                        Destroy(rail.GetComponent<MeshRenderer>());
                        amusementKit.Attach(rail.transform, "Park fence");
                        continue;
                    }
                    ParkDetail(rail.transform, "Fence lower rail", Vector3.down * .55f, new Vector3(5.6f, .15f, .15f), dark);
                    for (int i = -1; i <= 1; i++) ParkDetail(rail.transform, "Fence upright", new Vector3(i * 2.7f, -.45f, 0), new Vector3(.20f, 1.1f, .20f), dark);
                }
        }

        private void CreateAmusementProp(Vector3 position, string label)
        {
            bool kit = amusementKit != null;
            if (label == "Tree")
            {
                var pot = Box("Park planter tree", GroundPoint(position, .5f), new Vector3(3, 1, 3), lime); AddBlock(pot, true);
                if (kit && amusementKit.Has("Planter tree")) { Destroy(pot.GetComponent<MeshRenderer>()); amusementKit.Attach(pot.transform, "Planter tree"); return; }
                ParkDetail(pot.transform, "Tree trunk", Vector3.up * 1.4f, new Vector3(.55f, 2.1f, .55f), wood);
                ParkDetail(pot.transform, "Round park foliage", Vector3.up * 3.5f, new Vector3(4.2f, 4, 4.2f), grass, amusementGeometry.Sphere); return;
            }
            var post = Box("Balloon street lamp", GroundPoint(position, 2.8f), new Vector3(.50f, 5.6f, .5f), dark); AddBlock(post, true);
            if (kit && amusementKit.Has("Balloon lamp")) { Destroy(post.GetComponent<MeshRenderer>()); amusementKit.Attach(post.transform, "Balloon lamp", Vector3.down * 2.8f, Vector3.one); return; }
            ParkDetail(post.transform, "Park hanging banner", new Vector3(.95f, 1, 0), new Vector3(1.8f, 2.5f, .18f), parkPink);
            for (int i = 0; i < 3; i++) ParkDetail(post.transform, "Park balloon", new Vector3((i - 1) * 1.05f, 3.0f + (i == 1 ? .6f : 0), 0), new Vector3(1.9f, 2.2f, 1.9f), i == 0 ? parkYellow : i == 1 ? parkBlue : parkPink, amusementGeometry.Sphere);
        }
    }
}
