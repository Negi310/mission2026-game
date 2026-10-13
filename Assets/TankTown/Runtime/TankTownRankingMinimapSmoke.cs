#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TankTown
{
    public sealed class TankTownRankingMinimapSmoke : MonoBehaviour
    {
        private static string testKey;
        private Keyboard keyboard;
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-ranking-minimap-smoke") < 0) return;
            testKey = "TankTown.Tests.Ranking.Smoke." + Guid.NewGuid().ToString("N");
            TankTownGame.GenerationSeedOverride = 2026;
            TankTownGame.SkipIntroForTests = true; TankTownGame.DisableRecordSavingForTests = true;
            TankTownGame.RankingStorageOverrideForTests = testKey;
            var board = new TankTownRanking(true, 0, testKey);
            foreach (int score in new[] { 48000, 35000, 22000, 12000, 8000, 6000, 4000, 2000, 1000 }) board.Submit(score, 2025, out _);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (testKey == null || FindFirstObjectByType<TankTownRankingMinimapSmoke>() != null) return;
            var root = new GameObject("Ranking and minimap smoke"); DontDestroyOnLoad(root); root.AddComponent<TankTownRankingMinimapSmoke>();
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            keyboard = InputSystem.AddDevice<Keyboard>(); InputSystem.EnableDevice(keyboard); keyboard.MakeCurrent();
            output = Path.Combine(Application.dataPath, "..", "Captures"); Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(.6f);
            var game = FindFirstObjectByType<TankTownGame>();
            game.StartOrRestart(); game.Input.useHardware = true;
            yield return new WaitForSecondsRealtime(.5f);
            FreezeTank(game);
            var hud = game.GetComponent<TankTownHUD>(); hud.EnableDiagnosticMinimap(); hud.Minimap.Tick(); hud.Minimap.RenderNow();
            if (hud.Minimap.RenderCount == 0 || Camera.main != game.CameraRig.GetComponent<Camera>()) { Fail("missing live map or main camera changed"); yield break; }
            var before = Pixels(hud.Minimap.Texture);
            Capture("ranking-map-01-play", 1920, 1080);
            Capture("ranking-map-02-play-720", 1280, 720);
            SetTank(game, new Vector3(55, 0, -12), 90);
            yield return null; hud.Minimap.Tick(); hud.Minimap.RenderNow();
            if (Mathf.Abs(Mathf.DeltaAngle(hud.Minimap.Marker.localEulerAngles.z, -90)) > .1f || Vector2.Distance(hud.Minimap.Marker.anchoredPosition, hud.Minimap.MapPoint(game.Tank.transform.position)) > .1f) { Fail("player marker position or east-facing arrow"); yield break; }
            Capture("ranking-map-03-east", 1280, 720);
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var building in game.World.Buildings.Take(5))
            {
                foreach (var support in building.Blocks.Where(b => b != null && b.Foundation && b.Column < 2).ToArray()) support.Break(support.transform.position, 0);
                building.ReleaseUnsupported(building.transform.position, 0);
            }
            yield return new WaitForSecondsRealtime(.7f); hud.Minimap.RenderNow();
            var after = Pixels(hud.Minimap.Texture); int changed = before.Where((p, i) => !p.Equals(after[i])).Count();
            if (changed < 500 || game.RoundScore == 0) { Fail("map did not reflect actual city destruction"); yield break; }
            Capture("ranking-map-04-destruction", 1920, 1080);
            game.AdvanceTime(1000); yield return new WaitForSecondsRealtime(1.15f);
            int scoreNow = game.RoundScore, rank = game.FinalRank; string recordId = game.RoundRecord?.Id;
            if (recordId == null || game.Ranking.Records.Count != 10) { Fail("completed round did not enter the leaderboard"); yield break; }
            Capture("ranking-map-05-results", 1920, 1080);
            Capture("ranking-map-06-results-720", 1280, 720);
            var oldMapCamera = hud.Minimap.View; int oldGame = game.GetInstanceID();
            TankTownGame.GenerationSeedOverride = 67541;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
            game = FindFirstObjectByType<TankTownGame>();
            if (game == null || game.GetInstanceID() == oldGame || !game.IsPlaying || game.CitySeed != 67541 || oldMapCamera != null || game.Ranking.Records.All(x => x.Id != recordId)) { Fail("Space replay, saved ranking or old map cleanup"); yield break; }
            game.Input.useHardware = true; FreezeTank(game); SetTank(game, new Vector3(0, 0, -40), 0);
            yield return null; game.GetComponent<TankTownHUD>().EnableDiagnosticMinimap(); game.GetComponent<TankTownHUD>().Minimap.Tick(); game.GetComponent<TankTownHUD>().Minimap.RenderNow();
            Capture("ranking-map-07-new-city", 1920, 1080);
            Debug.Log($"RANKING MINIMAP SMOKE COMPLETE: score={scoreNow}, rank={rank}, records={game.Ranking.Records.Count}, changedMapPixels={changed}, replaySeed={game.CitySeed}, oldCameraReleased={oldMapCamera == null}, mapResolution={TankTownMinimap.TextureSize}, northFixed=true, persistedAcrossReplay=true");
            Clean(); Application.Quit(0);
        }
        private static void FreezeTank(TankTownGame game) { game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true; }
        private static void SetTank(TankTownGame game, Vector3 point, float yaw)
        {
            var body = game.Tank.GetComponent<Rigidbody>(); body.position = game.World.GroundPoint(point, game.Settings.rideHeight);
            body.rotation = Quaternion.Euler(0, yaw, 0); game.Tank.transform.SetPositionAndRotation(body.position, body.rotation);
            Physics.SyncTransforms(); game.CameraRig.Snap();
        }
        private static Color32[] Pixels(RenderTexture texture)
        {
            var previous = RenderTexture.active; var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            try { RenderTexture.active = texture; pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply(); return pixels.GetPixels32(); }
            finally { RenderTexture.active = previous; Destroy(pixels); }
        }
        private void Capture(string name, int width, int height) => TankTownSmokeCapture.Capture(Path.Combine(output, name + ".png"), true, width, height);
        private void Fail(string reason) { Debug.LogError("RANKING MINIMAP SMOKE FAILED: " + reason); Clean(); Application.Quit(1); }
        private static void Clean() { if (testKey != null) { PlayerPrefs.DeleteKey(testKey); PlayerPrefs.Save(); } }
    }
}
#endif
