// Root cause (confirmed via on-device logging): Meta's SampleSceneGroup.cs conditionally
// implements ISerializationCallbackReceiver and declares its editable scene list only under
// #if UNITY_EDITOR. That mismatched field layout between Editor and Player builds corrupts
// Resources.LoadAll<SampleSceneGroup>("") on Android/IL2CPP — it doesn't throw, but the
// returned object's scene list silently deserializes as empty (SceneCount=0), even though the
// same data is present and correct in the source .asset file.
//
// This breaks two separate things, both fixed here using the same hardcoded
// DisplayName -> SceneName mapping (copied from ISDKExampleScenes.asset):
//
// 1. SceneGroupLoader.BuildSceneGroups() filters out an entire group whose SceneCount is 0
//    before creating any tiles for it at all — so with the corrupted data, zero tiles ever
//    get created, not just disabled ones. SampleSceneGroupDataFix reflects into the
//    corrupted object's private _sceneInfos field and repopulates it, via a
//    RuntimeInitializeOnLoadMethod(BeforeSceneLoad) hook that runs before
//    SceneGroupLoader.Start() does, so BuildSceneGroups() sees a correct scene list and
//    actually creates the tiles.
// 2. Even with tiles created, SceneGroupLoader's on-device CheckSceneExists() compares a
//    bare scene name against SceneUtility.GetBuildIndexByScenePath (which expects a full
//    asset path), so it always returns false and every tile is created disabled.
//    SceneGroupLoaderDeviceFixRunner re-enables each tile's Toggle/Image and hides its
//    "missing" overlay after SceneGroupLoader runs.
//
// Both stages are project-side only; neither touches Meta's package source.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Oculus.Interaction.Samples;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace XRRacing.SdkPatches
{
    internal static class SampleSceneGroupDataFix
    {
        internal static readonly Dictionary<string, string> KnownSceneNamesByDisplayName = new Dictionary<string, string>
        {
            { "Comprehensive", "ComprehensiveRigExample" },
            { "Simultaneous Hands & Controllers", "ConcurrentHandsControllersExamples" },
            { "UI Set", "UISetExamples" },
            { "Poke", "PokeExamples" },
            { "Ray", "RayExamples" },
            { "Distance Grab", "DistanceGrabExamples" },
            { "Hand Grab", "HandGrabExamples" },
            { "Touch Grab", "TouchGrabExamples" },
            { "Hand Grab Use", "HandGrabUseExamples" },
            { "Snap", "SnapExamples" },
            { "Transformers", "TransformerExamples" },
            { "Panel With Manipulators", "PanelWithManipulators" },
            { "Gestures", "GestureExamples" },
            { "Hand Pose", "PoseExamples" },
            { "Locomotion", "LocomotionExamples" },
            { "Body Pose", "BodyPoseDetectionExamples" },
        };

        private static readonly System.Type SceneInfoType =
            typeof(SampleSceneGroup).GetNestedType("SceneInfo", BindingFlags.NonPublic);
        private static readonly FieldInfo SceneInfosField =
            typeof(SampleSceneGroup).GetField("_sceneInfos", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo SceneInfoDisplayNameField = SceneInfoType?.GetField("DisplayName");
        private static readonly FieldInfo SceneInfoSceneNameField = SceneInfoType?.GetField("SceneName");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // BeforeSceneLoad only fires once, before the app's very first scene — later
            // scene switches give SceneGroupLoader a fresh (re-corrupted) SampleSceneGroup
            // load, so the patch has to reapply before each one, not just the first.
            SceneManager.sceneLoaded += (_, _) => PatchCorruptedSceneGroups();
            PatchCorruptedSceneGroups();
        }

        private static void PatchCorruptedSceneGroups()
        {
            if (SceneInfoType == null || SceneInfosField == null)
            {
                return;
            }

            var groups = Resources.LoadAll<SampleSceneGroup>("");
            int patchedCount = 0;

            foreach (var group in groups)
            {
                if (group.SceneCount > 0)
                {
                    continue;
                }

                var array = System.Array.CreateInstance(SceneInfoType, KnownSceneNamesByDisplayName.Count);
                int index = 0;
                foreach (var kvp in KnownSceneNamesByDisplayName)
                {
                    var element = System.Activator.CreateInstance(SceneInfoType);
                    if (SceneInfoDisplayNameField != null && SceneInfoSceneNameField != null)
                    {
                        SceneInfoDisplayNameField.SetValue(element, kvp.Key);
                        SceneInfoSceneNameField.SetValue(element, kvp.Value);
                    }
                    array.SetValue(element, index++);
                }

                SceneInfosField.SetValue(group, array);
                patchedCount++;
            }

            Debug.Log($"[SceneGroupLoaderDeviceFix] PatchCorruptedSceneGroups patched {patchedCount} group(s)");
        }
    }

    internal static class SceneGroupLoaderDeviceFixBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var host = new GameObject("SceneGroupLoaderDeviceFixHost");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<SceneGroupLoaderDeviceFixRunner>();
        }
    }

    internal class SceneGroupLoaderDeviceFixRunner : MonoBehaviour
    {
        private static readonly System.Type TileViewType =
            typeof(SceneGroupLoader).GetNestedType("SceneTileView", BindingFlags.NonPublic);
        private static readonly FieldInfo ImageField = TileViewType?.GetField("Image");
        private static readonly FieldInfo SceneMissingOverlayField = TileViewType?.GetField("SceneMissingOverlay");

        private void Awake()
        {
            StartCoroutine(FixLoop());
        }

        private IEnumerator FixLoop()
        {
            var wait = new WaitForSeconds(0.5f);
            while (true)
            {
                Fix();
                yield return wait;
            }
        }

        private void Fix()
        {
            var buildSceneNames = new HashSet<string>();
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                buildSceneNames.Add(Path.GetFileNameWithoutExtension(path));
            }

            var loaders = Object.FindObjectsByType<SceneGroupLoader>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int toggleCount = 0;
            int fixedCount = 0;
            int imageFixedCount = 0;

            foreach (var loader in loaders)
            {
                foreach (var toggle in loader.GetComponentsInChildren<Toggle>(true))
                {
                    toggleCount++;

                    if (toggle.enabled)
                    {
                        continue;
                    }

                    if (!SampleSceneGroupDataFix.KnownSceneNamesByDisplayName.TryGetValue(toggle.gameObject.name, out var sceneName))
                    {
                        continue;
                    }

                    if (!buildSceneNames.Contains(sceneName))
                    {
                        continue;
                    }

                    toggle.enabled = true;
                    fixedCount++;

                    if (TileViewType == null)
                    {
                        continue;
                    }

                    var tileView = toggle.GetComponent(TileViewType);
                    if (tileView == null)
                    {
                        continue;
                    }

                    if (ImageField?.GetValue(tileView) is Image image)
                    {
                        image.enabled = true;
                        imageFixedCount++;
                    }

                    if (SceneMissingOverlayField?.GetValue(tileView) is Image overlay)
                    {
                        overlay.gameObject.SetActive(false);
                    }
                }
            }

            Debug.Log($"[SceneGroupLoaderDeviceFix] loaders={loaders.Length} toggles={toggleCount} buildScenes={buildSceneNames.Count} tileViewTypeFound={TileViewType != null} fixedThisPass={fixedCount} imageFixedThisPass={imageFixedCount}");
        }
    }
}
