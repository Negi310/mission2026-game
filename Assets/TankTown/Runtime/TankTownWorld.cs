using System.Collections.Generic;
using UnityEngine;

namespace TankTown
{
    public enum ToySurface { Paint, Concrete, Metal, Rubber, Glass, Organic, Wood }

    public sealed partial class TankTownWorld : MonoBehaviour
    {
        public Material ShotMaterial { get; private set; }
        public UpgradeIcons Icons { get; private set; }
        public ItemIcons SpecialIcons { get; private set; }
        public float HalfExtent { get; private set; }
        public float RoadWidth => Mathf.Clamp(game.Settings.roadWidth, 8, game.Settings.blockSpacing - 14);
        public float BuildingWidth => Mathf.Clamp(game.Settings.buildingWidth, 10, game.Settings.blockSpacing - RoadWidth - 4);
        public Vector3 SpawnPosition => GroundPoint(new Vector3(0, 0, -HalfExtent + 20), game.Settings.rideHeight);
        public TownTerrain Terrain { get; private set; }
        public TownLayout Layout { get; private set; }
        public int ParkCount { get; private set; }
        public Vector3 GroundPoint(Vector3 p, float lift = 0) { p.y = Terrain.HeightAt(p) + lift; return p; }
        public readonly List<BlockBuilding> Buildings = new List<BlockBuilding>();
        public readonly List<TankPickup> Pickups = new List<TankPickup>();
        public readonly List<ItemPickup> ItemPickups = new List<ItemPickup>();
        public readonly List<ChainExplosive> Explosives = new List<ChainExplosive>();
        public readonly List<TownTrafficCar> TrafficCars = new List<TownTrafficCar>();
        public readonly List<TownPedestrian> Pedestrians = new List<TownPedestrian>();
        public readonly List<TownMovingTarget> MovingTargets = new List<TownMovingTarget>();
        public readonly List<TownTrafficSignal> Signals = new List<TownTrafficSignal>();
        public Material SkyMaterial { get; private set; }
        private readonly List<Material> materials = new List<Material>();
        private readonly Dictionary<Color, Material> carPaints = new Dictionary<Color, Material>();
        private readonly ToyGeometry geometry = new ToyGeometry();
        private Material[] cannonShellMaterials;
        private TankTownGame game;
        private TownTrafficGrid trafficGrid;
        private Mesh proxyCube;
        private readonly List<Mesh> vehicleMeshes = new List<Mesh>();
        private float presentAt;
        internal void UpdatePresentation(Vector3 view)
        {
            bool updateLod = Time.unscaledTime >= presentAt;
            if (updateLod) presentAt = Time.unscaledTime + .10f;
            foreach (var building in Buildings)
                if (updateLod || building.Presentation?.NeedsDetailRefresh == true) building.Presentation?.Tick(view);
        }
        internal void RefreshMapMeshes()
        {
            foreach (var building in Buildings) building.Presentation?.RefreshMapMesh();
        }
        internal bool TrafficShouldStop(TownTrafficCar car, Vector3 position, Vector3 heading)
            => (trafficGrid ?? (trafficGrid = new TownTrafficGrid(this))).ShouldStop(car, position, heading);
        private Material road, cream, dark, glass, lime, grass, frame, rubber, wood, cannonPaint;
        private readonly Color[] palette = { new Color(1, .26f, .18f), new Color(.04f, .78f, 1), new Color(1, .68f, .04f), new Color(.62f, .27f, 1), new Color(1, .27f, .62f) };

        public Material MakeMaterial(Color color, ToySurface surface = ToySurface.Paint)
        {
            var template = Resources.Load<Material>("TankTownSurface");
            var material = template != null ? new Material(template) : new Material(Shader.Find("TankTown/Saturated Toon"));
            material.shader = Shader.Find("TankTown/Saturated Toon");
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Saturation", 1.05f);
            float detail = .035f, scale = 24, gloss = .26f, sharpness = 48, brushed = 0;
            switch (surface)
            {
                case ToySurface.Concrete: detail = .085f; scale = 12; gloss = .065f; sharpness = 18; break;
                case ToySurface.Metal: detail = .045f; scale = 32; gloss = .3f; sharpness = 72; brushed = .3f; break;
                case ToySurface.Rubber: detail = .09f; scale = 20; gloss = .07f; sharpness = 20; break;
                case ToySurface.Glass: detail = .01f; scale = 28; gloss = .42f; sharpness = 90; break;
                case ToySurface.Organic: detail = .06f; scale = 12; gloss = .12f; sharpness = 24; break;
                case ToySurface.Wood: detail = .065f; scale = 18; gloss = .08f; sharpness = 20; brushed = .65f; break;
            }
            material.SetFloat("_SurfaceDetail", detail);
            material.SetFloat("_DetailScale", scale);
            material.SetFloat("_Gloss", gloss);
            material.SetFloat("_HighlightPower", sharpness);
            material.SetFloat("_Brushed", brushed);
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
            if (Mathf.Min(size.x, Mathf.Min(size.y, size.z)) >= .06f)
                item.GetComponent<MeshFilter>().sharedMesh = geometry.RoundedBox(size, label == "Hull" || label == "Upper hull" || label == "Turret");
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) { item.GetComponent<Collider>().enabled = false; Destroy(item.GetComponent<Collider>()); }
            return item;
        }

