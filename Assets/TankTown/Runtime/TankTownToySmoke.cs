#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TankTown
{
    // Explicit visual fixtures exercise the shipped renderer, physics, and actual cannon.
    internal sealed class TankTownToySmoke : MonoBehaviour
    {
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-toy-smoke") >= 0)
                new GameObject("Toy presentation smoke").AddComponent<TankTownToySmoke>();
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true;
            output = Path.Combine(Application.dataPath, "..", "Captures"); Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(2);
            var game = FindFirstObjectByType<TankTownGame>();
            yield return Capture("toy-01-title", 1920, 1080);
            game.StartOrRestart(); game.Input.useHardware = true;
            yield return new WaitForSeconds(1.1f);
            game.Tank.enabled = false;
            var body = game.Tank.GetComponent<Rigidbody>(); body.linearVelocity = Vector3.zero; body.isKinematic = true;
            game.Items.Receive(ItemKind.Missiles);
            game.Stats.Upgrade(UpgradeKind.Power); game.Stats.Upgrade(UpgradeKind.Power);
            game.Stats.Upgrade(UpgradeKind.Rapid); game.Stats.Upgrade(UpgradeKind.Handling);
            yield return new WaitForSeconds(1.8f);
            yield return Capture("toy-02-play", 1920, 1080);
            yield return Capture("toy-03-play-small", 1280, 720);
            yield return Capture("toy-04-play-tall", 1440, 900);
            game.Items.FinishRound();
            var building = game.World.Buildings.Where(x => x.transform.position.x < 0 && x.transform.position.x > -game.Settings.blockSpacing)
                .OrderBy(x => x.transform.position.z)
                .First(x => game.World.Explosives.Any(e => e != null && e.name == "Explosive red barrel" && Vector3.Distance(e.transform.position, x.transform.position) < x.Width));
            var pillar = building.Blocks.First(x => x.Foundation && x.Column == 1);
            var barrel = game.World.Explosives.First(e => e != null && e.name == "Explosive red barrel" && Vector3.Distance(e.transform.position, building.transform.position) < building.Width);
            // Put one existing car beyond the cannon blast, within a later fuel blast.
            var chainCar = game.World.TrafficCars[0]; chainCar.enabled = false;
            var carBody = chainCar.GetComponent<Rigidbody>();
            carBody.position = game.World.GroundPoint(barrel.transform.position + Vector3.forward * 9.8f, .72f);
            carBody.rotation = Quaternion.identity;
            Vector3 start = game.World.GroundPoint(new Vector3(-2, 0, pillar.transform.position.z - 24), game.Settings.rideHeight);
            Vector3 forward = pillar.transform.position - start; forward.y = 0;
            body.isKinematic = false; game.Tank.Warp(new Pose(start, Quaternion.LookRotation(forward))); body.isKinematic = true;
            Physics.SyncTransforms(); game.CameraRig.Snap();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return null;
            yield return Capture("toy-07-aim", 1920, 1080);
            // Close diagnostic view checks material grain using the real town renderer.
            var camera = Camera.main;
            float fieldOfView = camera.fieldOfView;
            game.CameraRig.enabled = false;
            camera.fieldOfView = 40;
            camera.transform.position = game.Tank.transform.position + new Vector3(5, 3.5f, -5.5f);
            camera.transform.LookAt(game.Tank.transform.position + Vector3.up * .2f);
            yield return Capture("toy-16-materials", 1600, 900, false);
            camera.fieldOfView = fieldOfView;
            game.CameraRig.enabled = true; game.CameraRig.Snap();
            int before = game.BrokenBlocks;
            game.Tank.Fire(0); yield return new WaitForSeconds(.52f);
            yield return Capture("toy-08-impact", 1920, 1080);
            yield return new WaitForSeconds(.18f);
            yield return Capture("toy-09-chain", 1920, 1080);
            yield return new WaitForSeconds(.28f); game.Tank.Fire(1);
            yield return new WaitForSeconds(.33f);
            yield return Capture("toy-10-collapse", 1920, 1080);
            yield return new WaitForSeconds(1.5f);
            yield return Capture("toy-11-debris", 1920, 1080);
            game.Items.Receive(ItemKind.Missiles); game.Items.UseHeld(); yield return new WaitForSeconds(.45f);
            yield return Capture("toy-05-missiles", 1920, 1080);
            game.Items.Receive(ItemKind.Magnet); game.Items.Receive(ItemKind.HighSpeed); game.Items.Receive(ItemKind.Bulldozer);
            yield return new WaitForSeconds(.4f);
            yield return Capture("toy-06-active-items", 1920, 1080);
            game.Items.FinishRound();
            game.Tank.Explode();
            yield return new WaitForSeconds(.15f);
            // Freeze this actual crash so both window sizes capture the same UI state.
            Time.timeScale = 0;
            yield return Capture("toy-14-crash", 1920, 1080);
            yield return Capture("toy-15-crash-small", 1280, 720);
            Time.timeScale = 1;
            body.isKinematic = false; game.Tank.enabled = true;
            yield return new WaitForSeconds(game.Settings.respawnDelay + .6f);
            bool recovered = !game.Tank.Respawning && game.Tank.Visual.gameObject.activeSelf;
            game.Tank.enabled = false; body.linearVelocity = Vector3.zero; body.isKinematic = true;
            game.TogglePause(); yield return null;
            yield return Capture("toy-12-pause", 1440, 900);
            game.TogglePause(); game.AdvanceTime(1000); yield return null;
            yield return Capture("toy-13-results", 1920, 1080);
            bool damaged = game.BrokenBlocks > before;
            Debug.Log($"TOY SMOKE COMPLETE: realCannonDamage={damaged}, crashRecovery={recovered}, broken={game.BrokenBlocks}/{game.TotalBlocks}, chains={game.LongestChain}, state={game.State}");
            Application.Quit(damaged && recovered ? 0 : 1);
        }
        private IEnumerator Capture(string name, int width, int height, bool showHud = true)
        {
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            for (int i = 0; i < 5; i++) yield return null;
            TankTownSmokeCapture.Capture(Path.Combine(output, name + ".png"), showHud, width, height);
            Debug.Log($"TOY_CAPTURE: {name} {width}x{height}");
        }
    }
}
#endif
