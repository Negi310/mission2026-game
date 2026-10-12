#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TankTown
{
    public sealed class TankTownAmusementSmoke : MonoBehaviour
    {
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-amusement-smoke") < 0) return;
            TankTownGame.SelectedStage = TownStage.AmusementPark; TankTownGame.GenerationSeedOverride = 2026;
            TankTownGame.SkipIntroForTests = true; TankTownGame.DisableRecordSavingForTests = true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-amusement-smoke") >= 0)
                new GameObject("Amusement stage smoke").AddComponent<TankTownAmusementSmoke>();
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true; Time.captureDeltaTime = 1f / 60;
            output = Path.Combine(Application.dataPath, "..", "Captures"); Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(.4f);
            Debug.Log($"PLAYER DISPLAY: mode={Screen.fullScreenMode}, size={Screen.width}x{Screen.height}, desktop={Screen.currentResolution.width}x{Screen.currentResolution.height}, batch={Application.isBatchMode}");
            var game = FindFirstObjectByType<TankTownGame>(); Capture(game, "amusement-01-title");
            game.StartOrRestart(); game.Input.useHardware = true; yield return new WaitForSeconds(.3f);
            Capture(game, "amusement-02-play");
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var car in game.World.TrafficCars) car.gameObject.SetActive(false);
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true; game.CameraRig.enabled = false;
            bool previousFog = RenderSettings.fog; RenderSettings.fog = false;
            float previousFar = Camera.main.farClipPlane; Camera.main.farClipPlane = 1300;
            View(Vector3.zero, new Vector3(-380, 310, -440), 0); yield return null; Capture(game, "amusement-03-overview");
            RenderSettings.fog = previousFog; Camera.main.farClipPlane = previousFar;
            for (int style = 0; style < 7; style++)
            {
                var building = game.World.Buildings.First(x => x.Style == style);
                View(building.transform.position, new Vector3(-27, style == 0 || style == 3 ? 18 : 12, -35), style == 0 || style == 3 ? 12 : 5);
                yield return null; Capture(game, "amusement-" + (style + 4).ToString("00") + "-" + style);
            }
            var wheel = game.World.Buildings.First(x => x.Style == 0);
            game.Tank.Warp(new Pose(game.World.GroundPoint(wheel.transform.position + Vector3.back * 12, game.Settings.rideHeight), Quaternion.identity));
            Physics.SyncTransforms(); game.Input.PulsePad(0); yield return new WaitForSeconds(.6f);
            if (wheel.DetachedParts == 0 || wheel.FullyDestroyed) { Fail("normal cannon must damage the wheel locally"); yield break; }
            View(wheel.transform.position, new Vector3(-27, 18, -35), 12); yield return null; Capture(game, "amusement-11-wheel-hit");
            var coaster = game.World.Buildings.First(x => x.Style == 1);
            foreach (var foot in coaster.Blocks.Where(x => x.Foundation).ToArray()) foot.Break(foot.transform.position, 0);
            coaster.ReleaseUnsupported(coaster.transform.position, 0);
            View(coaster.transform.position, new Vector3(-27, 15, -35), 6); yield return new WaitForSeconds(.25f); Capture(game, "amusement-12-coaster-collapse");
            if (!coaster.FullyDestroyed || coaster.Reward.Area == null) { Fail("unsupported coaster must collapse and release its reward"); yield break; }
            yield return new WaitForSeconds(5); Capture(game, "amusement-13-reward");
            game.Tank.Warp(new Pose(game.World.SpawnPosition, Quaternion.identity)); game.CameraRig.enabled = true; game.CameraRig.Snap();
            yield return null; TankTownSmokeCapture.Capture(Path.Combine(output, "amusement-14-play-720.png"), true, 1280, 720);
            Debug.Log($"AMUSEMENT SMOKE COMPLETE: buildings={game.World.Buildings.Count}, extent={game.World.HalfExtent * 2}, parks={game.World.ParkCount}, sevenShapes=true, partialCannon=true, supportCollapse=true, rewardArea=true");
            Application.Quit(0);
        }
        private static void View(Vector3 focus, Vector3 offset, float height)
        { Camera.main.transform.position = focus + offset; Camera.main.transform.LookAt(focus + Vector3.up * height); }
        private void Capture(TankTownGame game, string label)
        {
            foreach (var building in game.World.Buildings) building.Presentation?.Tick(Camera.main.transform.position);
            game.World.RefreshMapMeshes(); TankTownSmokeCapture.Capture(Path.Combine(output, label + ".png"), true, 1920, 1080);
        }
        private static void Fail(string message) { Debug.LogError("AMUSEMENT SMOKE FAILED: " + message); Application.Quit(1); }
    }
}
#endif
