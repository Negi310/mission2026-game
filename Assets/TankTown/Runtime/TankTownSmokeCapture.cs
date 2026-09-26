#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankTown
{
    // Opt-in visual smoke run for the actual Windows player, not a mock-up.
    public sealed class TankTownSmokeCapture : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-smoke") >= 0)
                new GameObject("Visual smoke capture").AddComponent<TankTownSmokeCapture>();
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            string output = Path.Combine(Application.dataPath, "..", "Captures");
            Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(2);
            var game = FindFirstObjectByType<TankTownGame>();
            if (game == null) { Debug.LogError("SMOKE: missing game"); Application.Quit(1); yield break; }
            Capture(Path.Combine(output, "01-ready.png"));
            yield return new WaitForSecondsRealtime(.5f);
            game.StartOrRestart();
            var body = game.Tank.GetComponent<Rigidbody>();
            body.position = new Vector3(12, 1, -44);
            body.rotation = Quaternion.identity;
            game.CameraRig.Snap();
            yield return new WaitForSeconds(.1f);
            game.Tank.Fire(0);
            yield return new WaitForSeconds(.22f);
            Capture(Path.Combine(output, "02-destruction.png"));
            yield return new WaitForSeconds(.4f);
            game.Tank.Explode();
            yield return new WaitForSeconds(.15f);
            Capture(Path.Combine(output, "03-respawn.png"));
            yield return new WaitForSeconds(game.Settings.respawnDelay + .3f);
            game.AdvanceTime(1000);
            yield return new WaitForSecondsRealtime(.2f);
            Capture(Path.Combine(output, "04-results.png"));
            yield return new WaitForSecondsRealtime(.6f);
            Debug.Log($"SMOKE COMPLETE: blocks={game.BrokenBlocks}/{game.TotalBlocks}, crashes={game.Crashes}, state={game.State}");
            Application.Quit(0);
        }

        private static void Capture(string path)
        {
            var camera = Camera.main;
            var canvas = FindFirstObjectByType<Canvas>();
            var target = RenderTexture.GetTemporary(1440, 900, 24, RenderTextureFormat.ARGB32);
            var previousTarget = RenderTexture.active;
            var previousMode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            float previousDistance = canvas.planeDistance;
            Texture2D pixels = null;
            try
            {
                // Hidden Windows players may skip their backbuffer. Explicitly render the
                // actual game camera and HUD into a texture instead of capturing that buffer.
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .5f;
                Canvas.ForceUpdateCanvases();
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new InvalidOperationException("Render pipeline does not support visual smoke capture");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels = new Texture2D(1440, 900, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousDistance;
                RenderTexture.active = previousTarget;
                RenderTexture.ReleaseTemporary(target);
                if (pixels != null) Destroy(pixels);
            }
        }
    }
}
#endif