        public void Build(TankTownGame owner)
        {
            game = owner;
            HalfExtent = game.Settings.cityHalfBlocks * game.Settings.blockSpacing + 14 * game.StageScale;
            road = MakeMaterial(new Color(.13f, .2f, .32f), ToySurface.Concrete);
            road.SetFloat("_GroundDetail", 1);
            road.SetFloat("_Gloss", .035f);
            cream = MakeMaterial(new Color(1, .94f, .77f), ToySurface.Concrete);
            dark = MakeMaterial(new Color(.055f, .09f, .16f), ToySurface.Metal);
            glass = MakeMaterial(new Color(.04f, .34f, .59f), ToySurface.Glass);
            lime = MakeMaterial(new Color(.64f, 1, .04f));
            grass = MakeMaterial(new Color(.12f, .84f, .42f), ToySurface.Organic);
            frame = MakeMaterial(new Color(.85f, .91f, 1), ToySurface.Concrete);
            rubber = MakeMaterial(new Color(.12f, .16f, .19f), ToySurface.Rubber);
            wood = MakeMaterial(new Color(.3f, .19f, .105f), ToySurface.Wood);
            cannonPaint = MakeMaterial(new Color(1, .94f, .77f));
            ShotMaterial = MakeMaterial(new Color(1, .83f, .07f));
            cannonShellMaterials = new[]
            {
                MakeMaterial(new Color(.32f, .44f, .18f), ToySurface.Metal),
                MakeMaterial(new Color(.82f, .89f, .95f), ToySurface.Metal),
                MakeMaterial(new Color(1, .68f, .10f), ToySurface.Metal)
            };
            if (CandyTheme) InitializeCandyPalette();
            Icons = new UpgradeIcons();
            SpecialIcons = new ItemIcons();
            Layout = new TownLayout(game.Settings, game.CitySeed, RoadWidth);
            Terrain = new TownTerrain(game.Settings, transform, RoadWidth, Layout);
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
                    SurfaceBox("Lane dash", new Vector3(lane, 0, d), new Vector3(.32f, .02f, 2.1f), cream);
                    SurfaceBox("Lane dash", new Vector3(d, 0, lane), new Vector3(2.1f, .02f, .32f), cream);
                }
            }
            var buildingPaints = new Material[6];
            for (int i = 0; i < 6; i++) buildingPaints[i] = MakeMaterial(ItemIcons.Color((ItemKind)i), ToySurface.Concrete);
            if (CandyTheme) candyLollipops = buildingPaints;
            var random = new System.Random(unchecked(game.CitySeed ^ 0x4D190));
            foreach (var plot in Layout.Plots)
                {
                    int x = plot.X, z = plot.Z;
                    var center = GroundPoint(new Vector3((x + .5f) * spacing, 0, (z + .5f) * spacing));
                    if (plot.Park) CreatePark(center);
                    else CreateBuilding(GroundPoint(plot.Center), plot.Floors, buildingPaints[(int)plot.Reward], plot.Width, plot.Style, plot.Reward);
                    float sidewalk = BuildingWidth / 2 + 1.5f;
                    CreateProp(center + new Vector3(-sidewalk, 0, -sidewalk), "Street sign", ShotMaterial, true);
                    CreateProp(center + new Vector3(sidewalk, 0, sidewalk), "Tree", grass, false);
                    CreateProp(center + new Vector3(-sidewalk, 0, sidewalk), "Street lamp", cream, true);
                    var crate = Box("Crushable crate", GroundPoint(center + new Vector3(sidewalk, 0, -sidewalk), .6f), Vector3.one * 1.2f, lime);
                    AddBlock(crate, true);
                    var pickupPosition = new Vector3(x * spacing, 1.2f, (z + .2f + (float)random.NextDouble() * .6f) * spacing);
                    CreatePickup(pickupPosition, (UpgradeKind)random.Next(4));
                    CreatePickup(new Vector3((x + .2f + (float)random.NextDouble() * .6f) * spacing, 1.2f, z * spacing), (UpgradeKind)random.Next(4));
                }
            // The first straight has all four upgrades, so their effect is easy to try.
            for (int i = 0; i < 4; i++) CreatePickup(SpawnPosition + new Vector3(0, .2f, 8 + i * 7), (UpgradeKind)i, false);
            if (CandyTheme) CreateDoughnutGate();
            CreateTraffic();
            var scenery = gameObject.AddComponent<TownScenery>();
            SkyMaterial = scenery.Build(this, game);
            materials.Add(SkyMaterial);
            // The readable primitive is shared by all distant building combinations.
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            proxyCube = Instantiate(source.GetComponent<MeshFilter>().sharedMesh); proxyCube.name = "Proxy cube";
            source.SetActive(false); Destroy(source);
            foreach (var building in Buildings) building.Presentation = new TownBuildingVisual(building, proxyCube);
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.gameObject.name != "Town paving" && renderer.gameObject.name != "North south ramp avenue" && renderer.gameObject.name != "East west ramp avenue") continue;
                var copy = new GameObject("Minimap " + renderer.name, typeof(MeshFilter), typeof(MeshRenderer)); copy.transform.SetParent(transform, false);
                copy.layer = TownPhysics.MapLayer;
                copy.GetComponent<MeshFilter>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var picture = copy.GetComponent<MeshRenderer>(); picture.sharedMaterial = renderer.sharedMaterial;
                picture.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; picture.receiveShadows = false;
            }
            foreach (var plot in Layout.Plots)
                if (plot.Park)
                {
                    var park = Box("Minimap park", GroundPoint(plot.Center, .09f), new Vector3(BuildingWidth, .1f, BuildingWidth), grass, solid: false);
                    park.layer = TownPhysics.MapLayer;
                }
            BatchStaticSurfaces();
        }

        private void CreateTraffic()
        {
            int half = game.Settings.cityHalfBlocks;
            float spacing = game.Settings.blockSpacing, shoulder = RoadWidth / 2 + 1.2f;
            for (int x = -half; x <= half; x++) for (int z = -half; z <= half; z++)
            {
                if ((x + z + half * 2) % 3 == 0)
                {
                    var junction = new Vector3(x * spacing, 0, z * spacing);
                    var root = new GameObject("Crushable traffic signal"); root.transform.SetParent(transform);
                    root.transform.position = GroundPoint(junction + new Vector3(shoulder, 0, shoulder));
                    var collider = root.AddComponent<BoxCollider>(); collider.center = Vector3.up * 2; collider.size = new Vector3(.4f, 4, .4f);
                    AddBlock(root, true);
                    Box("Signal post", Vector3.up * 2, collider.size, dark, root.transform, false);
                    Box("Signal arm", new Vector3(-.8f, 3.9f, 0), new Vector3(2, .18f, .2f), cream, root.transform, false);
                    Box("Signal housing", new Vector3(-1.4f, 3.6f, 0), new Vector3(.75f, 1.8f, .6f), dark, root.transform, false);
                    Box("Cross signal arm", new Vector3(0, 3.9f, -.8f), new Vector3(.2f, .18f, 2), cream, root.transform, false);
                    Box("Cross signal housing", new Vector3(0, 3.6f, -1.4f), new Vector3(.6f, 1.8f, .75f), dark, root.transform, false);
                    var head = root.AddComponent<BoxCollider>(); head.center = new Vector3(-1.4f, 3.6f, 0); head.size = new Vector3(.75f, 1.8f, .6f);
                    var crossHead = root.AddComponent<BoxCollider>(); crossHead.center = new Vector3(0, 3.6f, -1.4f); crossHead.size = new Vector3(.6f, 1.8f, .75f);
                    var lights = new Renderer[6];
                    for (int i = 0; i < 6; i++)
                    {
                        var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere); bulb.name = "Traffic light"; bulb.transform.SetParent(root.transform, false);
                        bulb.transform.localPosition = i < 3 ? new Vector3(-1.4f, 4.12f - i * .52f, -.34f) : new Vector3(-.34f, 4.12f - (i - 3) * .52f, -1.4f); bulb.transform.localScale = Vector3.one * .38f;
                        bulb.GetComponent<Collider>().enabled = false; Destroy(bulb.GetComponent<Collider>());
                        lights[i] = bulb.GetComponent<Renderer>(); lights[i].sharedMaterial = cream;
                    }
                    var signal = root.AddComponent<TownTrafficSignal>(); signal.Initialize(game, junction, lights, (x - z + half * 2) * .75f); Signals.Add(signal);
                }
                if (x == half || z == half || (x + z) % 2 != 0) continue;
                var point = GroundPoint(new Vector3(x * spacing + shoulder, 0, (z + .5f) * spacing));
                var pole = CreatePowerPole(point);
                var other = CreatePowerPole(GroundPoint(point + Vector3.forward * 12));
                var span = new GameObject("Power line span"); span.transform.SetParent(transform);
                var wire = span.AddComponent<LineRenderer>(); wire.sharedMaterial = dark; wire.startWidth = wire.endWidth = .055f;
                wire.useWorldSpace = true; wire.positionCount = 13;
                for (int i = 0; i < 13; i++) { float t = i / 12f; wire.SetPosition(i, Vector3.Lerp(pole.transform.position, other.transform.position, t) + Vector3.up * (6 - Mathf.Sin(t * Mathf.PI) * 1.1f)); }
                span.AddComponent<TownPowerLine>().Initialize(pole, other);
            }
            for (int ring = 1; ring <= half; ring++)
            {
                float extent = ring * spacing + 3.6f, radius = 6;
                // One circuit passes through the starting avenue, so traffic is
                // visible during ordinary play instead of only around the outskirts.
                float right = ring == 1 ? 3.6f : extent;
                float left = -extent;
                float south = ring == 1 ? -half * spacing - 3.6f : -extent;
                var route = new Vector3[36];
                for (int corner = 0; corner < 4; corner++)
                {
                    var center = new Vector3(corner == 0 || corner == 3 ? right - radius : left + radius, 0, corner < 2 ? extent - radius : south + radius);
                    for (int n = 0; n <= 8; n++)
                    {
                        float angle = (corner * 90 + n * 11.25f) * Mathf.Deg2Rad;
                        route[corner * 9 + n] = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                    }
                }
                int count = game.Settings.trafficCarsPerCircuit;
                for (int i = 0; i < count; i++) CreateCar(route, Mathf.Repeat(i / (float)count + ring * .017f, 1), 8 + (i + ring) % 4, palette[(i + ring) % palette.Length]);
            }
            CreatePedestrians();
        }

        private DestructibleBlock CreatePowerPole(Vector3 point)
        {
            var pole = new GameObject("Crushable power pole"); pole.transform.SetParent(transform); pole.transform.position = point;
            var post = pole.AddComponent<BoxCollider>(); post.center = Vector3.up * 3.2f; post.size = new Vector3(.45f, 6.4f, .45f);
            AddBlock(pole, true); Box("Power pole post", post.center, post.size, cream, pole.transform, false);
            Box("Power cross arm", new Vector3(0, 5.7f, 0), new Vector3(2.8f, .3f, .3f), dark, pole.transform, false);
            for (int i = -1; i <= 1; i++) Box("Insulator", new Vector3(i * 1.1f, 6, 0), new Vector3(.35f, .5f, .35f), glass, pole.transform, false);
            return pole.GetComponent<DestructibleBlock>();
        }

        private void CreatePedestrians()
        {
            var shirts = new Material[palette.Length];
            for (int i = 0; i < shirts.Length; i++) shirts[i] = MakeMaterial(palette[i]);
            var skins = new[] { MakeMaterial(new Color(1, .76f, .51f)), MakeMaterial(new Color(.68f, .4f, .25f)), MakeMaterial(new Color(.94f, .6f, .39f)) };
            int half = game.Settings.cityHalfBlocks, variant = 0; float spacing = game.Settings.blockSpacing, sidewalk = RoadWidth / 2 + 1.1f;
            for (int x = -half; x < half; x++) for (int z = -half; z < half; z++)
            {
                float left = x * spacing + sidewalk, right = (x + 1) * spacing - sidewalk;
                float south = z * spacing + sidewalk, north = (z + 1) * spacing - sidewalk;
                var route = new[] { new Vector3(left, 0, south), new Vector3(left, 0, north), new Vector3(right, 0, north), new Vector3(right, 0, south) };
                for (int i = 0; i < game.Settings.pedestriansPerBlock; i++, variant++)
                {
                    var root = new GameObject("City pedestrian " + variant); root.transform.SetParent(transform);
                    var person = root.AddComponent<TownPedestrian>();
                    person.Initialize(game, route, i / (float)game.Settings.pedestriansPerBlock, variant, shirts[variant % shirts.Length], skins[variant % skins.Length], dark, geometry.LowPolyHead());
                    Pedestrians.Add(person);
                }
            }
        }

        private void BatchStaticSurfaces()
        {
            var fixedObjects = new List<GameObject>();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.name == "Lane dash") renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (renderer.gameObject.layer == TownPhysics.MapLayer || renderer.GetComponentInParent<DestructibleBlock>() != null ||
                    renderer.GetComponentInParent<BlockBuilding>() != null || renderer.GetComponentInParent<TankPickup>() != null ||
                    renderer.GetComponentInParent<ItemPickup>() != null || renderer.GetComponentInParent<TownPedestrian>() != null ||
                    renderer.name == "Distant island" || renderer.transform.parent?.name == "Soft toy cloud") continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null && filter.sharedMesh.isReadable) fixedObjects.Add(renderer.gameObject);
            }
            StaticBatchingUtility.Combine(fixedObjects.ToArray(), gameObject);
        }

        private void CreateCar(Vector3[] route, float phase, float speed, Color color)
        {
            var root = new GameObject("Explosive city car"); root.transform.SetParent(transform);
            var collider = root.AddComponent<BoxCollider>(); collider.center = Vector3.up * .25f; collider.size = new Vector3(1.8f, 2, 3.3f);
            if (!carPaints.TryGetValue(color, out var bodyColor)) { bodyColor = MakeMaterial(color); carPaints.Add(color, bodyColor); }
            Box("Car body", Vector3.zero, new Vector3(1.7f, .55f, 3.2f), bodyColor, root.transform, false);
            Box("Car cabin", new Vector3(0, .75f, -.15f), new Vector3(1.4f, 1.05f, 1.65f), bodyColor, root.transform, false);
            Box("Front windshield", new Vector3(0, .75f, .685f), new Vector3(1.22f, .65f, .035f), glass, root.transform, false);
            Box("Rear windshield", new Vector3(0, .75f, -.985f), new Vector3(1.22f, .65f, .035f), glass, root.transform, false);
            foreach (int side in new[] { -1, 1 })
            {
                Box("Car side windows", new Vector3(side * .71f, .75f, -.15f), new Vector3(.035f, .65f, 1.38f), glass, root.transform, false);
                Box("Headlight", new Vector3(side * .54f, -.01f, 1.62f), new Vector3(.35f, .19f, .05f), cream, root.transform, false);
                Box("Tail light", new Vector3(side * .54f, -.01f, -1.62f), new Vector3(.35f, .19f, .05f), ShotMaterial, root.transform, false);
                foreach (int end in new[] { -1, 1 })
                {
                    var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder); wheel.name = "Car wheel"; wheel.transform.SetParent(root.transform, false);
                    wheel.transform.localPosition = new Vector3(side * .86f, -.35f, end * 1); wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    wheel.transform.localScale = new Vector3(.65f, .13f, .65f); wheel.GetComponent<Renderer>().sharedMaterial = rubber;
                    wheel.GetComponent<Collider>().enabled = false; Destroy(wheel.GetComponent<Collider>());
                }
            }
            AddBlock(root, true);
            var explosive = root.AddComponent<ChainExplosive>(); explosive.Initialize(game, game.Settings.carExplosionRadius, vehicle: true); Explosives.Add(explosive);
            var traffic = root.AddComponent<TownTrafficCar>(); traffic.Initialize(game, route, phase, speed); TrafficCars.Add(traffic);
            // Only the car root moves, so retain the complete model in a material batch.
            var groups = new Dictionary<Material, List<UnityEngine.CombineInstance>>();
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (!groups.TryGetValue(renderer.sharedMaterial, out var list)) groups[renderer.sharedMaterial] = list = new List<UnityEngine.CombineInstance>();
                list.Add(new UnityEngine.CombineInstance { mesh = filter.sharedMesh, transform = root.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix });
                renderer.enabled = false;
            }
            var mesh = TownBuildingVisual.Combine(groups, "Batched traffic car", out var paints); vehicleMeshes.Add(mesh);
            var visual = new GameObject("Batched traffic car", typeof(MeshFilter), typeof(MeshRenderer)); visual.transform.SetParent(root.transform, false);
            visual.GetComponent<MeshFilter>().sharedMesh = mesh; visual.GetComponent<MeshRenderer>().sharedMaterials = paints;
        }

        private void SurfaceBox(string label, Vector3 p, Vector3 size, Material material)
        {
            var item = Box(label, GroundPoint(p, .08f), size, material, solid: false);
            item.transform.rotation = Quaternion.FromToRotation(Vector3.up, Terrain.NormalAt(p));
        }
        private void CreatePark(Vector3 center)
        {
            if (CandyTheme) { CreateCandyPark(center); return; }
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

        private void CreatePickup(Vector3 position, UpgradeKind kind, bool reserveOpeningStraight = true)
        {
            // Reserve the opening straight for its four tutorial pickups.
            if (reserveOpeningStraight && Mathf.Abs(position.x) < 4 && position.z > SpawnPosition.z - 8 && position.z < SpawnPosition.z + 40)
                position.x = RoadWidth * .32f;
            position = GroundPoint(position, 1.2f);
            var item = new GameObject(TankStats.Label(kind) + " UP");
            item.transform.SetParent(transform, false);
            item.transform.position = position;
            var pickup = item.AddComponent<TankPickup>();
            pickup.Initialize(game, kind, Icons.Material(kind));
            Pickups.Add(pickup);
        }

        private void CreateBuildingReward(BlockBuilding building)
        {
            var kind = building.RewardKind;
            var item = new GameObject(ItemIcons.Label(kind));
            item.transform.SetParent(transform, false);
            // Remain hidden until every structural part is detached, then mark the plot centre.
            item.transform.position = GroundPoint(building.transform.position, .75f);
            var pickup = item.AddComponent<ItemPickup>();
            pickup.Initialize(game, kind, SpecialIcons.Material(kind), building);
            building.Reward = pickup;
            ItemPickups.Add(pickup);
        }

        private void CreateExplosiveCluster(BlockBuilding building)
        {
            float side = building.transform.position.x < 0 ? 1 : -1;
            var start = building.transform.position + new Vector3(side * (building.Width / 2 + 1.5f), 0, -building.Width * .35f);
            var red = MakeMaterial(new Color(1, .11f, .025f));
            for (int i = 0; i < 4; i++)
            {
                bool tank = i == 3;
                var point = GroundPoint(start + Vector3.forward * i * 3.5f, tank ? 1.1f : .95f);
                var root = new GameObject(tank ? "Explosive fuel tank" : "Explosive red barrel");
                root.transform.SetParent(transform); root.transform.position = point;
                var collider = root.AddComponent<BoxCollider>();
                collider.size = tank ? new Vector3(2.2f, 2.2f, 2.8f) : new Vector3(1.3f, 1.9f, 1.3f);
                var shape = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                shape.transform.SetParent(root.transform, false);
                shape.transform.localScale = tank ? new Vector3(2.1f, 1.4f, 2.1f) : new Vector3(1.3f, .95f, 1.3f);
                if (tank) shape.transform.localRotation = Quaternion.Euler(90, 0, 0);
                shape.GetComponent<Renderer>().sharedMaterial = red;
                shape.GetComponent<Collider>().enabled = false; Destroy(shape.GetComponent<Collider>());
                foreach (float y in new[] { -.48f, .48f })
                    Box("Warning band", new Vector3(0, y, -.68f), new Vector3(tank ? 1.7f : 1.32f, .22f, .12f), ShotMaterial, root.transform, false);
                Box("Warning mark", new Vector3(0, 0, -.72f), new Vector3(.2f, .5f, .12f), cream, root.transform, false);
                Box("Warning dot", new Vector3(0, -.36f, -.72f), new Vector3(.2f, .14f, .12f), cream, root.transform, false);
                AddBlock(root, true);
                var explosive = root.AddComponent<ChainExplosive>();
                explosive.Initialize(game, tank ? game.Settings.fuelExplosionRadius : 8.5f, tank);
                Explosives.Add(explosive);
            }
        }

        private DestructibleBlock AddBlock(GameObject item, bool crushable)
        {
            var block = item.AddComponent<DestructibleBlock>();
            block.CanCrush = crushable;
            block.Kind = BuildingPart.Prop;
            block.Initialize(game);
            return block;
        }

        private void CreateBuilding(Vector3 center, int floors, Material color, float width, int style, ItemKind reward)
        {
            var root = new GameObject(CandyTheme ? (style == 0 ? "Shortcake tower" : style == 1 ? "Chocolate house" : "Biscuit house") : style == 0 ? "Tower frame" : style == 1 ? "Apartment frame" : "Shop frame");
            root.transform.SetParent(transform);
            root.transform.position = center;
            var building = root.AddComponent<BlockBuilding>();
            building.Initialize(game);
            building.Width = width; building.Floors = floors;
            building.Style = style; building.RewardKind = reward; building.FacadeColor = ItemIcons.Color(reward);
            Buildings.Add(building);
            float pillar = Mathf.Clamp(game.Settings.pillarWidth, .6f, width / 5);
            float height = game.Settings.floorHeight, h = (width - pillar) / 2;
            var corners = new[] { new Vector3(-h, 0, -h), new Vector3(h, 0, -h), new Vector3(h, 0, h), new Vector3(-h, 0, h) };
            var previous = new DestructibleBlock[4];
            var previousSlabs = new DestructibleBlock[4];
            for (int level = 0; level < floors; level++)
            {
                float levelWidth = CandyTheme && style == 0 ? width * (1 - level / 2 * .11f) : width;
                h = (levelWidth - pillar) / 2;
                corners[0] = new Vector3(-h, 0, -h); corners[1] = new Vector3(h, 0, -h);
                corners[2] = new Vector3(h, 0, h); corners[3] = new Vector3(-h, 0, h);
                float y = level * height;
                var pillars = new DestructibleBlock[4];
                var beams = new DestructibleBlock[4];
                var slabs = new DestructibleBlock[4];
                for (int i = 0; i < 4; i++)
                {
                    pillars[i] = Part("Pillar", corners[i] + Vector3.up * (y + height / 2), new Vector3(pillar, height - .12f, pillar), CandyTheme ? candyWafer : frame, BuildingPart.Pillar, level, i, building);
                    pillars[i].Foundation = level == 0;
                    if (level > 0) BlockBuilding.Connect(pillars[i], CandyTheme && style == 0 ? previousSlabs[i] : previous[i]);
                    var middle = (corners[i] + corners[(i + 1) % 4]) / 2;
                    beams[i] = Part("Beam", middle + Vector3.up * (y + height - .25f), i % 2 == 0 ? new Vector3(levelWidth - pillar, .5f, .65f) : new Vector3(.65f, .5f, levelWidth - pillar), CandyTheme ? color : frame, BuildingPart.Beam, level, i, building);
                    float q = (levelWidth - pillar) / 4;
                    slabs[i] = Part("Floor slab", new Vector3(Mathf.Sign(corners[i].x) * q, y + height - .12f, Mathf.Sign(corners[i].z) * q), new Vector3(q * 2 - .05f, .24f, q * 2 - .05f), CandyTheme ? candyCream : level == floors - 1 ? cream : frame, BuildingPart.Slab, level, i, building);
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
                        var wall = Part("Facade panel", rotation * new Vector3(panel * (levelWidth - 1.6f) / 4, 0, levelWidth / 2 - .2f) + Vector3.up * (y + height / 2 - .12f), new Vector3((levelWidth - 1.6f) / 2 - .08f, height - .65f, .28f), CandyTheme ? CandyWallMaterial(style) : color, BuildingPart.Wall, level, i, building);
                        wall.transform.localRotation = rotation;
                        if (CandyTheme) { DecorateCandyPanel(wall, style, color); continue; }
                        BlockBuilding.Connect(wall, beams[i]);
                        // Normalized local sizes keep these windows attached to thin facade panels.
                        for (int window = -1; window <= 1; window += 2)
                            Box("Window", new Vector3(window * .24f, .03f, .515f), new Vector3(.32f, .55f, .025f), glass, wall.transform, false);
                    }
                }
                previous = pillars;
                previousSlabs = slabs;
            }
            if (style == 2)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var roof = Part("Shop gable roof", new Vector3(0, floors * height + .65f, side * width / 4), new Vector3(width + .3f, .3f, width * .54f), CandyTheme ? candyWafer : color, BuildingPart.Prop, floors - 1, -1, building);
                    roof.transform.localRotation = Quaternion.Euler(side * 15, 0, 0);
                    if (CandyTheme) DecorateBiscuitRoof(roof, color);
                    foreach (var post in previous) BlockBuilding.Connect(roof, post);
                }
            }
            else
            {
                float roofHeight = CandyTheme ? .65f : style == 0 ? 3 : 1.2f;
                float roofWidth = CandyTheme && style == 0 ? width * (1 - (floors - 1) / 2 * .11f) : width;
                var roof = Part(CandyTheme ? "Candy roof topper" : style == 0 ? "Tower crown" : "Roof utility", new Vector3(0, floors * height + roofHeight / 2), new Vector3(roofWidth * (CandyTheme ? .72f : .3f), roofHeight, roofWidth * (CandyTheme ? .72f : .3f)), CandyTheme ? candyCream : style == 0 ? color : dark, BuildingPart.Prop, floors - 1, -1, building);
                if (CandyTheme) DecorateCandyRoof(roof, style, color, roofWidth);
                foreach (var post in previous) BlockBuilding.Connect(roof, post);
            }
            CreateBuildingReward(building);
            if ((Buildings.Count - 1) % 3 == 0) CreateExplosiveCluster(building);
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
            if (CandyTheme) { CreateCandyProp(position, label, color, sign); return; }
            position = GroundPoint(position);
            var item = new GameObject(label);
            item.transform.SetParent(transform, false);
            item.transform.position = position;
            var trunk = item.AddComponent<BoxCollider>();
            trunk.center = Vector3.up * 1.4f;
            trunk.size = new Vector3(sign ? .3f : .45f, 2.8f, sign ? .3f : .45f);
            AddBlock(item, true);
            Box("Post", trunk.center, trunk.size, sign ? dark : wood, item.transform, false);
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

        internal GameObject CreateCannonShell(Vector3 position, Vector3 direction)
        {
            var shot = new GameObject("Cannon shell", typeof(MeshFilter), typeof(MeshRenderer));
            shot.transform.SetParent(transform, false);
            shot.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            shot.GetComponent<MeshFilter>().sharedMesh = geometry.CannonShell();
            var renderer = shot.GetComponent<MeshRenderer>(); renderer.sharedMaterials = cannonShellMaterials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            return shot;
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
            Box("Upper hull", new Vector3(0, .48f, -.18f), new Vector3(1.95f, .35f, 2.35f), lime, visual, false);
            Box("Rear engine deck", new Vector3(0, .48f, -1.15f), new Vector3(1.55f, .16f, .52f), lime, visual, false);
            for (int i = -2; i <= 2; i++) Box("Engine vent", new Vector3(i * .2f, .575f, -1.15f), new Vector3(.1f, .018f, .34f), dark, visual, false);
            var turret = new GameObject("Turret pivot").transform;
            turret.SetParent(visual, false);
            turret.localPosition = new Vector3(0, .68f, -.15f);
            Box("Turret", Vector3.zero, new Vector3(1.5f, .75f, 1.6f), lime, turret, false);
            Box("Turret hatch", new Vector3(0, .4f, -.25f), new Vector3(.65f, .12f, .7f), lime, turret, false);
            var cannon = new GameObject("Fixed cannon pivot").transform;
            cannon.SetParent(turret, false);
            cannon.localPosition = new Vector3(0, .04f, .55f);
            cannon.localRotation = Quaternion.Euler(-game.Settings.cannonElevation, 0, 0);
            Box("Fixed cannon", new Vector3(0, 0, 1.25f), new Vector3(.4f, .4f, 2.5f), cannonPaint, cannon, false);
            Box("Muzzle", new Vector3(0, 0, 2.48f), new Vector3(.6f, .6f, .3f), dark, cannon, false);
            foreach (int side in new[] { -1, 1 })
            {
                Box("Track", new Vector3(side * 1.2f, -.35f, 0), new Vector3(.65f, .9f, 3.5f), rubber, visual, false);
                for (int w = -5; w <= 5; w++)
                {
                    Box("Track top tread", new Vector3(side * 1.2f, .12f, w * .29f), new Vector3(.69f, .09f, .2f), rubber, visual, false);
                    Box("Track side tread", new Vector3(side * 1.54f, -.35f, w * .29f), new Vector3(.07f, .7f, .2f), rubber, visual, false);
                }
                foreach (int end in new[] { -1, 1 }) for (int tread = -2; tread <= 2; tread++)
                    Box("Track end tread", new Vector3(side * 1.2f, -.35f + tread * .17f, end * 1.77f), new Vector3(.66f, .11f, .085f), rubber, visual, false);
                Box("Rear reflector", new Vector3(side * .83f, .1f, -1.57f), new Vector3(.26f, .12f, .07f), ShotMaterial, visual, false);
            }
            var muzzle = new GameObject("Shot origin").transform;
            muzzle.SetParent(cannon, false);
            muzzle.localPosition = new Vector3(0, 0, 2.8f);
            var motor = tank.AddComponent<TankMotor>();
            motor.Visual = visual;
            motor.Turret = turret;
            motor.Muzzle = muzzle;
            motor.Initialize(game);
            return motor;
        }

        private void OnDestroy()
        {
            foreach (var building in Buildings) building.Presentation?.Dispose();
            if (proxyCube != null) Destroy(proxyCube);
            foreach (var mesh in vehicleMeshes) if (mesh != null) Destroy(mesh);
            Terrain?.Dispose();
            geometry.Dispose();
            candyGeometry?.Dispose();
            foreach (var material in materials) if (material != null) Destroy(material);
            Icons?.Dispose();
            SpecialIcons?.Dispose();
        }
    }
}
