using System;
using Oculus.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XrRacing.Gameplay.Settings;
using XrRacing.Gameplay.Tracks;
using XrRacing.Gameplay.UI;
using XrRacing.Gameplay.Vehicle;

namespace XrRacing.Editor.UI
{
    /// <summary>
    /// Builds the driver settings menu (seat height/distance, recenter, pedal mapping, reset) into the open scene
    /// from Meta Interaction SDK UISet prefabs. Safe to re-run: it replaces the previous menu.
    /// </summary>
    public static class DriverSettingsMenuBuilder
    {
        private const string UISetPath = "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Objects/UISet/Prefabs/";
        private const string BackplatePrefab = "Backplate/EmptyUIBackplateWithCanvas.prefab";
        private const string SliderPrefab = "Slider/MediumSlider.prefab";
        private const string ToggleTilePrefab = "Button/UnityUIToggleBased/TextTileButton_IconAndLabel_Toggle.prefab";
        private const string SecondaryButtonPrefab = "Button/UnityUIButtonBased/SecondaryButton_IconAndLabel_UnityUIButton.prefab";
        private const string TextStylePrefab = "Dialog/Dialog2Button_TextOnly.prefab";

        private const string EyePointName = "DriverEyePoint";
        // Floor-level tracking: roughly where a seated player's eyes sit above the rig origin.
        private const float SeatedEyeHeight = 1.15f;

        private const float PanelWidth = 680f;
        private const float Padding = 32f;
        private const float Spacing = 16f;
        private const float RowHeight = 56f;
        // Selected Accel/Brake toggle fill (Meta-style blue).
        private static readonly Color SelectedColor = new Color(0.0f, 0.39f, 0.88f, 1f);

        private static TMP_Text _textStyle;

        private const string DrivingScenePath = "Assets/Gameplay/Scenes/DrivingPrototype_VR.unity";

        [MenuItem("XR Racing/Build Driver Settings Menu")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Report("Exit Play mode first.");
                return;
            }

            TryBuild();
        }

        /// <summary>
        /// Batch-mode entry: opens the VR driving scene, builds the menu and saves the scene. Exits with 1 on failure.
        /// Unity -batchmode -quit -projectPath &lt;project&gt; -executeMethod DriverSettingsMenuBuilder.BuildInDrivingScene
        /// </summary>
        public static void BuildInDrivingScene()
        {
            var scene = EditorSceneManager.OpenScene(DrivingScenePath, OpenSceneMode.Single);
            if (!TryBuild() || !EditorSceneManager.SaveScene(scene))
            {
                EditorApplication.Exit(1);
            }
        }

        private static bool TryBuild()
        {
            VRCameraHeightSmoother smoother = UnityEngine.Object.FindAnyObjectByType<VRCameraHeightSmoother>(FindObjectsInactive.Include);
            if (smoother == null || smoother.transform.parent == null)
            {
                Report("Couldn't find the camera rig (VRCameraHeightSmoother parented under the kart) in the open scene.");
                return false;
            }

            try
            {
                _textStyle = LoadPrefab(TextStylePrefab).GetComponentsInChildren<TMP_Text>(true)[0];

                Undo.SetCurrentGroupName("Build Driver Settings Menu");
                int undoGroup = Undo.GetCurrentGroup();

                Transform eyePoint = EnsureEyePoint(smoother.transform);
                DriverSeatAdjuster seat = EnsureSeatAdjuster(smoother, eyePoint);
                EnsureEventSystem();
                RemoveExistingMenus();
                MenuOverlayRenderingSetup.Run(); // layer + URP feature so the menu draws over the kart
                GameObject root = BuildMenu(seat, eyePoint);
                OverlayLayer.Apply(root);

                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(root.scene);
                Selection.activeGameObject = root;
                Debug.Log("[DriverSettingsMenuBuilder] Built DriverSettingsMenu. Save the scene to keep it.", root);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Report($"Build failed: {e.Message}");
                return false;
            }
            finally
            {
                _textStyle = null;
            }
        }

