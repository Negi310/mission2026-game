#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TankTown
{
    public sealed class TankTownGenerationSmoke : MonoBehaviour
    {
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-generation-smoke") >= 0 && FindFirstObjectByType<TankTownGenerationSmoke>() == null)
            {
                var root = new GameObject("Random town smoke"); DontDestroyOnLoad(root); root.AddComponent<TankTownGenerationSmoke>();
            }
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true; Time.captureDeltaTime = 1f / 60;
            output = Path.Combine(Application.dataPath, "..", "Captures"); Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(2);
            TankTownGame game = null;
            foreach (int seed in new[] { 2026, 67541 })
            {
                TankTownGame.GenerationSeedOverride = seed;
                Time.timeScale = 1; SceneManager.LoadScene("TankTown"); yield return null; yield return null;
                game = FindFirstObjectByType<TankTownGame>();
                if (seed == 2026) { Capture("generated-01-title", 1280, 720); yield return null; }
                game.StartOrRestart(); game.Input.useHardware = true;
                game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
                if (game.Remaining > game.Settings.roundSeconds || game.World.ItemPickups.Any(p => p.Kind != p.SourceBuilding.RewardKind)) { Fail("wrong time or color reward"); yield break; }
                View(game, new Vector3(-145, 165, -180), new Vector3(0, 4, 0));
                yield return null; Capture("generated-" + seed + "-overview", 1920, 1080, false);
                Debug.Log($"GENERATED MAP SMOKE: seed={game.CitySeed}, buildings={game.World.Buildings.Count}, parks={game.World.ParkCount}, colors={game.World.ItemPickups.Select(p => p.Kind).Distinct().Count()}, centerHeight={game.World.Terrain.HeightAt(Vector3.zero):0.00}");
            }
            // Use the first seed again for a repeatable weapon and timer tour.
            TankTownGame.GenerationSeedOverride = 2026; Time.timeScale = 1;
            SceneManager.LoadScene("TankTown"); yield return null; yield return null;
            game = FindFirstObjectByType<TankTownGame>(); game.StartOrRestart(); game.Input.useHardware = true;
            game.Tank.enabled = false; var tankBody = game.Tank.GetComponent<Rigidbody>(); tankBody.isKinematic = true;
            game.CameraRig.Snap(); yield return null; Capture("generated-02-street", 1280, 720);
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var person in game.World.Pedestrians) person.gameObject.SetActive(false);
            var front = game.World.TrafficCars[0]; var rear = game.World.TrafficCars[1];
            front.gameObject.SetActive(true); rear.gameObject.SetActive(true);
            Vector3 origin = game.World.SpawnPosition;
            front.SetCircuit(new[] { origin + Vector3.forward * 20, origin + Vector3.forward * 50 }, 0);
            rear.SetCircuit(new[] { origin - Vector3.forward * 8, origin - Vector3.forward * 13 }, 0);
            Physics.SyncTransforms();
            game.Items.Receive(ItemKind.Missiles);
            var rockets = FindObjectsByType<ItemMissile>(FindObjectsSortMode.None);
            if (rockets.Length != 5 || rockets.Any(m => m.TrackingTarget != front.GetComponent<TownMovingTarget>())) { Fail("missiles selected a hidden or rear target"); yield break; }
            yield return new WaitForSeconds(.32f); Capture("generated-03-forward-missiles", 1920, 1080);
            for (int i = 0; i < 180 && game.Items.MissileTrackedStrikes == 0; i++) yield return new WaitForFixedUpdate();
            if (game.Items.MissileTrackedStrikes == 0 || rear.GetComponent<DestructibleBlock>().Detached) { Fail("missiles missed the front car or destroyed the rear car"); yield break; }
            int tracked = game.Items.MissileTrackedStrikes;
            yield return new WaitForSeconds(game.Items.Remaining(ItemKind.Missiles) + .05f);
            foreach (var target in game.World.MovingTargets) if (target != null) target.gameObject.SetActive(false);
            game.Items.Receive(ItemKind.GigantBomb); game.Input.PulsePad(0);
            var bomb = FindFirstObjectByType<GigantBomb>();
            float throwSpeed = Vector3.ProjectOnPlane(bomb.GetComponent<Rigidbody>().linearVelocity, Vector3.up).magnitude;
            yield return new WaitForSeconds(.45f);
            View(game, origin + new Vector3(-30, 24, -8), origin + new Vector3(0, 8, 25));
            yield return null; Capture("generated-04-bomb-throw", 1920, 1080);
            while (game.Items.Weapon == ItemKind.GigantBomb) yield return null;
            // Let newly created blast visuals expand before recording the detonation.
            yield return new WaitForSeconds(.12f);
            Capture("generated-05-fast-bomb-explosion", 1920, 1080);
            game.CameraRig.enabled = true; game.CameraRig.Snap(); yield return null;
            game.AdvanceTime(Mathf.Max(0, game.Remaining - .08f));
            yield return new WaitForSecondsRealtime(.3f); Capture("generated-06-results", 1280, 720);
            if (game.State != RoundState.Results || throwSpeed < 37 || bomb != null) { Fail("bomb fuse or one-minute results failed"); yield break; }
            Debug.Log($"GENERATION SMOKE COMPLETE: seed={game.CitySeed}, seconds={game.Settings.roundSeconds}, buildings={game.World.Buildings.Count}, parks={game.World.ParkCount}, sixColors=true, forwardOnly=true, trackedHits={tracked}, bombSpeed={throwSpeed:0.00}, bombFuse={game.Settings.gigantBombFuse}, state={game.State}");
            TankTownGame.GenerationSeedOverride = null; Application.Quit(0);
        }
        private static void View(TankTownGame game, Vector3 position, Vector3 look)
        { game.CameraRig.enabled = false; Camera.main.transform.position = position; Camera.main.transform.LookAt(look); }
        private void Capture(string name, int width, int height, bool hud = true)
            => TankTownSmokeCapture.Capture(Path.Combine(output, name + ".png"), hud, width, height);
        private static void Fail(string message) { Debug.LogError("GENERATION SMOKE FAILED: " + message); Application.Quit(1); }
    }
}
#endif
