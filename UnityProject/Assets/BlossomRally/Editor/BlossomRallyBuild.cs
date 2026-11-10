// BlossomRallyBuild.cs - one-click tester builds.
//
// Menu "Blossom Rally":
//   Build Tester Release (Mac DMG + Windows EXE)   both at once, into Builds/Release-<version>/
//   Build macOS DMG (Universal)                     Blossom Rally.app packed into a drag-to-Applications .dmg
//   Build Windows EXE (64-bit)                      BlossomRally.exe + data folder, zipped for download
//   Open Builds Folder
//
// Needs, in Unity Hub > Installs > (your Unity) > Add modules:
//   * Mac Build Support (Mono)       - normally already installed with a Mac editor
//   * Windows Build Support (Mono)   - needed for the Windows .exe (builds fine from a Mac)
// The .dmg is created with macOS's own hdiutil, so the DMG step needs the editor to run on a Mac.
//
// Headless / CI entry points (used by .github/workflows/build-game.yml):
//   -executeMethod BlossomRally.EditorTools.BlossomRallyBuild.CIBuildMac      -> Builds/ci/mac/Blossom Rally.app
//   -executeMethod BlossomRally.EditorTools.BlossomRallyBuild.CIBuildWindows  -> Builds/ci/win/BlossomRally.exe
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BlossomRally.EditorTools
{
    public static class BlossomRallyBuild
    {
        const string ScenePath = "Assets/BlossomRally/Scenes/BlossomValley.unity";
        const string OutDir = "Builds";
        const string AppName = "Blossom Rally";
        const string ExeName = "BlossomRally";

        static string ReleaseDir { get { return Path.Combine(OutDir, "Release-" + BuildInfo.Version); } }

        // ------------------------------------------------------------------ menu
        [MenuItem("Blossom Rally/Build Tester Release (Mac DMG + Windows EXE)", false, 19)]
        static void BuildRelease() { Guarded("tester release", BuildReleaseInner); }

        static void BuildReleaseInner()
        {
            string dmg = BuildMacDmg(false);
            string win = BuildWindowsZip(false);
            string msg = "Tester release " + BuildInfo.Version + ":\n\n" +
                         "macOS:   " + (dmg ?? "FAILED (see Console)") + "\n" +
                         "Windows: " + (win ?? "FAILED (see Console)");
            Debug.Log("[BlossomRally] " + msg);
            EditorUtility.DisplayDialog("Blossom Rally", msg, "OK");
            Directory.CreateDirectory(ReleaseDir);
            EditorUtility.RevealInFinder(Path.GetFullPath(ReleaseDir));
        }

        [MenuItem("Blossom Rally/Build macOS DMG (Universal Intel + Apple Silicon)", false, 20)]
        static void MenuMac() { Guarded("macOS DMG", () => BuildMacDmg(true)); }

        [MenuItem("Blossom Rally/Build Windows EXE (64-bit, zipped)", false, 21)]
        static void MenuWindows() { Guarded("Windows EXE", () => BuildWindowsZip(true)); }

        /// <summary>Never fail silently: refuse in Play mode, show progress, catch and show any exception, keep a log.</summary>
        static void Guarded(string what, Action body)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Blossom Rally", "Stop Play mode first (click the Play button so it is not blue), then build again.", "OK");
                return;
            }
            if (EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("Blossom Rally", "Unity is still compiling scripts. Wait until the spinner at the bottom-right stops, then build again.", "OK");
                return;
            }
            Directory.CreateDirectory(OutDir);
            Log("=== build " + what + " " + BuildInfo.Version + " on Unity " + Application.unityVersion + " ===");
            try
            {
                EditorUtility.DisplayProgressBar("Blossom Rally", "Preparing the " + what + " build...", 0.05f);
                body();
            }
            catch (Exception e)
            {
                Log("EXCEPTION " + e);
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Blossom Rally - build problem",
                    "The " + what + " build stopped with this error:\n\n" + e.GetType().Name + ": " + e.Message +
                    "\n\nPlease send a screenshot of this and of the Console (Window > General > Console).", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void Log(string line)
        {
            try { File.AppendAllText(Path.Combine(OutDir, "build-log.txt"), DateTime.Now.ToString("HH:mm:ss") + "  " + line + "\n"); }
            catch (Exception) { }
        }

        [MenuItem("Blossom Rally/Open Builds Folder", false, 22)]
        static void OpenFolder()
        {
            Directory.CreateDirectory(OutDir);
            EditorUtility.RevealInFinder(Path.GetFullPath(OutDir));
        }

        // ------------------------------------------------------------------ macOS
        /// <summary>Builds the universal .app and packs it into a .dmg. Returns the .dmg path (or the .app path off-Mac), null on failure.</summary>
        static string BuildMacDmg(bool interactive)
        {
            if (!Prepare(interactive)) return null;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
            {
                Fail(interactive, "Mac Build Support is not installed.\n\nUnity Hub > Installs > your Unity > Add modules > Mac Build Support (Mono).");
                return null;
            }
            SetMacUniversal();
            string stage = Path.Combine(OutDir, "stage-mac");
            if (Directory.Exists(stage)) Directory.Delete(stage, true);
            Directory.CreateDirectory(stage);
            string app = Path.Combine(stage, AppName + ".app");
            if (!RunBuild(BuildTarget.StandaloneOSX, app, interactive)) return null;

            Directory.CreateDirectory(ReleaseDir);
            string dmg = Path.Combine(ReleaseDir, "BlossomRally-" + BuildInfo.Version + "-mac.dmg");
            if (Application.platform != RuntimePlatform.OSXEditor)
            {
                Debug.LogWarning("[BlossomRally] .app built at " + app + " - the .dmg step needs macOS (hdiutil). Zip the .app instead or run this on a Mac.");
                return Path.GetFullPath(app);
            }
            // ad-hoc sign so Apple Silicon runs it after download, then add the drag-to-Applications layout
            Run("/usr/bin/codesign", "--force --deep --sign - \"" + Path.GetFullPath(app) + "\"");
            Run("/bin/ln", "-sf /Applications \"" + Path.GetFullPath(Path.Combine(stage, "Applications")) + "\"");
            File.WriteAllText(Path.Combine(stage, "HOW TO OPEN.txt"), MacReadme());
            if (File.Exists(dmg)) File.Delete(dmg);
            bool ok = Run("/usr/bin/hdiutil", "create -volname \"" + AppName + " " + BuildInfo.Version + "\" -srcfolder \"" +
                          Path.GetFullPath(stage) + "\" -ov -format UDZO \"" + Path.GetFullPath(dmg) + "\"");
            if (!ok || !File.Exists(dmg)) { Fail(interactive, "hdiutil could not create the .dmg - see the Console."); return null; }
            Debug.Log("[BlossomRally] macOS DMG: " + Path.GetFullPath(dmg) + " (" + (new FileInfo(dmg).Length / (1024 * 1024)) + " MB)");
            if (interactive) EditorUtility.RevealInFinder(Path.GetFullPath(dmg));
            return Path.GetFullPath(dmg);
        }

        // ------------------------------------------------------------------ Windows
        /// <summary>Builds BlossomRally.exe (+ data folder) and zips it for download. Returns the .zip path, null on failure.</summary>
        static string BuildWindowsZip(bool interactive)
        {
            if (!Prepare(interactive)) return null;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                Fail(interactive, "Windows Build Support is not installed.\n\nUnity Hub > Installs > your Unity > Add modules > " +
                                  "Windows Build Support (Mono). It builds Windows games from your Mac. Restart Unity afterwards.");
                return null;
            }
            string stage = Path.Combine(OutDir, "stage-win", "BlossomRally-" + BuildInfo.Version + "-windows");
            if (Directory.Exists(Path.GetDirectoryName(stage))) Directory.Delete(Path.GetDirectoryName(stage), true);
            Directory.CreateDirectory(stage);
            string exe = Path.Combine(stage, ExeName + ".exe");
            if (!RunBuild(BuildTarget.StandaloneWindows64, exe, interactive)) return null;
            // debug symbols are not needed by testers
            foreach (var d in Directory.GetDirectories(stage, "*BurstDebugInformation_DoNotShip")) Directory.Delete(d, true);
            File.WriteAllText(Path.Combine(stage, "HOW TO PLAY.txt"), WindowsReadme());

            Directory.CreateDirectory(ReleaseDir);
            string zip = Path.Combine(ReleaseDir, "BlossomRally-" + BuildInfo.Version + "-windows.zip");
            if (File.Exists(zip)) File.Delete(zip);
            ZipFile.CreateFromDirectory(stage, zip, System.IO.Compression.CompressionLevel.Optimal, true);
            Debug.Log("[BlossomRally] Windows ZIP: " + Path.GetFullPath(zip) + " (" + (new FileInfo(zip).Length / (1024 * 1024)) + " MB)");
            if (interactive) EditorUtility.RevealInFinder(Path.GetFullPath(zip));
            return Path.GetFullPath(zip);
        }

        // ------------------------------------------------------------------ CI (batch mode)
        public static void CIBuildMac()
        {
            if (!Prepare(false)) EditorApplication.Exit(1);
            SetMacUniversal();
            bool ok = RunBuild(BuildTarget.StandaloneOSX, Path.Combine(OutDir, "ci", "mac", AppName + ".app"), false);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static void CIBuildWindows()
        {
            if (!Prepare(false)) EditorApplication.Exit(1);
            bool ok = RunBuild(BuildTarget.StandaloneWindows64, Path.Combine(OutDir, "ci", "win", ExeName + ".exe"), false);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        // ------------------------------------------------------------------ shared
        static void SetMacUniversal()
        {
            try { EditorUserBuildSettings.SetPlatformSettings("Standalone", "OSXUniversal", "Architecture", "x64ARM64"); }
            catch (Exception e) { Log("architecture setting skipped: " + e.Message); }   // falls back to the editor default
        }

        static bool Prepare(bool interactive)
        {
            BlossomRallySetup.EnsureLegacyInput();
            BlossomRallySetup.EnsureScene();
            if (!File.Exists(ScenePath))
            {
                Fail(interactive, "The game scene could not be created - open it once with Blossom Rally > Open Game Scene.");
                return false;
            }
            PlayerSettings.productName = AppName;
            PlayerSettings.companyName = "Blossom Rally";
            PlayerSettings.bundleVersion = BuildInfo.Version;
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
            return true;
        }

        static bool RunBuild(BuildTarget target, string path, bool interactive)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };
            Log("building " + target + " -> " + Path.GetFullPath(path));
            EditorUtility.DisplayProgressBar("Blossom Rally", "Building " + (target == BuildTarget.StandaloneOSX ? "macOS" : "Windows") + " (several minutes)...", 0.3f);
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            Log("result " + target + ": " + report.summary.result + ", errors " + report.summary.totalErrors);
            var sum = report.summary;
            if (sum.result == BuildResult.Succeeded)
            {
                Debug.Log("[BlossomRally] built " + target + " -> " + sum.outputPath + " (" + (sum.totalSize / (1024 * 1024)) + " MB) in " + sum.totalTime);
                return true;
            }
            Fail(interactive, target + " build " + sum.result + ". See the Console for details.");
            return false;
        }

        static bool Run(string file, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(file, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                using (var p = Process.Start(psi))
                {
                    string o = p.StandardOutput.ReadToEnd(), e = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    if (p.ExitCode != 0) Debug.LogWarning("[BlossomRally] " + Path.GetFileName(file) + " exit " + p.ExitCode + ": " + e + o);
                    return p.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BlossomRally] could not run " + file + ": " + ex.Message);
                return false;
            }
        }

        static void Fail(bool interactive, string msg)
        {
            Debug.LogError("[BlossomRally] " + msg);
            Log("FAIL " + msg);
            if (interactive && !Application.isBatchMode) EditorUtility.DisplayDialog("Blossom Rally", msg, "OK");
        }

        static string MacReadme()
        {
            return "Blossom Rally " + BuildInfo.Label + "\n\n" +
                   "1. Drag \"Blossom Rally\" onto the Applications folder.\n" +
                   "2. The first time: right-click (or Control-click) Blossom Rally in Applications and choose Open, then Open again.\n" +
                   "   On macOS 15+: if it says it can't be opened, go to System Settings > Privacy & Security, scroll down and click \"Open Anyway\".\n" +
                   "   (This test build is not notarized by Apple yet - that is why macOS asks.)\n\n" +
                   "Controls: W/S drive, A/D steer, SPACE handbrake/drift, ESC menu. Gamepads work too.\n" +
                   "F1 shows the frame rate, O takes photos. Thanks for testing!\n";
        }

        static string WindowsReadme()
        {
            return "Blossom Rally " + BuildInfo.Label + "\n\n" +
                   "1. Unzip the whole folder somewhere (e.g. Desktop). Keep BlossomRally.exe next to its BlossomRally_Data folder.\n" +
                   "2. Double-click BlossomRally.exe.\n" +
                   "   If Windows SmartScreen says \"Windows protected your PC\": click \"More info\" then \"Run anyway\".\n" +
                   "   (This test build is not code-signed yet - that is why Windows asks.)\n\n" +
                   "Controls: W/S drive, A/D steer, SPACE handbrake/drift, ESC menu. Xbox / PlayStation controllers work too.\n" +
                   "F1 shows the frame rate, O takes photos. Thanks for testing!\n";
        }
    }
}
