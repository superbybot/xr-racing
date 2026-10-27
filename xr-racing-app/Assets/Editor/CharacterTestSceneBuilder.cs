using System;
using System.IO;
using System.Reflection;
using Meta.XR.Movement;
using Meta.XR.Movement.Editor;
using Meta.XR.Movement.Retargeting;
using Meta.XR.Movement.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XrRacing.Gameplay.Characters;

namespace XrRacing.Editor.Characters
{
    /// <summary>
    /// Builds a standalone scene for testing the player's 3D character: an OVRCameraRig, the Karting driver
    /// (the character inside the kart prefab) driven by Movement SDK body/hand tracking, and a "mirror" in front
    /// of the player. The mirror is a second copy of the character that copies the tracked pose every frame
    /// (Movement SDK MirrorTransforms) under a parent with a negative Z scale, so it shows a reflection.
    /// Safe to re-run: it overwrites the scene and reuses the existing retargeting config.
    /// </summary>
    public static class CharacterTestSceneBuilder
    {
        private const string ScenePath = "Assets/Gameplay/Scenes/CharacterTest.unity";
        private const string CharacterModelPath = "Assets/References/Karting Reference/Art/Models/Player.FBX";
        private const string CameraRigPrefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
        // Distance from the player (standing at the origin, facing +Z) to the mirror plane.
        private const float MirrorDistance = 1.5f;

        [MenuItem("XR Racing/Build Character Test Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Report("Exit Play mode first.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            if (model == null)
            {
                Report("Character model not found at " + CharacterModelPath);
                return;
            }

            if (!EnsureRetargetingConfig(model))
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateEnvironment();
            var rig = CreateCameraRig();

            var playerCharacter = CreatePlayerCharacter(model, rig);
            if (playerCharacter == null)
            {
                return;
            }

            CreateMirror(model, playerCharacter.transform);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Report("Could not save " + ScenePath);
                return;
            }

            Report("Built " + ScenePath + ".");
        }

        /// <summary>
        /// Runs the Movement SDK's headless retargeting setup for the model unless its config JSON already exists
        /// next to the model. Returns false (after reporting) if no config could be produced.
        /// </summary>
        private static bool EnsureRetargetingConfig(GameObject model)
        {
            var configPath = Path.ChangeExtension(CharacterModelPath, ".json");
            if (AssetDatabase.LoadAssetAtPath<TextAsset>(configPath) != null)
            {
                return true;
            }

            var metadata = MSDKUtilityEditor.RunDefaultRetargetingSetup(model);
            if (metadata == null || metadata.ConfigJson == null)
            {
                Report("Movement SDK retargeting setup failed for " + CharacterModelPath + "; see the Console.");
                return false;
            }

            return true;
        }

        private static void CreateEnvironment()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
        }

