#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TankTown
{
    public sealed class TankTownHudSmoke : MonoBehaviour
    {
        private TankTownGame game;
        private string output;
        private int captures;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--tank-town-hud-smoke") < 0) return;
            TankTownGame.SelectedStage = Array.IndexOf(args, "--hud-amusement") >= 0 ? TownStage.AmusementPark : Array.IndexOf(args, "--hud-big") >= 0 ? TownStage.BigCity : Array.IndexOf(args, "--hud-town") >= 0 ? TownStage.Town : TownStage.CandyTown;
            TankTownGame.GenerationSeedOverride = 2026; TankTownGame.SkipIntroForTests = true; TankTownGame.DisableRecordSavingForTests = true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-hud-smoke") >= 0)
                new GameObject("Gameplay HUD smoke").AddComponent<TankTownHudSmoke>();
        }
        private void Capture(string name, int width = 1920, int height = 1080)
        {
            Canvas.ForceUpdateCanvases();
            VerifyLayout();
            TankTownSmokeCapture.Capture(Path.Combine(output, $"hud-{game.Stage}-{name}.png"), true, width, height, VerifyLayout); captures++;
        }
        private static Rect Bounds(RectTransform target)
        {
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            var space = GameObject.Find("Gameplay HUD").transform;
            var min = space.InverseTransformPoint(corners[0]); var max = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private void VerifyLayout()
        {
            var hud = game.GetComponent<TankTownHUD>();
            if (hud.Minimap != null || GameObject.Find("Minimap camera") != null || GameObject.Find("Chain celebration").GetComponent<Image>() != null)
                throw new InvalidOperationException("The minimal HUD must have no live map or chain card");
            if (game.IsPlaying && GameObject.Find("Gameplay HUD").GetComponentsInChildren<Text>().Any(x => System.Text.RegularExpressions.Regex.IsMatch(x.text, @"[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]")))
                throw new InvalidOperationException("Gameplay explanatory text remains");
            if (hud.SpecialsView.HeldRoot.gameObject.activeSelf != (game.Items.Held != ItemKind.None))
                throw new InvalidOperationException("An empty held weapon card must be hidden");
            if (GameObject.Find("Points").GetComponent<Text>().text != $"{game.RoundScore:N0} pt")
                throw new InvalidOperationException("HUD points differ from the round score");
            Rect screen = Bounds((RectTransform)GameObject.Find("Gameplay HUD").transform);
            foreach (var root in new[] { hud.SpecialsView.HeldRoot, hud.SpecialsView.AutomaticRoot, hud.SpecialsView.WeaponRoot, hud.UpgradesView.Root })
            {
                Rect rect = Bounds(root);
                if (!screen.Contains(rect.min) || !screen.Contains(rect.max)) throw new InvalidOperationException(root.name + " left the HUD viewport");
            }
            Rect held = Bounds(hud.SpecialsView.HeldRoot), auto = Bounds(hud.SpecialsView.AutomaticRoot), weapon = Bounds(hud.SpecialsView.WeaponRoot);
            if (held.Overlaps(auto) || held.Overlaps(weapon) || auto.Overlaps(weapon)) throw new InvalidOperationException("Special item widgets overlap");
            if (Bounds(hud.UpgradesView.Root).Overlaps(held) || Bounds(hud.UpgradesView.Root).Overlaps(auto) || Bounds(hud.UpgradesView.Root).Overlaps(weapon))
                throw new InvalidOperationException("The enlarged upgrade row overlaps a special item widget");
            foreach (var text in hud.SpecialsView.AutomaticRoot.GetComponentsInChildren<Text>())
                if (text.text.Length > 0 && (text.preferredWidth > text.rectTransform.rect.width + 1 || text.preferredHeight > text.rectTransform.rect.height + 1))
                    throw new InvalidOperationException("Special text clips: " + text.name + " / " + text.text);
        }
        private TankPickup Pickup(UpgradeKind kind)
        {
            var root = new GameObject("HUD smoke pickup"); root.transform.SetParent(game.World.transform);
            root.transform.position = game.Tank.transform.position + new Vector3(((int)kind - 1.5f) * 2, 0, 5);
            var pickup = root.AddComponent<TankPickup>(); pickup.Initialize(game, kind, game.World.Icons.Material(kind));
            game.World.Pickups.Add(pickup); return pickup;
        }
        private IEnumerator Start()
        {
            var run = Run();
            while (true)
            {
                bool more = false; Exception failure = null;
                try { more = run.MoveNext(); } catch (Exception error) { failure = error; }
                if (failure != null) { Debug.LogException(failure); Application.Quit(1); yield break; }
                if (!more) yield break;
                yield return run.Current;
            }
        }
        private IEnumerator Run()
        {
            Application.runInBackground = true; Time.captureDeltaTime = 1f / 60;
            yield return new WaitForSecondsRealtime(.5f);
            game = FindFirstObjectByType<TankTownGame>(); game.StartOrRestart(); game.Input.useHardware = true;
            game.Tank.Warp(new Pose(game.World.SpawnPosition, Quaternion.identity)); game.Tank.enabled = false;
            var body = game.Tank.GetComponent<Rigidbody>(); body.isKinematic = true; game.CameraRig.Snap();
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var person in game.World.Pedestrians) person.gameObject.SetActive(false);
            foreach (var item in game.World.ItemPickups) item.gameObject.SetActive(false);
            foreach (var item in game.World.Pickups) item.gameObject.SetActive(false);
            output = Path.Combine(Application.dataPath, "..", "Captures"); Directory.CreateDirectory(output);
            yield return new WaitForSeconds(.15f); Capture("01-empty");
            foreach (var kind in new[] { UpgradeKind.Speed, UpgradeKind.Power, UpgradeKind.Rapid, UpgradeKind.Handling })
                for (int i = 0; i < 2; i++) Pickup(kind).Collect();
            yield return new WaitForSeconds(.08f); Capture("02-level-roll");
            yield return new WaitForSeconds(.32f); Capture("03-eight-pickups");
            if (game.PickupDisplay.Count != 8 || GameObject.Find("Pickup batch count").GetComponent<Text>().text != "+8")
                throw new InvalidOperationException("Batch collection count failed");
            foreach (var kind in new[] { ItemKind.Missiles, ItemKind.Magnet, ItemKind.Bulldozer, ItemKind.HighSpeed }) game.Items.Receive(kind);
            game.Items.Receive(ItemKind.MegaBeam);
            yield return new WaitForSeconds(.4f); Capture("04-held-and-auto"); Capture("05-held-and-auto-720", 1280, 720); Capture("06-held-and-auto-900", 1440, 900);
            Capture("16-held-and-auto-5x4", 1280, 1024);
            game.Items.Receive(ItemKind.Magnet); yield return new WaitForSeconds(.05f); Capture("07-duration-reset");
            game.Items.UseHeld(); game.Items.Receive(ItemKind.GigantBomb);
            yield return new WaitForSeconds(.8f); Capture("08-beam-and-queued-bomb"); Capture("09-beam-720", 1280, 720);
            if (GameObject.Find("Special use prompt").GetComponent<Image>().color == TankTownMenuLayout.Lime) throw new InvalidOperationException("Queued weapon prompt must be dimmed");
            yield return new WaitForSeconds(1.6f); Capture("10-beam-ended");
            game.Items.UseHeld(); yield return null; Capture("11-bomb-flight");
            if (!GameObject.Find("Effect GigantBomb").GetComponentsInChildren<Text>().Any(x => x.text == "↗")) throw new InvalidOperationException("Bomb flight state failed");
            game.TogglePause(); yield return null; Capture("12-paused"); game.TogglePause();
            yield return new WaitForSeconds(6.5f); Capture("13-expired");
            if (GameObject.Find("Controls") != null || game.Items.Active != ItemKind.None) throw new InvalidOperationException("Controls removal or expiry failed");
            for (int depth = 1; depth <= 3; depth++) game.ChainBlast(new Vector3(800, 12, 800), .2f, depth);
            yield return new WaitForSeconds(.25f); Capture("14-chain");
            game.AdvanceTime(game.Remaining - 10); yield return null; Capture("15-final-seconds");
            Debug.Log($"HUD SMOKE COMPLETE: stage={game.Stage}, captures={captures}, simultaneousUpgrades=8, automaticEffects=4, beamWithQueuedBomb=True, resolutions=1920x1080/1280x720/1440x900, seed={game.CitySeed}");
            Application.Quit(0);
        }
    }
}
#endif
