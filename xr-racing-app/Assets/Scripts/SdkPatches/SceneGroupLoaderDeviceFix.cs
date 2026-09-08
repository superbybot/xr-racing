// Root cause (confirmed via on-device logging): Meta's SampleSceneGroup.cs conditionally
// implements ISerializationCallbackReceiver and declares its editable scene list only under
// #if UNITY_EDITOR. That mismatched field layout between Editor and Player builds corrupts
// Resources.LoadAll<SampleSceneGroup>("") on Android/IL2CPP — it doesn't throw, but the
// returned object's scene list silently deserializes as empty (SceneCount=0), even though the
// same data is present and correct in the source .asset file. SceneGroupLoader.BuildSceneGroups()
// reads that same broken call, so it builds zero scene groups/tiles on device.
//
// Since the corrupted data can't be trusted at runtime, this hardcodes the known-good
// DisplayName -> SceneName mapping (copied from ISDKExampleScenes.asset) and uses it to
// re-enable each tile's Toggle/Image and hide its "missing" overlay after SceneGroupLoader
// runs, without touching Meta's package source.
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

        private static readonly Dictionary<string, string> KnownSceneNamesByDisplayName = new Dictionary<string, string>
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

                    if (!KnownSceneNamesByDisplayName.TryGetValue(toggle.gameObject.name, out var sceneName))
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
