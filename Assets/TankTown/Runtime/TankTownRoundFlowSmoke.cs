#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TankTown
{
    public sealed class TankTownRoundFlowSmoke : MonoBehaviour
    {
        private Keyboard keyboard;
        private string output;
        private bool failed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-round-flow-smoke") < 0) return;
            TankTownGame.GenerationSeedOverride = 2026;
            TankTownGame.SkipIntroForTests = false;
            TankTownGame.DisableRecordSavingForTests = true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-round-flow-smoke") >= 0 && FindFirstObjectByType<TankTownRoundFlowSmoke>() == null)
            {
                var root = new GameObject("Round flow smoke"); DontDestroyOnLoad(root); root.AddComponent<TankTownRoundFlowSmoke>();
            }
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true;
            output = Path.Combine(Application.dataPath, "..", "Captures"); Directory.CreateDirectory(output);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            keyboard = InputSystem.AddDevice<Keyboard>(); InputSystem.EnableDevice(keyboard); keyboard.MakeCurrent();
            yield return new WaitForSecondsRealtime(.6f);
            var game = FindFirstObjectByType<TankTownGame>(); game.Input.useHardware = true;
            var overview = Camera.main.transform.position;
            Debug.Log($"TITLE CAMERA SMOKE: height={overview.y:0.0}, pitch={Camera.main.transform.eulerAngles.x:0.0}, beatSeconds={TankTownRoundFlow.BeatSeconds:0.00}, scoreCountSeconds={TankTownResultsScreen.CountSeconds:0.00}");
            Capture("flow-01-title", 1920, 1080);
            yield return new WaitForSecondsRealtime(4);
            Capture("flow-02-title-orbit", 1280, 720);
            if (game.State != RoundState.Ready || game.Remaining != 60 || Vector3.Distance(overview, Camera.main.transform.position) < 1) { Fail("title orbit or frozen timer"); yield break; }
            yield return Press(Key.H); Capture("flow-03-how-to", 1280, 720);
            if (!game.HowToOpen) { Fail("H did not open tutorial"); yield break; }
            yield return Press(Key.Escape);
            yield return Press(Key.Space);
            var seen = new List<string>(); bool covered = false;
            while (!game.IsPlaying)
            {
                covered |= game.Presentation.CoverOpacity == 1;
                string text = game.Presentation.Countdown;
                if (text.Length > 0 && !seen.Contains(text)) { seen.Add(text); Capture("flow-04-countdown-" + text, 1280, 720); }
                if (game.Remaining != 60 || FindFirstObjectByType<TankShell>() != null) { Fail("intro consumed time or fired"); yield break; }
                yield return null;
            }
            if (!covered || !seen.SequenceEqual(new[] { "3", "2", "1" })) { Fail("countdown sequence or uncovered camera cut"); yield break; }
            Capture("flow-05-go", 1280, 720);
            yield return new WaitForSecondsRealtime(.6f);
            Capture("flow-06-play", 1280, 720);
            // Create real destruction and a real chain before recording the results UI.
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
            foreach (var building in game.World.Buildings.Take(5))
            {
                foreach (var support in building.Blocks.Where(b => b != null && b.Foundation && b.Column < 2).ToArray()) support.Break(support.transform.position, 0);
                building.ReleaseUnsupported(building.transform.position, 0);
            }
            game.ChainBlast(new Vector3(700, 30, 700), 1, 3); game.ApplyPenalty();
            yield return new WaitForSecondsRealtime(.5f);
            game.AdvanceTime(1000); yield return new WaitForSecondsRealtime(.12f);
            Capture("flow-07-score-count-up", 1920, 1080);
            yield return new WaitForSecondsRealtime(TankTownResultsScreen.CountSeconds + .1f); Capture("flow-08-results", 1920, 1080);
            Capture("flow-09-results-720", 1280, 720);
            var results = game.GetComponent<TankTownHUD>().ResultsView;
            if (results.DisplayedScore != game.RoundScore || game.Remaining != 0 || game.State != RoundState.Results) { Fail("result score did not settle"); yield break; }
            int score = game.RoundScore, broken = game.BrokenBlocks, instance = game.GetInstanceID();
            // Replays exercise the real keyboard path, not the menu callback.
            float replayAt = Time.realtimeSinceStartup;
            TankTownGame.GenerationSeedOverride = 67541;
            yield return Press(Key.Space);
            if (game.State != RoundState.Restarting) { Fail("Space did not start replay"); yield break; }
            float timeout = Time.realtimeSinceStartup + 6;
            while (game != null && Time.realtimeSinceStartup < timeout) yield return null;
            game = FindFirstObjectByType<TankTownGame>();
            if (game == null || game.GetInstanceID() == instance) { Fail("replay did not reload"); yield break; }
            game.Input.useHardware = true;
            while (!game.IsPlaying && Time.realtimeSinceStartup < timeout) yield return null;
            float replaySeconds = Time.realtimeSinceStartup - replayAt;
            if (!game.IsPlaying || game.CitySeed != 67541 || game.BrokenBlocks != 0 || FindFirstObjectByType<TankShell>() != null) { Fail("new round state or carried Space shot"); yield break; }
            Capture("flow-10-replay-go", 1280, 720);
            yield return new WaitForSecondsRealtime(.4f); Capture("flow-11-replay", 1280, 720);
            yield return Press(Key.Space);
            if (FindFirstObjectByType<TankShell>() == null) { Fail("Space did not fire in play"); yield break; }
            Debug.Log($"ROUND FLOW SMOKE COMPLETE: titleOrbit=true, howTo=true, countdown=3/2/1/GO, coveredCut=true, frozenIntro=true, score={score}, broken={broken}, exactResults=true, spaceReplay=true, freshCity={game.CitySeed}, replaySeconds={replaySeconds:0.000}, spaceFiresInPlay=true");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-beam-range-check") >= 0)
                yield return BeamAcrossField(game);
            InputSystem.RemoveDevice(keyboard); Application.Quit(failed ? 1 : 0);
        }

        private IEnumerator BeamAcrossField(TankTownGame game)
        {
            yield return new WaitForSeconds(.4f);
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            foreach (var explosive in game.World.Explosives) if (explosive != null) explosive.gameObject.SetActive(false);
            foreach (var person in game.World.Pedestrians) person.gameObject.SetActive(false);
            foreach (var car in game.World.TrafficCars) car.gameObject.SetActive(false);
            float extent = game.World.HalfExtent;
            game.Tank.Warp(new Pose(game.World.GroundPoint(new Vector3(-extent + 12, 0, -extent + 12), game.Settings.rideHeight), Quaternion.Euler(0, 45, 0)));
            game.CameraRig.Snap();
            Vector3 start = game.Tank.Muzzle.position, direction = game.Tank.Muzzle.forward;
            float distance = 2 * (extent - 12) * Mathf.Sqrt(2) / Vector3.ProjectOnPlane(direction, Vector3.up).magnitude;
            var marker = game.World.Box("Beam range diagnostic target", start + direction * distance, Vector3.one * 3, game.World.ShotMaterial);
            var target = marker.AddComponent<DestructibleBlock>(); target.Initialize(game); Physics.SyncTransforms();
            game.Items.Receive(ItemKind.MegaBeam); game.Input.PulsePad(0);
            if (!target.Detached) { Fail("laser did not damage the opposite corner"); yield break; }
            var segment = GameObject.Find("Mega beam").GetComponentsInChildren<LineRenderer>().First(x => x.positionCount == 2);
            float visualRange = Vector3.Distance(segment.GetPosition(0), segment.GetPosition(1));
            if (visualRange * Vector3.ProjectOnPlane(direction, Vector3.up).magnitude < extent * 2 * Mathf.Sqrt(2)) { Fail("laser visual did not cross the whole field"); yield break; }
            yield return new WaitForSeconds(.08f);
            Capture("range-01-play-beam", 1280, 720);
            game.CameraRig.enabled = false;
            Camera.main.transform.position = new Vector3(-170, 140, 30); Camera.main.transform.LookAt(new Vector3(0, 12, 0));
            yield return null;
            TankTownSmokeCapture.Capture(Path.Combine(output, "range-02-field-beam.png"), false, 1920, 1080);
            Debug.Log($"FIELD BEAM SMOKE COMPLETE: visualRange={visualRange:0.0}, fieldDiagonal={extent * 2 * Mathf.Sqrt(2):0.0}, farTargetDistance={distance:0.0}, farTargetDetached=true, seconds={game.Settings.megaBeamSeconds}, radius={game.Settings.megaBeamRadius}");
        }
        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null;
            Debug.Log($"FLOW KEY CHECK: key={key}, enabled={keyboard.enabled}, current={Keyboard.current == keyboard}, down={keyboard[key].isPressed}, pressed={keyboard[key].wasPressedThisFrame}");
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }
        private void Capture(string name, int width, int height) => TankTownSmokeCapture.Capture(Path.Combine(output, name + ".png"), true, width, height);
        private void Fail(string message) { failed = true; Debug.LogError("ROUND FLOW SMOKE FAILED: " + message); Application.Quit(1); }
    }
}
#endif
