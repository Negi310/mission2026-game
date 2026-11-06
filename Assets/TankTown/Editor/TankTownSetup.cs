using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TankTown.Editor
{
    public static class TankTownSetup
    {
        public const string ScenePath = "Assets/TankTown/Scenes/TankTown.unity";

        [MenuItem("Tank Town/Create or open playable scene")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureAssets();
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void EnsureAssets()
        {
            Directory.CreateDirectory("Assets/TankTown/Scenes");
            Directory.CreateDirectory("Assets/TankTown/Resources");
            AssetDatabase.Refresh();
            if (!File.Exists("Assets/TankTown/Resources/TankTownSettings.asset"))
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<TankTownSettings>(), "Assets/TankTown/Resources/TankTownSettings.asset");
            if (!File.Exists("Assets/TankTown/Resources/TankTownSurface.mat"))
                AssetDatabase.CreateAsset(new Material(Shader.Find("TankTown/Saturated Toon")), "Assets/TankTown/Resources/TankTownSurface.mat");
            var surface = AssetDatabase.LoadAssetAtPath<Material>("Assets/TankTown/Resources/TankTownSurface.mat");
            surface.shader = Shader.Find("TankTown/Saturated Toon");
            surface.enableInstancing = true;
            EditorUtility.SetDirty(surface);
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Tank Town Game", typeof(TankTownGame));
                // Declare runtime fog in the scene too, so automatic build stripping
                // keeps the linear-fog shader variant used by the generated town.
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = new Color(.5f, .86f, 1);
                RenderSettings.fogStartDistance = 160;
                RenderSettings.fogEndDistance = 410;
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.productName = "Tank Town";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.allowFullscreenSwitch = true;
            AssetDatabase.SaveAssets();
        }

        public static void BuildWindows()
        {
            BuildWindowsPlayer("Builds/TankTown/TankTown.exe", BuildOptions.Development);
        }

        public static void BuildWindowsRelease()
        {
            const string outputPath = "Builds/TankTown-Release/TankTown.exe";
            string sourceCommit = System.Environment.GetEnvironmentVariable("TANKTOWN_BUILD_COMMIT");
            if (string.IsNullOrEmpty(sourceCommit) ||
                !System.Text.RegularExpressions.Regex.IsMatch(sourceCommit, "^[0-9a-f]{40}$"))
                throw new System.Exception("Set TANKTOWN_BUILD_COMMIT to the verified source commit before building a release.");

            BuildWindowsPlayer(outputPath, BuildOptions.None);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(outputPath), "build-info.json"),
                JsonUtility.ToJson(new ReleaseBuildInfo
                {
                    version = PlayerSettings.bundleVersion,
                    sourceCommit = sourceCommit,
                    unityVersion = Application.unityVersion,
                    platform = "Windows-x64",
                    developmentBuild = false
                }, true));
        }

        [System.Serializable]
        private sealed class ReleaseBuildInfo
        {
            public string version;
            public string sourceCommit;
            public string unityVersion;
            public string platform;
            public bool developmentBuild;
        }

        private static void BuildWindowsPlayer(string outputPath, BuildOptions options)
        {
            EnsureAssets();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = options
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Tank Town build failed: " + report.summary.result);
            foreach (string relative in new[] { "microbit-controller.json", "docs/microbit-controller.md", "hardware/microbit/main.py" })
            {
                string destination = Path.Combine(Path.GetDirectoryName(outputPath), relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(relative, destination, true);
            }
            File.Copy("hardware/microbit/launch-diagnostics.cmd",
                Path.Combine(Path.GetDirectoryName(outputPath), "接続診断.cmd"), true);
        }
    }
}
