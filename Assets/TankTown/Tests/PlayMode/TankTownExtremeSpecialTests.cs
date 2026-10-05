using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TankTown.Tests
{
    public sealed class TankTownExtremeSpecialTests
    {
        private TankTownGame game;
        private float captureStep;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            captureStep = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60;
            SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>(); game.StartOrRestart(); game.Input.useHardware = true;
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var person in game.World.Pedestrians) person.gameObject.SetActive(false);
            foreach (var pickup in game.World.Pickups) pickup.gameObject.SetActive(false);
            foreach (var pickup in game.World.ItemPickups) pickup.gameObject.SetActive(false);
        }
        [UnityTearDown] public IEnumerator TearDown() { Time.timeScale = 1; Time.captureDeltaTime = captureStep; yield return null; }
        private TankPickup Upgrade(Vector3 point)
        {
            var root = new GameObject("Magnet upgrade witness"); root.transform.SetParent(game.World.transform); root.transform.position = point;
            var pickup = root.AddComponent<TankPickup>(); pickup.Initialize(game, UpgradeKind.Power, game.World.Icons.Material(UpgradeKind.Power)); game.World.Pickups.Add(pickup); return pickup;
        }
        private ItemPickup Special(Vector3 point, ItemKind kind)
        {
            var root = new GameObject("Magnet special witness"); root.transform.SetParent(game.World.transform); root.transform.position = point;
            var pickup = root.AddComponent<ItemPickup>(); pickup.Initialize(game, kind, game.World.SpecialIcons.Material(kind)); game.World.ItemPickups.Add(pickup); return pickup;
        }
        internal static ChainExplosive Explosive(TankTownGame game, Vector3 point, float radius = 8.5f, bool fuel = false)
        {
            var root = game.World.Box("Missile explosive witness", point, Vector3.one * 2, game.World.ShotMaterial);
            var block = root.AddComponent<DestructibleBlock>(); block.Initialize(game);
            var explosive = root.AddComponent<ChainExplosive>(); explosive.Initialize(game, radius, fuel); game.World.Explosives.Add(explosive); return explosive;
        }
        private DestructibleBlock Witness(Vector3 point)
        {
            var root = game.World.Box("Chain strength witness", point, Vector3.one, game.World.ShotMaterial);
            var block = root.AddComponent<DestructibleBlock>(); block.Initialize(game); return block;
        }
        [UnityTest]
        public IEnumerator BombExplodesOnWallsPillarsAndSlabsBeforeGroundContact()
        {
            int index = 0;
            foreach (var kind in new[] { BuildingPart.Wall, BuildingPart.Pillar, BuildingPart.Slab })
            {
                Assert.That(game.Items.Receive(ItemKind.GigantBomb), Is.True);
                Assert.That(game.Items.UseHeld(), Is.True);
                yield return null;
                var bomb = Object.FindFirstObjectByType<GigantBomb>(); var body = bomb.GetComponent<Rigidbody>();
                body.position = new Vector3(0, 80 + index++ * 100, -65); body.linearVelocity = Vector3.forward * 90;
                var root = game.World.Box("Bomb contact " + kind, body.position + Vector3.forward * 12, new Vector3(9, 12, 1), game.World.ShotMaterial);
                var target = root.AddComponent<DestructibleBlock>(); target.Kind = kind; target.Initialize(game);
                var remote = Witness(body.position + Vector3.right * 45); Physics.SyncTransforms();
                for (int i = 0; i < 20 && !bomb.Detonated; i++) yield return new WaitForFixedUpdate();
                Assert.That(bomb.Detonated, Is.True, kind + " must detonate on the first physical contact");
                Assert.That(bomb.StructureImpact, Is.True); Assert.That(bomb.GroundImpact, Is.False);
                Assert.That(bomb.Contacts, Is.EqualTo(1), "Multiple colliders must not detonate the bomb twice");
                Assert.That(target.Detached && remote.Detached, Is.True);
                Assert.That(game.Items.Weapon, Is.EqualTo(ItemKind.None));
                yield return null;
            }
        }
        [UnityTest]
        public IEnumerator MagnetCollectsMultipleKindsWhileDrivingFasterAndLeavesOutsideItsRadius()
        {
            Vector3 origin = game.Tank.transform.position;
            var upgrade = Upgrade(origin + Vector3.forward * 38 + Vector3.right * 10);
            var special = Special(origin + Vector3.forward * 35 + Vector3.up * 15, ItemKind.MegaBeam);
            var automatic = Special(origin + Vector3.forward * 28 + Vector3.left * 6, ItemKind.HighSpeed);
            var far = Special(origin + Vector3.right * 75, ItemKind.Missiles); Vector3 farPosition = far.transform.position;
            game.Items.Receive(ItemKind.HighSpeed); game.Items.Receive(ItemKind.Magnet);
            game.Tank.GetComponent<Rigidbody>().isKinematic = false; game.Tank.enabled = true;
            yield return new WaitForSeconds(.7f);
            Assert.That(upgrade.Collected && special.Collected && automatic.Collected, Is.True);
            Assert.That(game.Items.Held, Is.EqualTo(ItemKind.MegaBeam));
            Assert.That(game.Items.Remaining(ItemKind.HighSpeed), Is.GreaterThan(5));
            Assert.That(game.Items.MagnetPullSpeed, Is.GreaterThan(game.Tank.ActualSpeed * 2));
            Assert.That(Vector3.Distance(game.Tank.transform.position, origin), Is.GreaterThan(3));
            Assert.That(far.Collected, Is.False); Assert.That(far.transform.position, Is.EqualTo(farPosition));
        }
        [UnityTest]
        public IEnumerator MagnetKeepsBuildingUnlockRulesAndRestoresDroppedRewardPhysicsWithAFullSlot()
        {
            var locked = game.World.Buildings[0].Reward; locked.gameObject.SetActive(true);
            Vector3 lockedPosition = locked.transform.position;
            var building = game.World.Buildings.First(x => x.Reward != locked && !ItemIcons.Automatic(x.Reward.Kind));
            var reward = building.Reward; reward.gameObject.SetActive(true);
            foreach (var support in building.Blocks.Where(x => x.Foundation).ToArray()) support.Break(support.transform.position, 0);
            building.ReleaseUnsupported(building.transform.position, 0); yield return new WaitForFixedUpdate();
            Assert.That(reward.Available, Is.True);
            reward.DropBody.position = game.Tank.transform.position + Vector3.right * 30 + Vector3.up * 12;
            game.Items.Receive(ItemKind.GigantBomb); game.Items.Receive(ItemKind.Magnet);
            yield return new WaitForSeconds(.4f);
            Assert.That(reward.Collected, Is.False, "A full slot must not discard the reward");
            Assert.That(reward.DropBody.isKinematic, Is.True);
            Assert.That(Vector3.Distance(reward.transform.position, game.Tank.transform.position), Is.LessThan(3));
            Assert.That(locked.Available, Is.False); Assert.That(locked.transform.position, Is.EqualTo(lockedPosition));
            game.Items.OnTankCrash(); yield return new WaitForSeconds(.15f);
            Assert.That(reward.DropBody.isKinematic, Is.False, "A cancelled magnet must restore gravity and collision");
        }
        [UnityTest]
        public IEnumerator MissileChoosesVisibleExplosivesAndMakesAPoweredTrackedHit()
        {
            var point = game.Tank.transform.position + Vector3.forward * 24;
            var explosive = Explosive(game, game.World.GroundPoint(point, 1.5f), fuel: true, radius: 24);
            var behind = Explosive(game, game.Tank.transform.position - Vector3.forward * 12);
            Physics.SyncTransforms(); game.CameraRig.Snap();
            game.Items.Receive(ItemKind.Missiles);
            var missiles = Object.FindObjectsByType<ItemMissile>(FindObjectsSortMode.None);
            Assert.That(missiles.All(x => x.TrackingTarget == explosive.GetComponent<TownMovingTarget>()), Is.True);
            for (int i = 0; i < 140 && !explosive.Primed; i++) yield return new WaitForFixedUpdate();
            Assert.That(explosive.Primed, Is.True); Assert.That(explosive.PowerMultiplier, Is.EqualTo(game.Settings.missileChainPowerMultiplier));
            Assert.That(game.Items.MissileTrackedStrikes, Is.GreaterThan(0));
            Assert.That(behind.Primed, Is.False, "Acquisition must still exclude objects behind the tank");
        }
        [UnityTest]
        public IEnumerator PoweredMissileChainPropagatesItsBonusWithoutMultiplyingAtEveryGeneration()
        {
            Vector3 origin = new Vector3(190, 15, -45);
            var first = Explosive(game, origin, 10);
            var second = Explosive(game, origin + Vector3.right * 13, 10);
            var witness = Witness(origin + Vector3.right * 26);
            var ordinary = Explosive(game, origin + Vector3.forward * 70, 10);
            var remote = Witness(ordinary.transform.position + Vector3.right * 13);
            Physics.SyncTransforms(); game.MissileBlast(origin);
            Assert.That(game.ExplosionPowerMultiplier, Is.EqualTo(1), "The missile bonus must not leak into other shots");
            for (int i = 0; i < 30 && !second.Primed; i++) yield return new WaitForFixedUpdate();
            Assert.That(second.Primed, Is.True);
            Assert.That(second.PowerMultiplier, Is.EqualTo(first.PowerMultiplier));
            Assert.That(second.Generation, Is.EqualTo(2));
            yield return new WaitForSeconds(.25f);
            Assert.That(witness.Detached, Is.True, "The strengthened second generation must reach beyond the old radius");
            game.Blast(ordinary.transform.position); yield return new WaitForSeconds(.3f);
            Assert.That(ordinary.PowerMultiplier, Is.EqualTo(1)); Assert.That(remote.Detached, Is.False);
        }
    }
}
