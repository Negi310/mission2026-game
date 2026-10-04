using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TankTown.Tests
{
    public sealed class TankTownDurabilityTests
    {
        private TankTownGame game;
        private float captureStep;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            captureStep = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60;
            SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>(); game.StartOrRestart();
            game.Input.useHardware = true; game.Tank.enabled = false;
            game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var car in game.World.TrafficCars) car.gameObject.SetActive(false);
        }
        [UnityTearDown]
        public IEnumerator TearDown() { Time.timeScale = 1; Time.captureDeltaTime = captureStep; yield return null; }

        internal static void PlaceForLowFacadeShot(TankTownGame game, BlockBuilding building)
        {
            var start = building.transform.position + new Vector3(0, game.Settings.rideHeight, -building.Width / 2 - 15);
            var body = game.Tank.GetComponent<Rigidbody>();
            body.isKinematic = false; game.Tank.Warp(new Pose(start, Quaternion.identity)); body.isKinematic = true;
            body.position = start; game.Tank.transform.position = start; Physics.SyncTransforms(); game.CameraRig.Snap();
        }

        [UnityTest]
        public IEnumerator ThreeRealCannonShotsCollapseApartmentTowerAndShopFromTheirBases()
        {
            foreach (string name in new[] { "Apartment frame", "Tower frame", "Shop frame" })
            {
                var building = game.World.Buildings.First(x => x.name == name);
                PlaceForLowFacadeShot(game, building);
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return null;
                var front = building.Blocks.Where(x => x.Foundation && x.Column < 2).ToArray();
                for (int shot = 1; shot <= 3; shot++)
                {
                    int before = game.Sound.PlayedCount(TownSound.Cannon);
                    game.Tank.Fire(shot % 2);
                    Assert.That(game.Sound.PlayedCount(TownSound.Cannon), Is.EqualTo(before + 1));
                    yield return new WaitForSeconds(.5f);
                    if (shot < 3)
                    {
                        Assert.That(building.Collapsed, Is.False, name + " must stand after " + shot + " shots");
                        Assert.That(building.DetachedParts, Is.Zero, "The first two hits must not secretly score a destroyed structure");
                        foreach (var support in front)
                            Assert.That(support.Durability01, Is.EqualTo(1 - shot / 3f).Within(.001f), "Both front supports must receive each real shot");
                        Assert.That(building.Reward.Available, Is.False);
                    }
                    else
                    {
                        Assert.That(building.CollapsedFromLevel, Is.Zero, name + " must collapse from the base on the third shot");
                        Assert.That(building.DestroyedFraction, Is.EqualTo(1));
                        Assert.That(building.Blocks.All(x => x == null || x.Detached), Is.True);
                        Assert.That(building.Reward.Available, Is.True);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator ACompoundColliderCannotTakeDuplicateBlastDamageAndPauseRejectsDamage()
        {
            var building = game.World.Buildings[0];
            var pillar = building.Blocks.First(x => x.Foundation);
            var child = new GameObject("Extra collider", typeof(BoxCollider));
            child.transform.SetParent(pillar.transform, false); child.GetComponent<BoxCollider>().size = Vector3.one * .8f;
            Physics.SyncTransforms();
            game.DamageSphere(pillar.transform.position, .2f, game.Stats.BlastImpulse);
            Assert.That(pillar.Durability01, Is.EqualTo(2f / 3).Within(.001f));
            game.TogglePause(); game.Blast(pillar.transform.position);
            Assert.That(pillar.Durability01, Is.EqualTo(2f / 3).Within(.001f));
            game.TogglePause();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DamageFeedbackKeepsCameraOcclusionAndPowerUpgradesReduceRequiredShots()
        {
            var building = game.World.Buildings[0];
            var point = building.transform.position + new Vector3(0, 2, -building.Width / 2);
            var pillar = building.Blocks.First(x => x.Foundation);
            var renderer = pillar.GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock(); properties.SetFloat("_CameraVisibility", .4f); renderer.SetPropertyBlock(properties);
            game.Blast(point);
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_StructuralDamage"), Is.GreaterThan(.3f));
            Assert.That(properties.GetFloat("_CameraVisibility"), Is.EqualTo(.4f).Within(.001f));
            var upgraded = game.World.Buildings[1];
            for (int i = 0; i < 6; i++) game.Stats.Upgrade(UpgradeKind.Power);
            var target = upgraded.transform.position + new Vector3(0, 2, -upgraded.Width / 2);
            game.Blast(target); Assert.That(upgraded.Collapsed, Is.False);
            game.Blast(target); Assert.That(upgraded.DestroyedFraction, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FastDebrisDamagesAStandingBuildingOnlyOncePerFragment()
        {
            var pillar = game.World.Buildings[0].Blocks.First(x => x.Foundation);
            Vector3 point = pillar.transform.position;
            var item = game.World.Box("Durability test fragment", point - Vector3.right * 6, Vector3.one * .8f, game.World.ShotMaterial);
            var source = item.AddComponent<DestructibleBlock>(); source.Initialize(game); source.Break(item.transform.position, 0);
            var body = source.GetComponent<Rigidbody>(); body.useGravity = false; body.linearVelocity = Vector3.right * 40;
            Physics.SyncTransforms();
            for (int i = 0; i < 25 && !source.GetComponent<FlyingDebris>().ImpactSpent; i++) yield return new WaitForFixedUpdate();
            Assert.That(source.GetComponent<FlyingDebris>().ImpactSpent, Is.True);
            Assert.That(game.DebrisExplosions, Is.EqualTo(1));
            Assert.That(pillar.Detached, Is.False);
            Assert.That(pillar.Durability01, Is.EqualTo(5f / 6).Within(.001f));
            yield return new WaitForSeconds(.25f);
            Assert.That(game.DebrisExplosions, Is.EqualTo(1), "One fragment cannot repeatedly chip the building on each bounce");
        }
    }
}
