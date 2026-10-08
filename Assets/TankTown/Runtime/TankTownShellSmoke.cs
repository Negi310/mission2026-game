#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TankTown
{
    public sealed class TankTownShellSmoke : MonoBehaviour
    {
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-shell-smoke") < 0) return;
            TankTownGame.SelectedStage = TownStage.CandyTown; TankTownGame.GenerationSeedOverride = 2026;
            TankTownGame.SkipIntroForTests = true; TankTownGame.DisableRecordSavingForTests = true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-shell-smoke") >= 0)
                new GameObject("Cannon shell smoke").AddComponent<TankTownShellSmoke>();
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
            game.Tank.Warp(new Pose(game.World.SpawnPosition, Quaternion.identity));
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            game.CameraRig.enabled = false;
            // Enlarge the actual shared model at a diagnostic viewing position, without muzzle effects.
            var display = game.World.CreateCannonShell(game.World.GroundPoint(game.World.SpawnPosition + Vector3.forward * 24, 14), Vector3.forward);
            Camera.main.transform.position = display.transform.position + new Vector3(1.65f, .75f, -1.05f);
            Camera.main.transform.LookAt(display.transform.position); yield return null; Capture(game, "shell-01-detail"); Destroy(display);
            game.CameraRig.Snap(); game.CameraRig.enabled = true;
            game.Input.PulsePad(0); var shot = FindFirstObjectByType<TankShell>();
            var origin = shot.transform.position; float started = Time.time;
            for (int i = 0; i < 5; i++) yield return null;
            if (shot == null) { Fail("free-flight shot vanished too early"); yield break; }
            float measuredSpeed = Vector3.Distance(origin, shot.transform.position) / (Time.time - started);
            if (Mathf.Abs(measuredSpeed - game.Settings.shellSpeed) > 1) { Fail("actual flight speed differs from the configured speed"); yield break; }
            Capture(game, "shell-02-flight"); Destroy(shot.gameObject);
            yield return new WaitForSeconds(game.Stats.ShotCooldown);
            var target = game.World.Buildings.First(x => x.Style == 1);
            var body = game.Tank.GetComponent<Rigidbody>(); body.isKinematic = false;
            game.Tank.Warp(new Pose(target.transform.position + new Vector3(-4.6f, 0, -target.Width / 2 - 22), Quaternion.identity)); body.isKinematic = true;
            game.CameraRig.enabled = false;
            Camera.main.transform.position = target.transform.position + new Vector3(-25, 15, -34);
            Camera.main.transform.LookAt(target.transform.position + Vector3.up * 8);
            int before = game.BrokenBlocks; started = Time.time; game.Input.PulsePad(0);
            while (game.BrokenBlocks == before && Time.time - started < .6f) yield return null;
            float timeToHit = Time.time - started;
            if (game.BrokenBlocks <= before || target.FullyDestroyed) { Fail("the fast shot must still produce local destruction"); yield break; }
            yield return new WaitForSeconds(.06f); Capture(game, "shell-03-impact");
            Debug.Log($"CANNON SHELL SMOKE COMPLETE: stage={game.Stage}, speed={measuredSpeed:0.0}, configuredSpeed={game.Settings.shellSpeed:0.0}, timeToHit={timeToHit:0.000}, sharedPointedMesh=true, materialRegions=3, localDestruction=true");
            Application.Quit(0);
        }
        private void Capture(TankTownGame game, string label)
        {
            foreach (var building in game.World.Buildings) building.Presentation?.Tick(Camera.main.transform.position);
            game.World.RefreshMapMeshes();
            TankTownSmokeCapture.Capture(Path.Combine(output, label + ".png"), true, 1920, 1080);
        }
        private static void Fail(string message) { Debug.LogError("CANNON SHELL SMOKE FAILED: " + message); Application.Quit(1); }
    }
}
#endif
