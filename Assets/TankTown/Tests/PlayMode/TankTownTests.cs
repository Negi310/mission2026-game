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
            var building = Object.FindFirstObjectByType<BlockBuilding>();
            var low = building.Blocks.Find(x => x.Level == 0);
            var high = building.Blocks.Find(x => x.Column == low.Column && x.Level == 2);
            var point = low.transform.position;
            game.Blast(point);
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
            body.position = new Vector3(12, 1, -44);
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
            body.position = new Vector3(12, 1, -43);
            body.rotation = Quaternion.identity;
            yield return new WaitForSeconds(.6f);
            Assert.That(game.Crashes, Is.EqualTo(1));
            Assert.That(game.Tank.Respawning, Is.True);
        }
    }
}
