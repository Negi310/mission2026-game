#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
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
            void Capture(string name) => TankTownSmokeCapture.Capture(Path.Combine(folder, name + ".png"), true, 1440, 900);
            Capture("wheel-title");
            game.Input.SetWheel(1); yield return new WaitForSecondsRealtime(.2f);
            game.Input.SetWheel(0); yield return new WaitForSecondsRealtime(.4f); Capture("wheel-title-turn");
            game.ToggleHowTo(); yield return new WaitForSecondsRealtime(.3f); Capture("wheel-help");
            game.ToggleHowTo(); game.StartOrRestart(); yield return null;
            game.TogglePause(); yield return new WaitForSecondsRealtime(.3f); Capture("wheel-pause");
            game.TogglePause(); game.AdvanceTime(100); yield return new WaitForSecondsRealtime(1.3f); Capture("wheel-results");
            game.Input.SetWheel(1); yield return new WaitForSecondsRealtime(.2f); game.Input.SetWheel(0);
            yield return new WaitForSecondsRealtime(.35f); game.Input.PulsePad(0);
            yield return new WaitForSecondsRealtime(.3f); Capture("wheel-records");
            if (!game.GetComponent<TankTownHUD>().ResultsView.DetailsOpen) { Debug.LogError("WHEEL MENU FAILED: result confirm"); Application.Quit(1); yield break; }
            Debug.Log("WHEEL MENU SMOKE COMPLETE: title, steering, howto, pause, results, records"); Application.Quit(0);
        }
    }
}
#endif
