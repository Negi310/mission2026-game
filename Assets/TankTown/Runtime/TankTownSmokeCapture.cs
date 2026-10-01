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
            var camera = Camera.main;
            game.CameraRig.enabled = false;
            camera.fieldOfView = 50;
            float previousFarClip = camera.farClipPlane;
            camera.farClipPlane = 1000;
            camera.transform.position = new Vector3(1.5f, 1.7f, -1.5f) * game.World.HalfExtent;
            camera.transform.LookAt(Vector3.up * 5);
            Capture(Path.Combine(output, "02-city.png"), false);
            camera.fieldOfView = game.Settings.cameraFieldOfView;
            camera.farClipPlane = previousFarClip;
            game.CameraRig.enabled = true;
            game.CameraRig.Snap();
            game.StartOrRestart();
            yield return new WaitForSeconds(3.25f);
            Capture(Path.Combine(output, "03-upgrades.png"));
            yield return new WaitForSeconds(2.2f);
            var body = game.Tank.GetComponent<Rigidbody>();
            var building = game.World.Buildings[0];
            body.position = building.transform.position + new Vector3(-4.6f, game.Settings.rideHeight, -building.Width / 2 - 9);
            body.rotation = Quaternion.identity;
            game.Tank.transform.SetPositionAndRotation(body.position, body.rotation);
            Physics.SyncTransforms();
            game.CameraRig.Snap();
            yield return new WaitForFixedUpdate();
            game.Tank.Fire(0);
            yield return null;
            Capture(Path.Combine(output, "04-muzzle.png"));
            yield return new WaitForSeconds(.28f);
            Capture(Path.Combine(output, "05-explosion.png"));
            yield return new WaitForSeconds(.2f);
            game.CameraRig.enabled = false;
            // An actual detached facade exposes the air-filled frame in a diagnostic view.
            foreach (var wall in building.Blocks.FindAll(x => x.Kind == BuildingPart.Wall && x.Column == 0)) wall.Break(wall.transform.position + Vector3.forward * 2, 18);
            building.ReleaseUnsupported(building.transform.position, 0);
            yield return new WaitForSeconds(.85f);
            camera.transform.position = building.transform.position + new Vector3(29, 23, -34);
            camera.transform.LookAt(building.transform.position + Vector3.up * 8);
            Capture(Path.Combine(output, "06-frame.png"), false);
            game.CameraRig.enabled = true;
            game.Tank.Explode();
            yield return new WaitForSeconds(.15f);
            Capture(Path.Combine(output, "07-respawn.png"));
            yield return new WaitForSeconds(game.Settings.respawnDelay + .3f);
            body.position = game.World.GroundPoint(new Vector3(0, 0, 8), game.Settings.rideHeight);
            body.rotation = Quaternion.identity; body.linearVelocity = Vector3.zero;
            game.Tank.transform.SetPositionAndRotation(body.position, body.rotation);
            Physics.SyncTransforms(); game.CameraRig.Snap();
            yield return new WaitForSeconds(2.8f);
            float hillHeight = body.position.y;
            Capture(Path.Combine(output, "08-slope.png"));
            // Exercise large collapses and the active-debris budget, with normal physics running.
            int peakBodies = 0;
            foreach (var targetBuilding in game.World.Buildings)
            {
                foreach (var pillar in targetBuilding.Blocks.FindAll(x => x != null && x.Foundation && x.Column < 2)) pillar.Break(pillar.transform.position, game.Stats.BlastImpulse);
                targetBuilding.ReleaseUnsupported(targetBuilding.transform.position, game.Stats.BlastImpulse);
                yield return new WaitForSeconds(.08f);
                peakBodies = Mathf.Max(peakBodies, FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length - 1);
            }
            game.AdvanceTime(1000);
            yield return new WaitForSecondsRealtime(.2f);
            Capture(Path.Combine(output, "09-results.png"));
            yield return new WaitForSecondsRealtime(.4f);
            Debug.Log($"SMOKE COMPLETE: blocks={game.BrokenBlocks}/{game.TotalBlocks}, crashes={game.Crashes}, state={game.State}, debrisPeak={peakBodies}, speedLevel={game.Stats.Level(UpgradeKind.Speed)}, powerLevel={game.Stats.Level(UpgradeKind.Power)}, rapidLevel={game.Stats.Level(UpgradeKind.Rapid)}, handlingLevel={game.Stats.Level(UpgradeKind.Handling)}, collapsedBuildings={game.World.Buildings.FindAll(x => x.Collapsed).Count}, parks={game.World.ParkCount}, hillHeight={hillHeight:0.00}");
            Application.Quit(0);
        }

        private static void Capture(string path, bool showHud = true)
        {
            var camera = Camera.main;
            var canvas = FindFirstObjectByType<Canvas>();
            var target = RenderTexture.GetTemporary(1440, 900, 24, RenderTextureFormat.ARGB32);
            var previousTarget = RenderTexture.active;
            bool previousEnabled = canvas.enabled;
            canvas.enabled = showHud;
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
                canvas.planeDistance = camera.nearClipPlane + .01f;
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
                canvas.enabled = previousEnabled;
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
