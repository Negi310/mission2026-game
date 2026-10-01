using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TankTown.Tests
{
    public sealed class TankTownTests
    {
        private TankTownGame game;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("TankTown");
            yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>();
            Assert.That(game, Is.Not.Null);
            game.StartOrRestart();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AutoDriveAndSteeringMoveTank()
        {
            var body = game.Tank.GetComponent<Rigidbody>();
            var start = body.position;
            game.Input.useHardware = true;
            game.Input.SetWheel(1);
            // Poll input once, then measure simulated physics steps rather than the
            // interpolated render pose after a wall-clock wait during scene warm-up.
            yield return null;
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(start, body.position), Is.GreaterThan(1));
            Assert.That(Quaternion.Angle(Quaternion.identity, body.rotation), Is.GreaterThan(10));
        }

        [UnityTest]
        public IEnumerator BlastReleasesSupportsAndScoresOnlyOnce()
        {
            var building = game.World.Buildings[0];
            var foundations = building.Blocks.FindAll(x => x.Foundation);
            var high = building.Blocks.Find(x => x.Kind == BuildingPart.Pillar && x.Level == 2);
            var low = foundations[0];
            var point = low.transform.position;
            foreach (var support in foundations) support.Break(support.transform.position, 0);
            building.ReleaseUnsupported(point, game.Stats.BlastImpulse);
            Assert.That(low.Detached, Is.True);
            Assert.That(high.Detached, Is.True);
            Assert.That(high.GetComponent<Rigidbody>(), Is.Not.Null);
            int score = game.BrokenBlocks;
            low.Break(point, 5);
            Assert.That(game.BrokenBlocks, Is.EqualTo(score));
            Assert.That(game.Destruction, Is.InRange(.01f, 100));
            yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator PressureNeedsReleaseAndTracksPadsIndependently()
        {
            int left = 0, right = 0;
            game.Input.PadPressed += pad => { if (pad == 0) left++; else right++; };
            game.Input.SetPressure(0, 1);
            game.Input.SetPressure(0, 1);
            game.Input.SetPressure(1, 1);
            Assert.That(left, Is.EqualTo(1));
            Assert.That(right, Is.EqualTo(1));
            game.Input.SetPressure(0, 0);
            game.Input.SetPressure(0, 1);
            Assert.That(left, Is.EqualTo(2));
            Assert.That(Object.FindObjectsByType<TankShell>(FindObjectsSortMode.None).Length, Is.EqualTo(1), "Cooldown must reject duplicate fire requests");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CrashPenalizesOnceAndRespawns()
        {
            float before = game.Remaining;
            game.Tank.Explode();
            game.Tank.Explode();
            Assert.That(game.Crashes, Is.EqualTo(1));
            Assert.That(game.Remaining, Is.EqualTo(before - game.Settings.respawnPenalty).Within(.01f));
            yield return new WaitForSeconds(game.Settings.respawnDelay + .15f);
            Assert.That(game.Tank.Respawning, Is.False);
            Assert.That(game.Tank.Visual.gameObject.activeSelf, Is.True);
            Assert.That(Mathf.Abs(game.Tank.transform.position.x), Is.LessThan(2));
        }

        [UnityTest]
        public IEnumerator PauseAndTimeoutFreezeGameplayAndRestartResetsCity()
        {
            game.TogglePause();
            float before = game.Remaining;
            game.AdvanceTime(20);
            Assert.That(game.Remaining, Is.EqualTo(before));
            game.TogglePause();
            game.AdvanceTime(1000);
            Assert.That(game.State, Is.EqualTo(RoundState.Results));
            Assert.That(game.Remaining, Is.Zero);
            game.Blast(new Vector3(12, 1, 12));
            Assert.That(game.BrokenBlocks, Is.Zero);
            game.StartOrRestart();
            yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>();
            Assert.That(game.State, Is.EqualTo(RoundState.Ready));
            Assert.That(game.Remaining, Is.EqualTo(180));
            Assert.That(game.BrokenBlocks, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RealShellHitsBuildingAndCrushablePropsBreakOnContact()
        {
            var body = game.Tank.GetComponent<Rigidbody>();
            var building = game.World.Buildings[0];
            body.position = building.transform.position + new Vector3(-4.6f, 1, -game.World.BuildingWidth / 2 - 6);
            body.rotation = Quaternion.identity;
            yield return new WaitForFixedUpdate();
            game.Tank.Fire(0);
            yield return new WaitForSeconds(.25f);
            Assert.That(game.BrokenBlocks, Is.GreaterThan(0), "A swept cannonball must hit and destroy the building ahead");
            var crate = System.Array.Find(Object.FindObjectsByType<DestructibleBlock>(FindObjectsSortMode.None), x => x.name == "Crushable crate");
            Assert.That(crate, Is.Not.Null);
            body.position = new Vector3(crate.transform.position.x, 1, crate.transform.position.z - 3);
            body.rotation = Quaternion.identity;
            yield return new WaitForSeconds(.3f);
            Assert.That(crate.Detached, Is.True);
        }

        [UnityTest]
        public IEnumerator SolidBuildingCollisionTriggersRespawn()
        {
            var body = game.Tank.GetComponent<Rigidbody>();
            body.position = game.World.Buildings[0].transform.position + new Vector3(-4.6f, 1, -game.World.BuildingWidth / 2 - 4);
            body.rotation = Quaternion.identity;
            yield return new WaitForSeconds(.6f);
            Assert.That(game.Crashes, Is.EqualTo(1));
            Assert.That(game.Tank.Respawning, Is.True);
        }
        [UnityTest]
        public IEnumerator ExpandedBuildingsAreHollowAndFacadesDoNotCarryTheFrame()
        {
            Assert.That(game.World.HalfExtent, Is.EqualTo(134));
            Assert.That(game.World.Buildings.Count, Is.EqualTo(36));
            var building = game.World.Buildings[0];
            Physics.SyncTransforms();
            var interior = Physics.OverlapBox(building.transform.position + Vector3.up * 1.8f, Vector3.one, Quaternion.identity, TownPhysics.StructureMask, QueryTriggerInteraction.Ignore);
            Assert.That(System.Array.Exists(interior, c => c.GetComponent<DestructibleBlock>() != null), Is.False, "The room must contain air, not hidden cubes");
            foreach (var wall in building.Blocks.FindAll(x => x.Kind == BuildingPart.Wall)) wall.Break(wall.transform.position, 0);
            building.ReleaseUnsupported(building.transform.position, 0);
            Assert.That(building.Blocks.FindAll(x => x.Kind == BuildingPart.Pillar).TrueForAll(x => !x.Detached), Is.True, "Removing facade cladding must leave the frame supported");
            yield return null;
        }

        [UnityTest]
        public IEnumerator DebrisPhysicallyContactsTankAndIsPushedAside()
        {
            var fragment = System.Array.Find(Object.FindObjectsByType<DestructibleBlock>(FindObjectsSortMode.None), x => x.name == "Crushable crate");
            fragment.Break(fragment.transform.position, 0);
            var debrisBody = fragment.GetComponent<Rigidbody>();
            var tankBody = game.Tank.GetComponent<Rigidbody>();
            var start = tankBody.position;
            debrisBody.useGravity = false;
            debrisBody.position = start + new Vector3(1.65f, 0, 4);
            debrisBody.linearVelocity = Vector3.back * 20;
            Assert.That(Physics.GetIgnoreLayerCollision(TownPhysics.TankLayer, TownPhysics.DebrisLayer), Is.False);
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.That(debrisBody.linearVelocity.z, Is.GreaterThan(0), "Incoming debris must be deflected by an actual contact");
            Assert.That(tankBody.position.z - start.z, Is.GreaterThan(1.2f));
            Assert.That(Quaternion.Angle(tankBody.rotation, Quaternion.identity), Is.LessThan(.2f), "Off-centre debris must not steer the tank");
            Assert.That(game.Crashes, Is.Zero);
            Assert.That(game.Tank.Respawning, Is.False);
        }

        [UnityTest]
        public IEnumerator PickupTriggerAppliesOnceAndEveryStatChanges()
        {
            float speed = game.Stats.Speed, power = game.Stats.BlastImpulse, cooldown = game.Stats.ShotCooldown, turn = game.Stats.TurnSpeed;
            var body = game.Tank.GetComponent<Rigidbody>();
            for (int i = 0; i < 4; i++)
            {
                var kind = (UpgradeKind)i;
                var pickup = game.World.Pickups.Find(x => x.Kind == kind && !x.Collected);
                body.position = pickup.transform.position;
                body.rotation = Quaternion.identity;
                for (int step = 0; step < 3; step++) yield return new WaitForFixedUpdate();
                Assert.That(pickup.Collected, Is.True);
                int level = game.Stats.Level(kind);
                Assert.That(pickup.Collect(), Is.False);
                Assert.That(game.Stats.Level(kind), Is.EqualTo(level));
            }
            Assert.That(game.Stats.Speed, Is.GreaterThan(speed));
            Assert.That(game.Stats.BlastImpulse, Is.GreaterThan(power));
            Assert.That(game.Stats.ShotCooldown, Is.LessThan(cooldown));
            Assert.That(game.Stats.TurnSpeed, Is.GreaterThan(turn));
            body.position = game.World.SpawnPosition;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.magnitude, Is.EqualTo(game.Stats.Speed).Within(.1f));
        }

        [UnityTest]
        public IEnumerator UpgradesAreCappedSurviveCrashAndResetOnNewRound()
        {
            for (int i = 0; i < 30; i++) game.Stats.Upgrade(UpgradeKind.Rapid);
            Assert.That(game.Stats.Level(UpgradeKind.Rapid), Is.EqualTo(game.Settings.maxUpgradeLevel));
            Assert.That(game.Stats.ShotCooldown, Is.GreaterThanOrEqualTo(.12f));
            game.Tank.Explode();
            yield return new WaitForSeconds(game.Settings.respawnDelay + .1f);
            Assert.That(game.Stats.Level(UpgradeKind.Rapid), Is.EqualTo(game.Settings.maxUpgradeLevel));
            game.AdvanceTime(1000);
            game.StartOrRestart();
            yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>();
            Assert.That(game.Stats.Level(UpgradeKind.Rapid), Is.Zero);
        }
        [UnityTest]
        public IEnumerator UpgradedCannonAcceptsShotsSoonerAndReachesFurther()
        {
            for (int i = 0; i < game.Settings.maxUpgradeLevel; i++) game.Stats.Upgrade(UpgradeKind.Rapid);
            game.Tank.Fire(0);
            yield return new WaitForSeconds(game.Stats.ShotCooldown + .025f);
            game.Tank.Fire(1);
            Assert.That(Object.FindObjectsByType<TankShell>(FindObjectsSortMode.None).Length, Is.EqualTo(2), "Both shots must be accepted within the old cooldown");
            var crate = System.Array.Find(Object.FindObjectsByType<DestructibleBlock>(FindObjectsSortMode.None), x => x.name == "Crushable crate");
            var point = crate.transform.position + Vector3.right * (game.Settings.blastRadius + 1.5f);
            game.Blast(point);
            Assert.That(crate.Detached, Is.False);
            for (int i = 0; i < game.Settings.maxUpgradeLevel; i++) game.Stats.Upgrade(UpgradeKind.Power);
            game.Blast(point);
            Assert.That(crate.Detached, Is.True, "The power upgrade must expand the real destruction query");
            yield return null;
        }
        [UnityTest]
        public IEnumerator ThreeSupportsStayBalancedButAdjacentPairCollapses()
        {
            var building = game.World.Buildings[0];
            var posts = building.Blocks.FindAll(x => x.Foundation);
            posts[0].Break(posts[0].transform.position, 0);
            building.ReleaseUnsupported(posts[0].transform.position, 0);
            Assert.That(building.Collapsed, Is.False, "Three widely separated posts can still balance a symmetric building");
            posts[1].Break(posts[1].transform.position, 0);
            building.ReleaseUnsupported(posts[1].transform.position, 0);
            Assert.That(building.CollapsedFromLevel, Is.Zero);
            Assert.That(building.Blocks.TrueForAll(x => x == null || x.Detached), Is.True, "Two posts on one side must not hold the entire building");
            yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator OneRemainingPillarCannotHoldTheBuilding()
        {
            var building = game.World.Buildings[0];
            var posts = building.Blocks.FindAll(x => x.Foundation);
            for (int i = 0; i < 3; i++) posts[i].Break(posts[i].transform.position, 0);
            building.ReleaseUnsupported(posts[0].transform.position, 0);
            Assert.That(building.Collapsed, Is.True);
            Assert.That(posts[3].Detached, Is.True);
            Assert.That(building.Blocks.Find(x => x.Kind == BuildingPart.Slab && x.Level == 3).GetComponent<Rigidbody>(), Is.Not.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UpperStoreyCanCollapseWhileFoundationStaysIntact()
        {
            var building = game.World.Buildings[0];
            var posts = building.Blocks.FindAll(x => x.Kind == BuildingPart.Pillar && x.Level == 2);
            posts[0].Break(posts[0].transform.position, 0);
            posts[1].Break(posts[1].transform.position, 0);
            building.ReleaseUnsupported(posts[0].transform.position, 0);
            Assert.That(building.CollapsedFromLevel, Is.EqualTo(2));
            Assert.That(building.Blocks.FindAll(x => x.Foundation).TrueForAll(x => !x.Detached), Is.True);
            Assert.That(building.Blocks.FindAll(x => x.Level >= 2).TrueForAll(x => x.Detached), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OppositeSupportsFallWhenLoadShiftsOutsideTheirFootprint()
        {
            var building = game.World.Buildings[0];
            var posts = building.Blocks.FindAll(x => x.Foundation);
            posts[1].Break(posts[1].transform.position, 0);
            posts[3].Break(posts[3].transform.position, 0);
            building.ReleaseUnsupported(posts[0].transform.position, 0);
            Assert.That(building.Collapsed, Is.False, "Opposite posts may support a centred load");
            foreach (var block in building.Blocks)
                if ((block.Kind == BuildingPart.Slab && block.Column == 1) || (block.Kind == BuildingPart.Wall && block.Column <= 1)) block.Break(block.transform.position, 0);
            building.ReleaseUnsupported(building.transform.position, 0);
            Assert.That(building.Collapsed, Is.True, "The same two posts must fail when the remaining load becomes unbalanced");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThickerPillarsAndLargerBlastHelpHitFacadeSupports()
        {
            Assert.That(game.World.RoadWidth, Is.EqualTo(16));
            var building = game.World.Buildings[0];
            var pillar = building.Blocks.Find(x => x.Foundation && x.Column == 0);
            Assert.That(pillar.transform.localScale.x, Is.EqualTo(1.8f).Within(.01f));
            game.Blast(building.transform.position + new Vector3(-4.6f, 1.72f, -game.World.BuildingWidth / 2));
            Assert.That(pillar.Detached, Is.True, "Hitting a facade panel must reach its nearby supporting post");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TreeCrownContactImmediatelyPushesTheWholeTree()
        {
            var tree = System.Array.Find(Object.FindObjectsByType<DestructibleBlock>(FindObjectsSortMode.None), x => x.name == "Tree");
            var tankBody = game.Tank.GetComponent<Rigidbody>();
            tankBody.position = tree.transform.position + new Vector3(2.2f, 1, -3.8f);
            tankBody.rotation = Quaternion.identity;
            game.Tank.transform.SetPositionAndRotation(tankBody.position, tankBody.rotation);
            Physics.SyncTransforms();
            // This lateral offset misses the narrow trunk and contacts only the crown.
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.That(tree.Detached, Is.True);
            Assert.That(tree.GetComponent<Rigidbody>().linearVelocity.z, Is.GreaterThan(0));
            Assert.That(tree.transform.Find("Tree crown").gameObject.layer, Is.EqualTo(TownPhysics.DebrisLayer));
            Assert.That(game.Crashes, Is.Zero);
            Assert.That(Quaternion.Angle(tankBody.rotation, Quaternion.identity), Is.LessThan(.2f));
        }

        [UnityTest]
        public IEnumerator CameraIsMountedNearTheTurretAndAimsForward()
        {
            game.CameraRig.Snap();
            var body = game.Tank.GetComponent<Rigidbody>();
            Assert.That(Vector3.Distance(game.CameraRig.transform.position, body.position), Is.LessThan(3));
            Assert.That(game.CameraRig.transform.position.y - body.position.y, Is.InRange(1.3f, 2));
            Assert.That(Vector3.Angle(game.CameraRig.transform.forward, body.rotation * Vector3.forward), Is.LessThan(5));
            Assert.That(Camera.main.fieldOfView, Is.EqualTo(72));
            yield return null;
        }
    }
}
