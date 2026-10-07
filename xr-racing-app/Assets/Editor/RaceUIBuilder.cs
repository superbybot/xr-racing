using System;
using System.IO;
using System.Linq;
using KartGame.KartSystems;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using XrRacing.Gameplay.Race;
using XrRacing.Gameplay.Tracks;
using XrRacing.Gameplay.UI;
using XrRacing.Gameplay.Vehicle;

namespace XrRacing.Editor.Race
{
    /// <summary>
    /// Builds the racing system (RaceManager, LapTracker, RaceCountdownUI, FloatingLapUI, RaceEndingMenu)
    /// into the active driving scene using Meta Interaction SDK UISet prefabs.
    /// Safe to re-run: replaces previous race system components.
    /// </summary>
    public static class RaceUIBuilder
    {
        private const string UISetPath = "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Objects/UISet/Prefabs/";
        private const string BackplatePrefab = "Backplate/EmptyUIBackplateWithCanvas.prefab";
        private const string SecondaryButtonPrefab = "Button/UnityUIButtonBased/SecondaryButton_IconAndLabel_UnityUIButton.prefab";
        private const string TextStylePrefab = "Dialog/Dialog2Button_TextOnly.prefab";

        private const string BeepSfxPath = "Assets/References/Karting Reference/AddOns/MgKarting_RemixSFX/UI/Notification_Bip_01.wav";
        private const string StartSfxPath = "Assets/References/Karting Reference/AddOns/MgKarting_RemixSFX/UI/StartSound_03.wav";
        private const string FinishSfxPath = "Assets/References/Karting Reference/AddOns/MgKarting_RemixSFX/Environment/FinishLine_01.wav";

        private const string DrivingScenePath = "Assets/Gameplay/Scenes/DrivingPrototype_VR.unity";
        private const string EyePointName = "DriverEyePoint";

        private static TMP_Text _textStyle;

        [MenuItem("XR Racing/Build Race System")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Report("Exit Play mode first.");
                return;
            }

            TryBuild();
        }

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
            ArcadeKart kart = UnityEngine.Object.FindAnyObjectByType<ArcadeKart>(FindObjectsInactive.Include);
            if (kart == null)
            {
                Report("No ArcadeKart found in the active scene.");
                return false;
            }

            TrackLoader trackLoader = UnityEngine.Object.FindAnyObjectByType<TrackLoader>(FindObjectsInactive.Include);
            if (trackLoader == null)
            {
                Report("No TrackLoader found in the active scene.");
                return false;
            }

            DriverSeatAdjuster seat = UnityEngine.Object.FindAnyObjectByType<DriverSeatAdjuster>(FindObjectsInactive.Include);
            Transform eyePoint = kart.transform.Find(EyePointName);
            if (eyePoint == null && seat != null)
            {
                eyePoint = seat.transform;
            }

