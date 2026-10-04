using System.Collections;
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
            foreach (var support in building.Blocks.FindAll(x => x != null && x.Foundation && x.Column < 2)) support.Break(support.transform.position, 0);
            building.ReleaseUnsupported(building.transform.position, 0);
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
            Assert.That(game.Remaining, Is.EqualTo(180));
            Assert.That(game.Tank.transform.position, Is.EqualTo(position));
            game.Input.PulsePad(1); yield return null;
            Assert.That(game.IsPlaying, Is.True); Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(title.activeSelf, Is.False);
            Assert.That(Object.FindFirstObjectByType<TankShell>(), Is.Null);
            Assert.That(game.Remaining, Is.LessThan(180));
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
        public IEnumerator EveryBuildingHasAnUpperFloorRewardAndAllSixKindsExist()
        {
            Assert.That(game.World.ItemPickups.Count, Is.EqualTo(game.World.Buildings.Count));
            for (int i = 0; i < 6; i++) Assert.That(game.World.ItemPickups.Exists(x => x.Kind == (ItemKind)i), Is.True);
            foreach (var building in game.World.Buildings)
            {
                var reward = building.Reward;
                Assert.That(reward.SourceBuilding, Is.EqualTo(building));
                Assert.That(reward.SupportingFloor.Kind, Is.EqualTo(BuildingPart.Slab));
                Assert.That(reward.SupportingFloor.Level, Is.EqualTo(building.Floors - 1));
                Assert.That(reward.transform.position.y - building.transform.position.y, Is.GreaterThan(building.Floors * game.Settings.floorHeight));
                Assert.That(reward.Available, Is.False); Assert.That(reward.DropBody.isKinematic, Is.True);
                Assert.That(reward.Collect(), Is.False);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator BreakingOnlyTheRewardFloorMakesItFallButDoesNotUnlockIt()
        {
            StartIsolated(); var reward = game.World.Buildings[0].Reward; reward.gameObject.SetActive(true);
            float height = reward.transform.position.y;
            reward.SupportingFloor.Break(reward.SupportingFloor.transform.position, 0);
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.That(reward.Released, Is.True); Assert.That(reward.DropBody.isKinematic, Is.False);
            Assert.That(reward.transform.position.y, Is.LessThan(height - .1f));
            Assert.That(reward.Available, Is.False); Assert.That(reward.Collect(), Is.False);
            Assert.That(game.Items.Held, Is.EqualTo(ItemKind.None));
        }

        [UnityTest]
        public IEnumerator MajorCollapseDropsTheActualRewardToTheGroundAndTankContactCollectsIt()
        {
            StartIsolated(); var building = game.World.Buildings[0]; var reward = building.Reward;
            reward.gameObject.SetActive(true); var original = reward.transform.position;
            Collapse(building);
            Assert.That(building.DestroyedFraction, Is.GreaterThanOrEqualTo(game.Settings.buildingRewardThreshold));
            for (int i = 0; i < 180; i++) yield return new WaitForFixedUpdate();
            Assert.That(reward.Available, Is.True); Assert.That(reward.DropBody.isKinematic, Is.False);
            float ground = game.World.Terrain.HeightAt(reward.transform.position);
            Assert.That(reward.transform.position.y, Is.InRange(ground + .5f, ground + .85f));
            Assert.That(new Vector2(reward.transform.position.x - original.x, reward.transform.position.z - original.z).magnitude, Is.LessThan(.2f));
            Assert.That(reward.Collected, Is.False);
            var body = game.Tank.GetComponent<Rigidbody>();
            body.position = game.World.GroundPoint(reward.transform.position, game.Settings.rideHeight);
            game.Tank.transform.position = body.position; Physics.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return null;
            Assert.That(reward.Collected, Is.True); Assert.That(game.Items.Held, Is.EqualTo(reward.Kind));
            Assert.That(reward.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator DestroyedBuildingProgressSurvivesDebrisCleanupAndFullSlotsLeaveTheReward()
        {
            StartIsolated(); var first = game.World.Buildings[0]; var reward = first.Reward;
            reward.gameObject.SetActive(true);
            foreach (var building in game.World.Buildings.GetRange(0, 5)) Collapse(building);
            yield return new WaitForFixedUpdate(); yield return null;
            Assert.That(first.DestroyedFraction, Is.GreaterThanOrEqualTo(game.Settings.buildingRewardThreshold));
            Assert.That(first.Blocks.Exists(x => x == null), Is.True, "Old debris must actually have been evicted");
            Assert.That(reward.Available, Is.True);
            Assert.That(game.Items.Receive(ItemKind.Missiles), Is.True);
            Assert.That(reward.Collect(), Is.False); Assert.That(reward.Collected, Is.False);
            Assert.That(reward.gameObject.activeSelf, Is.True);
            game.Items.UseHeld();
            Assert.That(reward.Collect(), Is.True);
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
            while (Object.FindObjectsByType<FlyingDebris>(FindObjectsSortMode.None).Length > 0 && Time.time - settledAt < (game.Settings.debrisChainLimit + 1) * game.Settings.debrisLifetime + 1)
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
            for (int i = 0; i < 4; i++) game.World.Explosives[i].gameObject.SetActive(true);
            var first = game.World.Explosives[0]; game.Blast(first.transform.position, .8f, 0);
            foreach (var building in game.World.Buildings.GetRange(0, 5)) Collapse(building);
            yield return null;
            Assert.That(first != null && first.Primed, Is.True, "A primed barrel must survive the normal debris eviction budget until it detonates");
            yield return new WaitForSeconds(.9f);
            Assert.That(game.ChainExplosions, Is.GreaterThanOrEqualTo(3));
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
