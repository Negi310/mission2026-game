#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace TankTown
{
    public sealed class MicrobitDiagnosticsSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--microbit-diagnostics-smoke") >= 0)
                new GameObject("Controller diagnostics visual check").AddComponent<MicrobitDiagnosticsSmoke>();
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return new WaitForSecondsRealtime(2);
            var panel = FindFirstObjectByType<MicrobitDiagnosticsPanel>();
            if (panel == null || !panel.Visible) { Debug.LogError("MICROBIT DIAGNOSTICS SMOKE FAILED: panel missing"); Application.Quit(1); yield break; }
            string folder = Path.Combine(Application.dataPath, "..", "Captures"); Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "controller-diagnostics.png");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1);
            if (!File.Exists(path)) { Debug.LogError("MICROBIT DIAGNOSTICS SMOKE FAILED: capture missing"); Application.Quit(1); yield break; }
            Debug.Log("MICROBIT DIAGNOSTICS SMOKE COMPLETE: " + panel.Report().Replace('\n', ' '));
            Application.Quit();
        }
    }
}
#endif