            try
            {
                _textStyle = LoadPrefab(TextStylePrefab).GetComponentsInChildren<TMP_Text>(true)[0];

                Undo.SetCurrentGroupName("Build Race System");
                int undoGroup = Undo.GetCurrentGroup();

                RemoveExistingRaceSystem();

                // 1. Ensure LapTracker is on the kart
                LapTracker lapTracker = kart.GetComponent<LapTracker>();
                if (lapTracker == null)
                {
                    lapTracker = Undo.AddComponent<LapTracker>(kart.gameObject);
                }

                // 2. Create Root RaceManager
                var managerObject = new GameObject("RaceManager");
                Undo.RegisterCreatedObjectUndo(managerObject, "Create RaceManager");
                RaceManager raceManager = managerObject.AddComponent<RaceManager>();
                AudioSource audioSource = managerObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;

                // 3. Build Countdown UI
                RaceCountdownUI countdownUI = BuildCountdownUI(kart.transform, eyePoint);

                // 4. Build Floating Lap UI
                FloatingLapUI floatingLapUI = BuildFloatingLapUI(kart.transform, eyePoint);

                // 5. Build Race Ending Menu
                RaceEndingMenu endingMenu = BuildEndingMenu(managerObject.transform, seat, eyePoint, trackLoader);

                // 6. Connect Serialized References on RaceManager
                AudioClip finishClip = AssetDatabase.LoadAssetAtPath<AudioClip>(FinishSfxPath);

                var serialized = new SerializedObject(raceManager);
                serialized.FindProperty("kart").objectReferenceValue = kart;
                serialized.FindProperty("trackLoader").objectReferenceValue = trackLoader;
                serialized.FindProperty("lapTracker").objectReferenceValue = lapTracker;
                serialized.FindProperty("countdownUI").objectReferenceValue = countdownUI;
                serialized.FindProperty("floatingLapUI").objectReferenceValue = floatingLapUI;
                serialized.FindProperty("endingMenu").objectReferenceValue = endingMenu;
                serialized.FindProperty("audioSource").objectReferenceValue = audioSource;
                serialized.FindProperty("finishClip").objectReferenceValue = finishClip;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(managerObject.scene);
                Selection.activeGameObject = managerObject;
                Debug.Log("[RaceUIBuilder] Successfully built Race System in scene.", managerObject);
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

        private static void RemoveExistingRaceSystem()
        {
            foreach (RaceManager manager in UnityEngine.Object.FindObjectsByType<RaceManager>(FindObjectsInactive.Include))
            {
                Undo.DestroyObjectImmediate(manager.gameObject);
            }

            foreach (FloatingLapUI lapUI in UnityEngine.Object.FindObjectsByType<FloatingLapUI>(FindObjectsInactive.Include))
            {
                Undo.DestroyObjectImmediate(lapUI.gameObject);
            }

            foreach (RaceCountdownUI countdownUI in UnityEngine.Object.FindObjectsByType<RaceCountdownUI>(FindObjectsInactive.Include))
            {
                Undo.DestroyObjectImmediate(countdownUI.gameObject);
            }
        }

        private static RaceCountdownUI BuildCountdownUI(Transform parent, Transform eyePoint)
        {
            var go = new GameObject("RaceCountdown", typeof(RaceCountdownUI));
            go.transform.SetParent(parent, false);
            RaceCountdownUI countdown = go.GetComponent<RaceCountdownUI>();

            GameObject panel = Instantiate(BackplatePrefab, go.transform);
            panel.name = "CountdownPanel";

            Vector3 localOffset = new Vector3(0f, 0f, 1.2f);
            if (eyePoint != null)
            {
                panel.transform.position = eyePoint.TransformPoint(localOffset);
                panel.transform.rotation = eyePoint.rotation;
            }
            else
            {
                panel.transform.localPosition = localOffset + Vector3.up * 1.15f;
                panel.transform.localRotation = Quaternion.identity;
            }

            var canvasRoot = (RectTransform)panel.transform.Find("CanvasRoot");
            var backplate = (RectTransform)canvasRoot.Find("UIBackplate");

            canvasRoot.sizeDelta = new Vector2(360f, 180f);
            backplate.sizeDelta = new Vector2(360f, 180f);

            var layout = backplate.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(16, 16, 16, 16);
                layout.childAlignment = TextAnchor.MiddleCenter;
            }

            TMP_Text text = CreateText(backplate, "CountdownText", "3", 64f, FontStyles.Bold, TextAlignmentOptions.Center);
            AudioSource audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;

            AudioClip beep = AssetDatabase.LoadAssetAtPath<AudioClip>(BeepSfxPath);
            AudioClip start = AssetDatabase.LoadAssetAtPath<AudioClip>(StartSfxPath);

            var serialized = new SerializedObject(countdown);
            serialized.FindProperty("panel").objectReferenceValue = panel;
            serialized.FindProperty("countdownText").objectReferenceValue = text;
            serialized.FindProperty("canvasGroup").objectReferenceValue = panel.GetComponent<CanvasGroup>();
            serialized.FindProperty("audioSource").objectReferenceValue = audio;
            serialized.FindProperty("countClip").objectReferenceValue = beep;
            serialized.FindProperty("startClip").objectReferenceValue = start;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            OverlayLayer.Apply(panel);
            return countdown;
        }

        private static FloatingLapUI BuildFloatingLapUI(Transform kart, Transform eyePoint)
        {
            var go = new GameObject("FloatingLapHUD", typeof(FloatingLapUI));
            go.transform.SetParent(kart, false);
            FloatingLapUI lapUI = go.GetComponent<FloatingLapUI>();

            GameObject panel = Instantiate(BackplatePrefab, go.transform);
            panel.name = "LapCard";

            // Local position on the driver's left side view
            Vector3 localOffset = new Vector3(-0.45f, 0.05f, 0.65f);
            Quaternion localRot = Quaternion.Euler(0f, 22f, 0f);

            if (eyePoint != null)
            {
                panel.transform.position = eyePoint.TransformPoint(localOffset);
                panel.transform.rotation = eyePoint.rotation * localRot;
            }
            else
            {
                panel.transform.localPosition = localOffset;
                panel.transform.localRotation = localRot;
            }

            var canvasRoot = (RectTransform)panel.transform.Find("CanvasRoot");
            var backplate = (RectTransform)canvasRoot.Find("UIBackplate");

            canvasRoot.sizeDelta = new Vector2(240f, 130f);
            backplate.sizeDelta = new Vector2(240f, 130f);

            var layout = backplate.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(16, 16, 12, 12);
                layout.spacing = 4f;
                layout.childAlignment = TextAnchor.MiddleCenter;
            }

