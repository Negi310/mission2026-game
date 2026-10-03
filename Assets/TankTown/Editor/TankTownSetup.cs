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
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            AssetDatabase.SaveAssets();
        }

        public static void BuildWindows()
        {
            EnsureAssets();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/TankTown/TankTown.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Tank Town build failed: " + report.summary.result);
        }
    }
}