        private static void Report(string message)
        {
            if (Application.isBatchMode)
            {
                Debug.LogError("[DriverSettingsMenuBuilder] " + message);
            }
            else
            {
                EditorUtility.DisplayDialog("Driver Settings Menu", message, "OK");
            }
        }

        private static Transform EnsureEyePoint(Transform rig)
        {
            Transform kart = rig.parent;
            Transform eyePoint = kart.Find(EyePointName);
            if (eyePoint != null)
            {
                return eyePoint;
            }

            eyePoint = new GameObject(EyePointName).transform;
            eyePoint.SetParent(kart, false);
            eyePoint.localPosition = rig.localPosition + rig.localRotation * new Vector3(0f, SeatedEyeHeight, 0f);
            eyePoint.localRotation = rig.localRotation;
            Undo.RegisterCreatedObjectUndo(eyePoint.gameObject, "Create Driver Eye Point");
            return eyePoint;
        }

        private static DriverSeatAdjuster EnsureSeatAdjuster(VRCameraHeightSmoother smoother, Transform eyePoint)
        {
            DriverSeatAdjuster seat = smoother.GetComponent<DriverSeatAdjuster>();
            if (seat == null)
            {
                seat = Undo.AddComponent<DriverSeatAdjuster>(smoother.gameObject);
            }

            var serialized = new SerializedObject(seat);
            serialized.FindProperty("eyePoint").objectReferenceValue = eyePoint;
            serialized.ApplyModifiedProperties();
            return seat;
        }

