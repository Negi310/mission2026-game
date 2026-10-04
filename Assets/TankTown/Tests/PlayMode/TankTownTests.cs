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
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
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
            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();
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
            Assert.That(game.State, Is.EqualTo(RoundState.Playing));
            Assert.That(game.Remaining, Is.InRange(179.5f, 180f), "Retry starts immediately, so its first rendered frame already consumes time");
            Assert.That(game.BrokenBlocks, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RealShellHitsBuildingAndCrushablePropsBreakOnContact()
        {
            var body = game.Tank.GetComponent<Rigidbody>();
            var building = game.World.Buildings[0];
            body.position = building.transform.position + new Vector3(-4.6f, 1, -building.Width / 2 - 6);
            body.rotation = Quaternion.identity;
            yield return new WaitForFixedUpdate();
            game.Tank.Fire(0);
            yield return new WaitForSeconds(.25f);
            Assert.That(building.DetachedParts, Is.GreaterThan(0), "A swept cannonball must break the directly hit part immediately");
            Assert.That(building.Collapsed, Is.False, "The first real shot must leave the building standing");
            var crate = System.Array.Find(Object.FindObjectsByType<DestructibleBlock>(FindObjectsSortMode.None), x => x.name == "Crushable crate");
            Assert.That(crate, Is.Not.Null);
            body.position = game.World.GroundPoint(new Vector3(crate.transform.position.x, 0, crate.transform.position.z - 3), game.Settings.rideHeight);
            body.rotation = Quaternion.identity;
            game.Tank.transform.SetPositionAndRotation(body.position, body.rotation);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.55f);
            Assert.That(crate.Detached, Is.True);
        }

        [UnityTest]
        public IEnumerator SolidBuildingCollisionTriggersRespawn()
        {
            var body = game.Tank.GetComponent<Rigidbody>();
            body.position = game.World.Buildings[0].transform.position + new Vector3(-4.6f, 1, -game.World.Buildings[0].Width / 2 - 4);
            body.rotation = Quaternion.identity;
            yield return new WaitForSeconds(1.3f);
            Assert.That(game.Crashes, Is.EqualTo(1));
            Assert.That(game.Tank.Respawning, Is.True);
        }
        [UnityTest]
        public IEnumerator ExpandedBuildingsAreHollowAndFacadesDoNotCarryTheFrame()
        {
            Assert.That(game.World.HalfExtent, Is.EqualTo(134));
            Assert.That(game.World.Buildings.Count, Is.EqualTo(33));
            Assert.That(game.World.ParkCount, Is.EqualTo(3));
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
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.That(debrisBody.linearVelocity.z, Is.GreaterThan(0), "Incoming debris must be deflected by an actual contact");
            Assert.That(tankBody.position.z - start.z, Is.GreaterThan(.3f));
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
            for (int i = 0; i < 100; i++) yield return new WaitForFixedUpdate();
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
        public IEnumerator FacadeBlastCracksNearbyThickSupportsWithoutDamagingRemoteOnes()
        {
            Assert.That(game.World.RoadWidth, Is.EqualTo(16));
            var building = game.World.Buildings[0];
            var pillar = building.Blocks.Find(x => x.Foundation && x.Column == 0);
            Assert.That(pillar.transform.localScale.x, Is.EqualTo(1.8f).Within(.01f));
            game.Blast(pillar.transform.position + Vector3.right * 6);
            Assert.That(pillar.Detached, Is.False, "One facade hit must leave the supporting post standing");
            Assert.That(pillar.Durability01, Is.InRange(.67f, .99f), "A nearby blast must cause distance-dependent cracks");
            Assert.That(building.Blocks.Find(x => x.Foundation && x.Column == 1).Durability01, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TreeCrownContactImmediatelyPushesTheWholeTree()
        {
            var tree = System.Array.Find(Object.FindObjectsByType<DestructibleBlock>(FindObjectsSortMode.None), x => x.name == "Tree" && x.transform.position.x < -60 && x.transform.position.z < -60);
            var tankBody = game.Tank.GetComponent<Rigidbody>();
            tankBody.position = tree.transform.position + new Vector3(2.2f, game.Settings.rideHeight, -3.8f);
            tankBody.rotation = Quaternion.identity;
            game.Tank.transform.SetPositionAndRotation(tankBody.position, tankBody.rotation);
            Physics.SyncTransforms();
            // This lateral offset misses the narrow trunk and contacts only the crown.
            for (int i = 0; i < 50; i++) yield return new WaitForFixedUpdate();
            Assert.That(tree.Detached, Is.True);
            Assert.That(tree.GetComponent<Rigidbody>().linearVelocity.z, Is.GreaterThan(0));
            Assert.That(tree.transform.Find("Tree crown").gameObject.layer, Is.EqualTo(TownPhysics.DebrisLayer));
            Assert.That(game.Crashes, Is.Zero);
            Assert.That(Quaternion.Angle(tankBody.rotation, Quaternion.identity), Is.LessThan(.2f));
        }

        [UnityTest]
        public IEnumerator CameraFramesTheTankFromBehindAndAimsWithTheTurret()
        {
            game.CameraRig.Snap();
            var body = game.Tank.GetComponent<Rigidbody>();
            Assert.That(Vector3.Distance(game.CameraRig.transform.position, body.position), Is.InRange(9.3f, 9.7f));
            Assert.That(Vector3.Dot(game.CameraRig.transform.position - body.position, -game.Tank.Turret.forward), Is.InRange(8.8f, 9.2f));
            Assert.That(game.CameraRig.transform.position.y - body.position.y, Is.InRange(2.8f, 3.2f));
            Assert.That(Vector3.Angle(game.CameraRig.transform.forward, Vector3.ProjectOnPlane(game.Tank.Turret.forward, Vector3.up)), Is.LessThan(.01f));
            Assert.That(Vector3.Angle(game.CameraRig.transform.up, Vector3.up), Is.LessThan(.01f));
            Assert.That(Camera.main.fieldOfView, Is.EqualTo(72));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AccelerationAndSteeringPreserveMomentum()
        {
            var body = game.Tank.GetComponent<Rigidbody>();
            game.Input.useHardware = true; game.Input.SetWheel(0);
            yield return null;
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Assert.That(game.Tank.ActualSpeed, Is.LessThan(game.Stats.Speed * .2f), "Starting must accelerate instead of jumping to full speed");
            for (int i = 0; i < 47; i++) yield return new WaitForFixedUpdate();
            Assert.That(game.Tank.ActualSpeed, Is.InRange(9, 11));
            game.Input.SetWheel(1); yield return null;
            for (int i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
            Assert.That(Mathf.Abs(Vector3.Dot(body.linearVelocity, body.rotation * Vector3.right)), Is.GreaterThan(.1f), "A turn must retain some lateral momentum");
            float beforeRelease = body.rotation.eulerAngles.y;
            game.Input.SetWheel(0); yield return null; yield return new WaitForFixedUpdate();
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(beforeRelease, body.rotation.eulerAngles.y)), Is.GreaterThan(.2f), "The steering wheel must return smoothly");
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
            float straight = body.rotation.eulerAngles.y;
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(straight, body.rotation.eulerAngles.y)), Is.LessThan(.4f), "The car must hold its heading after steering returns; ground-following pitch and roll are allowed");
        }

        [UnityTest]
        public IEnumerator VehicleClimbsAndFollowsTheRealRoadSurface()
        {
            var body = game.Tank.GetComponent<Rigidbody>();
            game.Input.useHardware = true; game.Input.SetWheel(0);
            body.position = game.World.GroundPoint(new Vector3(0, 0, 8), game.Settings.rideHeight);
            body.rotation = Quaternion.identity; body.linearVelocity = Vector3.zero;
            game.Tank.transform.SetPositionAndRotation(body.position, body.rotation); Physics.SyncTransforms();
            float startHeight = body.position.y, maxPitch = 0;
            yield return null;
            for (int i = 0; i < 200; i++)
            {
                yield return new WaitForFixedUpdate();
                maxPitch = Mathf.Max(maxPitch, Mathf.Abs((body.rotation * Vector3.forward).y));
            }
            Assert.That(body.position.z, Is.GreaterThan(30));
            Assert.That(body.position.y, Is.GreaterThan(startHeight + 2));
            Assert.That(maxPitch, Is.GreaterThan(.1f), "The tank must pitch upward on a slope");
            Assert.That(Physics.Raycast(body.position + Vector3.up, Vector3.down, out var ground, 5, TownPhysics.GroundMask), Is.True);
            Assert.That(body.position.y - ground.point.y, Is.EqualTo(game.Settings.rideHeight).Within(.25f));
            Assert.That(game.Crashes, Is.Zero);
        }

        [UnityTest]
        public IEnumerator VehicleCrossesSteepRoadAndItsCrestWithoutStalling()
        {
            yield return DriveTerrainRoute(new Vector3(-80, 0, 6), 0, 260, Vector3.forward, 35);
        }

        [UnityTest]
        public IEnumerator VehicleClimbsThePlazaShoulderWithoutStalling()
        {
            yield return DriveTerrainRoute(new Vector3(5, 0, 11), 90, 170, Vector3.right, 18);
        }

        [UnityTest]
        public IEnumerator VehicleDescendsThePlazaShoulderWithoutStalling()
        {
            yield return DriveTerrainRoute(new Vector3(23, 0, 11), 270, 140, Vector3.left, 15);
        }

        [UnityTest]
        public IEnumerator VehicleCrossesTheBankAtMaximumSpeedUpgrade()
        {
            for (int i = 0; i < game.Settings.maxUpgradeLevel; i++) game.Stats.Upgrade(UpgradeKind.Speed);
            yield return DriveTerrainRoute(new Vector3(5, 0, 11), 90, 80, Vector3.right, 24, true);
        }

        private IEnumerator DriveTerrainRoute(Vector3 start, float yaw, int steps, Vector3 direction, float minimumDistance, bool atCruise = false)
        {
            var body = game.Tank.GetComponent<Rigidbody>();
            game.Input.useHardware = true; game.Input.SetWheel(0);
            body.position = game.World.GroundPoint(start, game.Settings.rideHeight);
            body.rotation = Quaternion.Euler(0, yaw, 0); body.linearVelocity = atCruise ? direction * game.Stats.Speed : Vector3.zero;
            game.Tank.transform.SetPositionAndRotation(body.position, body.rotation); Physics.SyncTransforms();
            var origin = body.position;
            yield return null;
            // Finish after the terrain under test; faster settings must not send this route into the next building.
            for (int i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
                if (Vector3.Dot(body.position - origin, direction) > minimumDistance + 2 && game.Tank.ActualSpeed >= game.Stats.Speed * .85f) break;
            }
            Assert.That(game.Crashes, Is.Zero, "Ground alone must not cause a crash");
            Assert.That(Vector3.Dot(body.position - origin, direction), Is.GreaterThan(minimumDistance), $"Terrain blocked the vehicle at {body.position}, speed={game.Tank.ActualSpeed:0.00}");
            Assert.That(game.Tank.ActualSpeed, Is.GreaterThan(game.Stats.Speed * .75f), "The vehicle must recover cruising speed after the slope");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yaw, body.rotation.eulerAngles.y)), Is.LessThan(.5f), "Ground contacts must not steer the vehicle");
        }

        [UnityTest]
        public IEnumerator ElevatedCannonLaunchesAlongTheVisibleBarrel()
        {
            var body = game.Tank.GetComponent<Rigidbody>();
            body.position = game.World.GroundPoint(new Vector3(0, 0, -60), game.Settings.rideHeight);
            body.rotation = Quaternion.Euler(0, 35, 0); body.linearVelocity = Vector3.zero;
            game.Tank.transform.SetPositionAndRotation(body.position, body.rotation);
            game.Tank.Visual.localRotation = Quaternion.identity;
            Physics.SyncTransforms();
            var barrel = game.Tank.Turret.Find("Fixed cannon pivot/Fixed cannon");
            var direction = game.Tank.Muzzle.forward;
            var origin = game.Tank.Muzzle.position;
            float elevation = Mathf.Asin(Vector3.Dot(direction, game.Tank.transform.up)) * Mathf.Rad2Deg;
            Assert.That(elevation, Is.EqualTo(4).Within(.01f));
            Assert.That(Vector3.Angle(barrel.forward, direction), Is.LessThan(.01f));
            game.Tank.Fire(0);
            var shell = Object.FindFirstObjectByType<TankShell>();
            Assert.That(shell, Is.Not.Null);
            yield return null;
            var travel = shell.transform.position - origin;
            Assert.That(travel.y, Is.GreaterThan(.01f), "The actual shell must rise above its launch point");
            Assert.That(Vector3.Angle(travel, direction), Is.LessThan(.05f), "The shell must follow the barrel rather than the hull forward vector");
        }

        [UnityTest]
        public IEnumerator VariedPlotsAndPickupsStandOnTheElevatedGround()
        {
            var widths = new System.Collections.Generic.HashSet<float>();
            var floors = new System.Collections.Generic.HashSet<int>();
            float min = float.MaxValue, max = float.MinValue;
            foreach (var building in game.World.Buildings)
            {
                var p = building.transform.position; widths.Add(building.Width); floors.Add(building.Floors);
                min = Mathf.Min(min, p.y); max = Mathf.Max(max, p.y);
                Assert.That(p.y, Is.EqualTo(game.World.Terrain.HeightAt(p)).Within(.01f));
                foreach (var post in building.Blocks.FindAll(x => x.Foundation))
                    Assert.That(game.World.Terrain.HeightAt(post.transform.position), Is.EqualTo(p.y).Within(.01f), "Foundations must lie on a flat plot");
            }
            Assert.That(max - min, Is.GreaterThan(7));
            Assert.That(widths.Count, Is.GreaterThanOrEqualTo(3)); Assert.That(floors.Count, Is.GreaterThanOrEqualTo(4));
            foreach (var pickup in game.World.Pickups)
                Assert.That(pickup.transform.position.y - game.World.Terrain.HeightAt(pickup.transform.position), Is.EqualTo(1.2f).Within(.3f));
            var p1 = new Vector3(-80, 0, 20);
            Assert.That(Physics.Raycast(p1 + Vector3.up * 20, Vector3.down, out var hit, 30, TownPhysics.GroundMask), Is.True);
            Assert.That(hit.point.y, Is.EqualTo(game.World.Terrain.HeightAt(p1)).Within(.1f), "The rendered ramps must also be physical terrain");
            Assert.That(game.World.Terrain.HeightAt(new Vector3(0, 0, 39.99f)), Is.EqualTo(game.World.Terrain.HeightAt(new Vector3(0, 0, 40.01f))).Within(.01f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SlantedTerrainCannotSteerTheTankWithoutInput()
        {
            var body = game.Tank.GetComponent<Rigidbody>();
            game.Input.useHardware = true; game.Input.SetWheel(0);
            body.position = game.World.GroundPoint(new Vector3(8.1f, 0, 18), game.Settings.rideHeight);
            body.rotation = Quaternion.identity; body.linearVelocity = Vector3.zero;
            game.Tank.transform.SetPositionAndRotation(body.position, body.rotation); Physics.SyncTransforms();
            yield return null;
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, body.rotation.eulerAngles.y)), Is.LessThan(.4f), "Following a diagonal ground normal must not change heading");
            Assert.That(game.Crashes, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PlazaFountainHasAMatchingColliderAndIsCrushable()
        {
            var fountain = System.Array.Find(Object.FindObjectsByType<DestructibleBlock>(FindObjectsSortMode.None), x => x.name == "Crushable fountain" && x.transform.position.x < 0);
            var collider = fountain.GetComponent<MeshCollider>();
            Assert.That(collider.convex, Is.True);
            Physics.SyncTransforms();
            Assert.That(collider.bounds.size.y, Is.EqualTo(1).Within(.1f), "The low fountain must not use an oversized spherical collision shape");
            var body = game.Tank.GetComponent<Rigidbody>();
            body.position = game.World.GroundPoint(fountain.transform.position + Vector3.back * 4.3f, game.Settings.rideHeight);
            body.rotation = Quaternion.identity; body.linearVelocity = Vector3.zero;
            game.Tank.transform.SetPositionAndRotation(body.position, body.rotation); Physics.SyncTransforms();
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();
            Assert.That(fountain.Detached, Is.True);
            Assert.That(fountain.GetComponent<Rigidbody>().linearVelocity.z, Is.GreaterThan(0));
            Assert.That(game.Crashes, Is.Zero);
        }
    }
}
