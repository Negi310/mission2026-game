#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TankTown
{
    public sealed class TankTownVisualPolishSmoke : MonoBehaviour
    {
        private TankTownGame game;
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Contains("--tank-town-polish-smoke")) return;
            TankTownGame.SelectedStage = args.Contains("--polish-town") ? TownStage.Town : args.Contains("--polish-big") ? TownStage.BigCity : args.Contains("--polish-amusement") ? TownStage.AmusementPark : TownStage.CandyTown;
            TankTownGame.GenerationSeedOverride = 2026; TankTownGame.DisableRecordSavingForTests = true; TankTownGame.SkipIntroForTests = true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Environment.GetCommandLineArgs().Contains("--tank-town-polish-smoke")) new GameObject("Visual polish smoke").AddComponent<TankTownVisualPolishSmoke>();
        }

        private IEnumerator Start()
        {
            var run = Run();
            while (true)
            {
                object current = null; bool more = false; Exception failure = null;
                try { more = run.MoveNext(); if (more) current = run.Current; }
                catch (Exception error) { failure = error; }
                if (failure != null) { Debug.LogException(failure); Application.Quit(1); yield break; }
                if (!more) yield break;
                yield return current;
            }
        }

        private IEnumerator Run()
        {
            Application.runInBackground = true; Time.captureDeltaTime = 1f / 60;
            yield return null;
            game = FindFirstObjectByType<TankTownGame>();
            output = Path.Combine(Application.dataPath, "..", "Captures", "Polish-" + game.Stage); Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(.3f); Capture("01-title");
            game.StartOrRestart();
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            game.Input.useHardware = true;
            foreach (var pickup in game.World.Pickups) pickup.gameObject.SetActive(false);
            foreach (var pickup in game.World.ItemPickups) pickup.gameObject.SetActive(false);
            yield return new WaitForSeconds(.15f); Capture("02-play");
            var cameraData = Camera.main.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false; Capture("02-play-no-bloom"); cameraData.renderPostProcessing = true;
            var grading = VolumeManager.instance.stack.GetComponent<ColorAdjustments>();
            Debug.Log($"POLISH LIGHTING: tone={VolumeManager.instance.stack.GetComponent<Tonemapping>().mode.value}, exposure={grading.postExposure.value}, saturation={grading.saturation.value}, filter={grading.colorFilter.value}, bloom={VolumeManager.instance.stack.GetComponent<Bloom>().intensity.value}");
            Capture("02-play-720", 1280, 720);
            // Keep the normal play capture intact; isolate close-ups and effects from traffic collisions.
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var person in game.World.Pedestrians) person.gameObject.SetActive(false);
            game.CameraRig.enabled = false;
            var building = game.World.Buildings.FirstOrDefault(x => x.Floors > 1 && x.Blocks.Any(b => b.Kind == BuildingPart.Wall))
                ?? game.World.Buildings.First(x => x.Blocks.Any(b => b.Kind == BuildingPart.Wall));
            Camera.main.transform.position = building.transform.position + new Vector3(-building.Width * .8f, 10, -building.Width * 1.3f);
            Camera.main.transform.LookAt(building.transform.position + Vector3.up * 6);
            yield return null; Capture("03-building");
            if (game.BuiltStage == TownStage.Town || game.BuiltStage == TownStage.BigCity)
            {
                TreeView();
                yield return null; Capture("04-tree");
            }
            else if (game.BuiltStage == TownStage.CandyTown)
            {
                var chocolate = game.World.Buildings.First(x => x.Style == 1);
                Camera.main.transform.position = chocolate.transform.position + new Vector3(-chocolate.Width * .7f, 8, -chocolate.Width * 1.15f);
                Camera.main.transform.LookAt(chocolate.transform.position + Vector3.up * 5);
                yield return null; Capture("04-chocolate");
            }
            game.CameraRig.enabled = true; game.CameraRig.Snap(); yield return null;
            Vector3 point = game.Tank.Muzzle.position + game.Tank.Muzzle.forward * 14 + Vector3.up * .5f;
            game.Effects.Impact(point, game.Stats.BlastRadius);
            yield return new WaitForSeconds(.035f); Capture("05-flash");
            yield return new WaitForSeconds(.46f); Capture("06-flame");
            yield return new WaitForSeconds(2.0f); Capture("07-smoke");
            if (game.Effects.ActiveExplosionCount == 0 || !game.Effects.GetComponentsInChildren<ParticleSystem>().Any(x => x.IsAlive()))
                throw new InvalidOperationException("Expanded explosion ended before its aftermath");
            yield return new WaitForSeconds(1.5f); Capture("08-cleared");
            if (game.Effects.ActiveExplosionCount != 0 || Shader.GetGlobalInt("_ToyFlashCount") != 0) throw new InvalidOperationException("Explosion did not clear its particles and illumination");

            game.Effects.CarImpact(point, 10);
            yield return new WaitForSeconds(.5f); Capture("08b-car-flame");
            yield return new WaitForSeconds(TankExplosionVfx.BurstLifetime);
            game.Effects.SuperImpact(point + game.Tank.Muzzle.forward * 20, game.Settings.gigantBombRadius);
            yield return new WaitForSeconds(.5f); Capture("08c-super-flame");
            yield return new WaitForSeconds(TankExplosionVfx.BurstLifetime);

            // Measure VFX cleanup before invincible ramming can cause real secondary explosions.
            for (int i = 0; i < 40; i++) game.Effects.Impact(point + new Vector3(i % 4 * 2, 0, i / 4 * 2), 8);
            yield return null;
            if (game.Effects.ActiveExplosionCount > TankExplosionVfx.Capacity || game.Effects.AllocatedExplosionCount > TankExplosionVfx.Capacity)
                throw new InvalidOperationException("Explosion pool exceeded its budget");
            yield return new WaitForSeconds(TankExplosionVfx.BurstLifetime + .2f);
            if (game.Effects.ActiveExplosionCount != 0 || Shader.GetGlobalInt("_ToyFlashCount") != 0) throw new InvalidOperationException("Burst stress did not clear");

            game.Settings.magnetSeconds = game.Settings.bulldozerSeconds = game.Settings.highSpeedSeconds = 4;
            game.Items.Receive(ItemKind.Magnet); game.Items.Receive(ItemKind.Bulldozer); game.Items.Receive(ItemKind.HighSpeed);
            game.CameraRig.enabled = false;
            Camera.main.transform.position = game.Tank.transform.position + new Vector3(10, 8, -14);
            Camera.main.transform.LookAt(game.Tank.transform.position + Vector3.up * 1.5f);
            yield return new WaitForSeconds(.2f); Capture("09-effects");
            yield return new WaitForSeconds(1.0f);
            var hull = game.Tank.Visual.Find("Hull").GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock(); bool bright = false, normal = false;
            var ring = game.Tank.Visual.Find("Magnetic field ring").gameObject;
            var shield = game.Tank.Visual.Find("High speed energy shield").gameObject;
            float until = Time.time + .8f;
            while (Time.time < until)
            {
                hull.GetPropertyBlock(properties); bool flash = properties.GetFloat("_ItemFlash") > .5f;
                if (flash && !bright) { Capture("10-warning-flash"); bright = true; }
                if (!flash && !normal) { Capture("11-warning-normal"); normal = true; }
                if (!game.Items.Invincible || game.Tank.VehicleScale != game.Settings.bulldozerScale || ring.activeSelf != shield.activeSelf)
                    throw new InvalidOperationException("Expiry warning changed ability state or lost a visual layer");
                yield return null;
            }
            if (!bright || !normal) throw new InvalidOperationException("Item warning did not blink");
            game.TogglePause(); float remaining = game.Items.Remaining(ItemKind.Bulldozer); bool ringVisible = ring.activeSelf;
            yield return new WaitForSecondsRealtime(.2f);
            if (remaining != game.Items.Remaining(ItemKind.Bulldozer) || ring.activeSelf != ringVisible) throw new InvalidOperationException("Paused warning changed");
            game.TogglePause();
            game.Items.Receive(ItemKind.Magnet); game.Items.Receive(ItemKind.Bulldozer); game.Items.Receive(ItemKind.HighSpeed);
            yield return null; Capture("12-refreshed");
            game.Items.FinishRound(); yield return null;
            if (ring.activeSelf || shield.activeSelf || game.Tank.VehicleScale != 1) throw new InvalidOperationException("Item visuals did not reset");
            foreach (var renderer in game.Tank.Visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.GetPropertyBlock(properties);
                if (properties.GetFloat("_ItemFlash") != 0) throw new InvalidOperationException("Vehicle expiry tint survived cleanup");
            }
            game.CameraRig.enabled = true; game.CameraRig.Snap();
            // A real local wall hit also exercises batched detail detachment and the fracture path.
            var wall = building.Blocks.First(x => x.Kind == BuildingPart.Wall);
            int broken = game.BrokenBlocks; game.Blast(wall.transform.position, game.Stats.BlastRadius, game.Stats.BlastImpulse, wall);
            yield return new WaitForSeconds(.22f);
            Camera.main.transform.position = building.transform.position + new Vector3(-building.Width, 12, -building.Width * 1.3f);
            game.CameraRig.enabled = false; Camera.main.transform.LookAt(wall.transform.position);
            Capture("13-local-destruction");
            if (game.BrokenBlocks <= broken) throw new InvalidOperationException("Detailed wall did not break");
            Debug.Log($"VISUAL POLISH COMPLETE: stage={game.Stage}, seed={game.CitySeed}, layeredFireSmoke=true, flashLightBudget=4, pool={game.Effects.AllocatedExplosionCount}/{TankExplosionVfx.Capacity}, expiryAllThree=true, pause=true, refresh=true, restored=true, localWallBreak=true, meshShared=true");
            Application.Quit(0);
        }
        private void Capture(string name, int width = 1920, int height = 1080)
            => TankTownSmokeCapture.Capture(Path.Combine(output, name + ".png"), true, width, height);

        private void TreeView()
        {
            foreach (var tree in FindObjectsByType<DestructibleBlock>(FindObjectsSortMode.None).Where(x => x.name == "Tree").OrderBy(x => (x.transform.position - game.Tank.transform.position).sqrMagnitude))
            {
                Vector3 look = tree.transform.position + Vector3.up * 2.3f;
                for (int angle = 0; angle < 360; angle += 45)
                {
                    Vector3 eye = look + Quaternion.Euler(0, angle, 0) * new Vector3(0, 2.2f, -7);
                    if (Physics.CheckSphere(eye, .6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.Linecast(eye, look, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<DestructibleBlock>() != tree) continue;
                    Camera.main.transform.position = eye; Camera.main.transform.LookAt(look); return;
                }
            }
            throw new InvalidOperationException("No unobstructed tree detail view");
        }
    }
}
#endif
