#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TankTown
{
    public sealed class TankTownBeamCameraSmoke : MonoBehaviour
    {
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--tank-town-beam-camera-smoke") < 0) return;
            TankTownGame.SelectedStage = Array.IndexOf(args, "--tank-town-beam-camera-big") >= 0 ? TownStage.BigCity : TownStage.Town;
            TankTownGame.GenerationSeedOverride = 2026; TankTownGame.SkipIntroForTests = true; TankTownGame.DisableRecordSavingForTests = true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-beam-camera-smoke") >= 0)
                new GameObject("Beam camera smoke").AddComponent<TankTownBeamCameraSmoke>();
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true; Time.captureDeltaTime = 1f / 60;
            output = Path.Combine(Application.dataPath, "..", "Captures"); Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(.4f);
            var game = FindFirstObjectByType<TankTownGame>(); game.StartOrRestart(); game.Input.useHardware = true;
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var person in game.World.Pedestrians) person.gameObject.SetActive(false);
            foreach (var pickup in game.World.Pickups) pickup.gameObject.SetActive(false);
            foreach (var pickup in game.World.ItemPickups) pickup.gameObject.SetActive(false);
            var building = game.World.Buildings.Where(x => x.transform.position.z > 0).OrderBy(x => x.transform.position.sqrMagnitude).First();
            var point = building.transform.position + new Vector3(-4.6f, 0, -building.Width / 2 - 16);
            game.Tank.Warp(new Pose(point, Quaternion.identity));
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            game.CameraRig.Snap(); yield return new WaitForSeconds(.4f); Capture(game, "beam-camera-01-normal");
            game.Items.Receive(ItemKind.MegaBeam); game.Items.UseHeld();
            float fov = Camera.main.fieldOfView, maxStep = 0, maxAngle = 0, peakBlend = 0, peakRecoil = 0, maxRecoilStep = 0;
            var previousRecoil = Vector3.zero;
            float peakStructurePixels = 0, peakTankPixels = 0;
            int crossings = 0, previousSign = 0;
            var structureMarker = building.transform.position + Vector3.up * 8;
            var barrelRecoil = game.Tank.Muzzle.parent.GetComponent<TankBarrelRecoil>();
            float peakBarrel = 0;
            var previous = Camera.main.transform.position; var previousRotation = Camera.main.transform.rotation;
            for (int i = 0; i < 138; i++)
            {
                yield return null;
                var camera = Camera.main.transform;
                maxStep = Mathf.Max(maxStep, Vector3.Distance(camera.position, previous));
                maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(camera.rotation, previousRotation));
                peakBlend = Mathf.Max(peakBlend, game.CameraRig.BeamViewBlend);
                var recoil = game.CameraRig.BeamShakeOffset;
                peakBarrel = Mathf.Max(peakBarrel, barrelRecoil.Displacement);
                peakRecoil = Mathf.Max(peakRecoil, recoil.magnitude);
                maxRecoilStep = Mathf.Max(maxRecoilStep, Vector3.Distance(previousRecoil, recoil)); previousRecoil = recoil;
                peakStructurePixels = Mathf.Max(peakStructurePixels, RecoilPixels(structureMarker, recoil));
                peakTankPixels = Mathf.Max(peakTankPixels, RecoilPixels(game.Tank.transform.position + Vector3.up, recoil));
                float horizontal = Vector3.Dot(recoil, camera.right);
                int sign = horizontal > .001f ? 1 : horizontal < -.001f ? -1 : 0;
                if (sign != 0) { if (previousSign != 0 && sign != previousSign) crossings++; previousSign = sign; }
                previous = camera.position; previousRotation = camera.rotation;
                if (i == 41) Capture(game, "beam-camera-02-transition");
                if (i == 77) Capture(game, "beam-camera-03-destruction");
            }
            yield return new WaitForSeconds(2); Capture(game, "beam-camera-04-restored");
            if (peakBlend < .95f || game.CameraRig.BeamViewBlend > .005f || game.Items.BeamHits <= 0 || Camera.main.fieldOfView != fov)
            { Fail("laser framing, restoration, damage or fixed FOV check failed"); yield break; }
            if (peakRecoil > .00001f || maxRecoilStep > .00001f || crossings != 0 || peakStructurePixels > .001f || peakTankPixels > .001f || peakBarrel < .05f || peakBarrel > .0801f || barrelRecoil.Displacement > .00001f)
            { Fail("the view moved, visual barrel recoil was missing, or recoil did not reset"); yield break; }
            game.Items.Receive(ItemKind.Bulldozer);
            float dozerSpeed = game.Items.SpeedMultiplier;
            game.Items.Receive(ItemKind.HighSpeed); float combinedSpeed = game.Items.SpeedMultiplier;
            if (Mathf.Abs(dozerSpeed - .9f) > .001f || Mathf.Abs(combinedSpeed - .9f * game.Settings.highSpeedMultiplier) > .001f)
            { Fail("bulldozer speed multiplier check failed"); yield break; }
            Debug.Log($"BEAM CAMERA SMOKE COMPLETE: stage={game.Stage}, buildings={game.World.Buildings.Count}, beamHits={game.Items.BeamHits}, peakBlend={peakBlend:0.000}, restoredBlend={game.CameraRig.BeamViewBlend:0.000}, maxPositionStep={maxStep:0.000}, maxAngleStep={maxAngle:0.000}, peakRecoil={peakRecoil:0.00000}, maxRecoilStep={maxRecoilStep:0.00000}, horizontalCrossings={crossings}, structurePixels={peakStructurePixels:0.00}, tankPixels={peakTankPixels:0.00}, barrelRecoil={peakBarrel:0.00000}, fixedFov={fov:0.0}, dozerSpeed={dozerSpeed:0.00}, combinedSpeed={combinedSpeed:0.00}");
            Application.Quit(0);
        }
        private static float RecoilPixels(Vector3 point, Vector3 recoil)
        {
            // Moving the point by +recoil is equivalent to removing the camera
            // translation. Compare projections without changing the camera pose.
            var camera = Camera.main;
            var a = camera.WorldToViewportPoint(point); var b = camera.WorldToViewportPoint(point + recoil);
            return a.z > 0 && b.z > 0 ? new Vector2((a.x - b.x) * 1920, (a.y - b.y) * 1080).magnitude : 0;
        }
        private void Capture(TankTownGame game, string label)
        {
            foreach (var building in game.World.Buildings) building.Presentation?.Tick(Camera.main.transform.position);
            game.World.RefreshMapMeshes();
            TankTownSmokeCapture.Capture(Path.Combine(output, label + ".png"), true, 1920, 1080);
        }
        private static void Fail(string message) { Debug.LogError("BEAM CAMERA SMOKE FAILED: " + message); Application.Quit(1); }
    }
}
#endif
