#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TankTown
{
    public sealed class TankTownPickupFeedbackSmoke : MonoBehaviour
    {
        private TankTownGame game;
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-pickup-feedback-smoke") < 0) return;
            TankTownGame.SelectedStage = TownStage.CandyTown; TankTownGame.GenerationSeedOverride = 2026;
            TankTownGame.SkipIntroForTests = true; TankTownGame.DisableRecordSavingForTests = true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-pickup-feedback-smoke") >= 0)
                new GameObject("Pickup feedback smoke").AddComponent<TankTownPickupFeedbackSmoke>();
        }
        private TankPickup Pickup(UpgradeKind kind, Vector3 offset)
        {
            var root = new GameObject("Feedback demonstration pickup"); root.transform.SetParent(game.World.transform);
            root.transform.position = game.World.GroundPoint(game.Tank.transform.position + offset, 1.2f);
            var pickup = root.AddComponent<TankPickup>(); pickup.Initialize(game, kind, game.World.Icons.Material(kind));
            game.World.Pickups.Add(pickup); return pickup;
        }
        private void Capture(string name, int width = 1920, int height = 1080)
        {
            game.GetComponent<TankTownHUD>().Minimap?.RenderNow();
            TankTownSmokeCapture.Capture(Path.Combine(output, "upgrade-feedback-" + name + ".png"), true, width, height);
        }
        private IEnumerator Start()
        {
            var run = Run();
            while (true)
            {
                bool more = false; Exception failure = null;
                try { more = run.MoveNext(); }
                catch (Exception error) { failure = error; }
                if (failure != null) { Debug.LogException(failure); Application.Quit(1); yield break; }
                if (!more) yield break;
                yield return run.Current;
            }
        }
        private IEnumerator Run()
        {
            Application.runInBackground = true; Time.captureDeltaTime = 1f / 60;
            yield return new WaitForSecondsRealtime(.4f);
            game = FindFirstObjectByType<TankTownGame>(); game.StartOrRestart(); game.Input.useHardware = true;
            game.Tank.Warp(new Pose(game.World.SpawnPosition, Quaternion.identity)); game.Tank.enabled = false;
            var body = game.Tank.GetComponent<Rigidbody>(); body.isKinematic = true; game.CameraRig.Snap();
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var person in game.World.Pedestrians) person.gameObject.SetActive(false);
            foreach (var pickup in game.World.Pickups) pickup.gameObject.SetActive(false);
            foreach (var pickup in game.World.ItemPickups) pickup.gameObject.SetActive(false);
            output = Path.Combine(Application.dataPath, "..", "Captures"); Directory.CreateDirectory(output);
            var speed = Pickup(UpgradeKind.Speed, Vector3.forward * 5);
            yield return null; Capture("01-field");
            var original = speed.Icon; speed.Collect();
            yield return new WaitForSeconds(.10f); Capture("02-flight");
            yield return new WaitForSeconds(.22f); Capture("03-above");
            foreach (var kind in new[] { UpgradeKind.Power, UpgradeKind.Rapid, UpgradeKind.Handling })
                Pickup(kind, new Vector3(((int)kind - 2) * 2, 0, 5)).Collect();
            yield return new WaitForSeconds(.32f); Capture("04-multiple"); Capture("05-multiple-720", 1280, 720);
            if (game.PickupDisplay.Count != 4 || speed.Icon != original) throw new InvalidOperationException("Field icon continuity or simultaneous display failed");
            body.isKinematic = false; game.Tank.enabled = true; game.Input.SetWheel(.45f);
            yield return new WaitForSeconds(.42f); Capture("06-driving"); game.Input.SetWheel(0);
            game.Tank.enabled = false; body.linearVelocity = Vector3.zero; body.isKinematic = true;
            yield return new WaitForSeconds(1.7f);
            if (game.PickupDisplay.Count != 0 || speed.gameObject.activeSelf) throw new InvalidOperationException("Pickup display did not expire");
            var building = game.World.Buildings.First(x => x.Style == 2);
            var start = game.World.GroundPoint(building.transform.position + Vector3.back * (building.Width / 2 + 4.5f), game.Settings.rideHeight);
            game.Tank.Warp(new Pose(start, Quaternion.identity)); game.CameraRig.Snap();
            // Wait out the ordinary grace period after the diagnostic relocation.
            yield return new WaitForSeconds(1.6f);
            int before = game.BrokenBlocks; float crashAt = 0;
            body.isKinematic = false; game.Tank.enabled = true; body.linearVelocity = Vector3.forward * game.Stats.Speed;
            float deadline = Time.time + 2;
            while (!game.Tank.Respawning && Time.time < deadline) yield return null;
            if (!game.Tank.Respawning) throw new InvalidOperationException("The real building collision did not trigger a crash");
            crashAt = Time.time;
            yield return new WaitForSeconds(.06f); Capture("07-crash");
            var texts = FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (texts.Any(x => (x.name == "Action heading" || x.name == "Action caption") && x.gameObject.activeInHierarchy))
                throw new InvalidOperationException("Crash action text remained visible");
            while (game.Tank.Respawning && Time.time - crashAt < 1) yield return null;
            float respawnSeconds = Time.time - crashAt;
            if (game.Tank.Respawning || respawnSeconds > .65f || game.BrokenBlocks <= before)
                throw new InvalidOperationException("Fast recovery or the crash blast failed");
            yield return new WaitForSeconds(.45f); Capture("08-recovered");
            Debug.Log($"PICKUP FEEDBACK SMOKE COMPLETE: captures=8, reused=True, simultaneous=4, respawn={respawnSeconds:F3}s, crashDamage={game.BrokenBlocks - before}, crashes={game.Crashes}, seed={game.CitySeed}");
            Application.Quit(0);
        }
    }
}
#endif