        // Meta's canvas pointing (poke/ray on PointableCanvas) needs an EventSystem with PointableCanvasModule.
        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(PointableCanvasModule));
                Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
                return;
            }

            if (eventSystem.GetComponent<PointableCanvasModule>() == null)
            {
                Undo.AddComponent<PointableCanvasModule>(eventSystem.gameObject);
            }
        }

        private static void RemoveExistingMenus()
        {
            foreach (DriverSettingsMenu menu in UnityEngine.Object.FindObjectsByType<DriverSettingsMenu>(FindObjectsInactive.Include))
            {
                Undo.DestroyObjectImmediate(menu.gameObject);
            }
        }

        private static GameObject BuildMenu(DriverSeatAdjuster seat, Transform eyePoint)
        {
            var root = new GameObject("DriverSettingsMenu");
            Undo.RegisterCreatedObjectUndo(root, "Create Driver Settings Menu");
            DriverSettingsMenu menu = root.AddComponent<DriverSettingsMenu>();

            GameObject panel = Instantiate(BackplatePrefab, root.transform);
            panel.name = "DriverSettingsPanel";
            // Preview spot in the editor; at runtime the panel opens in front of the player's head.
            panel.transform.position = eyePoint.position + eyePoint.forward * 0.5f - eyePoint.up * 0.1f;
            panel.transform.rotation = eyePoint.rotation;

            var canvasRoot = (RectTransform)panel.transform.Find("CanvasRoot");
            var backplate = (RectTransform)canvasRoot.Find("UIBackplate");

            VerticalLayoutGroup column = backplate.GetComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset((int)Padding, (int)Padding, (int)Padding, (int)Padding);
            column.spacing = Spacing;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            float height = Padding * 2f;
            height += AddRow(Text(backplate, "Title", "Driver Settings", 30f, FontStyles.Bold, TextAlignmentOptions.Center, 0f), 48f);

            // Track picker, only once "XR Racing/Build Track Scenes" has added a TrackLoader.
            TrackLoader trackLoader = UnityEngine.Object.FindAnyObjectByType<TrackLoader>(FindObjectsInactive.Include);
            Toggle[] trackToggles = new Toggle[0];
            if (trackLoader != null && trackLoader.Tracks.Count > 0)
            {
                trackToggles = TrackRow(backplate, trackLoader, out float trackRowHeight);
                height += trackRowHeight;
            }

            (Slider heightSlider, TMP_Text heightValue) = SliderRow(backplate, "Height", "Seat height", out float rowHeight);
            height += rowHeight;
            (Slider distanceSlider, TMP_Text distanceValue) = SliderRow(backplate, "Distance", "Distance to wheel", out rowHeight);
            height += rowHeight;

            GameObject recenter = Instantiate(SecondaryButtonPrefab, backplate);
            recenter.name = "RecenterButton";
            SetButtonLabel(recenter, "Recenter Seat (sit or stand as you'll drive)", 20f);
            height += AddRow(recenter, RowHeight);

            height += AddRow(Text(backplate, "LeftHeader", "Left hand / controller", 22f, FontStyles.Bold, TextAlignmentOptions.Left, 0f), 36f);
            (DriverSettingsMenu.PedalSelector leftIndex, DriverSettingsMenu.PedalSelector leftThumb) = PedalRow(backplate, "Left", out rowHeight);
            height += rowHeight;
            height += AddRow(Text(backplate, "RightHeader", "Right hand / controller", 22f, FontStyles.Bold, TextAlignmentOptions.Left, 0f), 36f);
            (DriverSettingsMenu.PedalSelector rightIndex, DriverSettingsMenu.PedalSelector rightThumb) = PedalRow(backplate, "Right", out rowHeight);
            height += rowHeight;

            GameObject reset = Instantiate(SecondaryButtonPrefab, backplate);
            reset.name = "ResetButton";
            SetButtonLabel(reset, "Reset to defaults", 20f);
            height += AddRow(reset, RowHeight);

            height += Spacing * (backplate.childCount - 1);
            canvasRoot.sizeDelta = new Vector2(PanelWidth, height);
            backplate.sizeDelta = new Vector2(PanelWidth, height);

            var serialized = new SerializedObject(menu);
            serialized.FindProperty("panel").objectReferenceValue = panel;
            serialized.FindProperty("seat").objectReferenceValue = seat;
            serialized.FindProperty("heightSlider").objectReferenceValue = heightSlider;
            serialized.FindProperty("heightValue").objectReferenceValue = heightValue;
            serialized.FindProperty("distanceSlider").objectReferenceValue = distanceSlider;
            serialized.FindProperty("distanceValue").objectReferenceValue = distanceValue;
            serialized.FindProperty("recenterButton").objectReferenceValue = recenter.GetComponent<Button>();
            serialized.FindProperty("resetButton").objectReferenceValue = reset.GetComponent<Button>();
            SetSelector(serialized, "leftIndex", leftIndex);
            SetSelector(serialized, "leftThumb", leftThumb);
            SetSelector(serialized, "rightIndex", rightIndex);
            SetSelector(serialized, "rightThumb", rightThumb);
            serialized.FindProperty("trackLoader").objectReferenceValue = trackLoader;
            SerializedProperty toggles = serialized.FindProperty("trackToggles");
            toggles.arraySize = trackToggles.Length;
            for (int i = 0; i < trackToggles.Length; i++)
            {
                toggles.GetArrayElementAtIndex(i).objectReferenceValue = trackToggles[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // Track [Oval][Winding][Country][Mountain][Playground], one tile per TrackLoader track.
        private static Toggle[] TrackRow(Transform parent, TrackLoader loader, out float rowHeight)
        {
            Transform row = Row(parent, "TrackRow", 6f);
            Text(row, "TrackLabel", "Track", 20f, FontStyles.Bold, TextAlignmentOptions.Left, 64f);

            Transform group = Row(row, "Tracks", 4f);
            ToggleGroup toggleGroup = group.gameObject.AddComponent<ToggleGroup>();
            toggleGroup.allowSwitchOff = false;

            var toggles = new Toggle[loader.Tracks.Count];
            for (int i = 0; i < toggles.Length; i++)
            {
                // Selection is filled in at runtime from the loaded track; preview the first one here.
                toggles[i] = PedalToggle(group, toggleGroup, loader.Tracks[i].displayName, i == 0, 16f);
            }

            rowHeight = AddRow(row.gameObject, RowHeight);
            return toggles;
        }

        // [label][slider 1..10][value]
        private static (Slider, TMP_Text) SliderRow(Transform parent, string name, string label, out float rowHeight)
        {
            Transform row = Row(parent, name + "Row", 12f);
            Text(row, "Label", label, 22f, FontStyles.Normal, TextAlignmentOptions.Left, 200f);

            GameObject sliderGo = Instantiate(SliderPrefab, row);
            sliderGo.name = name + "Slider";
            Slider slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 1f;
            slider.maxValue = 10f;
            slider.wholeNumbers = true;
            slider.value = 5f;
            PreferSize(sliderGo, 300f, ((RectTransform)sliderGo.transform).sizeDelta.y);

            TMP_Text value = Text(row, "Value", "5", 22f, FontStyles.Bold, TextAlignmentOptions.Right, 48f);
            rowHeight = AddRow(row.gameObject, RowHeight);
            return (slider, value);
        }

        // Index [Accel|Brake]   Thumb [Accel|Brake]
        private static (DriverSettingsMenu.PedalSelector, DriverSettingsMenu.PedalSelector) PedalRow(Transform parent, string side, out float rowHeight)
        {
            Transform row = Row(parent, side + "PedalRow", 6f);
            Text(row, "IndexLabel", "Index", 20f, FontStyles.Normal, TextAlignmentOptions.Left, 64f);
            DriverSettingsMenu.PedalSelector index = PedalSelector(row, side + "Index", PedalAction.Accelerate);
            Spacer(row, 20f);
            Text(row, "ThumbLabel", "Thumb", 20f, FontStyles.Normal, TextAlignmentOptions.Left, 64f);
            DriverSettingsMenu.PedalSelector thumb = PedalSelector(row, side + "Thumb", PedalAction.Brake);
            rowHeight = AddRow(row.gameObject, RowHeight);
            return (index, thumb);
        }

        private static DriverSettingsMenu.PedalSelector PedalSelector(Transform parent, string name, PedalAction initial)
        {
            Transform group = Row(parent, name, 4f);
            ToggleGroup toggleGroup = group.gameObject.AddComponent<ToggleGroup>();
            toggleGroup.allowSwitchOff = false;

            return new DriverSettingsMenu.PedalSelector
            {
                accelerate = PedalToggle(group, toggleGroup, "Accel", initial == PedalAction.Accelerate),
                brake = PedalToggle(group, toggleGroup, "Brake", initial == PedalAction.Brake)
            };
        }

        private static Toggle PedalToggle(Transform parent, ToggleGroup group, string label, bool isOn, float fontSize = 18f)
        {
            GameObject go = Instantiate(ToggleTilePrefab, parent);
            go.name = label + "Toggle";
            SetButtonLabel(go, label, fontSize);
            PreferSize(go, 104f, 48f);

            // The tile's label box is sized for a 176px tile; fit it to this one so the text centers.
            foreach (TMP_Text text in go.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Label")
                {
                    text.rectTransform.sizeDelta = new Vector2(88f, text.rectTransform.sizeDelta.y);
                    PreferSize(text.gameObject, 88f, text.rectTransform.sizeDelta.y);
                }
            }

            Toggle toggle = go.GetComponent<Toggle>();
            toggle.graphic = AddSelectedFill(go);
            toggle.toggleTransition = Toggle.ToggleTransition.None;
            toggle.group = group;
            toggle.isOn = isOn;
            return toggle;
        }

        // Colored fill behind the label, shown only while the toggle is on (the Toggle's own graphic), so the
        // chosen pedal stands out. A copy of the tile's Background keeps its rounded corners; the tile's Animator
        // doesn't touch the copy.
        private static Graphic AddSelectedFill(GameObject tile)
        {
            Transform background = null;
            foreach (Transform child in tile.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Background")
                {
                    background = child;
                    break;
                }
            }

            if (background == null)
            {
                throw new InvalidOperationException($"'{tile.name}' has no Background to base the selected fill on.");
            }

            GameObject fill = UnityEngine.Object.Instantiate(background.gameObject, background, false);
            fill.name = "SelectedFill";

            for (int i = fill.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(fill.transform.GetChild(i).gameObject);
            }

            foreach (Behaviour extra in fill.GetComponents<Behaviour>())
            {
                if (!(extra is Image) && !(extra is LayoutElement) && extra.GetType().Name != "RoundedBoxUIProperties")
                {
                    UnityEngine.Object.DestroyImmediate(extra);
                }
            }

            LayoutElement layout = fill.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.ignoreLayout = true;
            }

            var rect = (RectTransform)fill.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();

            Image image = fill.GetComponent<Image>();
            image.color = SelectedColor;
            image.raycastTarget = false;
            return image;
        }

        private static void SetSelector(SerializedObject menu, string field, DriverSettingsMenu.PedalSelector selector)
        {
            menu.FindProperty(field + ".accelerate").objectReferenceValue = selector.accelerate;
            menu.FindProperty(field + ".brake").objectReferenceValue = selector.brake;
        }

        private static Transform Row(Transform parent, string name, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);

            HorizontalLayoutGroup layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return go.transform;
        }

        private static void Spacer(Transform parent, float width)
        {
            var go = new GameObject("Spacer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            PreferSize(go, width, 1f);
        }

        // Returns the height it adds to the column.
        private static float AddRow(Component component, float height)
        {
            return AddRow(component.gameObject, height);
        }

        private static float AddRow(GameObject go, float height)
        {
            PreferSize(go, -1f, height);
            return height;
        }

        private static TMP_Text Text(Transform parent, string name, string text, float size, FontStyles style,
            TextAlignmentOptions alignment, float width)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = _textStyle.font;
            tmp.color = _textStyle.color;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;

            if (width > 0f)
            {
                PreferSize(go, width, RowHeight);
            }

            return tmp;
        }

        // UISet buttons carry an icon, subtitle and second label we don't use; keep just the main label, centered.
        private static void SetButtonLabel(GameObject button, string text, float fontSize)
        {
            foreach (Transform child in button.GetComponentsInChildren<Transform>(true))
            {
                switch (child.name)
                {
                    case "Label":
                        TMP_Text label = child.GetComponent<TMP_Text>();
                        label.text = text;
                        label.fontSize = fontSize;
                        label.alignment = TextAlignmentOptions.Center;
                        label.textWrappingMode = TextWrappingModes.NoWrap;
                        break;
                    case "Subtitle":
                    case "Label (1)":
                    case "Icon":
                    case "Space":
                    case "Gap":
                        child.gameObject.SetActive(false);
                        break;
                }
            }
        }

        private static void PreferSize(GameObject go, float width, float height)
        {
            LayoutElement element = go.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = go.AddComponent<LayoutElement>();
            }

            element.layoutPriority = Mathf.Max(element.layoutPriority, 2);

            if (width >= 0f)
            {
                element.minWidth = width;
                element.preferredWidth = width;
            }

            element.minHeight = height;
            element.preferredHeight = height;
        }

        // Instantiates a UISet prefab and unpacks it so our edits are plain scene data, not prefab overrides.
        private static GameObject Instantiate(string prefabPath, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(prefabPath), parent);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return instance;
        }

        private static GameObject LoadPrefab(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UISetPath + prefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"UISet prefab not found: {UISetPath + prefabPath}");
            }

            return prefab;
        }
    }
}
