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
        private static Text _debugOverlayText;
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

        private void EnsureDebugOverlay(SceneGroupLoader loader)
        {
            if (_debugOverlayText != null)
            {
                return;
            }

            var canvas = loader.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var overlayGO = new GameObject("SceneGroupLoaderDeviceFixOverlay");
            overlayGO.transform.SetParent(canvas.transform, false);

            var imageGO = new GameObject("SceneGroupLoaderDeviceFixOverlayImage", typeof(RectTransform), typeof(Image));
            imageGO.transform.SetParent(overlayGO.transform, false);
            var imageRT = imageGO.GetComponent<RectTransform>();
            imageRT.anchorMin = new Vector2(0, 1);
            imageRT.anchorMax = new Vector2(0, 1);
            imageRT.pivot = new Vector2(0, 1);
            imageRT.anchoredPosition = new Vector2(20, -20);
            imageRT.sizeDelta = new Vector2(700, 260);
            var image = imageGO.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.7f);

            var textGO = new GameObject("SceneGroupLoaderDeviceFixOverlayText", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(imageGO.transform, false);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(10, 10);
            textRT.offsetMax = new Vector2(-10, -10);
            textRT.sizeDelta = Vector2.zero;
            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            _debugOverlayText = text;
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

            if (loaders.Length > 0)
            {
                EnsureDebugOverlay(loaders[0]);
                if (_debugOverlayText != null)
                {
                    _debugOverlayText.text = $"[SceneGroupLoaderDeviceFix] loaders={loaders.Length} toggles={toggleCount} sceneEntries={scenesByDisplayName.Count} buildScenes={buildSceneNames.Count} tileViewTypeFound={TileViewType != null} fixedThisPass={fixedCount} imageFixedThisPass={imageFixedCount}";
                }
            }

            Debug.Log($"[SceneGroupLoaderDeviceFix] loaders={loaders.Length} toggles={toggleCount} sceneEntries={scenesByDisplayName.Count} buildScenes={buildSceneNames.Count} tileViewTypeFound={TileViewType != null} fixedThisPass={fixedCount} imageFixedThisPass={imageFixedCount}");
        }
    }
}
