#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace TankTown
{
    public sealed class TankTownMobilitySmoke : MonoBehaviour
    {
        private string output;
        private float baselineFps, denseFps, laserFps;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-mobility-smoke") >= 0)
                new GameObject("Homing traffic and laser smoke").AddComponent<TankTownMobilitySmoke>();
        }
        private IEnumerator MeasureFrames(string label, Action<float> record, bool duringLaser = false)
        {
            var camera = Camera.main; var canvas = FindFirstObjectByType<Canvas>();
            var previousMode = canvas.renderMode; var previousCamera = canvas.worldCamera; float previousDistance = canvas.planeDistance;
            var target = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var sample = new Texture2D(1, 1, TextureFormat.RGB24, false);
            var request = new RenderPipeline.StandardRequest { destination = target };
            void Draw()
            {
                Canvas.ForceUpdateCanvases(); RenderPipeline.SubmitRenderRequest(camera, request);
                var previousTarget = RenderTexture.active; RenderTexture.active = target;
                // Synchronize GPU completion: hidden players can skip their window render.
                sample.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false); RenderTexture.active = previousTarget;
            }
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .01f;
                var warmup = Stopwatch.StartNew();
                while (warmup.Elapsed.TotalSeconds < (duringLaser ? .1 : .8)) { Draw(); yield return null; }
                var watch = Stopwatch.StartNew(); int frames = 0; double last = 0, worst = 0;
                while (watch.Elapsed.TotalSeconds < (duringLaser ? 1 : 3) && (!duringLaser || FindFirstObjectByType<TankTownGame>().Items.StopsForward))
                {
                    Draw(); yield return null; frames++; double now = watch.Elapsed.TotalSeconds;
                    worst = Math.Max(worst, now - last); last = now;
                }
                float fps = frames / (float)watch.Elapsed.TotalSeconds; record(fps);
                Debug.Log($"MOBILITY PERFORMANCE: mode={label}, offscreen=1920x1080-GPU-synchronized, averageFps={fps:0.0}, worstFrameMs={worst * 1000:0.0}, frames={frames}, playing={FindFirstObjectByType<TankTownGame>().IsPlaying}");
            }
            finally
            {
                canvas.renderMode = previousMode; canvas.worldCamera = previousCamera; canvas.planeDistance = previousDistance;
                RenderTexture.ReleaseTemporary(target); Destroy(sample);
            }
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true; Time.captureDeltaTime = 0;
            output = Path.Combine(Application.dataPath, "..", "Captures"); Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(2);
            var game = FindFirstObjectByType<TankTownGame>(); game.StartOrRestart(); game.Input.useHardware = true;
            var body = game.Tank.GetComponent<Rigidbody>(); game.Tank.enabled = false; body.isKinematic = true;
            int cars = game.World.TrafficCars.Count, people = game.World.Pedestrians.Count;
            if (cars != 72 || people != 216) { Fail("wrong production traffic population"); yield break; }
            for (int i = 0; i < cars; i++) game.World.TrafficCars[i].gameObject.SetActive(i % 4 == 0);
            foreach (var person in game.World.Pedestrians) person.gameObject.SetActive(false);
            yield return MeasureFrames("18cars-no-pedestrians", x => baselineFps = x);
            foreach (var car in game.World.TrafficCars) car.gameObject.SetActive(true);
            foreach (var person in game.World.Pedestrians) person.gameObject.SetActive(true);
            yield return MeasureFrames("72cars-216pedestrians", x => denseFps = x);
            Time.captureDeltaTime = 1f / 60;
            Capture("mobility-01-street", true);
            var citizen = game.World.Pedestrians[0];
            View(game, citizen.transform.position + new Vector3(-3, 2.4f, 4), citizen.transform.position + Vector3.up * 1.15f);
            Capture("mobility-02-pedestrian", false);
            foreach (var explosive in game.World.Explosives) if (explosive != null) explosive.gameObject.SetActive(false);
            foreach (var person in game.World.Pedestrians) person.gameObject.SetActive(false);
            var first = game.World.TrafficCars.Find(x => x != null && !x.GetComponent<DestructibleBlock>().Detached);
            var second = game.World.TrafficCars.Find(x => x != null && x != first && !x.GetComponent<DestructibleBlock>().Detached);
            first.gameObject.SetActive(true); second.gameObject.SetActive(true);
            first.SetCircuit(new[] { new Vector3(0, 0, -86), new Vector3(0, 0, -50) }, 0);
            second.SetCircuit(new[] { new Vector3(0, 0, -38), new Vector3(0, 0, -10) }, 0);
            Place(game, new Vector3(0, 0, -112));
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            game.Items.Receive(ItemKind.Missiles); game.Input.PulsePad(0);
            ItemMissile rocket = null;
            foreach (var missile in FindObjectsByType<ItemMissile>(FindObjectsSortMode.None))
                if (missile.TrackingTarget == first.GetComponent<TownMovingTarget>()) { rocket = missile; break; }
            if (rocket == null) { Fail("missiles did not select a moving car"); yield break; }
            var oldAim = rocket.AimPoint; yield return new WaitForSeconds(.3f);
            bool follows = rocket.AimPoint.z > oldAim.z + .1f;
            first.gameObject.SetActive(false); yield return null; yield return null;
            bool retarget = rocket.TrackingTarget == second.GetComponent<TownMovingTarget>();
            yield return new WaitForSeconds(.4f);
            View(game, new Vector3(-6, 10, -122), new Vector3(0, 16, -87)); Capture("mobility-03-homing", true);
            for (int i = 0; i < 180 && game.Items.MissileTrackedStrikes == 0; i++) yield return new WaitForFixedUpdate();
            if (!follows || !retarget || game.Items.MissileTrackedStrikes == 0) { Fail("real homing/retarget/contact failed"); yield break; }
            Capture("mobility-04-tracked-impact", true);
            while (game.Items.Weapon != ItemKind.None) yield return null;
            var carA = game.World.TrafficCars.Find(x => x != null && !x.GetComponent<DestructibleBlock>().Detached);
            var carB = game.World.TrafficCars.Find(x => x != null && x != carA && !x.GetComponent<DestructibleBlock>().Detached);
            carA.gameObject.SetActive(true); carB.gameObject.SetActive(true);
            carA.SetCircuit(new[] { new Vector3(0, 0, -85), new Vector3(0, 0, -45) }, 0);
            carB.SetCircuit(new[] { new Vector3(0, 0, -74), new Vector3(0, 0, -30) }, 0);
            Place(game, new Vector3(0, 0, -91));
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            int chains = game.ChainExplosions; game.Input.PulsePad(0);
            View(game, new Vector3(-6, 9, -100), new Vector3(0, 2, -84));
            yield return new WaitForSeconds(.48f); Capture("mobility-05-car-explosion", false);
            yield return new WaitForSeconds(.6f);
            int carChains = game.ChainExplosions - chains;
            if (carChains < 2) { Fail("larger car explosion did not chain"); yield break; }
            foreach (var explosive in game.World.Explosives) if (explosive != null) explosive.gameObject.SetActive(false);
            foreach (var person in game.World.Pedestrians) person.gameObject.SetActive(true);
            var building = game.World.Buildings.Find(x => x.name == "Apartment frame");
            Vector3 start = building.transform.position + new Vector3(0, 1, -building.Width / 2 - 15);
            Place(game, start); body.isKinematic = false; game.Tank.enabled = true; game.Input.SetWheel(1);
            yield return new WaitForSeconds(.15f);
            float normalTurn = Mathf.Abs(Mathf.DeltaAngle(0, game.Tank.Turret.localEulerAngles.y));
            Place(game, start); body.isKinematic = false; game.Tank.enabled = true; game.Input.SetWheel(1);
            game.Items.Receive(ItemKind.MegaBeam); game.Input.PulsePad(0);
            yield return new WaitForSeconds(.15f);
            float laserTurn = Mathf.Abs(Mathf.DeltaAngle(0, game.Tank.Turret.localEulerAngles.y));
            if (game.Items.BeamHits == 0 || laserTurn >= normalTurn * .5f || game.Tank.ActualSpeed > .1f)
            { Fail("wide laser or heavy steering failed"); yield break; }
            Capture("mobility-06-laser", true);
            game.Input.SetWheel(-1); yield return new WaitForSeconds(.3f); Capture("mobility-07-laser-sweep", true);
            yield return MeasureFrames("wide-laser-collapse", x => laserFps = x, true);
            yield return new WaitForSeconds(2);
            game.AdvanceTime(1000); yield return null;
            if (GameObject.Find("Mega beam") != null || FindObjectsByType<ItemMissile>(FindObjectsSortMode.None).Length != 0)
            { Fail("round cleanup failed"); yield break; }
            Capture("mobility-08-results", true);
            Debug.Log($"MOBILITY SMOKE COMPLETE: cars={cars}, pedestrians={people}, follows={follows}, retarget={retarget}, trackedHits={game.Items.MissileTrackedStrikes}, carChains={carChains}, carRadius={game.Settings.carExplosionRadius}, beamHits={game.Items.BeamHits}, beamLength={game.Settings.megaBeamLength}, beamRadius={game.Settings.megaBeamRadius}, normalTurn={normalTurn:0.00}, laserTurn={laserTurn:0.00}, baselineFps={baselineFps:0.0}, denseFps={denseFps:0.0}, laserFps={laserFps:0.0}, state={game.State}");
            Application.Quit();
        }
        private static void Place(TankTownGame game, Vector3 point)
        {
            var body = game.Tank.GetComponent<Rigidbody>(); body.isKinematic = false; game.Tank.Warp(new Pose(point, Quaternion.identity));
            game.Tank.enabled = false; body.isKinematic = true; game.CameraRig.enabled = true; game.CameraRig.Snap();
        }
        private static void View(TankTownGame game, Vector3 position, Vector3 target)
        { game.CameraRig.enabled = false; Camera.main.transform.position = position; Camera.main.transform.LookAt(target); }
        private void Capture(string name, bool hud) => TankTownSmokeCapture.Capture(Path.Combine(output, name + ".png"), hud, 1920, 1080);
        private static void Fail(string reason) { Debug.LogError("MOBILITY SMOKE FAILED: " + reason); Application.Quit(1); }
    }
}
#endif
