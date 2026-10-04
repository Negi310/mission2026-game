using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TankTown.Tests
{
    [SetUpFixture]
    public sealed class TownGenerationTestSeed
    {
        private int? previous;
        private bool previousIntro, previousRecords;
        [OneTimeSetUp] public void SetSeed()
        {
            previous = TankTownGame.GenerationSeedOverride; TankTownGame.GenerationSeedOverride = 2026;
            previousIntro = TankTownGame.SkipIntroForTests; TankTownGame.SkipIntroForTests = true;
            previousRecords = TankTownGame.DisableRecordSavingForTests; TankTownGame.DisableRecordSavingForTests = true;
        }
        [OneTimeTearDown] public void RestoreSeed()
        {
            TankTownGame.GenerationSeedOverride = previous;
            TankTownGame.SkipIntroForTests = previousIntro; TankTownGame.DisableRecordSavingForTests = previousRecords;
        }
    }

    public sealed class TankTownGenerationTests
    {
        private TankTownGame game;
        private float captureStep;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            captureStep = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60;
            SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>(); game.Input.useHardware = true;
        }
        [UnityTearDown]
        public IEnumerator TearDown() { Time.timeScale = 1; Time.captureDeltaTime = captureStep; yield return null; }

        [UnityTest]
        public IEnumerator LayoutsRepeatBySeedAndManySeedsKeepFlatPadsAndAllRewards()
        {
            var same = new TownLayout(game.Settings, game.CitySeed, game.World.RoadWidth);
            Assert.That(same.Plots.Select(p => p.Center), Is.EqualTo(game.World.Layout.Plots.Select(p => p.Center)));
            Assert.That(same.Plots.Select(p => p.Reward), Is.EqualTo(game.World.Layout.Plots.Select(p => p.Reward)));
            var signatures = new System.Collections.Generic.HashSet<string>();
            var combinations = new System.Collections.Generic.HashSet<string>();
            for (int seed = 0; seed < 100; seed++)
            {
                var layout = new TownLayout(game.Settings, seed, game.World.RoadWidth);
                var terrain = new TownTerrain(game.Settings, null, game.World.RoadWidth, layout);
                Assert.That(layout.Plots.Count(p => p.Park), Is.EqualTo(3));
                for (int kind = 0; kind < 6; kind++) Assert.That(layout.Plots.Any(p => !p.Park && (int)p.Reward == kind), Is.True);
                foreach (var plot in layout.Plots.Where(p => !p.Park))
                {
                    float height = terrain.HeightAt(plot.Center);
                    foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                        Assert.That(terrain.HeightAt(plot.Center + new Vector3(x, 0, z) * (plot.Width * .5f)), Is.EqualTo(height).Within(.001f), $"seed={seed}: foundations must fit the flat pad");
                    combinations.Add($"{plot.Style}/{plot.Reward}");
                }
                // Both sides of a junction must meet continuously.
                Assert.That(terrain.HeightAt(new Vector3(0, 0, 39.99f)), Is.EqualTo(terrain.HeightAt(new Vector3(0, 0, 40.01f))).Within(.001f));
                signatures.Add(string.Join(",", layout.Plots.Select(p => $"{p.Park}/{p.Style}/{p.Floors}/{p.Reward}/{p.Center}")));
                terrain.Dispose();
            }
            Assert.That(signatures.Count, Is.EqualTo(100));
            Assert.That(combinations.Count, Is.EqualTo(18), "All three shapes must occur with all six item colors");
            var different = new TownLayout(game.Settings, game.CitySeed + 1, game.World.RoadWidth);
            Assert.That(different.JunctionHeight(0, 0), Is.Not.EqualTo(same.JunctionHeight(0, 0)));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryFacadeColorMatchesItsActualUpperFloorReward()
        {
            foreach (var building in game.World.Buildings)
            {
                Assert.That(building.Reward.Kind, Is.EqualTo(building.RewardKind));
                Assert.That(building.FacadeColor, Is.EqualTo(ItemIcons.Color(building.Reward.Kind)));
                foreach (var wall in building.Blocks.Where(b => b.Kind == BuildingPart.Wall))
                    Assert.That(((Vector4)wall.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor") - (Vector4)building.FacadeColor).sqrMagnitude, Is.LessThan(.000001f));
                Assert.That(building.Reward.SupportingFloor.Level, Is.EqualTo(building.Floors - 1));
                Assert.That(building.Reward.Available, Is.False);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator NormalRetryGeneratesANewCityAndOneMinuteEndsTheRound()
        {
            var previous = TankTownGame.GenerationSeedOverride;
            try
            {
                TankTownGame.GenerationSeedOverride = null;
                SceneManager.LoadScene("TankTown"); yield return null;
                game = Object.FindFirstObjectByType<TankTownGame>();
                Assert.That(game.Remaining, Is.EqualTo(60));
                Assert.That(Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(t => t.text.Contains("3分") || t.text.Contains("3 MINUTE")), Is.False);
                int firstSeed = game.CitySeed;
                var first = game.World.Layout.Plots.Select(p => p.Reward).ToArray();
                game.StartOrRestart(); game.AdvanceTime(59.5f); Assert.That(game.IsPlaying, Is.True);
                game.AdvanceTime(.5f); Assert.That(game.State, Is.EqualTo(RoundState.Results));
                game.StartOrRestart(); yield return null;
                game = Object.FindFirstObjectByType<TankTownGame>();
                Assert.That(game.CitySeed, Is.Not.EqualTo(firstSeed));
                Assert.That(game.World.Layout.Plots.Select(p => p.Reward).ToArray(), Is.Not.EqualTo(first));
                Assert.That(game.Remaining, Is.InRange(59.5f, 60));
            }
            finally { TankTownGame.GenerationSeedOverride = previous; }
        }

        [UnityTest]
        public IEnumerator MissilesChooseVisibleForwardCarsAndReacquireUsingTheNewHeading()
        {
            game.StartOrRestart();
            game.Tank.enabled = false; var body = game.Tank.GetComponent<Rigidbody>();
            game.Tank.Warp(new Pose(new Vector3(0, 0, -105), Quaternion.identity)); body.isKinematic = true;
            foreach (var target in game.World.MovingTargets) target.gameObject.SetActive(false);
            var origin = game.Tank.transform.position;
            var front = TankTownMobilityTests.Car(game, origin + Vector3.forward * 18, origin + Vector3.forward * 35);
            var behind = TankTownMobilityTests.Car(game, origin - Vector3.forward * 7, origin - Vector3.forward * 20);
            var side = TankTownMobilityTests.Car(game, origin + Vector3.right * 20, origin + Vector3.right * 35);
            var blocked = TankTownMobilityTests.Car(game, origin + Vector3.forward * 36, origin + Vector3.forward * 55);
            var wall = game.World.Box("Sight blocker", game.World.GroundPoint(origin + Vector3.forward * 27, 4), new Vector3(10, 8, 1), game.World.ShotMaterial);
            game.CameraRig.Snap(); Physics.SyncTransforms();
            Assert.That(ItemMissile.VisibleAhead(game, front.GetComponent<TownMovingTarget>(), Vector3.forward), Is.True);
            Assert.That(ItemMissile.VisibleAhead(game, behind.GetComponent<TownMovingTarget>(), Vector3.forward), Is.False);
            Assert.That(ItemMissile.VisibleAhead(game, side.GetComponent<TownMovingTarget>(), Vector3.forward), Is.False);
            Assert.That(ItemMissile.VisibleAhead(game, blocked.GetComponent<TownMovingTarget>(), Vector3.forward), Is.False);
            game.Items.Receive(ItemKind.Missiles); game.Input.PulsePad(0);
            var rockets = Object.FindObjectsByType<ItemMissile>(FindObjectsSortMode.None);
            Assert.That(rockets.All(r => r.TrackingTarget == front.GetComponent<TownMovingTarget>()), Is.True);
            front.gameObject.SetActive(false); behind.gameObject.SetActive(false); blocked.gameObject.SetActive(false); wall.SetActive(false);
            // Remove scenery in this side direction; the test is about the new heading, not an unrelated building.
            foreach (var building in game.World.Buildings) building.gameObject.SetActive(false);
            game.Tank.Turret.rotation = Quaternion.Euler(0, 90, 0); game.CameraRig.Snap(); Physics.SyncTransforms();
            yield return null;
            Assert.That(rockets.All(r => r.TrackingTarget == side.GetComponent<TownMovingTarget>()), Is.True);
        }

        [UnityTest]
        public IEnumerator GiantBombTravelsFartherAndHasAFallbackForStuckFlights()
        {
            game.StartOrRestart(); game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var person in game.World.Pedestrians) person.gameObject.SetActive(false);
            game.Items.Receive(ItemKind.GigantBomb); game.Input.PulsePad(0);
            var bomb = Object.FindFirstObjectByType<GigantBomb>(); var bombBody = bomb.GetComponent<Rigidbody>();
            float speed = Vector3.Dot(bombBody.linearVelocity, Vector3.ProjectOnPlane(game.Tank.Muzzle.forward, Vector3.up).normalized);
            Assert.That(speed, Is.GreaterThan(89)); Assert.That(game.Items.Remaining(ItemKind.GigantBomb), Is.EqualTo(6));
            bombBody.position += Vector3.up * 80;
            Vector3 start = bombBody.position;
            yield return new WaitForSeconds(.6f);
            Assert.That(Vector3.ProjectOnPlane(bombBody.position - start, Vector3.up).magnitude, Is.GreaterThan(45));
            Assert.That(bomb.Detonated, Is.False);
            bombBody.isKinematic = true;
            yield return new WaitForSeconds(1);
            Assert.That(bomb.Detonated, Is.False, "The old 1.5 second fuse must not interrupt a flight");
            game.TogglePause(); float remaining = game.Items.Remaining(ItemKind.GigantBomb);
            yield return new WaitForSecondsRealtime(.1f); Assert.That(game.Items.Remaining(ItemKind.GigantBomb), Is.EqualTo(remaining)); game.TogglePause();
            yield return new WaitForSeconds(remaining + .05f);
            Assert.That(bomb == null, Is.True); Assert.That(game.Items.Weapon, Is.EqualTo(ItemKind.None));
        }
    }
}
