#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace TankTown
{
    public sealed class TankTownPerformanceSmoke : MonoBehaviour
    {
        [Serializable] private sealed class Result
        {
            public string phase;
            public int frames, maxDebris;
            public float fps, meanMs, p95Ms, worstMs;
            public long drawCalls, triangles, gcBytes;
        }
        [Serializable] private sealed class Report { public Result[] results; }
        private readonly List<Result> results = new List<Result>();
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-performance-smoke") < 0) return;
            TankTownGame.SelectedStage = Array.IndexOf(Environment.GetCommandLineArgs(), "--performance-candy") >= 0 ? TownStage.CandyTown : TownStage.BigCity; TankTownGame.GenerationSeedOverride = 2026;
            TankTownGame.SkipIntroForTests = true; TankTownGame.DisableRecordSavingForTests = true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-performance-smoke") < 0) return;
            new GameObject("Large town performance smoke").AddComponent<TankTownPerformanceSmoke>();
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true; Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0; Time.captureDeltaTime = 0;
            output = Path.Combine(Application.dataPath, "..", "Captures"); Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(.6f);
            var game = FindFirstObjectByType<TankTownGame>();
            yield return Measure(game, "title", 3, .6f);
            game.StartOrRestart(); game.Input.useHardware = true;
            game.Tank.Warp(new Pose(game.World.GroundPoint(new Vector3(0, 0, -80), game.Settings.rideHeight), Quaternion.identity));
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true; game.CameraRig.Snap();
            yield return Measure(game, "street-with-map", 4, .6f);
            game.GetComponent<TankTownHUD>().Minimap.SkipRenderingForDiagnostics = true;
            yield return Measure(game, "street-no-map", 3, .6f);
            game.GetComponent<TankTownHUD>().Minimap.SkipRenderingForDiagnostics = false;
            TankTownSmokeCapture.Capture(Path.Combine(output, "performance-street.png"), true, 1920, 1080);
            foreach (var car in game.World.TrafficCars) car.enabled = false;
            game.Items.Receive(ItemKind.GigantBomb); game.Items.UseHeld();
            var bomb = FindFirstObjectByType<GigantBomb>(); bomb.GetComponent<Rigidbody>().position = game.World.GroundPoint(new Vector3(20, 0, -60), 5);
            bomb.Detonate();
            yield return Measure(game, "destruction", 2.8f, 0);
            TankTownSmokeCapture.Capture(Path.Combine(output, "performance-destruction.png"), true, 1920, 1080);
            File.WriteAllText(Path.Combine(output, "performance-results.json"), JsonUtility.ToJson(new Report { results = results.ToArray() }, true));
            Debug.Log($"PERFORMANCE SMOKE COMPLETE: buildings={game.World.Buildings.Count}, blocks={game.TotalBlocks}, broken={game.BrokenBlocks}");
            Application.Quit(0);
        }
        private IEnumerator Measure(TankTownGame game, string phase, float seconds, float warmup)
        {
            var camera = Camera.main; var canvas = FindFirstObjectByType<Canvas>();
            var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; float oldDistance = canvas.planeDistance;
            var target = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
            var request = new RenderPipeline.StandardRequest { destination = target };
            var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            void Draw()
            {
                Canvas.ForceUpdateCanvases(); RenderPipeline.SubmitRenderRequest(camera, request);
                var oldTarget = RenderTexture.active; RenderTexture.active = target;
                pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false); RenderTexture.active = oldTarget;
            }
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .01f;
                var watch = Stopwatch.StartNew();
                while (watch.Elapsed.TotalSeconds < warmup) { Draw(); yield return null; }
                var times = new List<double>(); long drawTotal = 0, triTotal = 0, gcTotal = 0; int maxDebris = 0;
                watch.Restart(); double previous = 0;
                while (watch.Elapsed.TotalSeconds < seconds)
                {
                    Draw(); yield return null;
                    double now = watch.Elapsed.TotalMilliseconds; times.Add(now - previous); previous = now;
                    drawTotal += draws.Valid ? draws.LastValue : 0; triTotal += tris.Valid ? tris.LastValue : 0; gcTotal += gc.Valid ? gc.LastValue : 0;
                    maxDebris = Math.Max(maxDebris, game.ActiveDebrisCount);
                }
                times.Sort(); int frames = times.Count;
                var result = new Result { phase = phase, frames = frames, fps = frames / (float)watch.Elapsed.TotalSeconds,
                    meanMs = (float)watch.Elapsed.TotalMilliseconds / frames, p95Ms = (float)times[Math.Min(frames - 1, (int)(frames * .95))], worstMs = (float)times[frames - 1],
                    drawCalls = drawTotal / frames, triangles = triTotal / frames, gcBytes = gcTotal / frames, maxDebris = maxDebris };
                results.Add(result);
                Debug.Log($"TOWN PERFORMANCE: phase={phase}, fps={result.fps:0.0}, meanMs={result.meanMs:0.0}, p95Ms={result.p95Ms:0.0}, worstMs={result.worstMs:0.0}, draws={result.drawCalls}, triangles={result.triangles}, gcBytes={result.gcBytes}, debris={result.maxDebris}, synchronized=1920x1080");
            }
            finally
            {
                draws.Dispose(); tris.Dispose(); gc.Dispose();
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
                RenderTexture.ReleaseTemporary(target); Destroy(pixel);
            }
        }
    }
}
#endif
