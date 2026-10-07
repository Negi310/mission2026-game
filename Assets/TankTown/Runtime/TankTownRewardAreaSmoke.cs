#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TankTown
{
    public sealed class TankTownRewardAreaSmoke : MonoBehaviour
    {
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--tank-town-reward-area-smoke") < 0) return;
            TankTownGame.SelectedStage = Array.IndexOf(args, "--tank-town-reward-area-big") >= 0 ? TownStage.BigCity : TownStage.Town;
            TankTownGame.GenerationSeedOverride = 2026; TankTownGame.SkipIntroForTests = true; TankTownGame.DisableRecordSavingForTests = true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-reward-area-smoke") < 0) return;
            new GameObject("Reward area smoke").AddComponent<TankTownRewardAreaSmoke>();
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true; Time.captureDeltaTime = 1f / 60;
            output = Path.Combine(Application.dataPath, "..", "Captures"); Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(.4f);
            var game = FindFirstObjectByType<TankTownGame>(); game.StartOrRestart(); game.Input.useHardware = true;
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var person in game.World.Pedestrians) person.gameObject.SetActive(false);
            foreach (var pickup in game.World.Pickups) pickup.gameObject.SetActive(false);
            foreach (var pickup in game.World.ItemPickups) pickup.gameObject.SetActive(false);
            var building = game.World.Buildings.First(x => x.Reward.Kind == ItemKind.MegaBeam);
            var reward = building.Reward; reward.gameObject.SetActive(true);
            game.Tank.Warp(new Pose(game.World.GroundPoint(building.transform.position + Vector3.back * 27, game.Settings.rideHeight), Quaternion.identity));
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true; game.CameraRig.enabled = false;
            View(game, building.transform.position, new Vector3(-19, 14, -26), 7);
            Capture("reward-area-01-intact");
            var roof = building.Blocks.First(x => x.Kind == BuildingPart.Slab && x.Level == building.Floors - 1); roof.Break(roof.transform.position, 0);
            yield return new WaitForFixedUpdate();
            if (reward.Available || reward.Area != null || reward.GetComponentsInChildren<Renderer>().Any(x => x.enabled)) { Fail("partial destruction exposed a reward"); yield break; }
            var last = building.Blocks.First(x => x.Foundation && !x.Detached);
            foreach (var block in building.Blocks.Where(x => x != last && !x.Detached).ToArray()) block.Break(block.transform.position, 0);
            if (building.FullyDestroyed || reward.Area != null) { Fail("one surviving part must keep the reward locked"); yield break; }
            View(game, building.transform.position, new Vector3(-19, 14, -26), 2);
            yield return null;
            Capture("reward-area-02-last-part"); last.Break(last.transform.position, 0);
            if (!building.FullyDestroyed || reward.Area == null || !reward.Available) { Fail("full destruction did not spawn the area"); yield break; }
            yield return new WaitForSeconds(.15f); Capture("reward-area-03-spawn");
            yield return new WaitForSeconds(4.9f); Capture("reward-area-04-ready");
            var area = reward.Area; float radius = area.Radius;
            MoveTank(game, area.Center + Vector3.right * radius * .85f);
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return null;
            if (!reward.Collected || reward.Area != null || game.Items.Held != ItemKind.MegaBeam) { Fail("entering the circle did not collect and remove it"); yield break; }
            Capture("reward-area-05-collected");

            var second = game.World.Buildings.Where(x => x.Reward.Kind == ItemKind.GigantBomb).OrderBy(x => (x.transform.position - building.transform.position).sqrMagnitude).First();
            var pulled = second.Reward; pulled.gameObject.SetActive(true);
            MoveTank(game, second.transform.position + Vector3.right * (second.Width * game.Settings.specialAreaWidthRatio + 12));
            foreach (var block in second.Blocks.Where(x => !x.Detached).ToArray()) block.Break(block.transform.position, 0);
            yield return new WaitForSeconds(4.9f);
            var secondArea = pulled.Area; Vector3 anchor = secondArea.Center; float secondRadius = secondArea.Radius;
            View(game, anchor + Vector3.right * 8, new Vector3(-18, 13, -25), 2); Capture("reward-area-06-magnet-ready");
            game.Items.Receive(ItemKind.Magnet);
            for (int i = 0; i < 3; i++) yield return null;
            if (pulled.Area == null || pulled.Area.Center != anchor) { Fail("the area must stay anchored while the item is inside"); yield break; }
            Capture("reward-area-07-magnet-inside");
            for (int i = 0; i < 25 && pulled.Area != null; i++) yield return null;
            Vector3 offset = pulled.transform.position - anchor; offset.y = 0;
            if (pulled.Area != null || offset.magnitude <= secondRadius || pulled.Collected) { Fail("extracting the item must remove only the area while the slot is full"); yield break; }
            Capture("reward-area-08-magnet-outside");
            // Keep the magnet active until the extracted item reaches the tank;
            // cancelling it at the perimeter correctly leaves the item on that ground.
            for (int i = 0; i < 30 && Vector3.Distance(pulled.transform.position, game.Tank.transform.position) > 1.7f; i++) yield return null;
            // Use the held laser to free the manual slot through the real item flow.
            if (!game.Items.UseHeld()) { Fail("could not use the held weapon to free its slot"); yield break; }
            for (int i = 0; i < 30 && !pulled.Collected; i++) yield return null;
            if (!pulled.Collected || game.Items.Held != ItemKind.GigantBomb) { Fail("the extracted item could not be collected after freeing the slot"); yield break; }
            Capture("reward-area-09-magnet-collected");
            game.CameraRig.Snap(); yield return null;
            float laserElevation = Mathf.Asin(game.Tank.Turret.InverseTransformDirection(game.Tank.Muzzle.forward).y) * Mathf.Rad2Deg;
            var laser = GameObject.Find("Mega beam").transform.GetChild(0).GetComponent<LineRenderer>();
            if (Mathf.Abs(laserElevation - game.Settings.megaBeamElevation) > .01f || Vector3.Angle(laser.GetPosition(1) - laser.GetPosition(0), game.Tank.Muzzle.forward) > .01f)
            { Fail("laser and cannon did not share the lower elevation"); yield break; }
            Capture("reward-area-10-lower-laser");
            yield return new WaitForSeconds(game.Settings.megaBeamSeconds + .1f);
            float restoredElevation = Mathf.Asin(game.Tank.Turret.InverseTransformDirection(game.Tank.Muzzle.forward).y) * Mathf.Rad2Deg;
            if (game.Items.StopsForward || Mathf.Abs(restoredElevation - game.Settings.cannonElevation) > .01f) { Fail("cannon elevation did not restore after the laser"); yield break; }
            Debug.Log($"REWARD AREA VISUAL CHECK: lightHeight={game.Settings.specialAreaLightHeight:0.0}, laserElevation={laserElevation:0.0}, restoredElevation={restoredElevation:0.0}, beamAligned=true");
            Debug.Log($"REWARD AREA SMOKE COMPLETE: stage={game.Stage}, buildings={game.World.Buildings.Count}, fullDestructionOnly=true, plotCentre=true, radius={radius:0.00}, edgeEntry=true, fixedArea=true, magnetExit=true, fullSlotPreserved=true, extractedCollected=true, shaderSupported={game.Effects.RewardAreaLight.shader.isSupported}");
            Application.Quit(0);
        }
        private static void MoveTank(TankTownGame game, Vector3 point)
        {
            var body = game.Tank.GetComponent<Rigidbody>(); body.position = game.World.GroundPoint(point, game.Settings.rideHeight);
            game.Tank.transform.position = body.position; Physics.SyncTransforms();
        }
        private static void View(TankTownGame game, Vector3 focus, Vector3 offset, float height)
        {
            Camera.main.transform.position = focus + offset; Camera.main.transform.LookAt(focus + Vector3.up * height);
        }
        private void Capture(string label)
        {
            var game = FindFirstObjectByType<TankTownGame>();
            foreach (var building in game.World.Buildings) building.Presentation?.Tick(Camera.main.transform.position);
            game.World.RefreshMapMeshes();
            TankTownSmokeCapture.Capture(Path.Combine(output, label + ".png"), true, 1920, 1080);
        }
        private static void Fail(string message) { Debug.LogError("REWARD AREA SMOKE FAILED: " + message); Application.Quit(1); }
    }
}
#endif
