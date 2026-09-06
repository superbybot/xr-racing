using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class QuestBuildDeploy
{
    private const string BuildDir = "Builds/Android";
    private const string ApkPrefix = "xr-racing-app";

    [MenuItem("Build Commands/Build APK")]
    public static void BuildApk()
    {
        if (Application.isBatchMode)
        {
            string apkPath = BuildWithProfile("App", ApkPrefix);
            if (apkPath == null)
            {
                EditorApplication.Exit(1);
            }
        }
        else
        {
            BuildWithProfile("App", ApkPrefix);
        }
    }

    [MenuItem("Build Commands/Build and Deploy to Quest")]
    public static void BuildAndDeploy()
    {
        if (Application.isBatchMode)
        {
            string apkPath = BuildWithProfile("App", ApkPrefix);
            if (apkPath == null)
            {
                EditorApplication.Exit(1);
                return;
            }

            bool deployResult = Deploy(apkPath, PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
            if (!deployResult)
            {
                EditorApplication.Exit(1);
            }
        }
        else
        {
            string apkPath = BuildWithProfile("App", ApkPrefix);
            if (apkPath != null)
            {
                Deploy(apkPath, PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
            }
        }
    }

    [MenuItem("Build Commands/Build and Deploy Interaction SDK Samples")]
    public static void BuildAndDeployInteractionSdkSamples()
    {
        BuildAndDeployWithProfile("InteractionSdkSamples", "interaction-sdk-samples", ".interactionsamples");
    }

    [MenuItem("Build Commands/Build and Deploy Core SDK Samples")]
    public static void BuildAndDeployCoreSdkSamples()
    {
        BuildAndDeployWithProfile("CoreSdkSamples", "core-sdk-samples", ".coresdksamples");
    }

    private static void BuildAndDeployWithProfile(string profileName, string apkPrefix, string appIdSuffix)
    {
        string originalAppId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        string packageName = originalAppId + appIdSuffix;

        try
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, packageName);

            if (Application.isBatchMode)
            {
                string apkPath = BuildWithProfile(profileName, apkPrefix);
                if (apkPath == null)
                {
                    EditorApplication.Exit(1);
                    return;
                }

                bool profileDeployResult = Deploy(apkPath, packageName);
                if (!profileDeployResult)
                {
                    EditorApplication.Exit(1);
                }
            }
            else
            {
                string apkPath = BuildWithProfile(profileName, apkPrefix);
                if (apkPath != null)
                {
                    Deploy(apkPath, packageName);
                }
            }
        }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, originalAppId);
        }
    }

    [MenuItem("Build Commands/Deploy Last Build")]
    public static void DeployLastBuild()
    {
        string apkPath = FindLatestApk();
        if (apkPath == null)
        {
            Debug.LogError($"No build found in {BuildDir}. Run 'Build Commands/Build APK' first.");
            return;
        }

        Deploy(apkPath, PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
    }

    private static string BuildWithProfile(string profileName, string apkPrefix)
    {
        BuildProfile profile = BuildProfile.GetAllBuildProfiles()
            .FirstOrDefault(p => p.name == profileName);

        if (profile == null)
        {
            Debug.LogError($"Build profile '{profileName}' not found. Run 'Build Commands/Create Sample Build Profiles' first.");
            return null;
        }

        Directory.CreateDirectory(BuildDir);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string apkPath = Path.Combine(BuildDir, $"{apkPrefix}_{timestamp}.apk");

        var profileOptions = new BuildPlayerWithProfileOptions
        {
            buildProfile = profile,
            locationPathName = apkPath,
            options = BuildOptions.None
        };

        BuildReport profileReport = BuildPipeline.BuildPlayer(profileOptions);
        if (profileReport.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"Build failed: {profileReport.summary.result} ({profileReport.summary.totalErrors} errors)");
            return null;
        }

        Debug.Log($"Build succeeded: {apkPath} ({profileReport.summary.totalSize / (1024 * 1024)} MB)");
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

    private static bool Deploy(string apkPath, string packageName)
    {
        string adb = FindAdb();
        if (adb == null)
        {
            Debug.LogError("Could not locate adb. Set ANDROID_HOME/ANDROID_SDK_ROOT, or install Android platform-tools.");
            return false;
        }

        Debug.Log($"Installing {apkPath} on connected Quest...");
        if (!RunAdb(adb, $"install -r \"{apkPath}\"", out string installOutput))
        {
            Debug.LogError($"adb install failed:\n{installOutput}");
            return false;
        }
        Debug.Log($"Install output:\n{installOutput}");

        RunAdb(adb, $"shell monkey -p {packageName} -c android.intent.category.LAUNCHER 1", out string launchOutput);
        Debug.Log($"Launched {packageName} on Quest.");
        return true;
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