        private static GameObject CreateCameraRig()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraRigPrefabPath);
            if (prefab == null)
            {
                Report("OVRCameraRig prefab not found at " + CameraRigPrefabPath);
                return null;
            }

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rig.name = "OVRCameraRig";

            var manager = rig.GetComponent<OVRManager>();
            if (manager == null)
            {
                return rig;
            }

            var serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("requestBodyTrackingPermissionOnStartup").boolValue = true;
            serializedManager.FindProperty("_trackingOriginType").intValue = (int)OVRManager.TrackingOrigin.FloorLevel;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
            return rig;
        }

        /// <summary>
        /// The character the player embodies, at the tracking-space origin, with CharacterRetargeter and
        /// MetaSourceDataProvider added by the Movement SDK's own setup, plus a head-and-hands driver that takes over
        /// in the Meta XR Simulator (which replays a fixed body pose instead of tracking the user).
        /// </summary>
        private static GameObject CreatePlayerCharacter(GameObject model, GameObject rig)
        {
            var character = (GameObject)PrefabUtility.InstantiatePrefab(model);
            character.name = "PlayerCharacter";
            character.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            try
            {
                MSDKUtilityEditor.AddCharacterRetargeter(character);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            var retargeter = character.GetComponent<CharacterRetargeter>();
            if (retargeter == null)
            {
                Report("Adding the Movement SDK CharacterRetargeter failed; see the Console.");
                return null;
            }

            KeepCharacterProportions(retargeter);
            AddFingerSpreadTuner(character);
            AddSimulatorDriver(character, rig, retargeter);
            return character;
        }

        private static void AddFingerSpreadTuner(GameObject character)
        {
            var tuner = character.AddComponent<FingerSpreadTuner>();
            var serializedTuner = new SerializedObject(tuner);
            serializedTuner.FindProperty("_animator").objectReferenceValue = character.GetComponent<Animator>();
            serializedTuner.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The default "Source Body Proportions" resizes every bone to the player's own lengths, which squashes this
        /// cartoon's oversized mitten hands into the palm and kinks its forearms. Keep the model's bone lengths and
        /// retarget rotations only.
        /// </summary>
        private static void KeepCharacterProportions(CharacterRetargeter retargeter)
        {
            var serializedRetargeter = new SerializedObject(retargeter);
            serializedRetargeter.FindProperty("_skeletonRetargeter._retargetingBehavior").intValue =
                (int)MSDKUtility.RetargetingBehavior.RotationOnlyNoScaling;
            serializedRetargeter.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSimulatorDriver(GameObject character, GameObject rig, CharacterRetargeter retargeter)
        {
            if (rig == null)
            {
                return;
            }

            var driver = character.AddComponent<HeadAndHandsAvatarDriver>();
            var serializedDriver = new SerializedObject(driver);
            serializedDriver.FindProperty("_animator").objectReferenceValue = character.GetComponent<Animator>();
            serializedDriver.FindProperty("_head").objectReferenceValue =
                rig.transform.Find("TrackingSpace/CenterEyeAnchor");
            serializedDriver.FindProperty("_leftHand").objectReferenceValue =
                rig.transform.Find("TrackingSpace/LeftHandAnchor");
            serializedDriver.FindProperty("_rightHand").objectReferenceValue =
                rig.transform.Find("TrackingSpace/RightHandAnchor");

            var bodyTracking = serializedDriver.FindProperty("_bodyTrackingBehaviours");
            var sourceData = character.GetComponent<MetaSourceDataProvider>();
            bodyTracking.arraySize = sourceData != null ? 2 : 1;
            bodyTracking.GetArrayElementAtIndex(0).objectReferenceValue = retargeter;
            if (sourceData != null)
            {
                bodyTracking.GetArrayElementAtIndex(1).objectReferenceValue = sourceData;
            }

            serializedDriver.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateMirror(GameObject model, Transform playerCharacter)
        {
            // Reflecting across the plane z = MirrorDistance maps z to 2 * MirrorDistance - z.
            var mirrorSpace = new GameObject("MirrorSpace").transform;
            mirrorSpace.position = new Vector3(0f, 0f, 2f * MirrorDistance);
            mirrorSpace.localScale = new Vector3(1f, 1f, -1f);

            var reflection = (GameObject)PrefabUtility.InstantiatePrefab(model, mirrorSpace);
            reflection.name = "PlayerReflection";

            var mirror = reflection.AddComponent<MirrorTransforms>();
            var serializedMirror = new SerializedObject(mirror);
            serializedMirror.FindProperty("_target").objectReferenceValue = playerCharacter;
            serializedMirror.ApplyModifiedPropertiesWithoutUndo();
            typeof(MirrorTransforms)
                .GetMethod("FindBonePairs", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(mirror, null);
            EditorUtility.SetDirty(mirror);

            // Backdrop behind the reflection so the mirror reads as a framed surface rather than open space.
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "MirrorBackdrop";
            backdrop.transform.position = new Vector3(0f, 1.25f, 2f * MirrorDistance + 1f);
            backdrop.transform.localScale = new Vector3(2.5f, 2.5f, 1f);
            UnityEngine.Object.DestroyImmediate(backdrop.GetComponent<Collider>());
        }

        private static void Report(string message)
        {
            if (Application.isBatchMode)
            {
                Debug.LogError("[CharacterTestSceneBuilder] " + message);
            }
            else
            {
                EditorUtility.DisplayDialog("Build Character Test Scene", message, "OK");
            }
        }
    }
}
