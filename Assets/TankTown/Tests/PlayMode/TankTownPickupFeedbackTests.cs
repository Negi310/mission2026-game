using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TankTown.Tests
{
    public sealed class TankTownPickupFeedbackTests
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
            foreach (var block in Object.FindObjectsByType<DestructibleBlock>(FindObjectsSortMode.None)) block.gameObject.SetActive(false);
            foreach (var pickup in game.World.Pickups) pickup.gameObject.SetActive(false);
            foreach (var pickup in game.World.ItemPickups) pickup.gameObject.SetActive(false);
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown() { Time.timeScale = 1; Time.captureDeltaTime = captureStep; yield return null; }
        private TankPickup Pickup(UpgradeKind kind, Vector3 offset)
        {
            var root = new GameObject("Feedback test pickup"); root.transform.SetParent(game.World.transform);
            root.transform.position = game.Tank.transform.position + offset;
            var pickup = root.AddComponent<TankPickup>(); pickup.Initialize(game, kind, game.World.Icons.Material(kind));
            game.World.Pickups.Add(pickup); return pickup;
        }
        [UnityTest]
        public IEnumerator CollectionReusesFieldQuadsAndSeparatesThemAboveMovingTank()
        {
            var pickups = Enumerable.Range(0, 4).Select(i => Pickup((UpgradeKind)i, new Vector3(i - 1.5f, 0, 4))).ToArray();
            foreach (var pickup in pickups)
            {
                var renderer = pickup.Icon.GetComponent<Renderer>(); var position = pickup.Icon.position;
                Assert.That(pickup.Collect(), Is.True); Assert.That(pickup.Collect(), Is.False);
                Assert.That(pickup.Icon.position, Is.EqualTo(position), "Collection must start from the existing visual without a jump");
                Assert.That(pickup.Icon.GetComponent<Renderer>(), Is.SameAs(renderer));
                Assert.That(renderer.sharedMaterial, Is.SameAs(game.World.Icons.Material(pickup.Kind)));
                Assert.That(pickup.GetComponent<SphereCollider>().enabled, Is.False);
                Assert.That(game.Stats.Level(pickup.Kind), Is.EqualTo(1));
            }
            yield return new WaitForSeconds(game.Settings.pickupFlyTime + .2f);
            Assert.That(game.PickupDisplay.Count, Is.EqualTo(4));
            for (int i = 0; i < pickups.Length; i++)
            {
                Assert.That(pickups[i].Icon.position.y, Is.GreaterThan(game.Tank.transform.position.y + 2));
                if (i > 0) Assert.That(Vector3.Dot(pickups[i].Icon.position - pickups[i - 1].Icon.position, game.CameraRig.transform.right), Is.GreaterThan(1));
            }
            var before = pickups[0].Icon.position;
            game.Tank.Warp(new Pose(game.Tank.transform.position + Vector3.forward * 4, Quaternion.Euler(0, 40, 0)));
            yield return null;
            Assert.That(pickups[0].Icon.position.z - before.z, Is.GreaterThan(3));
            foreach (var pickup in pickups) Assert.That(Quaternion.Angle(pickup.Icon.rotation, game.CameraRig.transform.rotation), Is.LessThan(.01f));
        }
        [UnityTest]
        public IEnumerator PickupDisplayPausesFadesAndExpiresWithoutLosingTheUpgrade()
        {
            var pickup = Pickup(UpgradeKind.Power, Vector3.forward * 4);
            Assert.That(pickup.Collect(), Is.True); yield return new WaitForSeconds(.5f);
            game.TogglePause(); var position = pickup.Icon.position;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(pickup.Icon.position, Is.EqualTo(position)); Assert.That(game.PickupDisplay.Count, Is.EqualTo(1));
            game.TogglePause(); yield return new WaitForSeconds(game.Settings.pickupDisplayDuration - .65f);
            var properties = new MaterialPropertyBlock(); pickup.Icon.GetComponent<Renderer>().GetPropertyBlock(properties);
            Assert.That(properties.GetColor("_Color").a, Is.InRange(.05f, .95f));
            yield return new WaitForSeconds(.35f);
            Assert.That(pickup.gameObject.activeSelf, Is.False); Assert.That(game.PickupDisplay.Count, Is.Zero);
            Assert.That(game.Stats.Level(UpgradeKind.Power), Is.EqualTo(1));
            while (game.Stats.Upgrade(UpgradeKind.Power)) { }
            var capped = Pickup(UpgradeKind.Power, Vector3.forward * 4);
            Assert.That(capped.Collect(), Is.False); Assert.That(capped.Collected, Is.False);
            Assert.That(capped.GetComponent<SphereCollider>().enabled, Is.True); Assert.That(game.PickupDisplay.Count, Is.Zero);
        }
        [UnityTest]
        public IEnumerator CrashClearsPickupDisplayBlastsNearbyPropsAndReturnsQuickly()
        {
            var pickup = Pickup(UpgradeKind.Speed, Vector3.forward * 4); pickup.Collect();
            var center = game.Tank.transform.position + Vector3.up;
            DestructibleBlock Witness(Vector3 at)
            {
                var root = game.World.Box("Crash blast witness", at, Vector3.one, game.World.ShotMaterial);
                var block = root.AddComponent<DestructibleBlock>(); block.Initialize(game); return block;
            }
            var near = Witness(center + Vector3.right * 7); var far = Witness(center + Vector3.right * 14);
            Physics.SyncTransforms(); var before = game.Remaining; float start = Time.time;
            game.Tank.Explode(); game.Tank.Explode();
            Assert.That(near.Detached, Is.True); Assert.That(far.Detached, Is.False);
            Assert.That(game.Crashes, Is.EqualTo(1)); Assert.That(game.Remaining, Is.EqualTo(before - game.Settings.respawnPenalty).Within(.01f));
            Assert.That(game.PickupDisplay.Count, Is.Zero); Assert.That(pickup.gameObject.activeSelf, Is.False);
            game.Tank.GetComponent<Rigidbody>().isKinematic = false; game.Tank.enabled = true;
            yield return new WaitForFixedUpdate();
            Assert.That(near.GetComponent<Rigidbody>().linearVelocity.magnitude, Is.GreaterThan(5));
            while (game.Tank.Respawning && Time.time - start < .85f) yield return null;
            Assert.That(game.Tank.Respawning, Is.False); Assert.That(Time.time - start, Is.LessThan(.65f));
            Assert.That(game.Tank.Visual.gameObject.activeSelf, Is.True); Assert.That(game.Stats.Level(UpgradeKind.Speed), Is.EqualTo(1));
            yield return new WaitForSeconds(.4f);
            Assert.That(game.CameraRig.FadeOpacity, Is.LessThan(.01f));
            Assert.That(game.Tank.ActualSpeed, Is.GreaterThan(2));
            yield return new WaitForSeconds(.7f);
            pickup = Pickup(UpgradeKind.Handling, Vector3.forward * 4); pickup.Collect();
            game.AdvanceTime(1000); yield return null;
            Assert.That(game.PickupDisplay.Count, Is.Zero); Assert.That(pickup.gameObject.activeSelf, Is.False);
        }
    }
}
