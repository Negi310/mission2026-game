#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TankTown
{
    public sealed class TankTownExtremeSpecialSmoke : MonoBehaviour
    {
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-extreme-specials-smoke") < 0) return;
            TankTownGame.GenerationSeedOverride = 2026; TankTownGame.SkipIntroForTests = true; TankTownGame.DisableRecordSavingForTests = true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-extreme-specials-smoke") < 0 || FindFirstObjectByType<TankTownExtremeSpecialSmoke>() != null) return;
            var root = new GameObject("Extreme specials smoke"); DontDestroyOnLoad(root); root.AddComponent<TankTownExtremeSpecialSmoke>();
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true;
            output = Path.Combine(Application.dataPath, "..", "Captures"); Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(.6f);
            var game = FindFirstObjectByType<TankTownGame>(); game.StartOrRestart(); Isolate(game);
            var upgrades = new TankPickup[4]; Vector3 origin = game.Tank.transform.position;
            for (int i = 0; i < upgrades.Length; i++)
            {
                var root = new GameObject("Magnetic demonstration upgrade"); root.transform.SetParent(game.World.transform);
                root.transform.position = origin + new Vector3((i - 1.5f) * 6, 1, 26 + i * 4);
                upgrades[i] = root.AddComponent<TankPickup>(); upgrades[i].Initialize(game, (UpgradeKind)i, game.World.Icons.Material((UpgradeKind)i)); game.World.Pickups.Add(upgrades[i]);
            }
            var building = game.World.Buildings.First(x => x.Reward.Kind == ItemKind.MegaBeam); var reward = building.Reward;
            reward.gameObject.SetActive(true);
            foreach (var support in building.Blocks.Where(x => x.Foundation).ToArray()) support.Break(support.transform.position, 0);
            building.ReleaseUnsupported(building.transform.position, 0); yield return new WaitForFixedUpdate();
            reward.DropBody.position = origin + new Vector3(8, 12, 36);
            game.Items.Receive(ItemKind.HighSpeed); game.Items.Receive(ItemKind.Magnet);
            yield return new WaitForSecondsRealtime(.08f); Capture("specials-01-magnet-pull");
            yield return new WaitForSecondsRealtime(.65f); Capture("specials-02-magnet-collected");
            if (upgrades.Any(x => !x.Collected) || !reward.Collected || game.Items.Held != ItemKind.MegaBeam || game.Items.MagnetPullSpeed <= game.Tank.ActualSpeed) { Fail("magnet did not catch upgrades and a released reward while driving"); yield break; }
            float pullSpeed = game.Items.MagnetPullSpeed, tankSpeed = game.Tank.ActualSpeed;

            yield return FreshCity(game); game = FindFirstObjectByType<TankTownGame>(); Isolate(game); Freeze(game);
            origin = game.Tank.transform.position;
            var fuel = game.World.Explosives.First(x => x.IsFuelTank); fuel.gameObject.SetActive(true);
            fuel.transform.position = game.World.GroundPoint(origin + Vector3.forward * 24, 2);
            var car = game.World.TrafficCars[0]; car.gameObject.SetActive(true);
            var carExplosive = car.GetComponent<ChainExplosive>();
            car.SetCircuit(new[] { origin + Vector3.forward * 42, origin + Vector3.forward * 74 }, 0);
            Physics.SyncTransforms(); game.CameraRig.Snap(); game.Items.Receive(ItemKind.Missiles); game.Input.PulsePad(0);
            if (!FindObjectsByType<ItemMissile>(FindObjectsSortMode.None).Any(x => x.TrackingTarget == fuel.GetComponent<TownMovingTarget>())) { Fail("missiles did not acquire the fuel tank in front"); yield break; }
            yield return new WaitForSecondsRealtime(.23f); Capture("specials-03-missile-targets");
            float until = Time.realtimeSinceStartup + 4;
            while (!fuel.Primed && Time.realtimeSinceStartup < until) yield return null;
            if (!fuel.Primed || fuel.PowerMultiplier <= 1) { Fail("missile hit did not prime a stronger explosion"); yield break; }
            float fuelPower = fuel.PowerMultiplier;
            yield return new WaitForSecondsRealtime(.25f); Capture("specials-04-powered-chain");
            if (carExplosive.PowerMultiplier <= 1) { Fail("missile power did not propagate to the nearby car"); yield break; }
            float carPower = carExplosive.PowerMultiplier;

            yield return FreshCity(game); game = FindFirstObjectByType<TankTownGame>(); Isolate(game); Freeze(game);
            game.Items.Receive(ItemKind.GigantBomb); game.Input.PulsePad(0);
            var bomb = FindFirstObjectByType<GigantBomb>(); Vector3 launch = bomb.transform.position;
            float speed = Vector3.ProjectOnPlane(bomb.GetComponent<Rigidbody>().linearVelocity, Vector3.up).magnitude;
            yield return new WaitForSecondsRealtime(.45f);
            if (bomb == null) { Fail("bomb landed before its long throw was observable"); yield break; }
            float travel = Vector3.ProjectOnPlane(bomb.transform.position - launch, Vector3.up).magnitude;
            game.CameraRig.enabled = false;
            Camera.main.transform.position = launch + new Vector3(-38, 26, -15); Camera.main.transform.LookAt(bomb.transform.position);
            Capture("specials-05-bomb-long-throw");
            until = Time.realtimeSinceStartup + 6;
            while (bomb != null && !bomb.Detonated && Time.realtimeSinceStartup < until) yield return null;
            if (!bomb.GroundImpact || game.Items.Weapon != ItemKind.None || travel < 30 || speed < 89) { Fail("bomb range, landing explosion or immediate weapon release"); yield break; }
            Vector3 landing = bomb.DetonationPoint;
            Camera.main.transform.position = landing + new Vector3(-65, 46, -78); Camera.main.transform.LookAt(landing);
            yield return new WaitForSecondsRealtime(.10f); Capture("specials-06-giant-ground-explosion");
            yield return new WaitForSecondsRealtime(.8f); Capture("specials-07-aftermath");
            Debug.Log($"EXTREME SPECIALS SMOKE COMPLETE: magnetRadius={game.Settings.magnetRadius}, pullSpeed={pullSpeed:0.0}, tankSpeed={tankSpeed:0.0}, upgrades=4, specialCollected=true, fuelPower={fuelPower:0.0}, carPower={carPower:0.0}, bombSpeed={speed:0.0}, earlyBombTravel={travel:0.0}, groundImpact={bomb.GroundImpact}, bombRadius={game.Settings.gigantBombRadius}, bombImpulse={game.Settings.gigantBombImpulseMultiplier}, broken={game.BrokenBlocks}, activeDebris={game.ActiveDebrisCount}, state={game.State}");
            Application.Quit(0);
        }
        private static IEnumerator FreshCity(TankTownGame game) { game.TogglePause(); game.ReturnToTitle(); yield return null; game = FindFirstObjectByType<TankTownGame>(); game.StartOrRestart(); yield return null; }
        private static void Isolate(TankTownGame game)
        {
            game.Input.useHardware = true; game.Input.SetWheel(0);
            foreach (var item in game.World.Pickups) item.gameObject.SetActive(false);
            foreach (var item in game.World.ItemPickups) item.gameObject.SetActive(false);
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var person in game.World.Pedestrians) person.gameObject.SetActive(false);
        }
        private static void Freeze(TankTownGame game) { game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true; }
        private void Capture(string name) => TankTownSmokeCapture.Capture(Path.Combine(output, name + ".png"), true, 1920, 1080);
        private static void Fail(string reason) { Debug.LogError("EXTREME SPECIALS SMOKE FAILED: " + reason); Application.Quit(1); }
    }
}
#endif
