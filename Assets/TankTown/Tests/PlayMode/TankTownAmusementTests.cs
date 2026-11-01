using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TankTown.Tests
{
    public sealed class TankTownAmusementTests
    {
        private TankTownGame game;
        private float previousCapture;
        private bool previousIntro, previousSaving;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousCapture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60;
            previousIntro = TankTownGame.SkipIntroForTests; previousSaving = TankTownGame.DisableRecordSavingForTests;
            TankTownGame.SkipIntroForTests = true; TankTownGame.DisableRecordSavingForTests = true;
            TankTownGame.SelectedStage = TownStage.Town; SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>();
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TankTownGame.SelectedStage = TownStage.Town; TankTownGame.SkipIntroForTests = previousIntro;
            TankTownGame.DisableRecordSavingForTests = previousSaving; Time.captureDeltaTime = previousCapture; Time.timeScale = 1;
            yield return null;
        }
        private IEnumerator PlayPark()
        {
            game.SelectStage(TownStage.AmusementPark); game.StartOrRestart(); yield return null; yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>(); game.Input.useHardware = true;
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var car in game.World.TrafficCars) car.gameObject.SetActive(false);
        }
        [UnityTest]
        public IEnumerator FourthStageDefersLoadingAndKeepsReplayAndSeparateRecords()
        {
            var original = game; var preview = game.TitlePreview;
            GameObject.Find("Amusement stage button").GetComponent<Button>().onClick.Invoke(); yield return null;
            Assert.That(game, Is.SameAs(original)); Assert.That(game.TitlePreview, Is.SameAs(preview));
            Assert.That(game.Stage, Is.EqualTo(TownStage.AmusementPark)); Assert.That(game.World.AmusementTheme, Is.False);
            Assert.That(game.StageStorageSuffix, Is.EqualTo(".AmusementPark"));
            Canvas.ForceUpdateCanvases();
            foreach (string name in new[] { "Town stage button", "Large stage button", "Candy stage button", "Amusement stage button" })
            {
                var text = GameObject.Find(name).GetComponentInChildren<Text>();
                Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 1), name);
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), name);
            }
            yield return PlayPark();
            Assert.That(game.StageLabel, Is.EqualTo("遊園地")); Assert.That(game.World.AmusementTheme, Is.True);
            game.AdvanceTime(1000); game.StartOrRestart(); yield return null; yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>();
            Assert.That(game.Stage, Is.EqualTo(TownStage.AmusementPark)); Assert.That(game.IsPlaying, Is.True);
        }
        [UnityTest]
        public IEnumerator AllRideShapesHaveRealSupportsAndFullDestructionRewards()
        {
            yield return PlayPark();
            Assert.That(game.World.HalfExtent, Is.EqualTo(268)); Assert.That(game.Settings.roundSeconds, Is.EqualTo(80));
            Assert.That(game.World.Buildings.Count, Is.EqualTo(133)); Assert.That(game.World.ParkCount, Is.EqualTo(12));
            for (int style = 0; style <= 6; style++)
            {
                var building = game.World.Buildings.First(x => x.Style == style);
                Assert.That(building.Blocks.Count(x => x.Foundation), Is.GreaterThanOrEqualTo(4), building.name);
                building.ReleaseUnsupported(building.transform.position, 0);
                Assert.That(building.DetachedParts, Is.Zero, building.name + " must stand before damage");
                Assert.That(building.Reward.Area, Is.Null);
                foreach (var foot in building.Blocks.Where(x => x.Foundation).ToArray()) foot.Break(foot.transform.position, 0);
                building.ReleaseUnsupported(building.transform.position, 0);
                Assert.That(building.FullyDestroyed, Is.True, building.name + " must lose its real support");
                Assert.That(building.Reward.Area, Is.Not.Null, building.name);
            }
        }
        [UnityTest]
        public IEnumerator CannonBreaksWheelLocallyAndUnbalancedCoasterCollapses()
        {
            yield return PlayPark();
            var wheel = game.World.Buildings.First(x => x.Style == 0);
            game.Tank.Warp(new Pose(game.World.GroundPoint(wheel.transform.position + Vector3.back * 12, game.Settings.rideHeight), Quaternion.identity));
            Physics.SyncTransforms(); game.Input.PulsePad(0); yield return new WaitForSeconds(.6f);
            Assert.That(wheel.DetachedParts, Is.GreaterThan(0), "A normal shot must reach the low gondola");
            Assert.That(wheel.FullyDestroyed, Is.False); Assert.That(wheel.Reward.Area, Is.Null);
            var coaster = game.World.Buildings.First(x => x.Style == 1);
            var feet = coaster.Blocks.Where(x => x.Foundation).ToArray();
            foreach (var foot in feet.Take(feet.Length - 1)) foot.Break(foot.transform.position, 0);
            coaster.ReleaseUnsupported(coaster.transform.position, 0);
            Assert.That(coaster.FullyDestroyed, Is.True, "One remote support cannot balance the whole loop");
            Assert.That(coaster.Reward.Area, Is.Not.Null);
        }
        [UnityTest]
        public IEnumerator BoothsHaveEntrancesAndPropsCanBePushedAside()
        {
            yield return PlayPark();
            foreach (var booth in game.World.Buildings.Where(x => x.Style == 4 || x.Style == 5))
                Assert.That(booth.GetComponentsInChildren<Transform>().Count(x => x.name == "Booth entrance door"), Is.EqualTo(1));
            var fence = game.World.GetComponentsInChildren<DestructibleBlock>().First(x => x.name == "Park low fence");
            var tree = game.World.GetComponentsInChildren<DestructibleBlock>().First(x => x.name == "Park planter tree");
            foreach (var prop in new[] { fence, tree })
            {
                Assert.That(prop.CanCrush, Is.True); prop.Break(prop.transform.position, 2);
                Assert.That(prop.DebrisBodies.Any(x => !x.isKinematic), Is.True);
            }
        }
        [UnityTest]
        public IEnumerator SeededLayoutsKeepTheEntryLandmarksAndSmallMapsTerminate()
        {
            var settings = ScriptableObject.CreateInstance<TankTownSettings>(); settings.cityHalfBlocks = 6; settings.cityParks = 12;
            foreach (int seed in new[] { 2026, 67541, -74 })
            {
                var a = new TownLayout(settings, seed, 16, TownStage.AmusementPark); var b = new TownLayout(settings, seed, 16, TownStage.AmusementPark);
                Assert.That(a.Plots.Select(x => (x.Style, x.Center, x.Reward)), Is.EqualTo(b.Plots.Select(x => (x.Style, x.Center, x.Reward))));
                Assert.That(a.Plots.Single(x => x.X == -1 && x.Z == -5).Style, Is.EqualTo(0));
                Assert.That(a.Plots.Single(x => x.X == 0 && x.Z == -5).Style, Is.EqualTo(1));
                Assert.That(a.Plots.Single(x => x.X == 0 && x.Z == -4).Style, Is.EqualTo(3));
            }
            settings.cityHalfBlocks = 2; settings.cityParks = 10;
            Assert.That(new TownLayout(settings, 2026, 16, TownStage.AmusementPark).Plots.Count, Is.EqualTo(16));
            Object.Destroy(settings); yield return null;
        }
    }
}
