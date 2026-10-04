#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace TankTown
{
    public sealed class TankTownDurabilitySmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-durability-smoke") >= 0)
                new GameObject("Building durability smoke").AddComponent<TankTownDurabilitySmoke>();
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            Time.captureDeltaTime = 1f / 60; UnityEngine.Random.InitState(20261005);
            yield return new WaitForSecondsRealtime(2);
            var game = FindFirstObjectByType<TankTownGame>();
            game.StartOrRestart(); game.Input.useHardware = true; game.Tank.enabled = false;
            var body = game.Tank.GetComponent<Rigidbody>(); body.isKinematic = true;
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var car in game.World.TrafficCars) car.gameObject.SetActive(false);
            string output = Path.Combine(Application.dataPath, "..", "Captures"); Directory.CreateDirectory(output);
            foreach (string name in new[] { "Apartment frame", "Tower frame", "Shop frame" })
            {
                var building = game.World.Buildings.Find(x => x.name == name);
                var start = building.transform.position + new Vector3(0, game.Settings.rideHeight, -building.Width / 2 - 15);
                body.isKinematic = false; game.Tank.Warp(new Pose(start, Quaternion.identity)); body.isKinematic = true;
                body.position = start; game.Tank.transform.position = start; Physics.SyncTransforms(); game.CameraRig.Snap();
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return null;
                string label = name.Split(' ')[0].ToLowerInvariant();
                TankTownSmokeCapture.Capture(Path.Combine(output, "durability-" + label + "-0.png"), true, 1920, 1080);
                for (int shot = 1; shot <= 3; shot++)
                {
                    int before = game.Sound.PlayedCount(TownSound.Cannon);
                    game.Tank.Fire(shot % 2);
                    yield return new WaitForSeconds(.5f);
                    if (game.Sound.PlayedCount(TownSound.Cannon) != before + 1) { Fail("cannon did not fire"); yield break; }
                    var front = building.Blocks.Find(x => x != null && x.Foundation && x.Column == 0);
                    Debug.Log($"DURABILITY SHOT: building={label}, shot={shot}, detached={building.DetachedParts}/{building.Blocks.Count}, collapsed={building.Collapsed}, baseLevel={building.CollapsedFromLevel}, frontDurability={(front != null ? front.Durability01 : 0):0.000}, reward={building.Reward.Available}");
                    if (shot < 3 && (building.Collapsed || building.DetachedParts > 0 || front == null || Mathf.Abs(front.Durability01 - (1 - shot / 3f)) > .01f))
                    { Fail(label + " collapsed or missed before shot 3"); yield break; }
                    if (shot == 3 && (building.CollapsedFromLevel != 0 || building.DestroyedFraction != 1 || !building.Reward.Available))
                    { Fail(label + " did not fully collapse/unlock at shot 3"); yield break; }
                    if (shot == 3) yield return new WaitForSeconds(.4f);
                    TankTownSmokeCapture.Capture(Path.Combine(output, "durability-" + label + "-" + shot + ".png"), true, 1920, 1080);
                    if (shot == 2 && label == "apartment")
                    {
                        yield return new WaitForSeconds(.25f);
                        game.CameraRig.enabled = false;
                        Camera.main.transform.position = front.transform.position + new Vector3(4, 2, -6);
                        Camera.main.transform.LookAt(front.transform.position);
                        TankTownSmokeCapture.Capture(Path.Combine(output, "durability-cracks-close.png"), false, 1920, 1080);
                        game.CameraRig.enabled = true; game.CameraRig.Snap();
                    }
                }
            }
            game.AdvanceTime(1000);
            Debug.Log($"DURABILITY SMOKE COMPLETE: styles=3, shotsPerBuilding=3, broken={game.BrokenBlocks}, durability={game.Settings.buildingDurability}, coreRadius={game.Settings.structureBlastRadius}, ordinaryRadius={game.Stats.BlastRadius}, impulse={game.Stats.BlastImpulse}, state={game.State}");
            Application.Quit();
        }
        private static void Fail(string reason) { Debug.LogError("DURABILITY SMOKE FAILED: " + reason); Application.Quit(1); }
    }
}
#endif
