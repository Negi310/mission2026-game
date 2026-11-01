using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TankTown.Tests
{
    public sealed class TankTownAimTests
    {
        private TankTownGame game;
        private TownStage previousStage;
        private float previousStep;
        private Vector3 Origin => game.Tank.Muzzle.position;
        private Vector3 Forward => game.Tank.Muzzle.forward;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousStage = TankTownGame.SelectedStage; TankTownGame.SelectedStage = TownStage.Town;
            previousStep = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60;
            SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>(); game.StartOrRestart(); game.Input.useHardware = true;
            game.Tank.Warp(new Pose(new Vector3(0, 0, -1000), Quaternion.identity));
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            game.CameraRig.enabled = false;
            Camera.main.transform.position = Origin - Forward * 10 + Vector3.up * 4;
            Camera.main.transform.LookAt(Origin + Forward * 35);
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TankTownGame.SelectedStage = previousStage; Time.captureDeltaTime = previousStep; Time.timeScale = 1;
            yield return null;
        }

        private DestructibleBlock Target(float distance, float yaw)
        {
            var point = Origin + Quaternion.AngleAxis(yaw, Vector3.up) * Forward * distance;
            var box = game.World.Box("Aim test target", point, new Vector3(.7f, 1.5f, .7f), game.World.ShotMaterial);
            var block = box.AddComponent<DestructibleBlock>(); block.CanCrush = true; block.Initialize(game);
            Physics.SyncTransforms(); return block;
        }

        [UnityTest]
        public IEnumerator PicksTheClosestDirectionRatherThanTheClosestObject()
        {
            Target(12, 5); var centered = Target(40, 1.4f);
            var solution = game.Tank.CannonAim.Resolve(Camera.main, true);
            Assert.That(solution.Target, Is.SameAs(centered));
            Assert.That(Vector3.Angle(solution.Direction, Forward), Is.InRange(.1f, game.Settings.cannonAimAssistAngle));
            Assert.That(Physics.SphereCast(Origin, .25f, solution.Direction, out var hit, 50, TownPhysics.StructureMask), Is.True);
            Assert.That(hit.collider.GetComponent<DestructibleBlock>(), Is.SameAs(centered));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DirectHitsArePreservedAndEmptySpaceDoesNotBendShots()
        {
            var centered = Target(30, 0); Target(20, 4);
            var solution = game.Tank.CannonAim.Resolve(Camera.main, true);
            Assert.That(solution.Target, Is.SameAs(centered)); Assert.That(solution.Direction, Is.EqualTo(Forward));
            foreach (var target in Object.FindObjectsByType<DestructibleBlock>(FindObjectsSortMode.None))
                if (target.transform.position.z < -900) target.gameObject.SetActive(false);
            Physics.SyncTransforms(); solution = game.Tank.CannonAim.Resolve(Camera.main, true);
            Assert.That(solution.Target, Is.Null); Assert.That(solution.Direction, Is.EqualTo(Forward));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExcludesOutsideConeBehindOutOfRangeAndDetachedObjects()
        {
            Target(40, 9); Target(game.Settings.cannonAimAssistRange + 20, 2); Target(25, 180);
            var detached = Target(30, 3); detached.Break(detached.transform.position, 0);
            Physics.SyncTransforms(); var solution = game.Tank.CannonAim.Resolve(Camera.main, true);
            Assert.That(solution.Target, Is.Null); Assert.That(solution.Direction, Is.EqualTo(Forward));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CannotAssistThroughAnObstacleAtTheMuzzleOrCamera()
        {
            var target = Target(35, 3);
            var wall = game.World.Box("Aim blocker", Origin + Forward * 15, new Vector3(8, 8, 1), game.World.ShotMaterial);
            Physics.SyncTransforms(); Assert.That(game.Tank.CannonAim.Resolve(Camera.main, true).Target, Is.Null);
            wall.SetActive(false);
            var cameraBlock = game.World.Box("Camera-only aim blocker", Vector3.Lerp(Camera.main.transform.position, target.transform.position, .2f), new Vector3(3, 1.2f, 1), game.World.ShotMaterial);
            Physics.SyncTransforms(); Assert.That(game.Tank.CannonAim.Resolve(Camera.main, true).Target, Is.Null);
            cameraBlock.SetActive(false); Physics.SyncTransforms();
            Assert.That(game.Tank.CannonAim.Resolve(Camera.main, true).Target, Is.SameAs(target));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SettingsAndSpecialWeaponsBypassCannonAssistance()
        {
            Target(30, 3);
            game.Settings.cannonAimAssist = false;
            Assert.That(game.Tank.CannonAim.Resolve(Camera.main, true).Direction, Is.EqualTo(Forward));
            game.Settings.cannonAimAssist = true; game.Settings.cannonAimAssistAngle = 0;
            Assert.That(game.Tank.CannonAim.Resolve(Camera.main, true).Target, Is.Null);
            game.Settings.cannonAimAssistAngle = 6; game.Items.Receive(ItemKind.GigantBomb);
            Assert.That(game.Tank.CannonAim.Resolve(Camera.main, true).Target, Is.Null);
            game.Items.FinishRound(); game.Items.Receive(ItemKind.MegaBeam); game.Input.PulsePad(0);
            Assert.That(game.Items.StopsForward, Is.True);
            Assert.That(game.Tank.CannonAim.Resolve(Camera.main, true).Target, Is.Null);
            game.Items.FinishRound();
            var queuedTarget = Target(30, 3); // The preceding real laser destroys its own target.
            game.Items.Receive(ItemKind.GigantBomb);
            Assert.That(game.Items.UseHeld(), Is.True);
            // Keep the real flying bomb clear of the camera's sight line for this queued-weapon case.
            var bomb = Object.FindFirstObjectByType<GigantBomb>();
            bomb.GetComponent<Rigidbody>().position = Origin + Vector3.right * 25 + Vector3.up * 12;
            Physics.SyncTransforms();
            game.Items.Receive(ItemKind.MegaBeam);
            Assert.That(game.Items.Weapon, Is.EqualTo(ItemKind.GigantBomb));
            Assert.That(game.Items.Held, Is.EqualTo(ItemKind.MegaBeam));
            Assert.That(game.Tank.CannonAim.Resolve(Camera.main, true).Target, Is.SameAs(queuedTarget),
                "Normal shots must retain assistance while a bomb is in flight and the next special weapon is queued");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualCannonRefreshesThePreviewAndHitsWithoutHoming()
        {
            var target = Target(30, 2);
            var preview = game.Tank.CannonAim.Resolve(Camera.main, true);
            target.transform.position += Vector3.right;
            Physics.SyncTransforms();
            game.Input.PulsePad(0);
            var shell = Object.FindFirstObjectByType<TankShell>(); Assert.That(shell, Is.Not.Null);
            var direction = shell.transform.forward;
            Assert.That(Vector3.Angle(direction, preview.Direction), Is.GreaterThan(.5f), "Fire must re-query after a moving target leaves the cached preview point");
            Assert.That(Vector3.Angle(direction, Forward), Is.InRange(.1f, 6));
            game.Tank.Turret.localRotation = Quaternion.Euler(0, -20, 0);
            yield return null;
            Assert.That(Vector3.Angle(shell.transform.forward, direction), Is.LessThan(.001f), "Once launched, a normal shell must keep flying straight");
            yield return new WaitForSeconds(.4f);
            Assert.That(target.Detached, Is.True, "The real swept projectile must hit and destroy the assisted target");
        }

        [UnityTest]
        public IEnumerator EightySecondRoundTimerPauseAndTimeoutAgree()
        {
            Assert.That(game.Settings.roundSeconds, Is.EqualTo(80));
            yield return null;
            Assert.That(GameObject.Find("Round time").GetComponent<Text>().text, Is.EqualTo("01:20"));
            game.AdvanceTime(60); Assert.That(game.IsPlaying, Is.True); Assert.That(game.Remaining, Is.GreaterThan(19));
            game.TogglePause(); float remaining = game.Remaining; game.AdvanceTime(5);
            Assert.That(game.Remaining, Is.EqualTo(remaining));
            game.TogglePause(); game.AdvanceTime(remaining + .1f);
            Assert.That(game.State, Is.EqualTo(RoundState.Results)); Assert.That(game.Remaining, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ReticleShowsTheTargetAndNeverInterceptsInputOrAppearsInPause()
        {
            Target(30, 3); yield return new WaitForSeconds(.12f);
            var reticle = game.GetComponent<TankTownHUD>().GetComponentInChildren<TankTownReticle>();
            if (reticle == null) reticle = GameObject.Find("Cannon reticle").GetComponent<TankTownReticle>();
            Assert.That(reticle.HasTarget, Is.True); Assert.That(reticle.raycastTarget, Is.False);
            Canvas.ForceUpdateCanvases();
            game.TogglePause(); yield return null; Assert.That(reticle.enabled, Is.False);
        }
    }
}
