using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using XrRacing.Gameplay.UI;

namespace XrRacing.Editor.UI
{
    /// <summary>
    /// Makes the driver menu draw on top of the kart and track: adds the MenuOverlay layer, leaves it out of every
    /// URP renderer's normal opaque/transparent drawing, and adds a Render Objects feature that draws that layer
    /// after everything else with depth testing off. Safe to re-run. Called by the menu builder.
    /// </summary>
    public static class MenuOverlayRenderingSetup
    {
        private const string FeatureName = "Menu Overlay (draw on top)";

        [MenuItem("XR Racing/Set Up Menu Draw-On-Top")]
        public static void Run()
        {
            int layer = EnsureLayer();
            if (layer < 0)
            {
                Debug.LogError($"[MenuOverlayRenderingSetup] No free layer for '{OverlayLayer.Name}'.");
                return;
            }

            foreach (UniversalRendererData renderer in RenderersInUse())
            {
                SetUp(renderer, layer);
            }

            AssetDatabase.SaveAssets();
        }

        private static int EnsureLayer()
        {
            int existing = LayerMask.NameToLayer(OverlayLayer.Name);
            if (existing >= 0)
            {
                return existing;
            }

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++) // 0-7 are Unity's built-in layers
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = OverlayLayer.Name;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            }

            return -1;
        }

        // The URP renderers of every quality level (Android uses Mobile, the Editor uses PC) plus the default pipeline.
        private static IEnumerable<UniversalRendererData> RenderersInUse()
        {
            var pipelines = new HashSet<RenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                pipelines.Add(GraphicsSettings.defaultRenderPipeline);
            }

            for (int i = 0; i < QualitySettings.count; i++)
            {
                RenderPipelineAsset pipeline = QualitySettings.GetRenderPipelineAssetAt(i);
                if (pipeline != null)
                {
                    pipelines.Add(pipeline);
                }
            }

            var renderers = new HashSet<UniversalRendererData>();
            foreach (RenderPipelineAsset pipeline in pipelines)
            {
                var serialized = new SerializedObject(pipeline);
                SerializedProperty list = serialized.FindProperty("m_RendererDataList");
                for (int i = 0; list != null && i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue is UniversalRendererData data)
                    {
                        renderers.Add(data);
                    }
                }
            }

            return renderers;
        }

        private static void SetUp(UniversalRendererData renderer, int layer)
        {
            int bit = 1 << layer;
            renderer.opaqueLayerMask &= ~bit;
            renderer.transparentLayerMask &= ~bit;

            RenderObjects feature = renderer.rendererFeatures.Find(f => f != null && f.name == FeatureName) as RenderObjects;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<RenderObjects>();
                feature.name = FeatureName;
                AssetDatabase.AddObjectToAsset(feature, renderer);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

                var serialized = new SerializedObject(renderer);
                SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
                SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            RenderObjects.RenderObjectsSettings settings = feature.settings;
            settings.passTag = FeatureName;
            settings.Event = RenderPassEvent.AfterRenderingTransparents;
            settings.filterSettings.RenderQueueType = RenderQueueType.Transparent;
            settings.filterSettings.LayerMask = bit;
            settings.overrideDepthState = true;
            settings.depthCompareFunction = CompareFunction.Always;
            settings.enableWrite = false;
            feature.SetActive(true);

            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(renderer);
            Debug.Log($"[MenuOverlayRenderingSetup] '{renderer.name}': layer {layer} ({OverlayLayer.Name}) draws on top.");
        }
    }
}
