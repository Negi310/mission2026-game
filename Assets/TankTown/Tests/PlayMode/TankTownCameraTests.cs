using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TankTown.Tests
{
    public sealed class TankTownCameraTests
    {
        private TankTownGame game;
        private float previousCaptureStep;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousCaptureStep = Time.captureDeltaTime;
            SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>(); game.StartOrRestart();
            game.Input.useHardware = true; game.Input.SetWheel(0);
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var item in game.World.ItemPickups) item.gameObject.SetActive(false);
            foreach (var item in game.World.Pickups) item.gameObject.SetActive(false);
            Time.captureDeltaTime = 1f / 120;
        }

        [UnityTearDown]
        public IEnumerator TearDown() { Time.timeScale = 1; Time.captureDeltaTime = previousCaptureStep; yield return null; }

        private Transform StationaryView()
        {
            var target = new GameObject("Stationary camera target").transform;
            target.position = new Vector3(0, 1, -80);
            var aim = new GameObject("Test aim").transform; aim.SetParent(target, false);
            game.CameraRig.Target = target; game.CameraRig.AimTarget = aim; game.CameraRig.Terrain = null;
            game.CameraRig.Snap();
            return aim;
        }

        [UnityTest]
        public IEnumerator AbruptAimChangesEaseTheirSpeedAndKeepTheHorizonLevel()
        {
            var aim = StationaryView();
            var probe = game.CameraRig.gameObject.AddComponent<TankCameraMotionProbe>(); probe.Turret = aim;
            aim.rotation = Quaternion.Euler(35, 120, 25);
            yield return null;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, game.CameraRig.transform.eulerAngles.y)), Is.LessThan(.5f), "A new aim heading must not cut the view to that heading");
            yield return new WaitForSeconds(2.5f);
            Assert.That(Vector3.Angle(game.CameraRig.transform.forward, Vector3.ProjectOnPlane(aim.forward, Vector3.up)), Is.LessThan(1));
            aim.rotation = Quaternion.Euler(-25, -20, -20);
            yield return new WaitForSeconds(.7f);
            Assert.That(probe.MaxHorizonAngle, Is.LessThan(.01f));
            Assert.That(probe.MaxRotationSpeed, Is.InRange(30, 136));
            Assert.That(probe.MaxRotationAcceleration, Is.LessThan(250));
            Debug.Log($"COMFORT AIM TEST: frames={probe.Samples}, speed={probe.MaxRotationSpeed:0.000}, acceleration={probe.MaxRotationAcceleration:0.000}, horizon={probe.MaxHorizonAngle:0.000000}");
        }

        [UnityTest]
        public IEnumerator CannonAndLargeImpactDoNotShakeOrChangeTheFieldOfView()
        {
            StationaryView();
            var position = game.CameraRig.transform.position; var rotation = game.CameraRig.transform.rotation;
            float fov = Camera.main.fieldOfView;
            game.Tank.Fire(0);
            game.Blast(game.World.GroundPoint(new Vector3(100, 0, 100), 2));
            game.Items.Receive(ItemKind.HighSpeed);
            for (int i = 0; i < 40; i++)
            {
                yield return null;
                Assert.That(Vector3.Distance(game.CameraRig.transform.position, position), Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(game.CameraRig.transform.rotation, rotation), Is.LessThan(.01f));
                Assert.That(Camera.main.fieldOfView, Is.EqualTo(fov));
            }
        }

        [UnityTest]
        public IEnumerator BlockingPartsFadeAndRestoreWithoutMovingTheCameraOrSharedMaterial()
        {
            StationaryView();
            var camera = game.CameraRig;
            var position = camera.transform.position;
            var center = (position + camera.Target.position) * .5f;
            var box = game.World.Box("Camera occlusion witness", center, new Vector3(4, 5, 1), game.World.ShotMaterial);
            var obstruction = box.AddComponent<DestructibleBlock>(); obstruction.Kind = BuildingPart.Wall;
            obstruction.Initialize(game); Physics.SyncTransforms();
            var renderer = box.GetComponent<Renderer>(); var properties = new MaterialPropertyBlock();
            yield return new WaitForSeconds(.35f);
            Assert.That(Vector3.Distance(camera.transform.position, position), Is.LessThan(.001f), "An obstacle must not push the camera into the tank");
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_CameraVisibility"), Is.Zero, "Fully covered parts must not leave a distracting dotted pattern");
            Assert.That(renderer.sharedMaterial.GetFloat("_CameraVisibility"), Is.EqualTo(1), "Other parts that share this material must remain opaque");
            var block = box.GetComponent<DestructibleBlock>(); block.Break(center, 0);
            Assert.That(block.DebrisBodies.Count, Is.GreaterThan(1));
            foreach (var piece in block.DebrisBodies)
            {
                piece.constraints = RigidbodyConstraints.FreezeAll; piece.useGravity = false;
            }
            yield return new WaitForSeconds(.35f);
            foreach (var piece in block.DebrisBodies)
            {
                piece.GetComponent<Renderer>().GetPropertyBlock(properties);
                Assert.That(properties.GetFloat("_CameraVisibility"), Is.Zero, "Each separate fragment must keep fading while it blocks the view");
                piece.transform.position += Vector3.right * 20;
            }
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.35f);
            foreach (var piece in block.DebrisBodies)
            {
                piece.GetComponent<Renderer>().GetPropertyBlock(properties);
                Assert.That(properties.GetFloat("_CameraVisibility"), Is.EqualTo(1).Within(.001f), "A fragment must become opaque again after leaving the camera");
            }
            Assert.That(Vector3.Distance(camera.transform.position, position), Is.LessThan(.001f));
        }

        [UnityTest]
        public IEnumerator GiantTankChangesCameraDistanceGraduallyInBothDirections()
        {
            game.Tank.Warp(new Pose(new Vector3(0, 0, -80), Quaternion.identity)); game.CameraRig.Snap();
            // Isolate size framing from the laser's separate camera movement.
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            yield return new WaitForSeconds(.3f);
            float previous = HorizontalDistance();
            game.Items.Receive(ItemKind.Bulldozer);
            float maximumStep = 0;
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                float distance = HorizontalDistance();
                maximumStep = Mathf.Max(maximumStep, Mathf.Abs(distance - previous)); previous = distance;
            }
            Assert.That(previous, Is.InRange(9.1f, 12), "The first quarter second must ease the zoom rather than jump to giant framing");
            Assert.That(maximumStep, Is.LessThan(.15f));
            yield return new WaitForSeconds(2);
            Assert.That(HorizontalDistance(), Is.GreaterThan(19));
            previous = HorizontalDistance(); game.Tank.SetVehicleScale(1, false);
            yield return null; yield return null;
            Assert.That(HorizontalDistance(), Is.GreaterThan(previous - .5f), "Returning to normal size must also ease the camera distance");
            yield return new WaitForSeconds(.5f);
            Assert.That(HorizontalDistance(), Is.LessThan(previous - 1));
        }

        private float HorizontalDistance()
            => Vector3.ProjectOnPlane(game.CameraRig.transform.position - game.Tank.transform.position, Vector3.up).magnitude;

        [UnityTest]
        public IEnumerator LaserViewMovesAboveAndAsideSmoothlyAndReturnsAfterTheEffect()
        {
            Time.captureDeltaTime = 1f / 60;
            game.Tank.Warp(new Pose(new Vector3(0, 0, -80), Quaternion.identity));
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            var camera = game.CameraRig; camera.Terrain = null; camera.Snap();
            var normalPosition = camera.transform.position; var normalRotation = camera.transform.rotation;
            float fov = Camera.main.fieldOfView;
            game.Items.Receive(ItemKind.MegaBeam); game.Items.UseHeld();
            Assert.That(Vector3.Distance(camera.transform.position, normalPosition), Is.LessThan(.001f), "Starting a laser must not cut to its view");
            float maxStep = 0, maxAngle = 0; var previous = normalPosition; var previousRotation = normalRotation;
            for (int i = 0; i < 66; i++)
            {
                yield return null;
                maxStep = Mathf.Max(maxStep, Vector3.Distance(previous, camera.transform.position));
                maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(previousRotation, camera.transform.rotation));
                previous = camera.transform.position; previousRotation = camera.transform.rotation;
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, camera.transform.eulerAngles.z)), Is.LessThan(.01f));
                Assert.That(Camera.main.fieldOfView, Is.EqualTo(fov));
            }
            Assert.That(camera.BeamViewBlend, Is.GreaterThan(.96f));
            Assert.That(camera.transform.position.x - normalPosition.x, Is.GreaterThan(8));
            Assert.That(camera.transform.position.y - normalPosition.y, Is.GreaterThan(4));
            Assert.That(camera.transform.eulerAngles.x, Is.InRange(6, 10));
            Assert.That(maxStep, Is.LessThan(.6f)); Assert.That(maxAngle, Is.LessThan(1));
            game.TogglePause(); previous = camera.transform.position; previousRotation = camera.transform.rotation;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(Vector3.Distance(previous, camera.transform.position), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(previousRotation, camera.transform.rotation), Is.LessThan(.01f));
            game.TogglePause();
            yield return new WaitForSeconds(game.Items.Remaining(ItemKind.MegaBeam) + 2);
            Assert.That(camera.BeamViewBlend, Is.LessThan(.005f));
            Assert.That(Vector3.Distance(camera.transform.position, normalPosition), Is.LessThan(.15f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, normalRotation), Is.LessThan(.1f));
        }

        [UnityTest]
        public IEnumerator LaserViewFadesUpperForegroundWallsWithoutMovingToAvoidThem()
        {
            Time.captureDeltaTime = 1f / 60;
            game.Tank.Warp(new Pose(new Vector3(0, 0, -80), Quaternion.identity));
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            var camera = game.CameraRig; camera.Terrain = null; camera.Snap();
            game.Items.Receive(ItemKind.MegaBeam); game.Items.UseHeld(); yield return new WaitForSeconds(1.2f);
            var center = camera.transform.position + camera.transform.forward * 9 + Vector3.up * 2.3f;
            var box = game.World.Box("Upper laser-view foreground witness", center, new Vector3(3, 3, 1), game.World.ShotMaterial);
            var block = box.AddComponent<DestructibleBlock>(); block.Kind = BuildingPart.Wall; block.Initialize(game);
            var renderer = box.GetComponent<Renderer>(); var properties = new MaterialPropertyBlock();
            properties.SetFloat("_CameraVisibility", 1); renderer.SetPropertyBlock(properties);
            Physics.SyncTransforms(); var previous = camera.transform.position;
            for (int i = 0; i < 23; i++)
            {
                yield return null;
                Assert.That(Vector3.Distance(camera.transform.position, previous), Is.LessThan(.1f), "An obstruction must not force the camera to jump");
                previous = camera.transform.position;
            }
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_CameraVisibility"), Is.Zero, "Foreground above the tank-to-camera ray must also clear the laser view");
            Assert.That(block.Detached, Is.False, "Camera clearing must not destroy the obstruction");
            Assert.That(renderer.sharedMaterial.GetFloat("_CameraVisibility"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CancellingLaserAndRestartingDoNotLeaveTheSpecialViewActive()
        {
            game.Tank.Warp(new Pose(new Vector3(0, 0, -80), Quaternion.identity));
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            var camera = game.CameraRig; camera.Terrain = null; camera.Snap();
            var normalPosition = camera.transform.position;
            game.Items.Receive(ItemKind.MegaBeam); game.Items.UseHeld();
            yield return new WaitForSeconds(.7f);
            Assert.That(camera.BeamViewBlend, Is.GreaterThan(.8f));
            var previous = camera.transform.position; game.Items.OnTankCrash(); yield return null;
            Assert.That(Vector3.Distance(previous, camera.transform.position), Is.LessThan(.3f), "Cancelling must also blend back");
            yield return new WaitForSeconds(2);
            Assert.That(camera.BeamViewBlend, Is.LessThan(.005f));
            Assert.That(Vector3.Distance(camera.transform.position, normalPosition), Is.LessThan(.15f));
            game.Items.Receive(ItemKind.MegaBeam); game.Items.UseHeld(); yield return new WaitForSeconds(.7f);
            game.AdvanceTime(1000); yield return null;
            Assert.That(game.State, Is.EqualTo(RoundState.Results));
            game.StartOrRestart(); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>();
            Assert.That(game.Items.StopsForward, Is.False); Assert.That(game.CameraRig.BeamViewBlend, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RealUphillAndDownhillTravelKeepsTheHorizonLevelAndTheCameraAboveGround()
        {
            game.Items.Receive(ItemKind.HighSpeed);
            foreach (var pose in new[] { new Pose(new Vector3(0, 0, 8), Quaternion.identity), new Pose(new Vector3(0, 0, 62), Quaternion.Euler(0, 180, 0)) })
            {
                game.Tank.Warp(pose); game.CameraRig.Snap();
                for (int i = 0; i < 240; i++)
                {
                    yield return null;
                    var camera = game.CameraRig.transform;
                    Assert.That(Vector3.Angle(camera.up, Vector3.up), Is.LessThan(.01f));
                    Assert.That(camera.position.y - game.World.Terrain.HeightAt(camera.position), Is.GreaterThanOrEqualTo(1.49f));
                }
            }
            Assert.That(game.Crashes, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RespawnCutsAreCoveredAndResultsNeverRemainBlack()
        {
            game.Tank.Warp(new Pose(new Vector3(-80, 0, -80), Quaternion.Euler(0, 135, 0))); game.CameraRig.Snap();
            game.Tank.Explode();
            var previous = game.Tank.transform.position; bool returned = false;
            for (int i = 0; i < 220; i++)
            {
                yield return null;
                if (Vector3.Distance(previous, game.Tank.transform.position) > 20)
                {
                    returned = true;
                    Assert.That(game.CameraRig.FadeOpacity, Is.GreaterThanOrEqualTo(.999f), "The position/angle change must happen behind the fully covered view");
                }
                previous = game.Tank.transform.position;
            }
            Assert.That(returned, Is.True); Assert.That(game.Tank.Respawning, Is.False);
            Assert.That(game.CameraRig.FadeOpacity, Is.Zero);
            game.CameraRig.FadeOut(); yield return new WaitForSeconds(.25f);
            Assert.That(game.CameraRig.TransitionCovered, Is.True);
            game.AdvanceTime(1000); yield return null;
            Assert.That(game.State, Is.EqualTo(RoundState.Results)); Assert.That(game.CameraRig.FadeOpacity, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PausingFreezesTheCameraEvenWhenTheAimChanges()
        {
            var aim = StationaryView(); aim.rotation = Quaternion.Euler(0, 90, 0);
            yield return new WaitForSeconds(.25f);
            game.TogglePause();
            var position = game.CameraRig.transform.position; var rotation = game.CameraRig.transform.rotation;
            aim.rotation = Quaternion.Euler(0, -90, 0);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(Vector3.Distance(game.CameraRig.transform.position, position), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(game.CameraRig.transform.rotation, rotation), Is.LessThan(.01f));
            game.TogglePause(); yield return new WaitForSeconds(.4f);
            Assert.That(Quaternion.Angle(game.CameraRig.transform.rotation, rotation), Is.GreaterThan(1));
        }
    }
}