            TMP_Text label = CreateText(backplate, "LapLabel", "LAP", 22f, FontStyles.Bold, TextAlignmentOptions.Center);
            TMP_Text value = CreateText(backplate, "LapValue", "1 / 2", 36f, FontStyles.Bold, TextAlignmentOptions.Center);

            var serialized = new SerializedObject(lapUI);
            serialized.FindProperty("panel").objectReferenceValue = panel;
            serialized.FindProperty("lapLabel").objectReferenceValue = label;
            serialized.FindProperty("lapValue").objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            OverlayLayer.Apply(panel);
            return lapUI;
        }

        private static RaceEndingMenu BuildEndingMenu(Transform parent, DriverSeatAdjuster seat, Transform eyePoint, TrackLoader trackLoader)
        {
            var go = new GameObject("RaceEndingMenu", typeof(RaceEndingMenu));
            go.transform.SetParent(parent, false);
            RaceEndingMenu menu = go.GetComponent<RaceEndingMenu>();

            GameObject panel = Instantiate(BackplatePrefab, go.transform);
            panel.name = "EndingMenuPanel";

            Vector3 eyePos = eyePoint != null ? eyePoint.position : Vector3.up * 1.15f;
            Quaternion eyeRot = eyePoint != null ? eyePoint.rotation : Quaternion.identity;
            panel.transform.position = eyePos + (eyeRot * Vector3.forward * 0.7f);
            panel.transform.rotation = eyeRot;

            var canvasRoot = (RectTransform)panel.transform.Find("CanvasRoot");
            var backplate = (RectTransform)canvasRoot.Find("UIBackplate");

            var layout = backplate.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(24, 24, 24, 24);
                layout.spacing = 12f;
                layout.childAlignment = TextAnchor.UpperCenter;
            }

            CreateText(backplate, "Title", "Race Finished!", 26f, FontStyles.Bold, TextAlignmentOptions.Center);

            GameObject playAgain = Instantiate(SecondaryButtonPrefab, backplate);
            playAgain.name = "PlayAgainButton";
            SetButtonLabel(playAgain, "Play Again", 20f);

            GameObject playground = Instantiate(SecondaryButtonPrefab, backplate);
            playground.name = "PlaygroundButton";
            SetButtonLabel(playground, "Back to Playground", 20f);

            GameObject settings = Instantiate(SecondaryButtonPrefab, backplate);
            settings.name = "SettingsButton";
            SetButtonLabel(settings, "Driver Menu", 20f);

            canvasRoot.sizeDelta = new Vector2(400f, 260f);
            backplate.sizeDelta = new Vector2(400f, 260f);

            DriverSettingsMenu settingsMenu = UnityEngine.Object.FindAnyObjectByType<DriverSettingsMenu>(FindObjectsInactive.Include);

            var serialized = new SerializedObject(menu);
            serialized.FindProperty("panel").objectReferenceValue = panel;
            serialized.FindProperty("playAgainButton").objectReferenceValue = playAgain.GetComponent<Button>();
            serialized.FindProperty("playgroundButton").objectReferenceValue = playground.GetComponent<Button>();
            serialized.FindProperty("settingsButton").objectReferenceValue = settings.GetComponent<Button>();
            serialized.FindProperty("trackLoader").objectReferenceValue = trackLoader;
            serialized.FindProperty("settingsMenu").objectReferenceValue = settingsMenu;
            serialized.FindProperty("seat").objectReferenceValue = seat;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            OverlayLayer.Apply(panel);
            return menu;
        }

        private static TMP_Text CreateText(Transform parent, string name, string content, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            if (_textStyle != null)
            {
                tmp.font = _textStyle.font;
                tmp.color = _textStyle.color;
            }

            tmp.text = content;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void SetButtonLabel(GameObject button, string text, float fontSize)
        {
            foreach (Transform child in button.GetComponentsInChildren<Transform>(true))
            {
                switch (child.name)
                {
                    case "Label":
                        TMP_Text label = child.GetComponent<TMP_Text>();
                        if (label != null)
                        {
                            label.text = text;
                            label.fontSize = fontSize;
                            label.alignment = TextAlignmentOptions.Center;
                            label.textWrappingMode = TextWrappingModes.NoWrap;
                        }
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

        private static void Report(string message)
        {
            if (Application.isBatchMode)
            {
                Debug.LogError("[RaceUIBuilder] " + message);
            }
            else
            {
                EditorUtility.DisplayDialog("Race System Builder", message, "OK");
            }
        }
    }
}
