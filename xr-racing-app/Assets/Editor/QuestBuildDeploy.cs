using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class QuestBuildDeploy
{
    private const string BuildDir = "Builds/Android";
    private const string ApkPrefix = "xr-racing-app";

    [MenuItem("CI/CD/Build APK")]
    public static void BuildApk()
    {
        Build();
    }

    [MenuItem("CI/CD/Build and Deploy to Quest")]
    public static void BuildAndDeploy()
    {
        string apkPath = Build();
        if (apkPath != null)
        {
            Deploy(apkPath);
        }
    }

    [MenuItem("CI/CD/Deploy Last Build")]
    public static void DeployLastBuild()
    {
        string apkPath = FindLatestApk();
        if (apkPath == null)
        {
            Debug.LogError($"No build found in {BuildDir}. Run 'CI/CD/Build APK' first.");
            return;
        }

        Deploy(apkPath);
    }

    private static string Build()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("No enabled scenes in Build Settings. Add scenes via File > Build Settings before building.");
            return null;
        }

        Directory.CreateDirectory(BuildDir);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string apkPath = Path.Combine(BuildDir, $"{ApkPrefix}_{timestamp}.apk");

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"Build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            return null;
        }

        Debug.Log($"Build succeeded: {apkPath} ({report.summary.totalSize / (1024 * 1024)} MB)");
        return apkPath;
    }

    private static string FindLatestApk()
    {
        if (!Directory.Exists(BuildDir))
        {
            return null;
        }

        return Directory.GetFiles(BuildDir, "*.apk")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static void Deploy(string apkPath)
    {
        string adb = FindAdb();
        if (adb == null)
        {
            Debug.LogError("Could not locate adb. Set ANDROID_HOME/ANDROID_SDK_ROOT, or install Android platform-tools.");
            return;
        }

        string packageName = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);

        Debug.Log($"Installing {apkPath} on connected Quest...");
        if (!RunAdb(adb, $"install -r \"{apkPath}\"", out string installOutput))
        {
            Debug.LogError($"adb install failed:\n{installOutput}");
            return;
        }
        Debug.Log($"Install output:\n{installOutput}");

        RunAdb(adb, $"shell monkey -p {packageName} -c android.intent.category.LAUNCHER 1", out string launchOutput);
        Debug.Log($"Launched {packageName} on Quest.");
    }

    private static string FindAdb()
    {
        string exeName = Application.platform == RuntimePlatform.WindowsEditor ? "adb.exe" : "adb";

        // Unity's own configured Android SDK path (Preferences > External Tools) is
        // authoritative — it's what Unity itself uses to build/deploy Android, and
        // covers the common case of an SDK installed via Unity Hub with no separate
        // Android Studio install.
        string unitySdkRoot = UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath;
        if (!string.IsNullOrEmpty(unitySdkRoot))
        {
            string candidate = Path.Combine(unitySdkRoot, "platform-tools", exeName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // Fall back to the SDK bundled with this Unity install (present when Android
        // Build Support was installed without pointing Unity at an external SDK).
        string bundledSdkRoot = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "SDK");
        string bundledCandidate = Path.Combine(bundledSdkRoot, "platform-tools", exeName);
        if (File.Exists(bundledCandidate))
        {
            return bundledCandidate;
        }

        string envHome = Environment.GetEnvironmentVariable("ANDROID_HOME");
        if (string.IsNullOrEmpty(envHome))
        {
            envHome = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
        }

        if (!string.IsNullOrEmpty(envHome))
        {
            string candidate = Path.Combine(envHome, "platform-tools", exeName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] defaultPaths =
        {
            Path.Combine(home, "Library/Android/sdk/platform-tools", exeName), // macOS
            Path.Combine(home, "AppData/Local/Android/Sdk/platform-tools", exeName), // Windows
            Path.Combine(home, "Android/Sdk/platform-tools", exeName), // Linux
        };

        foreach (string candidate in defaultPaths)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool RunAdb(string adbPath, string arguments, out string output)
    {
        var psi = new ProcessStartInfo(adbPath, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(psi))
        {
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            output = stdout + stderr;
            return process.ExitCode == 0;
        }
    }
}
