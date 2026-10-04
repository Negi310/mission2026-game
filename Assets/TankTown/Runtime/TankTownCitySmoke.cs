#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace TankTown
{
    // Opt-in native-player diagnostics. Camera arrangements and shot setups are explicit fixtures.
    public sealed class TankTownCitySmoke : MonoBehaviour
    {
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--tank-town-city-smoke") >= 0 || Array.IndexOf(args, "--tank-town-title-wide-smoke") >= 0 || Array.IndexOf(args, "--tank-town-fuel-smoke") >= 0 || Array.IndexOf(args, "--tank-town-debris-smoke") >= 0)
                new GameObject("City presentation smoke").AddComponent<TankTownCitySmoke>();
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            output = Path.Combine(Application.dataPath, "..", "Captures"); Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(2);
            var game = FindFirstObjectByType<TankTownGame>();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-debris-smoke") >= 0)
            {
                yield return DebrisTour(game);
                yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-fuel-smoke") >= 0)
            {
                yield return FuelTour(game);
                yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-title-wide-smoke") >= 0)
            {
                TankTownSmokeCapture.Capture(Path.Combine(output, "city-title-wide.png"), true, 1920, 1080);
                game.StartOrRestart(); yield return new WaitForSeconds(1);
                TankTownSmokeCapture.Capture(Path.Combine(output, "city-play-wide.png"), true, 1920, 1080);
                Debug.Log("CITY WIDE SMOKE COMPLETE: title and gameplay 1920x1080"); Application.Quit(); yield break;
            }
            Capture("city-01-title");
            game.StartOrRestart(); game.Input.useHardware = true;
            yield return new WaitForSeconds(1.2f); Capture("city-02-play");
            var body = game.Tank.GetComponent<Rigidbody>(); game.Tank.enabled = false;
            body.linearVelocity = Vector3.zero; body.isKinematic = true;
            View(game, new Vector3(183, 132, -191), new Vector3(0, 2, 0)); Camera.main.farClipPlane = 650;
            Capture("city-03-island", false);
            var car = game.World.TrafficCars[0];
            View(game, car.transform.position + new Vector3(8, 6, -10), car.transform.position + Vector3.up);
            Capture("city-04-traffic", false);

            foreach (var explosive in game.World.Explosives) if (explosive != null) explosive.gameObject.SetActive(false);
            var next = game.World.TrafficCars[1];
            var targetPoint = new Vector3(0, 0, -85);
            for (int i = 0; i < 2; i++)
            {
                var target = i == 0 ? car : next; target.gameObject.SetActive(true); target.enabled = false;
                var targetBody = target.GetComponent<Rigidbody>();
                targetBody.position = game.World.GroundPoint(targetPoint + Vector3.forward * (i * 9), .72f); targetBody.rotation = Quaternion.identity;
            }
            body.isKinematic = false; game.Tank.Warp(new Pose(targetPoint - Vector3.forward * 6, Quaternion.identity)); body.isKinematic = true;
            Physics.SyncTransforms();
            // Let interpolated car render poses catch up to this diagnostic placement.
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return null;
            View(game, targetPoint + new Vector3(6, 8, -12), targetPoint + Vector3.forward * 4);
            Capture("city-05-car-targets", false); game.Input.PulsePad(0);
            yield return new WaitForSeconds(.3f); Capture("city-06-car-explosion", false);
            yield return new WaitForSeconds(.7f);
            int carExplosions = game.ChainExplosions;
            if (carExplosions != 2) { Debug.LogError($"CITY SMOKE: cannon chain failed, explosions={carExplosions}"); Application.Quit(1); yield break; }

            var building = game.World.Buildings[0]; var reward = building.Reward;
            foreach (var item in game.World.ItemPickups) item.gameObject.SetActive(item == reward);
            for (int i = 0; i < 4; i++) game.World.Explosives[i].gameObject.SetActive(true);
            var center = building.transform.position;
            View(game, center + new Vector3(27, 23, -32), center + Vector3.up * 10);
            Capture("city-07-rooftop", false);
            var barrel = game.World.Explosives[0]; game.Blast(barrel.transform.position, .8f, 0);
            yield return new WaitForSeconds(.32f); Capture("city-08-chain");
            yield return new WaitForSeconds(.4f);
            foreach (var support in building.Blocks.FindAll(x => x != null && x.Foundation && x.Column < 2)) support.Break(support.transform.position, 0);
            building.ReleaseUnsupported(center, 0);
            yield return new WaitForSeconds(.35f); Capture("city-09-falling-special", false);
            yield return new WaitForSeconds(3.8f);
            View(game, reward.transform.position + new Vector3(9, 5, -11), reward.transform.position + Vector3.up);
            Capture("city-10-ground-special", false);
            float groundGap = reward.transform.position.y - game.World.Terrain.HeightAt(reward.transform.position);
            if (!reward.Available || groundGap > 1.2f) { Debug.LogError($"CITY SMOKE: reward did not land unlocked, groundGap={groundGap}"); Application.Quit(1); yield break; }
            body.isKinematic = false; game.Tank.Warp(new Pose(reward.transform.position, Quaternion.identity)); game.Tank.enabled = true;
            game.CameraRig.enabled = true; game.CameraRig.Snap();
            yield return new WaitForSeconds(.4f); Capture("city-11-collected");
            if (!reward.Collected) { Debug.LogError("CITY SMOKE: physical collection failed"); Application.Quit(1); yield break; }
            game.Tank.Fire(0); float beamStart = Time.time;
            yield return new WaitForSeconds(.35f); Capture("city-12-beam");
            while (game.Items.StopsForward && Time.time - beamStart < 4) yield return null;
            float beamDuration = Time.time - beamStart;
            game.AdvanceTime(1000); yield return new WaitForSecondsRealtime(.15f); Capture("city-13-results");
            Debug.Log($"CITY SMOKE COMPLETE: cars={game.World.TrafficCars.Count}, signals={game.World.Signals.Count}, carExplosions={carExplosions}, explosions={game.ChainExplosions}, longestChain={game.LongestChain}, rewardCollected={reward.Collected}, rewardGroundGap={groundGap:0.000}, beamSeconds={beamDuration:0.000}, blocks={game.BrokenBlocks}/{game.TotalBlocks}, state={game.State}");
            Application.Quit(beamDuration >= 1.9f && beamDuration < 2.3f ? 0 : 1);
        }
        private void Capture(string name, bool hud = true) => TankTownSmokeCapture.Capture(Path.Combine(output, name + ".png"), hud);
        private IEnumerator FuelTour(TankTownGame game)
        {
            // Keep short explosion captures repeatable even if rendering a PNG takes time.
            Time.captureDeltaTime = 1f / 60;
            game.StartOrRestart(); game.Input.useHardware = true;
            yield return new WaitForSeconds(1);
            var body = game.Tank.GetComponent<Rigidbody>(); game.Tank.enabled = false;
            body.linearVelocity = Vector3.zero; body.isKinematic = true;
            var fuel = game.World.Explosives.Find(x => x.IsFuelTank);
            foreach (var explosive in game.World.Explosives) if (explosive != fuel) explosive.gameObject.SetActive(false);
            var building = game.World.Buildings[0];
            var pillar = building.Blocks.Find(x => x.Foundation && x.Column == 2);
            Vector3 farTarget = pillar.transform.position;
            AimAtFuelTarget(game, farTarget);
            yield return null; CaptureFuel("fuel-01-far-target");
            game.Tank.Fire(0);
            yield return new WaitForSeconds(.22f); CaptureFuel("fuel-02-ordinary-impact");
            yield return new WaitForSeconds(1);
            bool farStayedSafe = fuel != null && !fuel.Primed && !fuel.GetComponent<DestructibleBlock>().Detached && game.ChainExplosions == 0;
            int farBroken = game.BrokenBlocks;
            if (!farStayedSafe) { Debug.LogError("FUEL SMOKE: distant cannon impact incorrectly ignited the fuel tank"); Application.Quit(1); yield break; }
            AimAtFuelTarget(game, fuel.transform.position);
            yield return null; CaptureFuel("fuel-03-close-target");
            game.Tank.Fire(1);
            float started = Time.time;
            while (game.ChainExplosions == 0 && Time.time - started < 2) yield return null;
            if (game.ChainExplosions != 1) { Debug.LogError("FUEL SMOKE: direct cannon hit did not cause the fuel explosion"); Application.Quit(1); yield break; }
            yield return new WaitForSeconds(.12f); CaptureFuel("fuel-04-big-explosion");
            yield return new WaitForSeconds(2.6f); CaptureFuel("fuel-05-clear");
            Debug.Log($"FUEL SMOKE COMPLETE: farStayedSafe={farStayedSafe}, fuelExplosions={game.ChainExplosions}, farBroken={farBroken}, afterFuel={game.BrokenBlocks}, radius={game.Settings.fuelExplosionRadius}, impulse={game.Settings.blastImpulse * game.Settings.fuelImpulseMultiplier}");
            Application.Quit(game.BrokenBlocks > farBroken ? 0 : 1);
        }
        private void CaptureFuel(string name) => TankTownSmokeCapture.Capture(Path.Combine(output, name + ".png"), true, 1920, 1080);
        private IEnumerator DebrisTour(TankTownGame game)
        {
            Time.captureDeltaTime = 1f / 60;
            game.StartOrRestart(); game.Input.useHardware = true;
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            var building = game.World.Buildings[0];
            var pillar = building.Blocks.Find(x => x.Foundation && x.Column == 0);
            AimAtFuelTarget(game, pillar.transform.position);
            yield return null; CaptureFuel("debris-01-cannon-target");
            game.Tank.Fire(0);
            yield return new WaitForSeconds(.2f); CaptureFuel("debris-02-cannon-impact");
            View(game, building.transform.position + new Vector3(25, 21, -28), building.transform.position + Vector3.up * 9);
            yield return new WaitForSeconds(.25f); CaptureFuel("debris-03-city-fragments");
            yield return new WaitForSeconds(2.5f); CaptureFuel("debris-04-clear");
            int cityBroken = game.BrokenBlocks, cityBursts = game.DebrisExplosions;

            // 分割数と実接触を安定して撮るための診断用配置。二次爆発は通常の衝突経路を使う。
            Vector3 center = new Vector3(0, 6, -85);
            var floor = SmokePart(game, "Diagnostic floor", center, new Vector3(9, .3f, 6), BuildingPart.Slab);
            var wall = SmokePart(game, "Diagnostic impact wall", center + Vector3.right * 10, new Vector3(.35f, 5, 9), BuildingPart.Wall);
            Physics.SyncTransforms();
            View(game, center + new Vector3(-7, 14, -19), center + Vector3.right * 6);
            CaptureFuel("debris-05-floor-before");
            floor.Break(center - Vector3.right * 2, game.Stats.BlastImpulse);
            int pieces = floor.DebrisBodies.Count;
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            CaptureFuel("debris-06-split-floor");
            var incoming = floor.DebrisBodies[0];
            incoming.useGravity = false; incoming.position = wall.transform.position - Vector3.right * 6;
            incoming.rotation = Quaternion.identity; incoming.angularVelocity = Vector3.zero; incoming.linearVelocity = Vector3.right * 42;
            Physics.SyncTransforms();
            float start = Time.time; int before = game.DebrisExplosions;
            while (!wall.Detached && Time.time - start < 1) yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(.1f); CaptureFuel("debris-07-contact-explosion");
            yield return new WaitForSeconds(.25f); CaptureFuel("debris-08-new-fragments");
            int wallPieces = wall != null ? wall.DebrisBodies.Count : 0;
            Debug.Log($"DEBRIS SMOKE COMPLETE: floorPieces={pieces}, wallPieces={wallPieces}, contactBursts={game.DebrisExplosions - before}, cityBursts={cityBursts}, cityBroken={cityBroken}, totalBursts={game.DebrisExplosions}, impulse={game.Settings.blastImpulse}, fuelImpulse={game.Settings.blastImpulse * game.Settings.fuelImpulseMultiplier}, longestChain={game.LongestChain}");
            Application.Quit(pieces > 1 && wallPieces > 1 && game.DebrisExplosions > before ? 0 : 1);
        }
        private static DestructibleBlock SmokePart(TankTownGame game, string name, Vector3 position, Vector3 size, BuildingPart kind)
        {
            var item = game.World.Box(name, position, size, game.World.ShotMaterial);
            var block = item.AddComponent<DestructibleBlock>(); block.Kind = kind; block.Initialize(game);
            return block;
        }
        private static void AimAtFuelTarget(TankTownGame game, Vector3 target)
        {
            var body = game.Tank.GetComponent<Rigidbody>(); body.isKinematic = false;
            Vector3 start = game.World.GroundPoint(target + Vector3.right * 6, game.Settings.rideHeight);
            game.Tank.Warp(new Pose(start, Quaternion.LookRotation(Vector3.left))); body.isKinematic = true;
            Physics.SyncTransforms(); game.CameraRig.enabled = true; game.CameraRig.Snap();
        }
        private static void View(TankTownGame game, Vector3 position, Vector3 target)
        {
            game.CameraRig.enabled = false; Camera.main.transform.position = position; Camera.main.transform.LookAt(target);
        }
    }
}
#endif
