using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TankTown.Tests
{
    public sealed class TankTownRoundFlowTests
    {
        private TankTownGame game;
        private Keyboard keyboard;
        private float captureStep;
        private bool skipIntro;
        private bool runInBackground;
        private InputSettings.BackgroundBehavior backgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode editorBehavior;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            skipIntro = TankTownGame.SkipIntroForTests; TankTownGame.SkipIntroForTests = false;
            captureStep = Time.captureDeltaTime; Time.captureDeltaTime = 0;
            runInBackground = Application.runInBackground; Application.runInBackground = true;
            backgroundBehavior = InputSystem.settings.backgroundBehavior;
            editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            SceneManager.LoadScene("TankTown"); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>(); game.Input.useHardware = true;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorBehavior;
            Application.runInBackground = runInBackground;
            TankTownGame.SkipIntroForTests = skipIntro; Time.captureDeltaTime = captureStep;
            Time.timeScale = 1; yield return null;
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }

        private IEnumerator WaitForPlay()
        {
            float timeout = Time.realtimeSinceStartup + 3;
            while (!game.IsPlaying && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.That(game.IsPlaying, Is.True, "The rapid intro must reach play without another input");
        }

        [UnityTest]
        public IEnumerator TitleCameraOrbitsSlowlyWhileTimeTankAndBuildingsAreFrozen()
        {
            var view = Camera.main; Vector3 cameraPosition = view.transform.position;
            Quaternion cameraRotation = view.transform.rotation;
            Vector3 tankPosition = game.Tank.transform.position;
            Vector3 support = game.World.Buildings[0].Blocks[0].transform.position;
            float highestRoof = game.TitlePreview.HighestRoof;
            Assert.That(view.transform.position.y - highestRoof, Is.InRange(8, 14), "The title must stay near roof height and clear the tallest building");
            Assert.That(view.transform.forward.y, Is.InRange(-.35f, -.07f), "The town must be seen from the side with only a gentle downward pitch");
            float sampledAt = Time.unscaledTime;
            yield return new WaitForSecondsRealtime(.4f);
            float elapsed = Time.unscaledTime - sampledAt;
            // The same slow angular motion travels farther around the larger backdrop.
            float maxDistance = game.TitlePreview.HalfExtent * 1.22f * 4 * Mathf.Deg2Rad * elapsed;
            Assert.That(Vector3.Distance(cameraPosition, view.transform.position), Is.InRange(.1f, maxDistance));
            Assert.That(Quaternion.Angle(cameraRotation, view.transform.rotation), Is.InRange(.1f, 3));
            Assert.That(game.State, Is.EqualTo(RoundState.Ready)); Assert.That(Time.timeScale, Is.Zero);
            Assert.That(game.Remaining, Is.EqualTo(60)); Assert.That(game.BrokenBlocks, Is.Zero);
            Assert.That(game.Tank.transform.position, Is.EqualTo(tankPosition));
            Assert.That(game.World.Buildings[0].Blocks[0].transform.position, Is.EqualTo(support));
        }

        [UnityTest]
        public IEnumerator HOpensReadableHowToEscapeClosesAndSpaceStartsFromIt()
        {
            yield return Press(Key.H); Assert.That(game.HowToOpen, Is.True);
            var tutorial = GameObject.Find("How to card"); Assert.That(tutorial, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            foreach (var text in tutorial.GetComponentsInChildren<Text>())
            {
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.name);
                Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 1), text.name);
            }
            yield return Press(Key.Escape); Assert.That(game.HowToOpen, Is.False);
            Assert.That(game.State, Is.EqualTo(RoundState.Ready));
            GameObject.Find("How to button").GetComponent<Button>().onClick.Invoke(); yield return null;
            yield return Press(Key.Space); Assert.That(game.HowToOpen, Is.False);
            yield return WaitForPlay(); Assert.That(Object.FindFirstObjectByType<TankShell>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator FastThreeTwoOneGoFreezesInputsAndTimerAndCoversTheViewpointChange()
        {
            game.StartOrRestart();
            var seen = new List<string>(); bool covered = false;
            Vector3 initialTank = game.Tank.transform.position;
            float started = Time.realtimeSinceStartup;
            while (!game.IsPlaying && Time.realtimeSinceStartup - started < 3)
            {
                string number = game.Presentation.Countdown;
                if (number.Length > 0 && !seen.Contains(number)) seen.Add(number);
                covered |= game.Presentation.CoverOpacity == 1;
                Assert.That(game.Remaining, Is.EqualTo(60)); Assert.That(Time.timeScale, Is.Zero);
                Assert.That(game.Tank.transform.position, Is.EqualTo(initialTank));
                game.Input.PulsePad(0); game.Input.PulsePad(1); game.StartOrRestart();
                Assert.That(Object.FindFirstObjectByType<TankShell>(), Is.Null);
                yield return null;
            }
            Assert.That(game.IsPlaying, Is.True); Assert.That(covered, Is.True);
            Assert.That(seen, Is.EqualTo(new[] { "3", "2", "1" }));
            Assert.That(game.Presentation.Countdown, Is.EqualTo("GO!"));
            Assert.That(Time.realtimeSinceStartup - started, Is.LessThan(1.3f));
            Assert.That(game.CameraRig.enabled, Is.True);
            Assert.That(Camera.main.fieldOfView, Is.EqualTo(game.Settings.cameraFieldOfView));
            Assert.That(game.Remaining, Is.InRange(59.9f, 60));
            yield return new WaitForSecondsRealtime(TankTownRoundFlow.GoSeconds + .08f);
            Assert.That(game.Presentation.Countdown, Is.Empty);
            game.Input.PulsePad(0); Assert.That(Object.FindFirstObjectByType<TankShell>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ResultsCountUpInFrozenTimeAndShowExactScoreAndRoundMetrics()
        {
            game.StartOrRestart(); yield return WaitForPlay();
            game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
            int broken = game.TotalBlocks * 43 / 100;
            for (int i = 0; i < broken; i++) game.ScoreBlock();
            game.ChainBlast(new Vector3(700, 30, 700), 1, 4); game.ApplyPenalty();
            game.AdvanceTime(1000); yield return null;
            var results = game.GetComponent<TankTownHUD>().ResultsView;
            Assert.That(Time.timeScale, Is.Zero); Assert.That(game.Remaining, Is.Zero);
            Assert.That(results.DetailsOpen, Is.False);
            Assert.That(GameObject.Find("Local leaderboard"), Is.Null);
            Assert.That(GameObject.Find("Result metric value 0"), Is.Null);
            Assert.That(GameObject.Find("Result score card").GetComponentsInChildren<Text>().Length, Is.LessThanOrEqualTo(7));
            int previous = results.DisplayedScore;
            float timeout = Time.realtimeSinceStartup + 2;
            while (results.Counting && Time.realtimeSinceStartup < timeout)
            {
                yield return null; Assert.That(results.DisplayedScore, Is.GreaterThanOrEqualTo(previous)); previous = results.DisplayedScore;
            }
            Assert.That(results.DisplayedScore, Is.EqualTo(game.RoundScore));
            Assert.That(game.RoundScore, Is.EqualTo(Mathf.RoundToInt(100000f * broken / game.TotalBlocks)));
            yield return Press(Key.Tab); Assert.That(results.DetailsOpen, Is.True);
            Assert.That(GameObject.Find("Result metric value 0").GetComponent<Text>().text, Is.EqualTo(broken.ToString("N0")));
            Assert.That(GameObject.Find("Result metric value 1").GetComponent<Text>().text, Is.EqualTo("1"));
            Assert.That(GameObject.Find("Result metric value 2").GetComponent<Text>().text, Is.EqualTo("4 CHAIN"));
            Assert.That(GameObject.Find("Result metric value 3").GetComponent<Text>().text, Is.EqualTo("1"));
            Assert.That(game.NewBest, Is.True); Assert.That(game.BestScore, Is.EqualTo(game.RoundScore));
            Canvas.ForceUpdateCanvases();
            foreach (var text in GameObject.Find("Result details card").GetComponentsInChildren<Text>())
            {
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.name);
                Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 1), text.name);
            }
            yield return Press(Key.Escape); Assert.That(results.DetailsOpen, Is.False);
            Assert.That(results.DisplayedScore, Is.EqualTo(game.RoundScore));
            Assert.That(GameObject.Find("Local leaderboard"), Is.Null);
        }

        [UnityTest]
        public IEnumerator ActualSpaceImmediatelyReplaysDuringScoreAnimationWithoutShootingOrDoubleReload()
        {
            game.StartOrRestart(); yield return WaitForPlay();
            for (int i = 0; i < 100; i++) game.ScoreBlock();
            game.AdvanceTime(1000); yield return null;
            Assert.That(game.GetComponent<TankTownHUD>().ResultsView.Counting, Is.True);
            int oldInstance = game.GetInstanceID();
            yield return Press(Key.Space);
            Assert.That(game.State, Is.EqualTo(RoundState.Restarting));
            game.Input.PulsePad(0); game.StartOrRestart();
            float timeout = Time.realtimeSinceStartup + 3;
            while (game != null && Time.realtimeSinceStartup < timeout) yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>();
            Assert.That(game.GetInstanceID(), Is.Not.EqualTo(oldInstance));
            Assert.That(game.State, Is.EqualTo(RoundState.Starting));
            Assert.That(game.Presentation.CoverOpacity, Is.EqualTo(1));
            Assert.That(game.Remaining, Is.EqualTo(60)); Assert.That(game.BrokenBlocks, Is.Zero);
            yield return WaitForPlay();
            int newInstance = game.GetInstanceID(); yield return new WaitForSecondsRealtime(.15f);
            Assert.That(Object.FindFirstObjectByType<TankTownGame>().GetInstanceID(), Is.EqualTo(newInstance));
            Assert.That(Object.FindFirstObjectByType<TankShell>(), Is.Null);
            yield return Press(Key.Space); Assert.That(Object.FindFirstObjectByType<TankShell>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ReturnToTitleRestoresSlowOverviewAndTheHowToButton()
        {
            game.StartOrRestart(); yield return WaitForPlay(); game.AdvanceTime(1000);
            game.ReturnToTitle(); yield return null;
            game = Object.FindFirstObjectByType<TankTownGame>();
            Assert.That(game.State, Is.EqualTo(RoundState.Ready)); Assert.That(game.CameraRig.enabled, Is.False);
            Assert.That(Camera.main.transform.position.y, Is.InRange(38, 60));
            GameObject.Find("How to button").GetComponent<Button>().onClick.Invoke(); yield return null;
            Assert.That(game.HowToOpen, Is.True); Assert.That(GameObject.Find("How to card"), Is.Not.Null);
        }
    }
}
