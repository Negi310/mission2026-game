#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
namespace TankTown
{
    public sealed class TankTownWheelMenuSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--wheel-menu-smoke") < 0) return;
            TankTownGame.SkipIntroForTests = true; TankTownGame.DisableRecordSavingForTests = true;
            TankTownGame.GenerationSeedOverride = 2026;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        { if (Array.IndexOf(Environment.GetCommandLineArgs(), "--wheel-menu-smoke") >= 0) new GameObject("Wheel menu check").AddComponent<TankTownWheelMenuSmoke>(); }
        private IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return new WaitForSecondsRealtime(1);
            var game = FindFirstObjectByType<TankTownGame>(); game.Input.useHardware = true;
            string folder = Path.Combine(Application.dataPath, "..", "Captures"); Directory.CreateDirectory(folder);
            void Capture(string name)
            {
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(1920, 1080) })
                    TankTownSmokeCapture.Capture(Path.Combine(folder, name + "-" + size.x + "x" + size.y + ".png"), true, size.x, size.y, ValidateText);
            }
            Capture("wheel-title");
            game.Input.SetWheel(1); yield return new WaitForSecondsRealtime(.2f);
            game.Input.SetWheel(0); yield return new WaitForSecondsRealtime(.4f); Capture("wheel-title-turn");
            game.ToggleHowTo(); yield return new WaitForSecondsRealtime(.3f); Capture("wheel-help");
            game.Input.PulsePad(0); yield return null;
            if(game.HowToOpen || game.State != RoundState.Ready) { Debug.LogError("WHEEL MENU FAILED: tutorial return"); Application.Quit(1); yield break; }
            game.StartOrRestart(); yield return null;
            game.TogglePause(); yield return new WaitForSecondsRealtime(.3f); Capture("wheel-pause");
            game.Input.SetWheel(1); yield return new WaitForSecondsRealtime(.2f); game.Input.SetWheel(0);
            yield return new WaitForSecondsRealtime(.3f); game.Input.PulsePad(0);
            yield return new WaitForSecondsRealtime(.3f); Capture("wheel-pause-help");
            game.Input.PulsePad(0); yield return null;
            if(game.State != RoundState.Paused) { Debug.LogError("WHEEL MENU FAILED: pause tutorial return"); Application.Quit(1); yield break; }
            game.TogglePause(); game.AdvanceTime(100);
            for (int i = 0; i < 18; i++)
            {
                game.Input.PulsePad(i % 2);
                if (game.State != RoundState.Results) { Debug.LogError("WHEEL MENU FAILED: firing burst restarted the round"); Application.Quit(1); yield break; }
                yield return new WaitForSecondsRealtime(.1f);
            }
            game.Input.SetPressure(0, 1); yield return new WaitForSecondsRealtime(1.5f);
            if(game.GetComponent<TankTownHUD>().ResultsView.CanConfirm) { Debug.LogError("WHEEL MENU FAILED: held pressure armed results"); Application.Quit(1); yield break; }
            game.Input.SetPressure(0, 0);
            float deadline = Time.realtimeSinceStartup + 4;
            while (!game.GetComponent<TankTownHUD>().ResultsView.CanConfirm && Time.realtimeSinceStartup < deadline) yield return null;
            if(!game.GetComponent<TankTownHUD>().ResultsView.CanConfirm) { Debug.LogError("WHEEL MENU FAILED: results did not rearm after release"); Application.Quit(1); yield break; }
            Capture("wheel-results");
            game.Input.SetWheel(1); yield return new WaitForSecondsRealtime(.2f); game.Input.SetWheel(0);
            yield return new WaitForSecondsRealtime(.35f); game.Input.PulsePad(0);
            yield return new WaitForSecondsRealtime(.3f); Capture("wheel-records");
            if (!game.GetComponent<TankTownHUD>().ResultsView.DetailsOpen) { Debug.LogError("WHEEL MENU FAILED: result confirm"); Application.Quit(1); yield break; }
            Debug.Log("WHEEL MENU SMOKE COMPLETE: title, steering, howto, pause, pause guide, results, records, firing burst and held-pressure guard, text geometry at 1280x720, 1024x768, 1920x1080"); Application.Quit(0);
        }

        private static void ValidateText()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            var area = ((RectTransform)canvas.transform).rect;
            int checkedLabels = 0;
            foreach (var text in canvas.GetComponentsInChildren<Text>())
            {
                if (!text.enabled || string.IsNullOrWhiteSpace(text.text)) continue;
                float alpha = text.color.a;
                foreach (var group in text.GetComponentsInParent<CanvasGroup>()) alpha *= group.alpha;
                if (alpha < .01f) continue;
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas.transform, text.transform);
                if (bounds.max.x < area.xMin || bounds.min.x > area.xMax || bounds.max.y < area.yMin || bounds.min.y > area.yMax) continue;
                if (text.font == null) throw new InvalidOperationException("MENU FONT FAILED: missing font: " + text.name);
                text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
                using (var generator = new TextGenerator())
                {
                    generator.Populate(text.text, text.GetGenerationSettings(text.rectTransform.rect.size));
                    if (generator.vertexCount < 4)
                        throw new InvalidOperationException("MENU FONT FAILED: empty glyph mesh: " + text.name + "; text=" + text.text + "; font=" + text.font.name);
                }
                checkedLabels++;
            }
            if (checkedLabels < 3) throw new InvalidOperationException("MENU FONT FAILED: no visible menu labels were checked");
            Debug.Log("MENU TEXT GEOMETRY PASS: " + checkedLabels + " visible labels");
        }
    }
}
#endif
