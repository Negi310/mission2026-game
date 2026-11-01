#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace TankTown
{
    public sealed class TankTownAimSmoke : MonoBehaviour
    {
        private TankTownGame game;
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--tank-town-aim-smoke") < 0) return;
            TankTownGame.SelectedStage = Array.IndexOf(args, "--aim-town") >= 0 ? TownStage.Town :
                Array.IndexOf(args, "--aim-big") >= 0 ? TownStage.BigCity :
                Array.IndexOf(args, "--aim-amusement") >= 0 ? TownStage.AmusementPark : TownStage.CandyTown;
            TankTownGame.GenerationSeedOverride = 2026; TankTownGame.SkipIntroForTests = true;
            TankTownGame.DisableRecordSavingForTests = true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-aim-smoke") >= 0)
                new GameObject("Cannon aim smoke").AddComponent<TankTownAimSmoke>();
        }
        private IEnumerator Start()
        {
            var run = Run();
            while (true)
            {
                bool more = false; Exception failure = null;
                try { more = run.MoveNext(); } catch (Exception error) { failure = error; }
                if (failure != null) { Debug.LogException(failure); Application.Quit(1); yield break; }
                if (!more) yield break;
                yield return run.Current;
            }
        }
        private IEnumerator Run()
        {
            Application.runInBackground = true; Time.captureDeltaTime = 1f / 60;
            yield return new WaitForSecondsRealtime(.3f);
            game = FindFirstObjectByType<TankTownGame>();
            output = Path.Combine(Application.dataPath, "..", "Captures", "Aim-" + game.Stage);
            Directory.CreateDirectory(output);
            game.StartOrRestart(); game.Input.useHardware = true;
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            game.CameraRig.Snap(); yield return null; Capture("01-play");
            if (game.Settings.roundSeconds != 80) throw new InvalidOperationException("The round must last 80 seconds.");
            var building = game.World.Buildings[0];
            game.Tank.Warp(new Pose(building.transform.position + new Vector3(-building.Width * .5f - 2, 0, -building.Width * .5f - 24), Quaternion.identity));
            game.CameraRig.enabled = false;
            CannonAimSolution solution = default;
            float correction = 0;
            for (int yaw = -18; yaw <= 18; yaw++)
            {
                game.Tank.Turret.localRotation = Quaternion.Euler(0, yaw, 0); game.CameraRig.Snap();
                Physics.SyncTransforms(); solution = game.Tank.CannonAim.Resolve(Camera.main, true);
                correction = Vector3.Angle(solution.Direction, game.Tank.Muzzle.forward);
                if (solution.Target != null && correction > .1f) break;
            }
            if (solution.Target == null || correction <= .1f || correction > game.Settings.cannonAimAssistAngle + .01f)
                throw new InvalidOperationException("A visible near-miss must find a valid assisted building target.");
            yield return new WaitForSeconds(.1f); Capture("02-assist"); Capture("03-assist-720", 1280, 720);
            int before = game.BrokenBlocks;
            game.Input.PulsePad(0); yield return null; Capture("04-fire");
            for (int i = 0; i < 60 && game.BrokenBlocks == before; i++) yield return null;
            if (game.BrokenBlocks <= before) throw new InvalidOperationException("The actual assisted cannon shot failed to break the building.");
            yield return new WaitForSeconds(.1f); Capture("05-impact");
            game.TogglePause(); yield return null; Capture("06-pause"); game.TogglePause();
            game.AdvanceTime(game.Remaining + 1); yield return null; Capture("07-results");
            Debug.Log($"CANNON AIM SMOKE COMPLETE: stage={game.Stage}, seconds={game.Settings.roundSeconds}, correction={correction:0.00}, broken={game.BrokenBlocks - before}, captures=7");
            Application.Quit(0);
        }
        private void Capture(string name, int width = 1920, int height = 1080)
        {
            var hud = game.GetComponent<TankTownHUD>();
            TankTownSmokeCapture.Capture(Path.Combine(output, name + ".png"), true, width, height, hud.RefreshLayoutForCapture);
        }
    }
}
#endif
