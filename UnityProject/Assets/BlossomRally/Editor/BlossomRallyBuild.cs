// BlossomRallyBuild.cs - one-click macOS builds.
// Menu: Blossom Rally > Build macOS App (Apple Silicon) / (Universal) / Open Builds Folder.
// Needs the "Mac Build Support" module in Unity Hub (installed by default on a Mac editor).
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BlossomRally.EditorTools
{
    public static class BlossomRallyBuild
    {
        const string ScenePath = "Assets/BlossomRally/Scenes/BlossomValley.unity";
        const string OutDir = "Builds";

        [MenuItem("Blossom Rally/Build macOS App (Apple Silicon)", false, 20)]
        static void BuildArm() { Build("ARM64", "BlossomRally.app"); }

        [MenuItem("Blossom Rally/Build macOS App (Universal Intel + Apple Silicon)", false, 21)]
        static void BuildUniversal() { Build("x64ARM64", "BlossomRally_Universal.app"); }

        [MenuItem("Blossom Rally/Open Builds Folder", false, 22)]
        static void OpenFolder()
        {
            Directory.CreateDirectory(OutDir);
            EditorUtility.RevealInFinder(Path.GetFullPath(OutDir));
        }

        static void Build(string arch, string appName)
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("Blossom Rally", "Open the game scene once first (Blossom Rally > Open Game Scene).", "OK");
                return;
            }
            PlayerSettings.productName = "Blossom Rally";
            PlayerSettings.companyName = "Blossom Rally";
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.blossomrally.game");
#else
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "com.blossomrally.game");
#endif
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            // architecture via the string API (works on every supported Unity version)
            EditorUserBuildSettings.SetPlatformSettings("Standalone", "OSXUniversal", "Architecture", arch);

            Directory.CreateDirectory(OutDir);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(OutDir, appName),
                target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            var sum = report.summary;
            if (sum.result == BuildResult.Succeeded)
            {
                Debug.Log("[BlossomRally] built " + sum.outputPath + " (" + (sum.totalSize / (1024 * 1024)) + " MB) in " + sum.totalTime);
                EditorUtility.RevealInFinder(Path.GetFullPath(sum.outputPath));
            }
            else
            {
                EditorUtility.DisplayDialog("Blossom Rally", "Build " + sum.result + ". See the Console for details." +
                    "\n\nIf it says the platform is not installed, add \"Mac Build Support\" in Unity Hub > Installs > Add modules.", "OK");
            }
        }
    }
}
