using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TankTown.Tests
{
    public sealed class TankTownCandyTests
    {
        private TankTownGame game;
        private float captureStep;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            captureStep = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60;
            TankTownGame.SelectedStage = TownStage.Town;
            SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>();
        }
        [UnityTearDown]
        public IEnumerator TearDown() { TankTownGame.SelectedStage = TownStage.Town; Time.captureDeltaTime = captureStep; Time.timeScale = 1; yield return null; }
        private IEnumerator PlayCandy()
        {
            game.SelectStage(TownStage.CandyTown); game.StartOrRestart(); yield return null; yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>(); game.Input.useHardware = true;
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
        }
        [UnityTest]
        public IEnumerator CandySelectionDefersLoadingAndHasSeparateRecordsAndReplay()
        {
            var original = game; var preview = game.TitlePreview; int seed = game.CitySeed;
            GameObject.Find("Candy stage button").GetComponent<Button>().onClick.Invoke(); yield return null;
            Assert.That(game, Is.SameAs(original)); Assert.That(game.TitlePreview, Is.SameAs(preview));
            Assert.That(game.Stage, Is.EqualTo(TownStage.CandyTown)); Assert.That(game.World.CandyTheme, Is.False);
            Assert.That(game.StageStorageSuffix, Is.EqualTo(".CandyTown"));
            Canvas.ForceUpdateCanvases();
            foreach (var name in new[] { "Town stage button", "Large stage button", "Candy stage button" })
            {
                var text = GameObject.Find(name).GetComponentInChildren<Text>();
                Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 1), name);
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), name);
            }
            game.StartOrRestart(); yield return null; yield return null; game = Object.FindFirstObjectByType<TankTownGame>();
            Assert.That(game.IsPlaying && game.World.CandyTheme, Is.True); Assert.That(game.CitySeed, Is.EqualTo(seed));
            Assert.That(game.Settings.roundSeconds, Is.EqualTo(80)); Assert.That(game.World.HalfExtent, Is.EqualTo(268));
            game.AdvanceTime(1000); game.StartOrRestart(); yield return null; yield return null; game = Object.FindFirstObjectByType<TankTownGame>();
            Assert.That(game.Stage, Is.EqualTo(TownStage.CandyTown)); Assert.That(game.IsPlaying, Is.True);
            game.TogglePause(); game.ReturnToTitle(); yield return null; yield return null; game = Object.FindFirstObjectByType<TankTownGame>();
            game.SelectStage(TownStage.Town); Assert.That(game.StageStorageSuffix, Is.Empty);
        }
        [UnityTest]
        public IEnumerator ExpandedCandyWorldHasSixHouseTypesPhysicalGateAndNoGameplayLogo()
        {
            yield return PlayCandy();
            Assert.That(game.World.Buildings.Count, Is.EqualTo(133)); Assert.That(game.World.ParkCount, Is.EqualTo(12));
            foreach (var name in new[] { "Shortcake tower", "Chocolate house", "Biscuit house", "Pink candy shop", "Aqua ice cream shop", "Cupcake cafe", "Doughnut gate" })
                Assert.That(game.World.Buildings.Any(x => x.name == name), Is.True, name);
            Assert.That(game.World.ItemPickups.Select(x => x.Kind).Distinct().Count(), Is.EqualTo(6));
            foreach (var building in game.World.Buildings)
            {
                Assert.That(building.Blocks.Count(x => x.Foundation), Is.EqualTo(4));
                Assert.That(building.Reward.Area, Is.Null); Assert.That(building.Reward.Available, Is.False);
            }
            var gate = game.World.Buildings.Single(x => x.Style == 3);
            Assert.That(gate.Blocks.Count, Is.EqualTo(16));
            Assert.That(gate.Blocks.All(x => x.GetComponent<Collider>() != null), Is.True);
            Assert.That(gate.GetComponentsInChildren<MeshFilter>().Any(x => x.sharedMesh.name == "Smooth doughnut segment"), Is.True);
            Assert.That(game.World.GetComponentsInChildren<Transform>().Any(x => x.name == "Lollipop tree"), Is.True);
            Assert.That(game.World.GetComponentsInChildren<Transform>().Any(x => x.name == "Roof strawberry"), Is.True);
            var cake = game.World.Buildings.First(x => x.Style == 0);
            Assert.That(cake.Blocks.First(x => x.Kind == BuildingPart.Slab && x.Level == cake.Floors - 1).transform.localScale.x,
                Is.LessThan(cake.Blocks.First(x => x.Kind == BuildingPart.Slab && x.Level == 0).transform.localScale.x), "Cake tiers must get smaller above the base");
            Assert.That(GameObject.Find("Gameplay HUD").GetComponentsInChildren<Text>(true).Any(x => x.text == "TANK" || x.text == "TOWN"), Is.False);
        }
        [UnityTest]
        public IEnumerator NewPastelHousesKeepEntrancesTexturesAndLocalDestruction()
        {
            yield return PlayCandy();
            foreach (int style in new[] { 4, 5, 6 })
            {
                var building = game.World.Buildings.First(x => x.Style == style);
                Assert.That(building.GetComponentsInChildren<Transform>().Count(x => x.name == "Building entrance"), Is.EqualTo(1));
                var sign = building.GetComponentsInChildren<Transform>().Single(x => x.name == "Candy reward sign");
                Assert.That(sign.GetComponent<Renderer>().sharedMaterial, Is.SameAs(game.World.SpecialIcons.Material(building.RewardKind)));
                var wall = building.Blocks.First(x => x.Kind == BuildingPart.Wall);
                Assert.That(wall.GetComponent<Renderer>().sharedMaterial.IsKeywordEnabled("_CANDY_TEXTURE"), Is.True);
                var window = wall.GetComponentsInChildren<Transform>().First(x => x.name == "Pastel candy window");
                Assert.That(window.GetComponentsInChildren<Collider>().Any(x => x.enabled), Is.False);
                wall.Break(wall.transform.position, 0); building.ReleaseUnsupported(wall.transform.position, 0);
                Assert.That(wall.DebrisBodies, Does.Contain(window.GetComponentInParent<Rigidbody>()));
                Assert.That(building.FullyDestroyed || building.Reward.Available, Is.False, "A local hit must not unlock the whole building");
                foreach (var part in building.Blocks) if (!part.Detached) part.Break(part.transform.position, 0);
                yield return null;
                Assert.That(building.FullyDestroyed && building.Reward.Available, Is.True, "New houses must release the normal full-destruction reward");
            }
        }
        [UnityTest]
        public IEnumerator CandyAndTownWallsKeepTheirSupportsUntilTheAttachedBeamBreaks()
        {
            game.StartOrRestart(); game.Input.useHardware = true;
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var building in game.World.Buildings.GroupBy(x => x.Style).Select(x => x.First())) VerifyWallSupport(building);
            TankTownGame.SelectedStage = TownStage.CandyTown; SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>(); game.StartOrRestart(); game.Input.useHardware = true;
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var building in game.World.Buildings.Where(x => x.Style != 3).GroupBy(x => x.Style).Select(x => x.First())) VerifyWallSupport(building);
        }
        private static void VerifyWallSupport(BlockBuilding building)
        {
            var walls = building.Blocks.Where(x => x.Kind == BuildingPart.Wall).ToArray();
            foreach (var wall in walls)
                Assert.That(wall.Links.Any(x => x.Kind == BuildingPart.Beam && x.Level == wall.Level && x.Column == wall.Column), Is.True, building.name + " / wall has its own beam");
            building.ReleaseUnsupported(building.transform.position, 0);
            Assert.That(building.DetachedParts, Is.Zero, building.name + ": an intact building must not shed walls during the first support check");
            var hit = walls.First(x => x.Level == 0 && x.Column == 0);
            hit.Break(hit.transform.position, 0); building.ReleaseUnsupported(hit.transform.position, 0);
            Assert.That(building.DetachedParts, Is.EqualTo(1), "Breaking one wall must not release the other facades");
            var beam = hit.Links.Single(x => x.Kind == BuildingPart.Beam);
            beam.Break(beam.transform.position, 0); building.ReleaseUnsupported(beam.transform.position, 0);
            Assert.That(walls.Where(x => x.Level == beam.Level && x.Column == beam.Column).All(x => x.Detached), Is.True, "The damaged beam releases its own walls");
            Assert.That(walls.Where(x => x.Level != beam.Level || x.Column != beam.Column).All(x => !x.Detached), Is.True, "Other walls still have their own supports");
            foreach (var support in building.Blocks.Where(x => x.Foundation).Take(3).ToArray()) support.Break(support.transform.position, 0);
            building.ReleaseUnsupported(building.transform.position, 0);
            Assert.That(building.FullyDestroyed && building.Reward.Available, Is.True, "Loss of real structural support must still collapse the building and release its reward");
        }
        [UnityTest]
        public IEnumerator ChocolateBreaksLocallyAndOnlyFullDestructionReleasesItsReward()
        {
            yield return PlayCandy();
            var building = game.World.Buildings.First(x => x.Style == 1);
            var wall = building.Blocks.First(x => x.Kind == BuildingPart.Wall);
            wall.Break(wall.transform.position, 0); building.ReleaseUnsupported(wall.transform.position, 0);
            Assert.That(wall.DebrisBodies.Count, Is.GreaterThan(1));
            Assert.That(building.FullyDestroyed, Is.False); Assert.That(building.Reward.Area, Is.Null);
            Assert.That(wall.DebrisBodies.SelectMany(x => x.GetComponentsInChildren<Transform>()).Any(x => x.name == "Chocolate square"), Is.True);
            foreach (var part in building.Blocks.Where(x => !x.Detached).ToArray()) part.Break(part.transform.position, 0);
            Assert.That(building.FullyDestroyed, Is.True); Assert.That(building.Reward.Area, Is.Not.Null);
            Assert.That(Vector2.Distance(new Vector2(building.transform.position.x, building.transform.position.z), new Vector2(building.Reward.Area.Center.x, building.Reward.Area.Center.z)), Is.LessThan(.01f));
            yield return null;
        }
        [UnityTest]
        public IEnumerator CannonDestroysCakePartsAndUnsupportedDoughnutCollapses()
        {
            yield return PlayCandy();
            var cake = game.World.Buildings.First(x => x.Style == 0);
            game.Tank.Warp(new Pose(cake.transform.position + new Vector3(-4.6f, 0, -cake.Width / 2 - 18), Quaternion.identity));
            foreach (var item in game.World.Pickups) item.gameObject.SetActive(false);
            game.Input.PulsePad(0); yield return new WaitForSeconds(.8f);
            Assert.That(game.BrokenBlocks, Is.GreaterThan(0)); Assert.That(cake.FullyDestroyed, Is.False);
            var gate = game.World.Buildings.Single(x => x.Style == 3);
            foreach (var foot in gate.Blocks.Where(x => x.Foundation).ToArray()) foot.Break(foot.transform.position, 0);
            gate.ReleaseUnsupported(gate.transform.position, 0);
            Assert.That(gate.FullyDestroyed, Is.True); Assert.That(gate.Reward.Area, Is.Not.Null);
            Assert.That(gate.Blocks.Where(x => x.name == "Doughnut slice").All(x => x.DebrisBodies.Count == 1), Is.True, "Curved slices must keep their shape instead of repeating the whole mesh in each box fragment");
        }
    }
}
