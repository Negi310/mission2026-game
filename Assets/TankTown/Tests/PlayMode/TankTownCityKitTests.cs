using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TankTown.Tests
{
    public sealed class TankTownCityKitTests
    {
        private TankTownGame game;
        private TownStage previousStage;
        private bool previousSkip;

        private IEnumerator Load(TownStage stage)
        {
            TankTownGame.SelectedStage = stage; TankTownGame.SkipIntroForTests = true;
            SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>(); game.StartOrRestart();
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            yield return null;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousStage = TankTownGame.SelectedStage; previousSkip = TankTownGame.SkipIntroForTests;
            yield return Load(TownStage.Town);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TankTownGame.SelectedStage = previousStage; TankTownGame.SkipIntroForTests = previousSkip;
            Time.timeScale = 1; yield return null;
        }

        private static Transform[] Children(Component root, string name) =>
            root.GetComponentsInChildren<Transform>(true).Where(x => x.name == name).ToArray();

        [UnityTest]
        public IEnumerator BuildingsAndStreetFurnitureUseTheBlenderKit()
        {
            Assert.That(game.World.CityKit, Is.Not.Null);
            var tower = game.World.Buildings.First(x => x.Style == 0);
            var apartment = game.World.Buildings.First(x => x.Style == 1);
            var shop = game.World.Buildings.First(x => x.Style == 2);
            Assert.That(Children(tower, "Crown beacon"), Has.Length.EqualTo(1));
            Assert.That(Children(apartment, "Balcony slab").Length, Is.GreaterThan(0));
            Assert.That(Children(apartment, "Window shutters").Length, Is.GreaterThan(0));
            Assert.That(Children(apartment, "Water tank roof"), Has.Length.EqualTo(1));
            Assert.That(Children(shop, "Awning stripes").Length, Is.GreaterThan(0));
            Assert.That(Children(shop, "Roof tile ridges"), Has.Length.EqualTo(2));

            // Balconies sit on the front and back faces above the ground floor only.
            foreach (var balcony in Children(apartment, "Balcony slab"))
            {
                var wall = balcony.parent.GetComponent<DestructibleBlock>();
                Assert.That(wall.Level, Is.GreaterThan(0)); Assert.That(wall.Column % 2, Is.Zero);
            }

            var lamp = Object.FindObjectsByType<DestructibleBlock>(FindObjectsSortMode.None).First(x => x.name == "Street lamp");
            Assert.That(lamp.GetComponents<BoxCollider>(), Has.Length.EqualTo(2), "Post and lantern keep their collision boxes");
            Assert.That(lamp.transform.Find("Top").GetComponent<Renderer>().sharedMaterial.GetColor("_EmissionColor").maxColorComponent, Is.GreaterThan(1));
            var fountain = Object.FindObjectsByType<DestructibleBlock>(FindObjectsSortMode.None).First(x => x.name == "Crushable fountain");
            Assert.That(fountain.GetComponent<MeshRenderer>() == null, Is.True, "The collision cylinder must not be drawn over the model");
            Assert.That(fountain.transform.Find("Fountain stone"), Is.Not.Null);
            var barrel = game.World.Explosives.First(x => x.name == "Explosive red barrel");
            Assert.That(barrel.GetComponentInChildren<Renderer>().name, Is.EqualTo("Barrel body"), "The primed tint goes to the red body");

            var canopies = Enumerable.Range(0, 3).Select(i => game.World.CityKit.Mesh($"Tree canopy {i}")).ToArray();
            var crowns = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(x => x.name == "Tree crown").ToArray();
            Assert.That(crowns.All(x => canopies.Contains(x.sharedMesh)), Is.True);

            // Every kit renderer must use the shared toy materials, and stay batchable.
            var kitRenderers = game.World.GetComponentsInChildren<MeshFilter>(true)
                .Where(x => game.World.CityKit.DistantShape(x.sharedMesh) != null).Select(x => x.GetComponent<MeshRenderer>()).ToArray();
            Assert.That(kitRenderers.Length, Is.GreaterThan(1000));
            Assert.That(kitRenderers.All(x => x.sharedMaterials.Length == 1 && x.sharedMaterial.shader.name == "TankTown/Saturated Toon"), Is.True);
            Assert.That(kitRenderers.All(x => x.GetComponent<MeshFilter>().sharedMesh.isReadable), Is.True, "Building batching combines these meshes");
            Assert.That(kitRenderers.Where(x => x.name != "Tree crown").All(x => x.GetComponent<Collider>() == null), Is.True,
                "Trim must not add invisible obstacles; only tree crowns keep their own sphere");
            // Each one-material part is centred on its own shape, so the far boxes do not overlap the whole piece.
            var window = tower.GetComponentsInChildren<Transform>().First(x => x.name == "Window");
            var frame = window.parent.Cast<Transform>().First(x => x.name == "Deep window frame");
            Assert.That(window.GetComponent<MeshFilter>().sharedMesh.bounds.size.z, Is.LessThan(.1f), "The glass mesh holds only the thin pane");
            Assert.That(Vector3.Distance(window.localPosition, frame.localPosition), Is.GreaterThan(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RoadsCloudsAndDistantSceneryUseTheKit()
        {
            int half = game.Settings.cityHalfBlocks, lanes = half * 2 + 1;
            var objects = game.World.GetComponentsInChildren<Transform>(true);
            // Four approaches per junction, minus the outward stubs along the town edge.
            Assert.That(objects.Count(x => x.name == "Crosswalk"), Is.EqualTo(lanes * lanes * 4 - lanes * 4));
            Assert.That(objects.Count(x => x.name == "Manhole"), Is.EqualTo(lanes * half * 2 * 2));
            var dashes = objects.Where(x => x.name == "Lane dash").Select(x => x.GetComponent<MeshRenderer>()).ToArray();
            Assert.That(dashes.Length, Is.GreaterThan(100));
            Assert.That(dashes.All(x => x.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off), Is.True);
            Assert.That(dashes.All(x => x.GetComponent<Collider>() == null), Is.True, "Road marks must not bump the tank");
            foreach (var crosswalk in objects.Where(x => x.name == "Crosswalk"))
            {
                // Each crosswalk lies just outside its junction, on the road surface.
                Vector3 p = crosswalk.position; float spacing = game.Settings.blockSpacing;
                float offset = Mathf.Max(Mathf.Abs(p.x - Mathf.Round(p.x / spacing) * spacing), Mathf.Abs(p.z - Mathf.Round(p.z / spacing) * spacing));
                Assert.That(offset, Is.EqualTo(game.World.RoadWidth / 2 + 1.6f).Within(.01f));
                Assert.That(p.y - game.World.GroundPoint(p).y, Is.EqualTo(.08f).Within(.02f));
            }

            foreach (var signal in game.World.Signals)
            {
                Assert.That(signal.transform.Find("Signal post and housings"), Is.Not.Null);
                Assert.That(signal.GetComponentsInChildren<Renderer>().Count(x => x.name == "Traffic light"), Is.EqualTo(6), "The switched bulbs remain");
                Assert.That(signal.GetComponents<BoxCollider>(), Has.Length.EqualTo(3));
            }

            var islands = objects.Where(x => x.name == "Distant island").ToArray();
            Assert.That(islands, Has.Length.EqualTo(10));
            Assert.That(islands.All(x => x.Find("Island beach") != null && x.GetComponentsInChildren<Collider>().Length == 0), Is.True);
            var boats = objects.Where(x => x.name == "Distant sailboat").ToArray();
            Assert.That(boats, Has.Length.EqualTo(5));
            float coast = game.World.HalfExtent + 14;
            Assert.That(boats.All(x => new Vector2(x.position.x, x.position.z).magnitude > coast), Is.True, "Boats float beyond the coast");
            Assert.That(islands.Min(x => new Vector2(x.position.x, x.position.z).magnitude), Is.GreaterThan(boats.Max(x => new Vector2(x.position.x, x.position.z).magnitude)));

            var clouds = objects.Where(x => x.name == "Soft toy cloud").ToArray();
            Assert.That(clouds, Has.Length.EqualTo(16));
            Assert.That(clouds.All(x => x.Find("Cloud puffs") != null && x.GetComponentsInChildren<Collider>().Length == 0), Is.True);
            var puffs = clouds[0].Find("Cloud puffs").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(puffs.colors.Length, Is.EqualTo(puffs.vertexCount), "Clouds are shaded by their vertex colours");
            Vector3 before = clouds[0].position; yield return new WaitForSeconds(.5f);
            Assert.That(clouds[0].position.x - before.x, Is.EqualTo(.45f * .5f).Within(.08f), "Each cloud drifts once per frame, not twice");
        }

        [UnityTest]
        public IEnumerator KitTrimKeepsItsSizeOnScaledPartsAndInTheDistance()
        {
            var shop = game.World.Buildings.First(x => x.Style == 2);
            var awning = Children(shop, "Awning stripes").First().GetComponent<MeshRenderer>();
            // The wall is scaled to its panel size; the awning must still be its modelled ~3.3m width, not stretched by the wall.
            Assert.That(awning.bounds.size.magnitude, Is.InRange(2.5f, 4.5f));
            var crate = Object.FindObjectsByType<DestructibleBlock>(FindObjectsSortMode.None).First(x => x.name == "Crushable crate");
            var body = crate.transform.Find("Crate body").GetComponent<Renderer>();
            Assert.That(body.bounds.size.x, Is.EqualTo(1.1f).Within(.15f));

            var mesh = awning.GetComponent<MeshFilter>().sharedMesh;
            var box = game.World.CityKit.DistantShape(mesh);
            Assert.That(Vector3.Distance(box.bounds.center, mesh.bounds.center), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(box.bounds.size, mesh.bounds.size), Is.LessThan(.001f));
            Assert.That(game.World.CityKit.DistantShape(game.World.Buildings[0].Blocks[0].GetComponent<MeshFilter>().sharedMesh), Is.Null,
                "Ordinary unit parts keep the shared proxy cube");

            // The lightweight distant mesh must follow the detailed outline instead of adding metre cubes at the trim.
            var visual = shop.Presentation;
            Assert.That(visual.MapMesh.bounds.max.y, Is.EqualTo(visual.DetailMesh.bounds.max.y).Within(.3f));
            Assert.That(visual.MapMesh.bounds.size.x, Is.EqualTo(visual.DetailMesh.bounds.size.x).Within(.3f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SwitchingTheKitOffRestoresTheBoxCity()
        {
            var settings = Resources.Load<TankTownSettings>("TankTownSettings");
            bool previous = settings.importedCityModels;
            settings.importedCityModels = false;
            try
            {
                yield return Load(TownStage.Town);
                Assert.That(game.World.CityKit, Is.Null);
                Assert.That(Children(game.World, "Awning stripes"), Is.Empty);
                Assert.That(Children(game.World, "Crosswalk"), Is.Empty);
                Assert.That(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(x => x.name == "Soft toy cloud").All(x => x.childCount == 5), Is.True,
                    "The box city keeps its five-sphere clouds");
                Assert.That(Children(game.World, "Deep window frame").Length, Is.GreaterThan(100));
                var fountain = Object.FindObjectsByType<DestructibleBlock>(FindObjectsSortMode.None).First(x => x.name == "Crushable fountain");
                Assert.That(fountain.GetComponent<MeshRenderer>() != null, Is.True);
            }
            finally { settings.importedCityModels = previous; }
        }
    }
}
