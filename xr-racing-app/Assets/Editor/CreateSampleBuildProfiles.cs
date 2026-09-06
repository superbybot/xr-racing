using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

public static class CreateSampleBuildProfiles
{
    [MenuItem("Build Commands/Create Sample Build Profiles")]
    public static void Create()
    {
        InstalledPlatformInfo androidPlatform = BuildProfile.GetInstalledPlatformModules()
            .FirstOrDefault(p => p.displayName.Contains("Android"));

        CreateOrUpdateProfile("InteractionSdkSamples", androidPlatform.platformGuid,
            FindScenesUnderFoldersStartingWith("Meta XR Interaction"));

        CreateOrUpdateProfile("CoreSdkSamples", androidPlatform.platformGuid,
            FindScenesUnderFoldersStartingWith("Meta XR Core SDK"));
    }

    private static string[] FindScenesUnderFoldersStartingWith(string folderNamePrefix)
    {
        const string samplesRoot = "Assets/Samples";

        return Directory.GetDirectories(samplesRoot)
            .Where(dir => Path.GetFileName(dir).StartsWith(folderNamePrefix))
            .SelectMany(dir => Directory.GetFiles(dir, "*.unity", SearchOption.AllDirectories))
            .Select(p => p.Replace('\\', '/'))
            .OrderBy(p => p)
            .ToArray();
    }

    private static void CreateOrUpdateProfile(string profileName, GUID platformGuid, string[] scenePaths)
    {
        BuildProfile profile = BuildProfile.GetAllBuildProfiles()
            .FirstOrDefault(p => p.name == profileName);

        if (profile == null)
        {
            profile = BuildProfile.CreateBuildProfile(platformGuid, profileName, null);
        }

        profile.overrideGlobalScenes = true;
        profile.scenes = scenePaths
            .Select(path => new EditorBuildSettingsScene(path, true))
            .ToArray();

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        Debug.Log($"Build profile '{profileName}' now has {scenePaths.Length} scenes.");
    }
}
