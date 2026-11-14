using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace TankTown.Tests
{
    public sealed class TankTownWheelMenuTests
    {
        [Test]
        public void MenuTextStillProducesGlyphsWhenFontMetricsExceedItsLabelBox()
        {
            var canvas = new GameObject("Font layout regression", typeof(Canvas));
            var style = new ToyHudStyle();
            var font = Resources.Load<Font>("Fonts/NotoSansJP-Regular");
            Assert.That(font, Is.Not.Null, "Japanese font data must be bundled");
            try
            {
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var layout = new TankTownMenuLayout("Font layout", canvas.transform, font, style, Color.clear);
                string[] samples = { "ひとやすみ", "TANK TOWN", "—", "もどる" };
                int[] sizes = { 72, 92, 86, 29 };
                float[] heights = { 125, 125, 110, 38 };
                var missing = new System.Collections.Generic.List<string>();
                for (int i = 0; i < samples.Length; i++)
                {
                    var label = layout.Label(layout.Layout, "Font sample " + i, samples[i], Vector2.zero, new Vector2(1200, heights[i]), sizes[i], Color.white);
                    font.RequestCharactersInTexture(samples[i], sizes[i], FontStyle.Bold);
                    using (var generator = new TextGenerator())
                    {
                        var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
                        generator.Populate(label.text, settings);
                        if (generator.vertexCount < 4) missing.Add(samples[i] + " (font=" + sizes[i] + ", height=" + heights[i] + ")");
                    }
                }
                Assert.That(missing, Is.Empty, "Visible menu labels must not disappear: " + string.Join(", ", missing));
            }
            finally { Object.DestroyImmediate(canvas); style.Dispose(); }
        }

        [Test]
        public void ResultConfirmWaitsForRevealAndForTheFiringBurstToStop()
        {
            var gate = new ResultConfirmGate(); gate.Reset(10);
            Assert.That(gate.TryConfirm(10), Is.False, "The frame ending the round cannot confirm a menu");
            for (int i = 1; i <= 40; i++)
            {
                double now = 10 + i * .1;
                gate.Observe(now, false);
                Assert.That(gate.TryConfirm(now), Is.False, "Continuous firing must remain blocked after the reveal");
            }
            for (int i = 1; i <= 6; i++) gate.Observe(14 + i * .1, false);
            Assert.That(gate.Ready, Is.False);
            gate.Observe(14.66, false); Assert.That(gate.Ready, Is.True);
            Assert.That(gate.TryConfirm(14.7), Is.True, "A fresh decision after the quiet interval works");
            gate.Reset(20); Assert.That(gate.Ready, Is.False, "Every result starts locked");
            Assert.That(gate.TryConfirm(21), Is.False, "Even idle entry must finish the reveal first");
        }

        [Test]
        public void HoldingConfirmCannotArmTheResultMenu()
        {
            var gate = new ResultConfirmGate(); gate.Reset(0);
            for (int i = 0; i <= 50; i++) gate.Observe(i * .1, true);
            Assert.That(gate.Ready, Is.False);
            for (int i = 1; i <= 6; i++) gate.Observe(5 + i * .1, false);
            Assert.That(gate.Ready, Is.False);
            gate.Observe(5.66, false); Assert.That(gate.Ready, Is.True);
            Assert.That(gate.TryConfirm(5.7), Is.True);
        }

        [Test]
        public void SlowFramesCannotMakeContinuousFiringCountAsIdle()
        {
            var gate = new ResultConfirmGate(); gate.Reset(0);
            for (int i = 1; i <= 10; i++)
            {
                gate.Observe(i * 2, false);
                Assert.That(gate.TryConfirm(i * 2), Is.False, "A long frame between firing events must not arm replay");
            }
        }

        [UnityTest]
        public IEnumerator FiringAcrossRoundEndCannotRestartUntilReleasedAndPressedAgain()
        {
            var oldStage = TankTownGame.SelectedStage;
            var oldSeed = TankTownGame.GenerationSeedOverride;
            var oldSkip = TankTownGame.SkipIntroForTests;
            var oldSaving = TankTownGame.DisableRecordSavingForTests;
            TankTownGame game = null;
            try
            {
                TankTownGame.SelectedStage = TownStage.Town;
                TankTownGame.GenerationSeedOverride = 2026;
                TankTownGame.SkipIntroForTests = true;
                TankTownGame.DisableRecordSavingForTests = true;
                SceneManager.LoadScene("TankTown"); yield return null;
                game = Object.FindFirstObjectByType<TankTownGame>();
                var view = game.GetComponent<TankTownHUD>().ResultsView;
                game.StartOrRestart(); Assert.That(game.State, Is.EqualTo(RoundState.Playing));
                game.AdvanceTime(100);
                game.Input.PulsePad(0);
                Assert.That(game.State, Is.EqualTo(RoundState.Results), "Input before the first results HUD update is consumed");
                for (int i = 0; i < 18; i++)
                {
                    yield return new WaitForSecondsRealtime(.1f);
                    game.Input.PulsePad(i % 2);
                    Assert.That(game.State, Is.EqualTo(RoundState.Results), "Firing continues past the reveal without replaying");
                }
                game.Input.SetPressure(0, 1);
                yield return new WaitForSecondsRealtime(1.5f);
                Assert.That(view.CanConfirm, Is.False, "Held pressure never arms the menu");
                game.Input.SetPressure(0, 0);
                float deadline = Time.realtimeSinceStartup + 4;
                while (!view.CanConfirm && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(view.CanConfirm, Is.True);
                TankTownGame.SkipIntroForTests = false;
                game.Input.PulsePad(0);
                Assert.That(game.State, Is.EqualTo(RoundState.Restarting), "A new press after releasing starts one replay");
            }
            finally
            {
                if (game != null) Object.Destroy(game.gameObject);
                TankTownGame.SelectedStage = oldStage;
                TankTownGame.GenerationSeedOverride = oldSeed;
                TankTownGame.SkipIntroForTests = oldSkip;
                TankTownGame.DisableRecordSavingForTests = oldSaving;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SteeringHasNeutralGateAndConfirmInvokesOnlyOneChoice()
        {
            TankTownGame.DisableRecordSavingForTests = true;
            SceneManager.LoadScene("TankTown"); yield return null;
            var game = Object.FindFirstObjectByType<TankTownGame>();
            game.Input.useHardware = true; game.Input.SetWheel(1); yield return null;
            var canvas = new GameObject("Test carousel canvas", typeof(Canvas));
            var style = new ToyHudStyle(); var font = Resources.Load<Font>("Fonts/NotoSansJP-Regular");
            var carousel = new TankTownCardCarousel(game, canvas.transform, font, style, "Test carousel");
            int first = 0, second = 0;
            carousel.Set("Test", "", new[] {
                new TankTownCardCarousel.Choice("A", "", "GO", Color.red, () => first++),
                new TankTownCardCarousel.Choice("B", "", "GO", Color.blue, () => second++) });
            carousel.Tick(.1f); Assert.That(carousel.Selected, Is.Zero, "Held wheel on entry requires neutral first");
            game.Input.SetWheel(0); yield return null; carousel.Tick(.1f);
            game.Input.SetWheel(.3f); yield return null; carousel.Tick(.1f); Assert.That(carousel.Selected, Is.Zero, "Small noise cannot change selection");
            game.Input.SetWheel(.65f); yield return null; carousel.Tick(.1f); Assert.That(carousel.Selected, Is.Zero, "Tilting beyond the former threshold must no longer switch menus");
            game.Input.SetWheel(1); yield return null; carousel.Tick(.1f); Assert.That(carousel.Selected, Is.EqualTo(1));
            carousel.Confirm(); carousel.Confirm(); Assert.That(second, Is.EqualTo(1)); Assert.That(first, Is.Zero);
            Object.Destroy(canvas); style.Dispose(); game.Input.SetWheel(0);
            TankTownGame.DisableRecordSavingForTests = false;
        }
    }
}
