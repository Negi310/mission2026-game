#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-title-layout") >= 0)
                yield return TitleLayout(game);
            yield return new WaitForSecondsRealtime(4);
            Capture("flow-02-title-orbit", 1280, 720);
            if (game.State != RoundState.Ready || game.Remaining != game.Settings.roundSeconds || Vector3.Distance(overview, Camera.main.transform.position) < 1) { Fail("title orbit or frozen timer"); yield break; }
            yield return Press(Key.H); Capture("flow-03-how-to", 1280, 720);
            if (!game.HowToOpen) { Fail("H did not open tutorial"); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-menu-layout") >= 0)
                CaptureMenuLayouts("How to card", "howto", false);
            yield return Press(Key.Escape);
            yield return Press(Key.Space);
            var seen = new List<string>(); bool covered = false;
            while (!game.IsPlaying)
            {
                covered |= game.Presentation.CoverOpacity == 1;
                string text = game.Presentation.Countdown;
                if (text.Length > 0 && !seen.Contains(text)) { seen.Add(text); Capture("flow-04-countdown-" + text, 1280, 720); }
                if (game.Remaining != game.Settings.roundSeconds || FindFirstObjectByType<TankShell>() != null) { Fail("intro consumed time or fired"); yield break; }
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
            bool existingBest = Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-result-existing-best") >= 0;
            if (existingBest)
            {
                game.Ranking.Submit(60000, 2026, out _);
                // This opt-in development fixture changes memory only; it never writes a player record.
                typeof(TankTownGame).GetProperty(nameof(TankTownGame.BestScore)).SetValue(game, 60000);
            }
            game.AdvanceTime(1000);
            if (existingBest && (game.NewBest || game.BestScore != 60000 || game.FinalRank != 2))
            { Fail("existing-best result fixture did not produce the normal rank-two palette"); yield break; }
            Debug.Log($"RESULT PALETTE SMOKE: newBest={game.NewBest}, rank={game.FinalRank}, best={game.BestScore}, score={game.RoundScore}");
            yield return new WaitForSecondsRealtime(.12f);
            Capture("flow-07-score-count-up", 1920, 1080);
            yield return new WaitForSecondsRealtime(TankTownResultsScreen.CountSeconds + .6f); Capture("flow-08-results", 1920, 1080);
            Capture("flow-09-results-720", 1280, 720);
            var results = game.GetComponent<TankTownHUD>().ResultsView;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-menu-layout") >= 0)
            {
                if (results.DetailsOpen || GameObject.Find("Local leaderboard") != null ||
                    GameObject.Find("Result score card").GetComponentsInChildren<Text>().Length > 12)
                    throw new InvalidOperationException("The default result still contains detailed records");
                CaptureMenuLayouts("Result score card", "results", false);
                CaptureMenuLayouts("Result score card", "results-max", true);
                int records = game.Ranking.Records.Count;
                yield return Press(Key.Tab);
                if (!results.DetailsOpen) throw new InvalidOperationException("Tab did not open result details");
                CaptureMenuLayouts("Result details card", "details", false);
                CaptureMenuLayouts("Result details card", "details-max", true);
                yield return Press(Key.Escape);
                if (results.DetailsOpen || GameObject.Find("Local leaderboard") != null)
                    throw new InvalidOperationException("Escape did not return to the simple result");
                ClickMenuControl("Result details button"); yield return null;
                if (!results.DetailsOpen) throw new InvalidOperationException("Mouse did not open result details");
                ClickMenuControl("Result details close button"); yield return null;
                if (results.DetailsOpen || results.DisplayedScore != game.RoundScore || game.Ranking.Records.Count != records)
                    throw new InvalidOperationException("Closing result details changed the score or records");
                // The global replay action must also work from the optional records view.
                yield return Press(Key.Tab);
                Debug.Log("MENU LAYOUT COMPLETE: colorful party result and optional records; keyboard and mouse disclosure; no duplicate record; four resolutions; screen insets; readable text; maximum score and ten ranking rows");
            }
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

        private void CaptureMenuLayouts(string rootName, string prefix, bool maximumValues)
        {
            var root = GameObject.Find(rootName);
            var originals = root.GetComponentsInChildren<UnityEngine.UI.Text>().ToDictionary(text => text, text => text.text);
            try
            {
                foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1440, 900), new Vector2Int(1280, 1024) })
                    TankTownSmokeCapture.Capture(Path.Combine(output, $"menus-{prefix}-{size.x}x{size.y}.png"), true, size.x, size.y, () =>
                    {
                        if (maximumValues)
                        {
                            bool details = rootName == "Result details card";
                            GameObject.Find(details ? "Result detail score" : "Result score").GetComponent<Text>().text = details ? "100,000 pt" : "100,000<size=38> pt</size>";
                            GameObject.Find(details ? "Result best score" : "Result record note").GetComponent<Text>().text = "BEST 100,000 pt";
                            if (!details)
                            {
                                GameObject.Find("Result hero rank").GetComponent<Text>().text = "#11";
                                GameObject.Find("Result toy score board").GetComponent<TankTownResultArt>().SetMotion(1);
                            }
                            if (details)
                            {
                                GameObject.Find("Result ranking position").GetComponent<Text>().text = "10位";
                                for (int i = 0; i < 10; i++) GameObject.Find("Ranking score " + i).GetComponent<Text>().text = $"{100000 - i * 1234:N0} pt";
                                for (int i = 0; i < 4; i++) GameObject.Find("Result metric value " + i).GetComponent<Text>().text = i == 2 ? "999 CHAIN" : "99,999";
                            }
                        }
                        Canvas.ForceUpdateCanvases();
                        var canvas = root.GetComponentInParent<Canvas>(); var viewport = ((RectTransform)canvas.transform).rect;
                        var safe = Rect.MinMaxRect(viewport.xMin + 24, viewport.yMin + 24, viewport.xMax - 24, viewport.yMax - 24);
                        foreach (var text in root.GetComponentsInChildren<Text>())
                        {
                            if (text.preferredWidth > text.rectTransform.rect.width + 1 || text.preferredHeight > text.rectTransform.rect.height + 1)
                                throw new InvalidOperationException("Menu text clips: " + text.name + " / " + text.text);
                            var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                            foreach (var corner in corners)
                                if (!safe.Contains(canvas.transform.InverseTransformPoint(corner)))
                                    throw new InvalidOperationException("Menu text touches the screen edge: " + text.name);
                        }
                    });
            }
            finally { foreach (var pair in originals) pair.Key.text = pair.Value; }
        }

        private static void ClickMenuControl(string name)
        {
            Canvas.ForceUpdateCanvases();
            var target = (RectTransform)GameObject.Find(name).transform;
            var pointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center)), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count == 0 || ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler) != target.gameObject)
                throw new InvalidOperationException("Mouse cannot activate result control: " + name);
        }

        private IEnumerator TitleLayout(TankTownGame game)
        {
            int world = game.World.GetInstanceID(); TownStage initial = game.Stage;
            string[] names = { "Town stage button", "Large stage button", "Candy stage button", "Amusement stage button" };
            for (int i = 0; i < names.Length; i++)
            {
                yield return Press((Key)((int)Key.Digit1 + i));
                if (game.Stage != (TownStage)i || game.World.GetInstanceID() != world)
                    throw new InvalidOperationException("Stage selection changed the title world or failed");
                for (int j = 0; j < names.Length; j++)
                    if (GameObject.Find(names[j] + " selected").GetComponent<Image>().enabled != (i == j))
                        throw new InvalidOperationException("Stage underline differs from the selected stage");
                TankTownSmokeCapture.Capture(Path.Combine(output, "title-clean-stage-" + i + ".png"), true, 1920, 1080, VerifyTitleLayout);
            }
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1440, 900), new Vector2Int(1280, 1024) })
                TankTownSmokeCapture.Capture(Path.Combine(output, $"title-clean-{size.x}x{size.y}.png"), true, size.x, size.y, VerifyTitleLayout);
            // Raycast the actual title UI before dispatching the pointer click.
            var target = (RectTransform)GameObject.Find(names[(int)initial]).transform;
            var pointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center)), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count == 0 || ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler) != target.gameObject || game.Stage != initial)
                throw new InvalidOperationException("Mouse cannot select the visible stage tab");
            yield return null;
            Debug.Log("TITLE LAYOUT COMPLETE: four keyboard tabs and selected underlines; mouse raycast; no world regeneration; four resolutions; text and viewport bounds");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tank-town-title-hover") >= 0)
                yield return TitleHover();
        }

        private IEnumerator TitleHover()
        {
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            EventSystem.current.SetSelectedGameObject(null);
            string[] names = { "Title play button", "Town stage button", "Large stage button", "Candy stage button", "Amusement stage button", "How to button" };
            foreach (string name in names)
            {
                var root = GameObject.Find(name); var motion = root.GetComponent<TankTownMenuHover>();
                var hit = (RectTransform)root.transform; Vector2 position = hit.anchoredPosition; Vector3 scale = hit.localScale;
                ExecuteEvents.Execute(root, pointer, ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.075f);
                if (motion.Visual.localScale.x <= 1 || Time.timeScale != 0)
                    throw new InvalidOperationException("Title hover does not animate while time is frozen: " + name);
                yield return new WaitForSecondsRealtime(.2f);
                if (motion.Visual.localScale.x < 1.023f || hit.anchoredPosition != position || hit.localScale != scale)
                    throw new InvalidOperationException("Title hover moved its hit region or failed to rise: " + name);
                if (name == names[0]) Capture("title-hover-raised", 1920, 1080);
                ExecuteEvents.Execute(root, pointer, ExecuteEvents.pointerDownHandler);
                yield return new WaitForSecondsRealtime(.15f);
                if (motion.Visual.localScale.x >= 1)
                    throw new InvalidOperationException("Title button does not compress on press: " + name);
                if (name == names[0]) Capture("title-hover-pressed", 1920, 1080);
                ExecuteEvents.Execute(root, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(root, pointer, ExecuteEvents.pointerExitHandler);
                yield return new WaitForSecondsRealtime(.4f);
                if (Mathf.Abs(motion.Visual.localScale.x - 1) > .001f || motion.Visual.anchoredPosition.magnitude > .01f)
                    throw new InvalidOperationException("Title hover does not settle after exit: " + name);
                if (name == names[0]) Capture("title-hover-restored", 1920, 1080);
            }
            var play = GameObject.Find(names[0]);
            ExecuteEvents.Execute(play, new AxisEventData(EventSystem.current), ExecuteEvents.selectHandler);
            yield return new WaitForSecondsRealtime(.25f);
            if (play.GetComponent<TankTownMenuHover>().Visual.localScale.x < 1.02f)
                throw new InvalidOperationException("Keyboard focus does not animate");
            ExecuteEvents.Execute(play, new BaseEventData(EventSystem.current), ExecuteEvents.deselectHandler);
            var game = FindFirstObjectByType<TankTownGame>(); game.ToggleHowTo(); yield return null;
            if (play.GetComponent<TankTownMenuHover>().Visual.localScale != Vector3.one)
                throw new InvalidOperationException("Hidden title retained hover state");
            game.ToggleHowTo(); yield return null;
            Debug.Log("TITLE HOVER COMPLETE: six controls; unscaled rise, compression and return; stable hit rectangles; keyboard focus; disable reset");
        }

        private static void VerifyTitleLayout()
        {
            Canvas.ForceUpdateCanvases();
            var root = GameObject.Find("Title content");
            if (root.GetComponentsInChildren<Text>().Length != 9 || GameObject.Find("Title glass panel") != null || GameObject.Find("Title item legend") != null)
                throw new InvalidOperationException("The title still contains extra information");
            var canvas = root.GetComponentInParent<Canvas>(); var viewport = ((RectTransform)canvas.transform).rect;
            foreach (var text in root.GetComponentsInChildren<Text>())
            {
                if (text.preferredWidth > text.rectTransform.rect.width + 1 || text.preferredHeight > text.rectTransform.rect.height + 1)
                    throw new InvalidOperationException("Title text clips: " + text.name);
                var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners)
                    if (!viewport.Contains(canvas.transform.InverseTransformPoint(corner)))
                        throw new InvalidOperationException("Title text leaves viewport: " + text.name);
            }
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
