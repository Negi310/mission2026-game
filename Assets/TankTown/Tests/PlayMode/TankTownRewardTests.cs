using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TankTown.Tests
{
    public sealed class TankTownRewardTests
    {
        private TankTownGame game;
        private float captureStep;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            captureStep = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60;
            SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>();
            game.Input.useHardware = true;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1; Time.captureDeltaTime = captureStep; yield return null;
        }

        private void StartIsolated()
        {
            game.StartOrRestart();
            game.Tank.enabled = false;
            game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            foreach (var item in game.World.Explosives) item.gameObject.SetActive(false);
            foreach (var item in game.World.ItemPickups) item.gameObject.SetActive(false);
        }

        private void Collapse(BlockBuilding building)
        {
            foreach (var support in building.Blocks.Where(x => x != null && x.Foundation).ToArray()) support.Break(support.transform.position, 0);
            building.ReleaseUnsupported(building.transform.position, 0);
        }
        private void MoveTank(Vector3 point)
        {
            var body = game.Tank.GetComponent<Rigidbody>(); body.position = game.World.GroundPoint(point, game.Settings.rideHeight);
            game.Tank.transform.position = body.position; Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator TitleFreezesTheWorldAndOnePressurePulseStartsWithoutShooting()
        {
            var title = GameObject.Find("Title screen");
            Assert.That(title, Is.Not.Null);
            Assert.That(game.State, Is.EqualTo(RoundState.Ready));
            Assert.That(Time.timeScale, Is.Zero);
            var position = game.Tank.transform.position;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(game.Remaining, Is.EqualTo(80));
            Assert.That(game.Tank.transform.position, Is.EqualTo(position));
            game.Input.PulsePad(1); yield return null;
            Assert.That(game.IsPlaying, Is.True); Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(title.activeSelf, Is.False);
            Assert.That(Object.FindFirstObjectByType<TankShell>(), Is.Null);
            Assert.That(game.Remaining, Is.LessThan(80));
        }

        [UnityTest]
        public IEnumerator MenuReturnsToAFreshTitleAndThePlayButtonStarts()
        {
            game.StartOrRestart(); game.TogglePause(); game.ReturnToTitle(); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>();
            Assert.That(game.State, Is.EqualTo(RoundState.Ready));
            Assert.That(game.BrokenBlocks, Is.Zero); Assert.That(game.ChainExplosions, Is.Zero);
            Assert.That(game.World.ItemPickups.TrueForAll(x => !x.Collected && !x.Released), Is.True);
            var button = GameObject.Find("Title play button").GetComponent<Button>();
            button.onClick.Invoke(); yield return null;
            Assert.That(game.IsPlaying, Is.True);
            game.AdvanceTime(1000); game.ReturnToTitle(); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>();
            Assert.That(game.State, Is.EqualTo(RoundState.Ready));
            Assert.That(Time.timeScale, Is.Zero);
        }

        [UnityTest]
        public IEnumerator EveryBuildingHasAHiddenGroundCentreRewardAndAllSixKindsExist()
        {
            Assert.That(game.World.ItemPickups.Count, Is.EqualTo(game.World.Buildings.Count));
            for (int i = 0; i < 6; i++) Assert.That(game.World.ItemPickups.Exists(x => x.Kind == (ItemKind)i), Is.True);
            foreach (var building in game.World.Buildings)
            {
                var reward = building.Reward;
                Assert.That(reward.SourceBuilding, Is.EqualTo(building));
                Assert.That(reward.transform.position, Is.EqualTo(game.World.GroundPoint(building.transform.position, .75f)));
                Assert.That(reward.GetComponentsInChildren<Renderer>().All(x => !x.enabled), Is.True);
                Assert.That(reward.Available, Is.False); Assert.That(reward.DropBody, Is.Null); Assert.That(reward.Area, Is.Null);
                Assert.That(reward.Collect(), Is.False);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator BreakingTheRoofAndSomeWallsNeverSpawnsAnArea()
        {
            StartIsolated(); var reward = game.World.Buildings[0].Reward; reward.gameObject.SetActive(true);
            var building = reward.SourceBuilding; Vector3 position = reward.transform.position;
            var roof = building.Blocks.First(x => x.Kind == BuildingPart.Slab && x.Level == building.Floors - 1);
            roof.Break(roof.transform.position, 0);
            var wall = building.Blocks.First(x => x.Kind == BuildingPart.Wall); wall.Break(wall.transform.position, 0);
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.That(reward.Released, Is.False); Assert.That(reward.DropBody, Is.Null); Assert.That(reward.Area, Is.Null);
            Assert.That(reward.transform.position, Is.EqualTo(position));
            Assert.That(reward.Available, Is.False); Assert.That(reward.Collect(), Is.False);
            Assert.That(game.Items.Held, Is.EqualTo(ItemKind.None));
        }

        [UnityTest]
        public IEnumerator FullDestructionSpawnsTheGroundAreaAndEnteringItsEdgeCollectsIt()
        {
            StartIsolated(); var building = game.World.Buildings[0]; var reward = building.Reward;
            reward.gameObject.SetActive(true); var original = reward.transform.position;
            Collapse(building);
            Assert.That(building.FullyDestroyed, Is.True);
            Assert.That(reward.Available, Is.True); Assert.That(reward.DropBody.isKinematic, Is.True);
            Assert.That(reward.transform.position, Is.EqualTo(original)); Assert.That(reward.Area, Is.Not.Null);
            var area = reward.Area;
            Assert.That(area.Center, Is.EqualTo(game.World.GroundPoint(building.transform.position)));
            Assert.That(area.Radius, Is.GreaterThanOrEqualTo(7));
            Assert.That(reward.Collected, Is.False);
            MoveTank(area.Center + Vector3.right * (area.Radius * 1.04f));
            yield return new WaitForFixedUpdate(); Assert.That(reward.Collected, Is.False, "Overlapping the outer edge with the hull is not entering the circle");
            MoveTank(area.Center + Vector3.right * (area.Radius * .85f));
            yield return new WaitForFixedUpdate(); yield return null;
            Assert.That(reward.Collected, Is.True);
            if (ItemIcons.Automatic(reward.Kind)) Assert.That(game.Items.Remaining(reward.Kind), Is.GreaterThan(0));
            else Assert.That(game.Items.Held, Is.EqualTo(reward.Kind));
            Assert.That(reward.gameObject.activeSelf, Is.False);
            Assert.That(reward.Area, Is.Null); Assert.That(area == null || !area.gameObject.activeSelf, Is.True);
        }
        [UnityTest]
        public IEnumerator MagnetMovesOnlyTheItemAndRemovesTheAreaExactlyWhenItLeavesTheCircle()
        {
            StartIsolated(); var building = game.World.Buildings.First(x => x.Reward.Kind == ItemKind.MegaBeam);
            var reward = building.Reward; reward.gameObject.SetActive(true); Collapse(building);
            var area = reward.Area; Vector3 center = area.Center;
            MoveTank(center + Vector3.right * (area.Radius * 1.8f));
            game.Items.Receive(ItemKind.GigantBomb); // Keep the extracted item observable rather than consuming it.
            yield return null; // The newly started round needs a scaled frame before attraction moves.
            reward.Attract(game.Tank.transform.position + Vector3.up, 10);
            Assert.That(reward.Area, Is.SameAs(area)); Assert.That(area.Center, Is.EqualTo(center));
            Assert.That(reward.transform.position.x, Is.GreaterThan(center.x));
            for (int i = 0; i < 40 && reward.Area != null; i++) reward.Attract(game.Tank.transform.position + Vector3.up, 120);
            Assert.That(reward.Area, Is.Null); Assert.That(area.gameObject.activeSelf, Is.False);
            Assert.That(reward.Collected, Is.False); Assert.That(reward.gameObject.activeSelf, Is.True);
            Assert.That(Vector2.Distance(new Vector2(reward.transform.position.x, reward.transform.position.z), new Vector2(center.x, center.z)), Is.GreaterThan(area.Radius));
            yield return new WaitForSeconds(.15f);
            Assert.That(reward.DropBody.isKinematic, Is.False, "An extracted item survives cancelled attraction and resumes ground collision");
        }
        [UnityTest]
        public IEnumerator OneRemainingPartKeepsTheRewardLockedAndTheLastPartSpawnsOnlyOneArea()
        {
            StartIsolated(); var building = game.World.Buildings[0]; var reward = building.Reward;
            reward.gameObject.SetActive(true); var last = building.Blocks.First(x => x.Foundation);
            foreach (var block in building.Blocks.Where(x => x != last).ToArray()) block.Break(block.transform.position, 0);
            Assert.That(building.DestroyedFraction, Is.GreaterThan(.9f)); Assert.That(building.FullyDestroyed, Is.False);
            Assert.That(reward.Available, Is.False); Assert.That(reward.Area, Is.Null);
            last.Break(last.transform.position, 0); var area = reward.Area;
            Assert.That(area, Is.Not.Null); Assert.That(building.FullyDestroyed, Is.True);
            last.Break(last.transform.position, 0); reward.SpawnArea();
            Assert.That(reward.Area, Is.SameAs(area));
            Assert.That(Object.FindObjectsByType<SpecialItemArea>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyedBuildingProgressSurvivesDebrisCleanupAndFullSlotsLeaveTheReward()
        {
            StartIsolated();
            int originalLimit = game.Settings.maxDebris;
            // この検証は消去後の報酬に焦点を当て、既定の個数に依存しない。
            game.Settings.maxDebris = 128;
            try
            {
                var first = game.World.Buildings.Find(x => !ItemIcons.Automatic(x.Reward.Kind)); var reward = first.Reward;
                reward.gameObject.SetActive(true);
                Collapse(first);
                foreach (var building in game.World.Buildings.GetRange(0, 5)) Collapse(building);
                yield return new WaitForFixedUpdate(); yield return null;
                Assert.That(first.FullyDestroyed, Is.True);
                Assert.That(first.Blocks.Exists(x => x == null), Is.True, "Old debris must actually have been evicted");
                Assert.That(reward.Available, Is.True);
                MoveTank(reward.Area.Center);
                Assert.That(game.Items.Receive(ItemKind.GigantBomb), Is.True);
                Assert.That(reward.Collect(), Is.False); Assert.That(reward.Collected, Is.False);
                Assert.That(reward.gameObject.activeSelf, Is.True);
                game.Items.UseHeld();
                Assert.That(reward.Collect(), Is.True);
            }
            finally { game.Settings.maxDebris = originalLimit; }
        }

        [UnityTest]
        public IEnumerator AShotAtOneBarrelPropagatesToNeighboursAndDamagesTheBuilding()
        {
            StartIsolated();
            for (int i = 0; i < 4; i++) game.World.Explosives[i].gameObject.SetActive(true);
            var first = game.World.Explosives[0];
            game.Blast(first.transform.position, .8f, 0);
            Assert.That(first.Primed, Is.True); Assert.That(game.ChainExplosions, Is.Zero);
            yield return new WaitForSeconds(1.2f);
            Assert.That(game.ChainExplosions, Is.GreaterThanOrEqualTo(3));
            Assert.That(game.LongestChain, Is.GreaterThanOrEqualTo(2));
            Assert.That(game.World.Buildings[0].DetachedParts, Is.GreaterThan(0));
            // 新しい破片の衝突連鎖が収束した後に、重複加点がないことを確認する。
            float settledAt = Time.time;
            while (Object.FindObjectsByType<FlyingDebris>(FindObjectsSortMode.None).Length > 0 && Time.time - settledAt < (game.Settings.debrisChainLimit + 1) * (game.Settings.debrisLifetime + game.Settings.debrisLifetimeSpread) + 1)
                yield return null;
            Assert.That(Object.FindObjectsByType<FlyingDebris>(FindObjectsSortMode.None).Length, Is.Zero, "The debris chain must finish and clean up");
            int score = game.BrokenBlocks, explosions = game.ChainExplosions;
            yield return new WaitForSeconds(.4f);
            Assert.That(game.ChainExplosions, Is.EqualTo(explosions));
            Assert.That(game.BrokenBlocks, Is.EqualTo(score));
        }

        [UnityTest]
        public IEnumerator DebrisEvictionCannotCancelAnAlreadyPrimedChain()
        {
            StartIsolated();
            int originalLimit = game.Settings.maxDebris;
            game.Settings.maxDebris = 128;
            try
            {
                for (int i = 0; i < 4; i++) game.World.Explosives[i].gameObject.SetActive(true);
                var first = game.World.Explosives[0]; game.Blast(first.transform.position, .8f, 0);
                foreach (var building in game.World.Buildings.GetRange(0, 5)) Collapse(building);
                yield return null;
                Assert.That(game.World.Buildings[0].Blocks.Exists(x => x == null), Is.True, "The fixture must actually evict old debris");
                Assert.That(first != null && first.Primed, Is.True, "A primed barrel must survive the normal debris eviction budget until it detonates");
                yield return new WaitForSeconds(.9f);
                Assert.That(game.ChainExplosions, Is.GreaterThanOrEqualTo(3));
            }
            finally { game.Settings.maxDebris = originalLimit; }
        }

        [UnityTest]
        public IEnumerator FallingPhysicalDebrisIgnitesAnExplosiveAndPauseFreezesItsFuse()
        {
            StartIsolated(); var explosive = game.World.Explosives[0]; explosive.gameObject.SetActive(true);
            var fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fragment.transform.position = explosive.transform.position + Vector3.up * 6;
            fragment.transform.localScale = Vector3.one * .8f;
            var block = fragment.AddComponent<DestructibleBlock>(); block.Initialize(game);
            block.Break(fragment.transform.position, 0);
            for (int i = 0; i < 90 && !explosive.Primed; i++) yield return new WaitForFixedUpdate();
            Assert.That(explosive.Primed, Is.True);
            game.TogglePause(); int count = game.ChainExplosions;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(game.ChainExplosions, Is.EqualTo(count));
            game.TogglePause(); yield return new WaitForSeconds(.3f);
            Assert.That(game.ChainExplosions, Is.GreaterThan(count));
        }
    }
}
