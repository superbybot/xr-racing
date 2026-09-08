// Oculus.Interaction.Samples.SceneGroupLoader (Meta XR Interaction SDK; used by the
// ISDKExampleMenu prefab present in every Interaction SDK sample scene) checks whether a
// menu tile's scene exists by calling SceneUtility.GetBuildIndexByScenePath(sceneInfo.SceneName)
// on device. sceneInfo.SceneName is only the bare scene name (SampleSceneGroup.cs sets it from
// SceneAsset.name), not a path, so GetBuildIndexByScenePath — which matches on full asset path —
// always returns -1 on Quest even when the scene really is in the build. Every tile ends up
// disabled and marked missing. This runs after SceneGroupLoader builds the menu and re-checks
// each tile against the scene names actually present in this build, restoring the ones that
// are really there, without touching Meta's package source.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            var scenesByDisplayName = Resources.LoadAll<SampleSceneGroup>("")
                .Where(g => g.GroupEnabled && g.SceneCount > 0)
                .SelectMany(g => g.GetScenes())
                .GroupBy(s => s.DisplayName)
                .Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => g.First());

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

                    if (!scenesByDisplayName.TryGetValue(toggle.gameObject.name, out var sceneInfo))
                    {
                        continue;
                    }

                    if (!buildSceneNames.Contains(sceneInfo.SceneName))
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

            Debug.Log($"[SceneGroupLoaderDeviceFix] loaders={loaders.Length} toggles={toggleCount} sceneEntries={scenesByDisplayName.Count} buildScenes={buildSceneNames.Count} tileViewTypeFound={TileViewType != null} fixedThisPass={fixedCount} imageFixedThisPass={imageFixedCount}");
        }
    }
}
