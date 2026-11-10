// BlossomRallySetup.cs - one-time project setup when the project is first opened:
//  * linear colour space (correct pastel colours)
//  * legacy Input Manager enabled ("Both") so the keyboard controls work in any Unity version
//  * creates + opens Assets/BlossomRally/Scenes/BlossomValley.unity and adds it to Build Settings
// Menu: Blossom Rally > Open Game Scene / Play
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlossomRally.EditorTools
{
    [InitializeOnLoad]
    public static class BlossomRallySetup
    {
        const string ScenePath = "Assets/BlossomRally/Scenes/BlossomValley.unity";
        const string SessionKey = "BlossomRally.SetupDone";

        static BlossomRallySetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(SessionKey, false)) return;
                SessionState.SetBool(SessionKey, true);
                Setup(false);
            };
        }

        [MenuItem("Blossom Rally/Open Game Scene", false, 1)]
        static void OpenScene() { Setup(true); }

        [MenuItem("Blossom Rally/Play", false, 2)]
        static void Play()
        {
            Setup(true);
            EditorApplication.isPlaying = true;
        }

        static void Setup(bool forceOpen)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            EnsureLegacyInput();
            EnsureScene();
            var active = SceneManager.GetActiveScene();
            bool emptyUntitled = string.IsNullOrEmpty(active.path) && active.rootCount <= 2;
            if (forceOpen || emptyUntitled || active.path == ScenePath)
            {
                if (active.path != ScenePath && (!active.isDirty || EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()))
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        internal static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                // Unity refuses to add a scene next to an unsaved "Untitled" scene, so the game scene replaces the
                // open scene instead (an Untitled scene holds nothing of yours; a saved, modified scene is offered
                // for saving first). The game scene is left open afterwards.
                var prev = SceneManager.GetActiveScene();
                if (!string.IsNullOrEmpty(prev.path) && prev.isDirty && !Application.isBatchMode)
                    EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var go = new GameObject("BlossomRally");
                go.AddComponent<GameManager>();
                var cam = new GameObject("Main Camera");
                cam.tag = "MainCamera";
                cam.AddComponent<Camera>();
                cam.AddComponent<AudioListener>();
                cam.transform.position = new Vector3(0f, 120f, -300f);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        /// <summary>
        /// Input handling: the game works with the new Input System (keyboard + gamepad through the
        /// Input System package) and also with "Both". Only "Input Manager (Old)" (0) is fixed, by switching
        /// to the new Input System (1) - the same thing the Input System package asks for, so the two never
        /// fight (Unity 6.6 does not keep "Both" reliably). Name kept for older callers.
        /// </summary>
        internal static void EnsureLegacyInput()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null || prop.intValue != 0) return;      // 1 = new, 2 = both: both fine
            prop.intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            if (!Application.isBatchMode && EditorUtility.DisplayDialog("Blossom Rally",
                    "Blossom Rally uses the new Input System (keyboard + gamepad). Active Input Handling was set to " +
                    "'Input System Package (New)'.\n\nUnity must restart to apply it. Restart now?", "Restart", "Later"))
                EditorApplication.OpenProject(Directory.GetCurrentDirectory());
        }
    }
}
