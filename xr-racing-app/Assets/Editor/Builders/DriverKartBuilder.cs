using UnityEditor;
using UnityEngine;
using XrRacing.References.KartGame.KartSystems;

namespace XrRacing.Editor.Builders
{
    /// <summary>
    /// Seats the 3D driver character in the player kart prefab: a DriverSeat anchor, the Karting driver model animated
    /// into its driving pose, the head-and-hands driver that makes the upper body follow the headset and hands, and
    /// the first-person head hider. Safe to re-run: it replaces any previous DriverCharacter.
    /// </summary>
    public static class DriverKartBuilder
    {
        private const string KartPrefabPath = "Assets/Gameplay/Prefabs/Vehicle/PlayerKart_VR.prefab";
        private const string DriverModelPath = "Assets/References/Karting Reference/Art/Models/Player.FBX";
        private const string DriverControllerPath =
            "Assets/References/Karting Reference/Animations/Controllers/PlayerController.controller";
        private const string SeatName = "DriverSeat";
        private const string CharacterName = "DriverCharacter";
        private const string IgnoreRaycastLayerName = "Ignore Raycast";
        private const int AlwaysMode = 1;
        private static readonly Vector3 SeatPosition = new Vector3(0.0094f, 0f, -0.09f);

        // [MenuItem("XR Racing/Build Driver Kart")]
        public static void Build()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(DriverModelPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(DriverControllerPath);
            if (model == null || controller == null)
            {
                Debug.LogError("[DriverKartBuilder] Driver model or animator controller not found.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(KartPrefabPath);
            try
            {
                Wire(root, model, controller);
                PrefabUtility.SaveAsPrefabAsset(root, KartPrefabPath);
                Debug.Log("[DriverKartBuilder] Seated the driver in " + KartPrefabPath + ".");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Wire(GameObject root, GameObject model, RuntimeAnimatorController controller)
        {
            var kart = root.GetComponentInChildren<ArcadeKart>(true);
            var head = FindChild(root.transform, "CenterEyeAnchor");
            var leftHand = FindChild(root.transform, "LeftHandOnControllerAnchor") ?? FindChild(root.transform, "LeftHandAnchor");
            var rightHand = FindChild(root.transform, "RightHandOnControllerAnchor") ?? FindChild(root.transform, "RightHandAnchor");
            var eyeCamera = head != null ? head.GetComponent<Camera>() : null;
            if (kart == null || head == null || leftHand == null || rightHand == null || eyeCamera == null)
            {
                Debug.LogError("[DriverKartBuilder] Kart prefab is missing the ArcadeKart or camera rig anchors.");
                return;
            }

            var eyePoint = FindChild(root.transform, "DriverEyePoint");
            if (eyePoint != null)
            {
                eyePoint.localPosition = new Vector3(0.01f, 1.03f, -0.06f);
            }

            string[] hiddenVisuals = { "OVRHandVisualLeft", "OVRHandVisualRight", "OVRControllerVisualLeft", "OVRControllerVisualRight" };
            foreach (var visualName in hiddenVisuals)
            {
                var visual = FindChild(root.transform, visualName);
                if (visual != null)
                {
                    visual.gameObject.SetActive(false);
                }
            }

            var seat = FindChild(root.transform, SeatName);
            if (seat == null)
            {
                seat = new GameObject(SeatName).transform;
                seat.SetParent(root.transform, false);
            }

            seat.localPosition = SeatPosition;
            seat.localRotation = Quaternion.identity;

            var existing = FindChild(root.transform, CharacterName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var character = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            character.name = CharacterName;
            character.transform.SetPositionAndRotation(seat.position, seat.rotation);
            SetLayer(character, LayerMask.NameToLayer(IgnoreRaycastLayerName));

            var animator = character.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            var driver = character.AddComponent<XrRacing.Gameplay.Characters.HeadAndHandsAvatarDriver>();
            var serializedDriver = new SerializedObject(driver);
            serializedDriver.FindProperty("_mode").enumValueIndex = AlwaysMode;
            serializedDriver.FindProperty("_animator").objectReferenceValue = animator;
            serializedDriver.FindProperty("_head").objectReferenceValue = head;
            serializedDriver.FindProperty("_leftHand").objectReferenceValue = leftHand;
            serializedDriver.FindProperty("_rightHand").objectReferenceValue = rightHand;
            serializedDriver.FindProperty("_seat").objectReferenceValue = seat;
            serializedDriver.FindProperty("_matchPlayerHeight").boolValue = false;
            serializedDriver.ApplyModifiedPropertiesWithoutUndo();

            var hider = character.AddComponent<XrRacing.Gameplay.Characters.FirstPersonHeadHider>();
            var serializedHider = new SerializedObject(hider);
            serializedHider.FindProperty("_animator").objectReferenceValue = animator;
            serializedHider.FindProperty("_firstPersonCamera").objectReferenceValue = eyeCamera;
            serializedHider.ApplyModifiedPropertiesWithoutUndo();

            var playerAnimator = character.AddComponent<KartPlayerAnimator>();
            playerAnimator.Kart = kart;
            playerAnimator.PlayerAnimator = animator;

            var wheelInput = root.GetComponentInChildren<XrRacing.Gameplay.Input.XRWheelInput>(true);
            if (wheelInput != null)
            {
                var leftInteractors = FindChild(root.transform, "ComprehensiveInteractorsLeft");
                var rightInteractors = FindChild(root.transform, "ComprehensiveInteractorsRight");
                var serializedWheelInput = new SerializedObject(wheelInput);
                serializedWheelInput.FindProperty("_leftInteractorsRoot").objectReferenceValue = leftInteractors;
                serializedWheelInput.FindProperty("_rightInteractorsRoot").objectReferenceValue = rightInteractors;
                serializedWheelInput.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private static void SetLayer(GameObject target, int layer)
        {
            foreach (var child in target.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }
    }
}
