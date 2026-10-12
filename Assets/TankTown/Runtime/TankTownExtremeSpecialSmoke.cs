#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TankTown
{
    public sealed class TankTownExtremeSpecialSmoke : MonoBehaviour
    {
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-extreme-specials-smoke") < 0) return;
            TankTownGame.GenerationSeedOverride = 2026;
            TankTownGame.SelectedStage = TownStage.Town;
            TankTownGame.SkipIntroForTests = Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-stages-check") < 0;
            TankTownGame.DisableRecordSavingForTests = true;
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
            var game = FindFirstObjectByType<TankTownGame>();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-stages-check") >= 0)
            {
                yield return StagesCheck(game);
                yield break;
            }
            game.StartOrRestart(); Isolate(game);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-bomb-structure-check") >= 0)
            {
                yield return BombStructureCheck(game);
                yield break;
            }
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
            Physics.SyncTransforms(); game.CameraRig.Snap(); game.Items.Receive(ItemKind.Missiles);
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
        private IEnumerator BombStructureCheck(TankTownGame game)
        {
            var building = game.World.Buildings[0];
            var wall = building.Blocks.First(x => x.Kind == BuildingPart.Wall && x.Level == Mathf.Min(1, building.Floors - 1) && x.Column == 0);
            Vector3 wallPoint = wall.GetComponent<Collider>().bounds.center;
            Vector3 outward = Vector3.ProjectOnPlane(wallPoint - building.transform.position, Vector3.up).normalized;
            if (outward.sqrMagnitude < .01f) outward = Vector3.forward;
            Vector3 launchPoint = wallPoint + outward * 12;
            game.CameraRig.enabled = false;
            Camera.main.transform.position = launchPoint + outward * 24 + Vector3.up * 13;
            Camera.main.transform.LookAt(wallPoint);
            game.Tank.Warp(new Pose(launchPoint + outward * 8, Quaternion.LookRotation(-outward, Vector3.up)));
            Freeze(game);
            game.Items.Receive(ItemKind.GigantBomb); game.Input.PulsePad(0);
            var bomb = FindFirstObjectByType<GigantBomb>();
            if (bomb == null) { Fail("could not spawn giant bomb for structure contact"); yield break; }
            var body = bomb.GetComponent<Rigidbody>();
            body.position = launchPoint; body.linearVelocity = -outward * game.Settings.gigantBombThrowSpeed;
            Physics.SyncTransforms();
            TankTownSmokeCapture.Capture(Path.Combine(output, "bomb-structure-before.png"), true, 1920, 1080);
            float started = Time.realtimeSinceStartup;
            while (bomb != null && !bomb.Detonated && Time.realtimeSinceStartup - started < 1.2f) yield return null;
            bool detonated = bomb.Detonated, structureImpact = bomb.StructureImpact, groundImpact = bomb.GroundImpact;
            int contacts = bomb.Contacts;
            float impactSeconds = Time.realtimeSinceStartup - started;
            if (!detonated || !structureImpact || groundImpact || contacts != 1 || game.Items.Weapon != ItemKind.None)
            {
                Fail($"structure contact did not detonate correctly: detonated={detonated}, structure={structureImpact}, ground={groundImpact}, contacts={contacts}");
                yield break;
            }
            yield return new WaitForSecondsRealtime(.10f);
            TankTownSmokeCapture.Capture(Path.Combine(output, "bomb-structure-explosion.png"), true, 1920, 1080);
            Debug.Log($"BOMB STRUCTURE SMOKE COMPLETE: impactSeconds={impactSeconds:0.000}, kind={wall.Kind}, groundImpact={groundImpact}, structureImpact={structureImpact}, contacts={contacts}, radius={game.Settings.gigantBombRadius}, impulse={game.Settings.gigantBombImpulseMultiplier}, broken={game.BrokenBlocks}, state={game.State}");
            Application.Quit(0);
        }
        private IEnumerator StagesCheck(TankTownGame game)
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            var keyboard = InputSystem.AddDevice<Keyboard>(); InputSystem.EnableDevice(keyboard); keyboard.MakeCurrent();
            float classicExtent = game.World.HalfExtent; int classicBuildings = game.World.Buildings.Count;
            var titleGame = game; var preview = game.TitlePreview; int titleSeed = game.CitySeed;
            Capture("stages-01-classic-title");
            yield return Press(keyboard, Key.Digit2);
            game = FindFirstObjectByType<TankTownGame>();
            if (game != titleGame || game.TitlePreview != preview || game.CitySeed != titleSeed || game.State != RoundState.Ready || game.Stage != TownStage.BigCity || preview.HalfExtent != classicExtent * 2)
            { Fail("title selection reloaded or changed the fixed large-town preview"); yield break; }
            yield return null; Capture("stages-02-large-title");
            Camera.main.transform.position = new Vector3(0, preview.HalfExtent * 2.35f, -preview.HalfExtent * .55f);
            Camera.main.transform.LookAt(Vector3.zero);
            TankTownSmokeCapture.Capture(Path.Combine(output, "stages-03-large-overview.png"), false, 1920, 1080);
            yield return Press(keyboard, Key.Space);
            float until = Time.realtimeSinceStartup + 5;
            while (!game.IsPlaying && Time.realtimeSinceStartup < until) { game = FindFirstObjectByType<TankTownGame>(); yield return null; }
            if (!game.IsPlaying) { Fail("space did not start the selected stage"); yield break; }
            if (game.World.HalfExtent != classicExtent * 2 || game.World.Buildings.Count != classicBuildings * 4) { Fail("chosen large play field was not generated"); yield break; }
            game.Input.useHardware = true;
            yield return new WaitForSecondsRealtime(.2f); Capture("stages-04-large-play");
            int cars = game.World.TrafficCars.Count, people = game.World.Pedestrians.Count;
            Isolate(game);
            game.Tank.Warp(new Pose(game.World.SpawnPosition, Quaternion.identity)); Freeze(game); game.CameraRig.Snap();
            game.Items.Receive(ItemKind.MegaBeam);
            var rewardRoot = new GameObject("Stage smoke auto missile pickup"); rewardRoot.transform.SetParent(game.World.transform);
            rewardRoot.transform.position = game.Tank.transform.position;
            var reward = rewardRoot.AddComponent<ItemPickup>(); reward.Initialize(game, ItemKind.Missiles, game.World.SpecialIcons.Material(ItemKind.Missiles));
            bool automatic = reward.Collect();
            var rockets = FindObjectsByType<ItemMissile>(FindObjectsSortMode.None);
            if (!automatic || rockets.Length != 5 || game.Items.Held != ItemKind.MegaBeam || game.Items.Weapon != ItemKind.None)
            { Fail("automatic missile pickup displaced the held laser"); yield break; }
            float launchHeight = rockets.Min(x => x.transform.position.y);
            yield return new WaitForSeconds(.25f); Capture("stages-05-auto-missiles");
            bool flying = rockets.Any(x => x.transform.position.y > launchHeight + 3);
            yield return Press(keyboard, Key.Space);
            if (!flying || !game.Items.StopsForward || !game.Items.MissilesActive) { Fail($"missiles did not fly independently with the laser: flying={flying}, beam={game.Items.StopsForward}, missileTime={game.Items.Remaining(ItemKind.Missiles)}, state={game.State}"); yield break; }
            yield return new WaitForSecondsRealtime(.1f); Capture("stages-06-missiles-laser");
            yield return new WaitForSeconds(game.Settings.megaBeamSeconds + .1f);
            if (!game.Items.MissilesActive || FindObjectsByType<ItemMissile>(FindObjectsSortMode.None).Length != 5) { Fail("laser completion removed missiles"); yield break; }
            game.Items.Receive(ItemKind.Missiles); game.Items.Receive(ItemKind.GigantBomb);
            yield return Press(keyboard, Key.Space);
            var bomb = FindFirstObjectByType<GigantBomb>();
            if (bomb == null || !game.Items.MissilesActive) { Fail("bomb and automatic missiles did not coexist"); yield break; }
            yield return new WaitForSecondsRealtime(.1f); Capture("stages-07-missiles-bomb");
            until = Time.realtimeSinceStartup + 6;
            while (bomb != null && !bomb.Detonated && Time.realtimeSinceStartup < until) yield return null;
            if (!bomb.Detonated || !game.Items.MissilesActive) { Fail("bomb detonation stopped the missile timer"); yield break; }
            yield return new WaitForSecondsRealtime(.1f); Capture("stages-08-bomb-explosion");
            game.AdvanceTime(1000); yield return new WaitForSecondsRealtime(1.1f); Capture("stages-09-large-results");
            if (game.Items.MissilesActive || FindObjectsByType<ItemMissile>(FindObjectsSortMode.None).Length > 0) { Fail("round end did not clear missiles"); yield break; }
            yield return Press(keyboard, Key.Space);
            game = FindFirstObjectByType<TankTownGame>(); until = Time.realtimeSinceStartup + 5;
            while (!game.IsPlaying && Time.realtimeSinceStartup < until) { game = FindFirstObjectByType<TankTownGame>(); yield return null; }
            if (!game.IsPlaying || game.Stage != TownStage.BigCity) { Fail("replay lost the selected stage"); yield break; }
            game.TogglePause(); game.ReturnToTitle(); yield return null; yield return null;
            yield return Press(keyboard, Key.Digit1);
            game = FindFirstObjectByType<TankTownGame>(); yield return null;
            TankTownSmokeCapture.Capture(Path.Combine(output, "stages-10-classic-restored-720.png"), true, 1280, 720);
            if (game.TitlePreview.HalfExtent != classicExtent * 2) { Fail("classic selection changed the title backdrop"); yield break; }
            yield return Press(keyboard, Key.Space); until = Time.realtimeSinceStartup + 5;
            while (!game.IsPlaying && Time.realtimeSinceStartup < until) { game = FindFirstObjectByType<TankTownGame>(); yield return null; }
            if (!game.IsPlaying || game.Stage != TownStage.Town || game.World.HalfExtent != classicExtent || game.World.Buildings.Count != classicBuildings)
            { Fail("original stage was not restored"); yield break; }
            Debug.Log($"STAGES SMOKE COMPLETE: classicExtent={classicExtent}, largeExtent={classicExtent * 2}, areaMultiplier=4, classicBuildings={classicBuildings}, largeBuildings={classicBuildings * 4}, largeCars={cars}, largePeople={people}, autoMissiles=5, laserCoexists=true, bombCoexists=true, replayPreserved=true, classicRestored=true, sourceHalfBlocks={Resources.Load<TankTownSettings>("TankTownSettings").cityHalfBlocks}");
            InputSystem.RemoveDevice(keyboard); Application.Quit(0);
        }
        private static IEnumerator Press(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
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
