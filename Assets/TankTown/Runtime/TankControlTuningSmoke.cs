#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace TankTown
{
    public sealed class TankControlTuningSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--control-tuning-smoke") >= 0)
                new GameObject("Control tuning visual check").AddComponent<TankControlTuningSmoke>();
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return new WaitForSecondsRealtime(2);
            var panel = FindFirstObjectByType<TankControlTuningPanel>();
            if (panel == null) { Debug.LogError("CONTROL TUNING SMOKE FAILED: panel missing"); Application.Quit(1); yield break; }
            panel.Open();
            yield return new WaitForEndOfFrame();
            string directory = Path.Combine(Application.dataPath, "..", "Captures"); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "control-tuning.png"); ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1);
            if (!File.Exists(path)) { Debug.LogError("CONTROL TUNING SMOKE FAILED: capture missing"); Application.Quit(1); yield break; }
            Debug.Log("CONTROL TUNING SMOKE COMPLETE"); Application.Quit();
        }
    }
}
#endif
