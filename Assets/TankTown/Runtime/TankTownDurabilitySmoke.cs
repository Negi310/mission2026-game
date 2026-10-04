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
                    int detached = building.DetachedParts;
                    int before = game.Sound.PlayedCount(TownSound.Cannon);
                    game.Tank.Fire(shot % 2);
                    yield return new WaitForSeconds(.5f);
                    if (game.Sound.PlayedCount(TownSound.Cannon) != before + 1) { Fail("cannon did not fire"); yield break; }
                    var front = building.Blocks.Find(x => x != null && x.Foundation && x.Column == 0);
                    Debug.Log($"DURABILITY SHOT: building={label}, shot={shot}, detached={building.DetachedParts}/{building.Blocks.Count}, collapsed={building.Collapsed}, baseLevel={building.CollapsedFromLevel}, frontDurability={(front != null ? front.Durability01 : 0):0.000}, reward={building.Reward.Available}");
                    if (building.Collapsed || building.DetachedParts < detached || building.DestroyedFraction >= .3f || building.Reward.Available)
                    { Fail(label + " did not break locally on shot " + shot); yield break; }
                    if (shot == 1 && building.DetachedParts < 3)
                    { Fail(label + " close blast did not widen the initial hole"); yield break; }
                    if (shot == 3) yield return new WaitForSeconds(.4f);
                    TankTownSmokeCapture.Capture(Path.Combine(output, "durability-" + label + "-" + shot + ".png"), true, 1920, 1080);
                    if (shot == 3 && label == "apartment")
                    {
                        yield return new WaitForSeconds(.25f);
                        var cracked = building.Blocks.Find(x => x != null && !x.Detached && x.Durability01 < .99f);
                        if (cracked == null) { Fail("no surrounding cracks"); yield break; }
                        game.CameraRig.enabled = false;
                        Camera.main.transform.position = cracked.transform.position + new Vector3(4, 2, -6);
                        Camera.main.transform.LookAt(cracked.transform.position);
                        TankTownSmokeCapture.Capture(Path.Combine(output, "durability-cracks-close.png"), false, 1920, 1080);
                        game.CameraRig.enabled = true; game.CameraRig.Snap();
                    }
                }
                // 壁を削った後、実弾を左右の支柱へ個別に当てる。一本だけでは全壊させない。
                for (int column = 0; column < 2; column++)
                {
                    var support = building.Blocks.Find(x => x != null && x.Foundation && x.Column == column);
                    start.x = support.transform.position.x;
                    body.isKinematic = false; game.Tank.Warp(new Pose(start, Quaternion.identity)); body.isKinematic = true;
                    body.position = start; game.Tank.transform.position = start; Physics.SyncTransforms(); game.CameraRig.Snap();
                    yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return null;
                    int before = game.Sound.PlayedCount(TownSound.Cannon);
                    game.Tank.Fire(column); yield return new WaitForSeconds(.5f);
                    if (game.Sound.PlayedCount(TownSound.Cannon) != before + 1 || !support.Detached)
                    { Fail(label + " real pillar shot missed column " + column); yield break; }
                    if ((column == 0 && building.Collapsed) || (column == 1 && (building.DestroyedFraction != 1 || !building.Reward.Available)))
                    { Fail(label + " support balance did not match local damage"); yield break; }
                    Debug.Log($"LOCAL SUPPORT SHOT: building={label}, column={column}, detached={building.DetachedParts}/{building.Blocks.Count}, collapsed={building.Collapsed}, reward={building.Reward.Available}");
                    TankTownSmokeCapture.Capture(Path.Combine(output, "durability-" + label + "-support-" + column + ".png"), true, 1920, 1080);
                }
            }
            game.AdvanceTime(1000);
            Debug.Log($"DURABILITY SMOKE COMPLETE: styles=3, facadeShots=3, aimedSupportShots=2, localBreak=True, broken={game.BrokenBlocks}, durability={game.Settings.buildingDurability}, ordinaryRadius={game.Stats.BlastRadius}, breakRadius={game.Settings.blastBreakRadius}, damageMultiplier={game.Settings.blastDamageMultiplier}, impulse={game.Stats.BlastImpulse}, state={game.State}");
            Application.Quit();
        }
        private static void Fail(string reason) { Debug.LogError("DURABILITY SMOKE FAILED: " + reason); Application.Quit(1); }
    }
}
#endif
