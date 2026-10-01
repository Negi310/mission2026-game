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
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-smoke") >= 0 ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-items-smoke") >= 0 ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-camera-smoke") >= 0)
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
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-camera-smoke") >= 0)
            {
                yield return CameraTour(game, output);
                yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-items-smoke") >= 0)
            {
                yield return ItemsTour(game, output);
                yield break;
            }
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
            float hillSpeed = game.Tank.ActualSpeed;
            float cannonElevation = Mathf.Asin(Vector3.Dot(game.Tank.Muzzle.forward, game.Tank.transform.up)) * Mathf.Rad2Deg;
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
            Debug.Log($"SMOKE COMPLETE: blocks={game.BrokenBlocks}/{game.TotalBlocks}, crashes={game.Crashes}, state={game.State}, debrisPeak={peakBodies}, speedLevel={game.Stats.Level(UpgradeKind.Speed)}, powerLevel={game.Stats.Level(UpgradeKind.Power)}, rapidLevel={game.Stats.Level(UpgradeKind.Rapid)}, handlingLevel={game.Stats.Level(UpgradeKind.Handling)}, collapsedBuildings={game.World.Buildings.FindAll(x => x.Collapsed).Count}, parks={game.World.ParkCount}, hillHeight={hillHeight:0.00}, hillSpeed={hillSpeed:0.00}, cannonElevation={cannonElevation:0.00}");
            Application.Quit(0);
        }

        private IEnumerator CameraTour(TankTownGame game, string output)
        {
            game.StartOrRestart(); game.Input.useHardware = true; game.Input.SetWheel(1);
            yield return null;
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            var body = game.Tank.GetComponent<Rigidbody>();
            float turretLead = Mathf.DeltaAngle(0, game.Tank.Turret.localEulerAngles.y);
            float earlyHull = Mathf.DeltaAngle(0, body.rotation.eulerAngles.y);
            game.CameraRig.Snap();
            float earlyCamera = HorizontalAngle(Camera.main.transform.forward, body.rotation * Vector3.forward);
            float cameraTurret = HorizontalAngle(Camera.main.transform.forward, game.Tank.Muzzle.forward);
            Capture(Path.Combine(output, "turret-camera-01-lead.png"));
            yield return new WaitForSeconds(.45f);
            float followCamera = HorizontalAngle(Camera.main.transform.forward, body.rotation * Vector3.forward);
            float followTurret = HorizontalAngle(Camera.main.transform.forward, game.Tank.Turret.forward);
            float hullTurn = Mathf.DeltaAngle(earlyHull, body.rotation.eulerAngles.y);
            Capture(Path.Combine(output, "turret-camera-02-follow.png"));
            game.Input.SetWheel(0);
            yield return new WaitForSeconds(.7f);
            float centeredCamera = HorizontalAngle(Camera.main.transform.forward, body.rotation * Vector3.forward);
            Capture(Path.Combine(output, "turret-camera-03-centered.png"));
            Debug.Log($"TURRET CAMERA SMOKE COMPLETE: turretLead={turretLead:0.00}, earlyHull={earlyHull:0.00}, earlyCameraHullAngle={earlyCamera:0.00}, earlyCameraTurretAngle={cameraTurret:0.00}, followCameraHullAngle={followCamera:0.00}, followCameraTurretAngle={followTurret:0.00}, hullTurn={hullTurn:0.00}, centeredCameraHullAngle={centeredCamera:0.00}, hullDelay={game.Settings.hullTurnDelay:0.00}, steeringResponse={game.Settings.steeringResponse:0.0}, maxTurretYaw={game.Settings.turretMaxYaw:0.0}, crashes={game.Crashes}");
            Application.Quit(0);
        }

        private static float HorizontalAngle(Vector3 a, Vector3 b)
            => Vector3.Angle(Vector3.ProjectOnPlane(a, Vector3.up), Vector3.ProjectOnPlane(b, Vector3.up));

        private IEnumerator ItemsTour(TankTownGame game, string output)
        {
            game.StartOrRestart();
            game.Input.useHardware = true; game.Input.SetWheel(0);
            var body = game.Tank.GetComponent<Rigidbody>();
            var camera = Camera.main;
            // Keep the real icons visible, but prevent unrelated pickups during this tour.
            foreach (var pickup in game.World.ItemPickups) pickup.GetComponent<Collider>().enabled = false;
            var spawn = body.position;
            for (int i = 0; i < 6; i++)
                game.World.ItemPickups[i].transform.position = game.World.GroundPoint(spawn + new Vector3((i - 2.5f) * 5, 0, 34), 1.2f);
            DiagnosticView(game, spawn + new Vector3(0, 9, -10), spawn + new Vector3(0, 3, 34));
            yield return null;
            Capture(Path.Combine(output, "items-00-field.png"));

            game.Input.SetWheel(1);
            yield return null; // TankInput samples the hardware value in Update.
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            float turretLead = Mathf.DeltaAngle(0, game.Tank.Turret.localEulerAngles.y);
            float earlyHull = Mathf.DeltaAngle(0, body.rotation.eulerAngles.y);
            DiagnosticView(game, body.position + new Vector3(-7, 6, -9), body.position + Vector3.up);
            Capture(Path.Combine(output, "items-01-turret-lead.png"), false);
            for (int i = 0; i < 23; i++) yield return new WaitForFixedUpdate();
            float hullFollow = Mathf.DeltaAngle(0, body.rotation.eulerAngles.y);
            DiagnosticView(game, body.position + new Vector3(-7, 6, -9), body.position + Vector3.up);
            yield return null;
            Capture(Path.Combine(output, "items-02-hull-follow.png"), false);
            game.Input.SetWheel(0);

            ItemFixture(game, 0, 16);
            game.Items.Receive(ItemKind.MegaBeam); game.Input.PulsePad(0);
            var beamStart = body.position;
            yield return new WaitForSeconds(.4f);
            float beamDistance = Vector3.ProjectOnPlane(body.position - beamStart, Vector3.up).magnitude;
            Capture(Path.Combine(output, "items-03-mega-beam.png"));
            int beamHits = game.Items.BeamHits;

            ItemFixture(game, 3, 16);
            game.Items.Receive(ItemKind.Missiles); game.Input.PulsePad(1);
            DiagnosticView(game, body.position + new Vector3(-23, 27, -25), game.World.Buildings[3].transform.position + Vector3.up * 8);
            yield return new WaitForSeconds(.35f);
            int missileCount = FindObjectsByType<ItemMissile>(FindObjectsSortMode.None).Length;
            Capture(Path.Combine(output, "items-04-missiles-up.png"));
            yield return new WaitForSeconds(3);
            Capture(Path.Combine(output, "items-05-missile-rain.png"));
            int missileStrikes = game.Items.MissileStrikes;

            yield return WaitForTankRecovery(game);
            game.Items.FinishRound();
            game.Tank.Warp(new Pose(game.World.GroundPoint(spawn, game.Settings.rideHeight), Quaternion.identity));
            int beforeMagnet = game.World.Pickups.FindAll(x => x.Collected).Count;
            int arranged = 0;
            foreach (var pickup in game.World.Pickups)
            {
                if (pickup.Collected) continue;
                pickup.transform.position = game.World.GroundPoint(body.position + new Vector3((arranged - 1.5f) * 4, 0, 8), 1.2f);
                if (++arranged == 4) break;
            }
            game.Items.Receive(ItemKind.Magnet);
            DiagnosticView(game, body.position + new Vector3(-10, 9, -10), body.position + new Vector3(0, 0, 6));
            yield return new WaitForSeconds(.14f);
            Capture(Path.Combine(output, "items-06-magnet.png"));
            yield return new WaitForSeconds(.6f);
            int magnetCollected = game.World.Pickups.FindAll(x => x.Collected).Count - beforeMagnet;

            yield return WaitForTankRecovery(game);
            ItemFixture(game, 2, 12);
            int beforeDozerCrashes = game.Crashes;
            game.Items.Receive(ItemKind.Bulldozer);
            yield return new WaitForSeconds(.3f);
            float dozerScale = game.Tank.VehicleScale;
            DiagnosticView(game, body.position + new Vector3(-16, 12, -20), body.position + new Vector3(0, 1, 8));
            yield return null;
            Capture(Path.Combine(output, "items-07-bulldozer.png"));
            yield return new WaitForSeconds(1.1f);
            DiagnosticView(game, body.position + new Vector3(-19, 14, -22), body.position + new Vector3(0, 2, 8));
            yield return null;
            Capture(Path.Combine(output, "items-08-bulldozer-ram.png"));
            int dozerCrashes = game.Crashes - beforeDozerCrashes;

            yield return WaitForTankRecovery(game);
            ItemFixture(game, 12, 12);
            game.Items.Receive(ItemKind.GigantBomb); game.Input.PulsePad(0);
            var bomb = FindFirstObjectByType<GigantBomb>();
            DiagnosticView(game, body.position + new Vector3(-28, 28, -32), game.World.Buildings[12].transform.position + Vector3.up * 8);
            yield return new WaitForSeconds(.35f);
            Capture(Path.Combine(output, "items-09-bomb-throw.png"));
            yield return new WaitForSeconds(.8f);
            int bombContacts = bomb.Contacts;
            Capture(Path.Combine(output, "items-10-bomb-impact.png"));
            int beforeBomb = game.BrokenBlocks;
            var bombPosition = bomb.transform.position;
            DiagnosticView(game, bombPosition + new Vector3(-46, 34, -50), bombPosition);
            yield return new WaitForSeconds(game.Items.Remaining(ItemKind.GigantBomb) + .12f);
            Capture(Path.Combine(output, "items-11-gigant-explosion.png"));
            int blastBlocks = game.BrokenBlocks - beforeBomb;

            yield return WaitForTankRecovery(game);
            ItemFixture(game, 18, 12);
            int beforeDashCrashes = game.Crashes;
            game.Items.Receive(ItemKind.HighSpeed);
            yield return new WaitForSeconds(.2f);
            DiagnosticView(game, body.position + new Vector3(-9, 6, -10), body.position + new Vector3(0, 1, 3));
            yield return null;
            Capture(Path.Combine(output, "items-12-high-speed.png"));
            yield return new WaitForSeconds(1.4f);
            float dashSpeed = game.Tank.ActualSpeed;
            int dashCrashes = game.Crashes - beforeDashCrashes;
            game.CameraRig.enabled = true; game.CameraRig.Snap();
            game.AdvanceTime(1000);
            yield return new WaitForSecondsRealtime(.2f);
            Capture(Path.Combine(output, "items-13-results.png"));
            Debug.Log($"ITEMS SMOKE COMPLETE: blocks={game.BrokenBlocks}/{game.TotalBlocks}, crashes={game.Crashes}, turretLead={turretLead:0.00}, earlyHull={earlyHull:0.00}, hullFollow={hullFollow:0.00}, beamTravel={beamDistance:0.000}, beamHits={beamHits}, missiles={missileCount}, missileStrikes={missileStrikes}, magnetCollected={magnetCollected}, dozerScale={dozerScale:0.00}, dozerCrashes={dozerCrashes}, bombContacts={bombContacts}, bombBlastBlocks={blastBlocks}, dashSpeed={dashSpeed:0.00}, dashCrashes={dashCrashes}, state={game.State}");
            yield return new WaitForSecondsRealtime(.2f);
            Application.Quit(0);
        }

        private static IEnumerator WaitForTankRecovery(TankTownGame game)
        {
            // Normal missile/bomb use does not grant invincibility. Let any real crash finish
            // before assigning the next diagnostic item; otherwise Receive correctly rejects it.
            while (game.Tank.Respawning) yield return null;
        }

        private static void ItemFixture(TankTownGame game, int buildingIndex, float distance)
        {
            game.Items.FinishRound();
            var building = game.World.Buildings[buildingIndex];
            var point = building.transform.position + new Vector3(-4.6f, 0, -building.Width / 2 - distance);
            game.Tank.Warp(new Pose(game.World.GroundPoint(point, game.Settings.rideHeight), Quaternion.identity));
            Physics.SyncTransforms();
            game.CameraRig.enabled = true; game.CameraRig.Snap();
        }

        private static void DiagnosticView(TankTownGame game, Vector3 position, Vector3 target)
        {
            game.CameraRig.enabled = false;
            Camera.main.transform.position = position;
            Camera.main.transform.LookAt(target);
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
