using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TankTown.Tests
{
    public sealed class TankTownShellTests
    {
        private TankTownGame game;
        private float captureStep;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            captureStep = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60;
            SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>(); game.StartOrRestart(); game.Input.useHardware = true;
            game.Settings.cannonAimAssist = false; // These tests exercise the unassisted barrel direction.
            game.Tank.Warp(new Pose(game.World.SpawnPosition, Quaternion.identity));
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var person in game.World.Pedestrians) person.gameObject.SetActive(false);
            foreach (var pickup in game.World.Pickups) pickup.gameObject.SetActive(false);
            foreach (var pickup in game.World.ItemPickups) pickup.gameObject.SetActive(false);
        }
        [UnityTearDown]
        public IEnumerator TearDown() { Time.timeScale = 1; Time.captureDeltaTime = captureStep; yield return null; }

        [UnityTest]
        public IEnumerator ActualShotUsesSharedDirectionalGeometryAndPauseFreezesIt()
        {
            game.Tank.Turret.localRotation = Quaternion.Euler(0, 15, 0);
            game.Input.PulsePad(0); var shell = Object.FindFirstObjectByType<TankShell>();
            Assert.That(shell, Is.Not.Null);
            var mesh = shell.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.bounds.size.z, Is.GreaterThan(mesh.bounds.size.x * 2));
            Assert.That(shell.GetComponent<Renderer>().sharedMaterials.Length, Is.EqualTo(3));
            Assert.That(Vector3.Distance(shell.transform.forward, game.Tank.Muzzle.forward), Is.LessThan(.00001f));
            Assert.That(shell.GetComponent<Collider>(), Is.Null);
            yield return null;
            game.TogglePause(); var position = shell.transform.position;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(shell.transform.position, Is.EqualTo(position));
            game.TogglePause(); yield return new WaitForSeconds(game.Stats.ShotCooldown + .03f);
            game.Input.PulsePad(0);
            foreach (var shot in Object.FindObjectsByType<TankShell>(FindObjectsSortMode.None))
                Assert.That(shot.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh), "Firing must reuse the shape instead of allocating a new mesh");
        }
        [UnityTest]
        public IEnumerator ShellTravelsAtTheConfiguredHigherSpeedAlongTheMuzzleDirection()
        {
            game.Input.PulsePad(0); var shell = Object.FindFirstObjectByType<TankShell>(); yield return null;
            Assert.That(shell, Is.Not.Null);
            var origin = shell.transform.position; float started = Time.time; var direction = shell.transform.forward;
            for (int i = 0; i < 3; i++) yield return null;
            Assert.That(shell, Is.Not.Null);
            var travel = shell.transform.position - origin;
            Assert.That(travel.magnitude, Is.EqualTo(game.Settings.shellSpeed * (Time.time - started)).Within(.05f));
            Assert.That(travel.magnitude / (Time.time - started), Is.GreaterThan(110));
            Assert.That(Vector3.Distance(travel.normalized, direction), Is.LessThan(.00001f));
        }
        [UnityTest]
        public IEnumerator HighSpeedSweepHitsAThinWallBetweenLowFrameRateSamples()
        {
            Time.captureDeltaTime = 1f / 20;
            var muzzle = game.Tank.Muzzle;
            var target = game.World.Box("Thin swept-shell witness", muzzle.position + muzzle.forward * 4.5f, new Vector3(3, 3, .08f), game.World.ShotMaterial);
            target.transform.rotation = Quaternion.LookRotation(muzzle.forward);
            var wall = target.AddComponent<DestructibleBlock>(); wall.CanCrush = true; wall.Kind = BuildingPart.Wall; wall.Initialize(game);
            Physics.SyncTransforms(); game.Input.PulsePad(0);
            yield return null; yield return null;
            Assert.That(wall.Detached, Is.True, "A whole flight segment must be tested even when the shell travels beyond a thin wall in one frame");
            Assert.That(Object.FindFirstObjectByType<TankShell>(), Is.Null);
        }
    }
}
