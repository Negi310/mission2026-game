#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TankTown
{
    public sealed class TankTownCandySmoke : MonoBehaviour
    {
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-candy-smoke") < 0) return;
            TankTownGame.SelectedStage = TownStage.CandyTown; TankTownGame.GenerationSeedOverride = 2026;
            TankTownGame.SkipIntroForTests = true; TankTownGame.DisableRecordSavingForTests = true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-candy-smoke") >= 0)
                new GameObject("Candy stage smoke").AddComponent<TankTownCandySmoke>();
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true; Time.captureDeltaTime = 1f / 60;
            output = Path.Combine(Application.dataPath, "..", "Captures"); Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(.4f);
            var game = FindFirstObjectByType<TankTownGame>(); Capture(game, "candy-01-title");
            game.StartOrRestart(); game.Input.useHardware = true; game.Input.SetWheel(0);
            yield return new WaitForSeconds(.6f); Capture(game, "candy-02-play");
            if (GameObject.Find("Gameplay HUD").GetComponentsInChildren<Text>().Any(x => x.text == "TANK" || x.text == "TOWN")) { Fail("the gameplay logo remains"); yield break; }
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var person in game.World.Pedestrians) person.gameObject.SetActive(false);
            var cake = game.World.Buildings.First(x => x.Style == 0);
            game.Tank.Warp(new Pose(game.World.SpawnPosition, Quaternion.identity));
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true; game.CameraRig.enabled = false;
            View(cake.transform.position, new Vector3(-28, 21, -34), 12); yield return null; Capture(game, "candy-03-cake");
            View(cake.transform.position, new Vector3(-18, 8, -22), 7); yield return null; Capture(game, "candy-08-cake-texture");
            View(cake.transform.position, new Vector3(-24, 5, -26), 10); yield return null; Capture(game, "candy-12-strawberry-tiers");
            var chocolate = game.World.Buildings.First(x => x.Style == 1);
            View(chocolate.transform.position, new Vector3(-17, 8, -22), 7); yield return null; Capture(game, "candy-09-chocolate-texture");
            var facade = chocolate.Blocks.First(x => x.Kind == BuildingPart.Wall && x.Level == 1);
            Camera.main.transform.position = facade.transform.position + facade.transform.forward * 12 - facade.transform.right * 4 + Vector3.up * 3;
            Camera.main.transform.LookAt(facade.transform.position); yield return null; Capture(game, "candy-13-chocolate-relief");
            var sideFacade = chocolate.Blocks.First(x => x.Kind == BuildingPart.Wall && x.Level == 1 && x.Column == 3);
            Camera.main.transform.position = sideFacade.transform.position + sideFacade.transform.forward * 12 - sideFacade.transform.right * 4 + Vector3.up * 3;
            Camera.main.transform.LookAt(sideFacade.transform.position); yield return null; Capture(game, "candy-15-chocolate-side");
            MoveTank(game, chocolate.transform.position + new Vector3(-4.6f, 0, -chocolate.Width / 2 - 19));
            View(chocolate.transform.position, new Vector3(-26, 16, -32), 9);
            int before = game.BrokenBlocks; game.Input.PulsePad(0); yield return new WaitForSeconds(.6f);
            if (game.BrokenBlocks <= before || chocolate.FullyDestroyed) { Fail("the cannon must break candy locally"); yield break; }
            Capture(game, "candy-04-chocolate-hit");
            foreach (var part in chocolate.Blocks.Where(x => !x.Detached).ToArray()) part.Break(part.transform.position, 0);
            yield return new WaitForSeconds(4.9f);
            if (chocolate.Reward.Area == null) { Fail("full candy destruction did not release an area"); yield break; }
            View(chocolate.transform.position, new Vector3(-24, 16, -32), 2); Capture(game, "candy-05-reward");
            var biscuit = game.World.Buildings.First(x => x.Style == 2);
            View(biscuit.transform.position, new Vector3(-20, 12, -25), 7); yield return null; Capture(game, "candy-10-biscuit-texture");
            var cupcake = game.World.GetComponentsInChildren<Transform>().First(x => x.name == "Crushable cupcake");
            View(cupcake.position, new Vector3(-10, 6, -12), 2.5f); yield return null; Capture(game, "candy-14-cupcake-cream");
            var lollipop = game.World.GetComponentsInChildren<Transform>().First(x => x.name == "Lollipop tree");
            View(lollipop.position, new Vector3(-5, 4, -7), 2.5f); yield return null; Capture(game, "candy-11-lollipop-texture");
            var gate = game.World.Buildings.Single(x => x.Style == 3);
            MoveTank(game, gate.transform.position + Vector3.back * 34);
            View(gate.transform.position, new Vector3(-26, 18, -36), 9); yield return null; Capture(game, "candy-06-doughnut");
            foreach (var support in gate.Blocks.Where(x => x.Foundation).ToArray()) support.Break(support.transform.position, 0);
            gate.ReleaseUnsupported(gate.transform.position, 0);
            yield return new WaitForSeconds(.4f); Capture(game, "candy-07-doughnut-collapse");
            if (!gate.FullyDestroyed || gate.Reward.Area == null) { Fail("unsupported doughnut did not collapse and release its reward"); yield break; }
            int textureCount = game.World.GetComponentsInChildren<Renderer>().SelectMany(x => x.sharedMaterials).Where(x => x != null && x.IsKeywordEnabled("_CANDY_TEXTURE")).Select(x => x.GetTexture("_BaseMap")).Where(x => x != null).Distinct().Count();
            if (textureCount != 10) { Fail("all ten candy textures must be used by the stage"); yield break; }
            Debug.Log($"CANDY SMOKE COMPLETE: stage={game.Stage}, buildings={game.World.Buildings.Count}, cakes={game.World.Buildings.Count(x => x.Style == 0)}, chocolate={game.World.Buildings.Count(x => x.Style == 1)}, biscuits={game.World.Buildings.Count(x => x.Style == 2)}, candyTextures={textureCount}, physicalGate=true, cannonLocalBreak=true, fullRewardArea=true, gameplayLogo=false");
            Application.Quit(0);
        }
        private static void MoveTank(TankTownGame game, Vector3 point)
        {
            var body = game.Tank.GetComponent<Rigidbody>(); body.position = game.World.GroundPoint(point, game.Settings.rideHeight); body.rotation = Quaternion.identity;
            game.Tank.transform.SetPositionAndRotation(body.position, body.rotation); Physics.SyncTransforms();
        }
        private static void View(Vector3 focus, Vector3 offset, float height)
        { Camera.main.transform.position = focus + offset; Camera.main.transform.LookAt(focus + Vector3.up * height); }
        private void Capture(TankTownGame game, string label)
        {
            foreach (var building in game.World.Buildings) building.Presentation?.Tick(Camera.main.transform.position);
            game.World.RefreshMapMeshes();
            TankTownSmokeCapture.Capture(Path.Combine(output, label + ".png"), true, 1920, 1080);
        }
        private static void Fail(string message) { Debug.LogError("CANDY SMOKE FAILED: " + message); Application.Quit(1); }
    }
}
#endif
