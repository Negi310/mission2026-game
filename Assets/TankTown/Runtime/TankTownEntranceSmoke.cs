#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TankTown
{
    public sealed class TankTownEntranceSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--tank-town-entrance-smoke") < 0) return;
            var argument = args.FirstOrDefault(x => x.StartsWith("--tank-town-entrance-stage="));
            TankTownGame.SelectedStage = argument != null && Enum.TryParse(argument.Substring("--tank-town-entrance-stage=".Length), out TownStage stage) ? stage : TownStage.CandyTown;
            TankTownGame.GenerationSeedOverride = 2026; TankTownGame.SkipIntroForTests = true; TankTownGame.DisableRecordSavingForTests = true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-entrance-smoke") >= 0)
                new GameObject("Entrance smoke").AddComponent<TankTownEntranceSmoke>();
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true; Time.captureDeltaTime = 1f / 60;
            yield return new WaitForSecondsRealtime(.4f);
            var game = FindFirstObjectByType<TankTownGame>(); game.StartOrRestart(); game.Input.useHardware = true;
            game.Tank.Warp(new Pose(game.World.SpawnPosition, Quaternion.identity)); game.Tank.enabled = false;
            game.Tank.GetComponent<Rigidbody>().isKinematic = true; game.CameraRig.enabled = false;
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var person in game.World.Pedestrians) person.gameObject.SetActive(false);
            var houses = game.World.Buildings.Where(x => x.Style != 3).ToArray();
            if (houses.Any(x => x.GetComponentsInChildren<Transform>().Count(t => t.name == "Building entrance") != 1))
            { Debug.LogError("ENTRANCE SMOKE FAILED: a house is missing its entrance"); Application.Quit(1); yield break; }
            string output = Path.Combine(Application.dataPath, "..", "Captures"); Directory.CreateDirectory(output);
            foreach (var house in houses.GroupBy(x => x.Style).Select(x => x.First()))
            {
                var door = house.GetComponentsInChildren<Transform>().First(x => x.name == "Building entrance");
                Camera.main.transform.position = door.position + door.forward * 11 + door.right * 4 + Vector3.up * 4;
                Camera.main.transform.LookAt(door.position + Vector3.up * 1.8f); yield return null;
                foreach (var building in game.World.Buildings) building.Presentation?.Tick(Camera.main.transform.position);
                game.World.RefreshMapMeshes();
                game.GetComponent<TankTownHUD>().Minimap?.RenderNow();
                TankTownSmokeCapture.Capture(Path.Combine(output, $"entrance-{game.Stage}-{house.Style}.png"), true, 1920, 1080);
            }
            Debug.Log($"ENTRANCE SMOKE COMPLETE: stage={game.Stage}, houses={houses.Length}, entrances={houses.Length}, styles=3, seed={game.CitySeed}");
            Application.Quit(0);
        }
    }
}
#endif
