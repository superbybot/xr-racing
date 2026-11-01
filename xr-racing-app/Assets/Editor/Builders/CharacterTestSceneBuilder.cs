using System;
using System.IO;
using System.Reflection;
using Meta.XR.Movement;
using Meta.XR.Movement.AI;
using Meta.XR.Movement.Editor;
using Meta.XR.Movement.Retargeting;
using Meta.XR.Movement.Utils;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XrRacing.Gameplay.Characters;

namespace XrRacing.Editor.Characters
{
    /// <summary>
    /// Builds a standalone scene for testing the player's 3D character: an OVRCameraRig with Interaction SDK
    /// controller rays, one body-tracked player character per entry in <see cref="Characters"/> (only the selected
    /// one active), a "mirror" in front of the player, and a lineup of the characters to point at and pick from.
    /// The mirror is a second copy of the character that copies the tracked pose every frame (Movement SDK
    /// MirrorTransforms) under a parent with a negative Z scale, so it shows a reflection.
    /// Safe to re-run: it overwrites the scene and reuses each character's existing retargeting config.
    /// </summary>
    public static class CharacterTestSceneBuilder
    {
        private const string ScenePath = "Assets/Gameplay/Scenes/CharacterTest.unity";
        private const string KartingPath = "Assets/References/Karting Reference/";
        private const string CameraRigPrefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
        private const string InteractionRigPrefabPath =
            "Packages/com.meta.xr.sdk.interaction.ovr/Runtime/Prefabs/OVRComprehensiveInteractionRig.prefab";
        private const string SynthesizerDataPath = "Packages/com.meta.xr.sdk.movement/Runtime/Native/Data/";
        // Distance from the player (standing at the origin, facing +Z) to the mirror plane.
        private const float MirrorDistance = 1.5f;
        // The lineup stands in an arc on the player's right, this far away, with enough angle between models that
        // their T-posed arms don't overlap.
        private const float LineupRadius = 2.6f;
        private const float LineupFirstAngle = 35f;
        private const float LineupAngleStep = 33f;

        private const float RacerIndexSpread = 1.7f;
        private const float RacerIndexOffset = 2.96f;

        // The first entry is the default. The driver keeps the FingerSpreadTuner defaults tuned on its hands (its
        // config, Player.json, also has finger spread baked into the T-pose); the racers share values tuned on the cat.
        private static readonly (string Label, string AssetPath)[] Characters =
        {
            ("Driver", KartingPath + "Art/Models/Player.FBX"),
            ("Cat", KartingPath + "AddOns/MgKarting_CatRacer/Prefabs/CatRacer_Body.prefab"),
            ("Chicken", KartingPath + "AddOns/MgKarting_ChickenRacer/Prefabs/ChickenRacer_Body.prefab"),
            ("Knight", KartingPath + "AddOns/MgKarting_KnightRacer/Prefabs/KnightRacer_Body.prefab")
        };

        // Interaction rig parts the scene doesn't want: its ghost hands and controller models would draw over the
        // avatar's own hands, and its thumbstick teleport would fight the left-stick walking.
        private static readonly string[] HiddenInteractionParts =
        {
            "OVRHandVisualLeft", "OVRHandVisualRight", "OVRControllerVisualLeft", "OVRControllerVisualRight",
            "LocomotionControllerInteractorGroup", "MicroGesturesLocomotionHandInteractorGroup"
        };

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

            var models = new GameObject[Characters.Length];
            for (var i = 0; i < Characters.Length; i++)
            {
                models[i] = AssetDatabase.LoadAssetAtPath<GameObject>(Characters[i].AssetPath);
                if (models[i] == null)
                {
                    Report("Character model not found at " + Characters[i].AssetPath);
                    return;
                }

                if (!EnsureRetargetingConfig(models[i]))
                {
                    return;
                }
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateEnvironment();
            var rig = CreateCameraRig();
            AddInteractionRig(rig);

            var mirrorSpace = CreateMirrorSpace();
            var lineup = new GameObject("CharacterLineup").AddComponent<CharacterLineup>();
            var players = new GameObject[Characters.Length];
            var reflections = new GameObject[Characters.Length];
            var labels = new TMPro.TMP_Text[Characters.Length];
            for (var i = 0; i < Characters.Length; i++)
            {
                var name = i == 0 ? "PlayerCharacter" : "PlayerCharacter_" + Characters[i].Label;
                var body = CreatePlayerCharacter(models[i], rig, name);
                if (body == null)
                {
                    return;
                }

                // The whole instance (head and accessories included) is what the lineup switches on and off.
                players[i] = body.transform.root.gameObject;
                reflections[i] = CreateReflection(models[i], body.transform, mirrorSpace,
                    name.Replace("PlayerCharacter", "PlayerReflection"));
                labels[i] = CreateLineupOption(models[i], Characters[i].Label, i, lineup);
            }

            // Deactivated last: the mirror's bone pairing above needs the characters active.
            for (var i = 1; i < Characters.Length; i++)
            {
                players[i].SetActive(false);
                reflections[i].SetActive(false);
            }

            SetLineupChoices(lineup, players, reflections, labels);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Report("Could not save " + ScenePath);
                return;
            }

            Report("Built " + ScenePath + ".");
        }

        /// <summary>
        /// The cat racer prefab renames its armature node, which the model's humanoid avatar looks up by its FBX
        /// name, so the Animator resolves no bones (and the finger spread tuner finds no fingers). Gives every
        /// renamed object under the rigged body its FBX name back.
        /// </summary>
        private static void RestoreModelNames(GameObject body)
        {
            foreach (var part in body.GetComponentsInChildren<Transform>(true))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(part);
                if (part != body.transform && source != null && source.name != part.name)
                {
                    part.name = source.name;
                }
            }

            var animator = body.GetComponent<Animator>();
            if (animator != null)
            {
                animator.Rebind();
            }
        }

        /// <summary>
        /// The rigged model inside a character asset: the asset itself for a bare FBX, or the FBX instance that the
        /// racer prefabs nest under a plain root alongside their head and accessories.
        /// </summary>
        private static GameObject FindBody(GameObject character)
        {
            var animator = character.GetComponentInChildren<Animator>(true);
            return animator != null ? animator.gameObject : null;
        }

        /// <summary>
        /// The racer FBXs (Blender exports) put an armature node rotated -90° on X between the body and its root
        /// bone, which the root bone cancels with +90°. The retargeter writes the root bone's rotation as if its
        /// parent were the body, dropping that +90° and laying the whole skeleton on its back, so zero the armature
        /// node's rotation while keeping the root bone's world rotation. Skinning follows the bones, so the mesh is
        /// unchanged. The racers' karting animator controller also goes: it writes the armature node's -90° back
        /// every frame, and the retargeter doesn't need it.
        /// </summary>
        private static void FlattenArmatureRotation(GameObject body)
        {
            var animator = body.GetComponent<Animator>();
            var hips = animator != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;

            var rootBone = hips != null ? hips.parent : null;
            var armature = rootBone != null ? rootBone.parent : null;
            if (armature == null || armature == body.transform)
            {
                return;
            }

            animator.runtimeAnimatorController = null;
            var rootRotation = rootBone.rotation;
            armature.localRotation = Quaternion.identity;
            rootBone.rotation = rootRotation;
        }

        /// <summary>
        /// Freezes a lineup display in the T-pose from its retargeting config instead of the racers' karting
        /// animation. The config's T-pose rotations are relative to the rigged body.
        /// </summary>
        private static void PoseInTPose(GameObject display, GameObject model)
        {
            var body = FindBody(display);
            var fbx = PrefabUtility.GetCorrespondingObjectFromOriginalSource(FindBody(model));
            var config = AssetDatabase.LoadAssetAtPath<TextAsset>(
                Path.ChangeExtension(AssetDatabase.GetAssetPath(fbx), ".json"));
            if (body == null || config == null)
            {
                return;
            }

            body.GetComponent<Animator>().enabled = false;

            var bones = new System.Collections.Generic.Dictionary<string, Transform>();
            foreach (var bone in body.GetComponentsInChildren<Transform>(true))
            {
                bones.TryAdd(bone.name, bone);
            }

            // Joints are listed parents first, so setting world rotations in order keeps each child's result.
            foreach (var joint in ((JObject)JObject.Parse(config.text)["target"]["tpose"]).Properties())
            {
                if (!bones.TryGetValue(joint.Name, out var bone))
                {
                    continue;
                }

                var rotation = joint.Value["rotation"];
                bone.rotation = body.transform.rotation * new Quaternion((float)rotation["x"], (float)rotation["y"],
                    (float)rotation["z"], (float)rotation["w"]);
            }
        }

        /// <summary>
        /// Runs the Movement SDK's headless retargeting setup on the character's rigged FBX unless its config JSON
        /// already exists next to it. The FBX rather than the racer prefab: the setup overflows its stack on the
        /// prefabs' extra head and accessory objects. Returns false (after reporting) if no config could be produced.
        /// </summary>
        private static bool EnsureRetargetingConfig(GameObject character)
        {
            var body = FindBody(character);
            var fbx = body != null ? PrefabUtility.GetCorrespondingObjectFromOriginalSource(body) : null;
            var fbxPath = fbx != null ? AssetDatabase.GetAssetPath(fbx) : null;
            if (string.IsNullOrEmpty(fbxPath))
            {
                Report("No rigged model found in " + AssetDatabase.GetAssetPath(character));
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<TextAsset>(Path.ChangeExtension(fbxPath, ".json")) != null)
            {
                return true;
            }

            var metadata = MSDKUtilityEditor.RunDefaultRetargetingSetup(AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath));
            if (metadata == null || metadata.ConfigJson == null)
            {
                Report("Movement SDK retargeting setup failed for " + fbxPath + "; see the Console.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Adds the Interaction SDK rig (the same one the kart uses) so the controllers cast rays at the lineup.
        /// </summary>
        private static void AddInteractionRig(GameObject rig)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InteractionRigPrefabPath);
            if (rig == null || prefab == null)
            {
                Debug.LogWarning("[CharacterTestSceneBuilder] Interaction rig not added; the lineup can't be pointed at.");
                return;
            }

            var interaction = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rig.transform);

            // OVRCameraRigRef lives in Oculus.Interaction.OVR, which the editor assembly doesn't reference.
            foreach (var behaviour in interaction.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Name == "OVRCameraRigRef")
                {
                    var serializedRef = new SerializedObject(behaviour);
                    serializedRef.FindProperty("_ovrCameraRig").objectReferenceValue = rig.GetComponent<OVRCameraRig>();
                    serializedRef.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            foreach (var part in interaction.GetComponentsInChildren<Transform>(true))
            {
                if (Array.IndexOf(HiddenInteractionParts, part.name) >= 0)
                {
                    part.gameObject.SetActive(false);
                }
            }
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
        /// A character the player embodies, at the tracking-space origin, with CharacterRetargeter and
        /// MetaSourceDataProvider added by the Movement SDK's own setup, plus a head-and-hands driver that takes over
        /// in the Meta XR Simulator (which replays a fixed body pose instead of tracking the user). Returns the rigged
        /// body, which carries all of these; for the racer prefabs it's a child of the instance root.
        /// </summary>
        private static GameObject CreatePlayerCharacter(GameObject model, GameObject rig, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = name;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var character = FindBody(instance);

            RestoreModelNames(character);
            FlattenArmatureRotation(character);

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
            AddFingerSpreadTuner(character, character != instance);
            AddJoystickWalking(character, rig);
            AddSimulatorDriver(character, rig, retargeter);
            return character;
        }

        /// <summary>
        /// Left-stick walking: the Movement SDK's AI Motion Synthesizer generates the legs from joystick input while
        /// body tracking keeps driving the upper body, and the character's root follows the camera rig, which
        /// <see cref="JoystickRigMover"/> slides at the same velocity the legs walk at.
        /// </summary>
        private static void AddJoystickWalking(GameObject character, GameObject rig)
        {
            var sourceData = character.GetComponent<MetaSourceDataProvider>();
            if (rig == null || sourceData == null)
            {
                return;
            }

            var joystick = character.AddComponent<AIMotionSynthesizerJoystickInput>();
            var serializedJoystick = new SerializedObject(joystick);
            // Push forward to walk where you're looking.
            serializedJoystick.FindProperty("_referenceTransform").objectReferenceValue =
                rig.transform.Find("TrackingSpace/CenterEyeAnchor");
            serializedJoystick.ApplyModifiedPropertiesWithoutUndo();

            var serializedSource = new SerializedObject(sourceData);
            serializedSource.FindProperty("_enableAIMotionSynthesizer").boolValue = true;
            var config = serializedSource.FindProperty("_aiMotionSynthesizerConfig");
            config.FindPropertyRelative("Config").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>(SynthesizerDataPath + "AIMotionSynthesizerSkeletonData.json");
            config.FindPropertyRelative("ModelAsset").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>(SynthesizerDataPath + "AIMotionSynthesizerModel.bytes");
            config.FindPropertyRelative("GuidanceAsset").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>(SynthesizerDataPath + "AIMotionSynthesizerGuidance.bytes");
            config.FindPropertyRelative("InputProvider").objectReferenceValue = joystick;
            config.FindPropertyRelative("RootMotionMode").intValue = (int)RootMotionMode.ApplyFromReference;
            config.FindPropertyRelative("ReferenceTransform").objectReferenceValue = rig.transform;
            config.FindPropertyRelative("UpperBodySource").intValue =
                (int)MSDKAIMotionSynthesizer.PoseSource.BodyTracking;
            config.FindPropertyRelative("LowerBodySource").intValue =
                (int)MSDKAIMotionSynthesizer.PoseSource.AIMotionSynthesizer;
            config.FindPropertyRelative("BlendMode").intValue = (int)BlendMode.Input;
            serializedSource.ApplyModifiedPropertiesWithoutUndo();

            var mover = character.AddComponent<JoystickRigMover>();
            var serializedMover = new SerializedObject(mover);
            serializedMover.FindProperty("_rig").objectReferenceValue = rig.transform;
            serializedMover.FindProperty("_inputProvider").objectReferenceValue = joystick;
            serializedMover.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The driver starts from the script defaults, which were tuned on its hands; the racers (whose rigged body
        /// is a child of the prefab root) start from the values tuned on the cat. Each character has its own tuner,
        /// so adjusting one in Play mode doesn't affect the others.
        /// </summary>
        private static void AddFingerSpreadTuner(GameObject character, bool isRacer)
        {
            var tuner = character.AddComponent<FingerSpreadTuner>();
            var serializedTuner = new SerializedObject(tuner);
            serializedTuner.FindProperty("_animator").objectReferenceValue = character.GetComponent<Animator>();
            if (isRacer)
            {
                serializedTuner.FindProperty("_index").floatValue = RacerIndexSpread;
                serializedTuner.FindProperty("_indexOffset").floatValue = RacerIndexOffset;
            }

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

        private static Transform CreateMirrorSpace()
        {
            // Reflecting across the plane z = MirrorDistance maps z to 2 * MirrorDistance - z.
            var mirrorSpace = new GameObject("MirrorSpace").transform;
            mirrorSpace.position = new Vector3(0f, 0f, 2f * MirrorDistance);
            mirrorSpace.localScale = new Vector3(1f, 1f, -1f);

            // Backdrop behind the reflection so the mirror reads as a framed surface rather than open space.
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "MirrorBackdrop";
            backdrop.transform.position = new Vector3(0f, 1.25f, 2f * MirrorDistance + 1f);
            backdrop.transform.localScale = new Vector3(2.5f, 2.5f, 1f);
            UnityEngine.Object.DestroyImmediate(backdrop.GetComponent<Collider>());
            return mirrorSpace;
        }

        private static GameObject CreateReflection(GameObject model, Transform playerBody, Transform mirrorSpace,
            string name)
        {
            var reflection = (GameObject)PrefabUtility.InstantiatePrefab(model, mirrorSpace);
            reflection.name = name;
            RestoreModelNames(FindBody(reflection));
            FlattenArmatureRotation(FindBody(reflection));

            var mirror = FindBody(reflection).AddComponent<MirrorTransforms>();
            var serializedMirror = new SerializedObject(mirror);
            serializedMirror.FindProperty("_target").objectReferenceValue = playerBody;
            serializedMirror.ApplyModifiedPropertiesWithoutUndo();
            typeof(MirrorTransforms)
                .GetMethod("FindBonePairs", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(mirror, null);
            EditorUtility.SetDirty(mirror);
            return reflection;
        }

        /// <summary>
        /// One standing model in the lineup arc on the player's right, on a pedestal with its name above it, that a
        /// controller ray can select. Returns the name label so the lineup can highlight the current choice.
        /// </summary>
        private static TMPro.TMP_Text CreateLineupOption(GameObject model, string label, int index,
            CharacterLineup lineup)
        {
            var angle = (LineupFirstAngle + LineupAngleStep * index) * Mathf.Deg2Rad;
            var position = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * LineupRadius;

            var option = new GameObject("LineupOption_" + label).transform;
            option.SetParent(lineup.transform, false);
            option.SetPositionAndRotation(position, Quaternion.LookRotation(-position, Vector3.up));

            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.SetParent(option, false);
            pedestal.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            pedestal.transform.localScale = new Vector3(0.6f, 0.05f, 0.6f);
            UnityEngine.Object.DestroyImmediate(pedestal.GetComponent<Collider>());

            var display = (GameObject)PrefabUtility.InstantiatePrefab(model, option);
            display.name = "Display";
            display.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.1f, 0f), Quaternion.identity);
            RestoreModelNames(FindBody(display));
            PoseInTPose(display, model);

            // The ray targets a box around the whole model.
            var bounds = new Bounds(option.position + Vector3.up, Vector3.zero);
            foreach (var renderer in display.GetComponentsInChildren<Renderer>())
            {
                bounds.Encapsulate(renderer.bounds);
            }

            var box = option.gameObject.AddComponent<BoxCollider>();
            box.center = option.InverseTransformPoint(bounds.center);
            box.size = new Vector3(Mathf.Max(bounds.size.x, 0.5f), bounds.size.y, Mathf.Max(bounds.size.z, 0.5f));

            var surface = option.gameObject.AddComponent<Oculus.Interaction.Surfaces.ColliderSurface>();
            var serializedSurface = new SerializedObject(surface);
            serializedSurface.FindProperty("_collider").objectReferenceValue = box;
            serializedSurface.ApplyModifiedPropertiesWithoutUndo();

            var interactable = option.gameObject.AddComponent<Oculus.Interaction.RayInteractable>();
            var serializedInteractable = new SerializedObject(interactable);
            serializedInteractable.FindProperty("_surface").objectReferenceValue = surface;
            serializedInteractable.ApplyModifiedPropertiesWithoutUndo();

            var choice = option.gameObject.AddComponent<CharacterLineupOption>();
            var serializedChoice = new SerializedObject(choice);
            serializedChoice.FindProperty("_lineup").objectReferenceValue = lineup;
            serializedChoice.FindProperty("_index").intValue = index;
            serializedChoice.FindProperty("_interactable").objectReferenceValue = interactable;
            serializedChoice.ApplyModifiedPropertiesWithoutUndo();

            var text = new GameObject("Label").AddComponent<TMPro.TextMeshPro>();
            text.transform.SetParent(option, false);
            // TextMeshPro reads from its -Z side; the option's forward faces the player.
            text.transform.SetLocalPositionAndRotation(new Vector3(0f, bounds.max.y + 0.15f, 0f),
                Quaternion.Euler(0f, 180f, 0f));
            text.text = label;
            text.fontSize = 2f;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(1.5f, 0.4f);
            return text;
        }

        private static void SetLineupChoices(CharacterLineup lineup, GameObject[] players, GameObject[] reflections,
            TMPro.TMP_Text[] labels)
        {
            var serializedLineup = new SerializedObject(lineup);
            var choices = serializedLineup.FindProperty("_choices");
            choices.arraySize = players.Length;
            for (var i = 0; i < players.Length; i++)
            {
                var choice = choices.GetArrayElementAtIndex(i);
                choice.FindPropertyRelative("Player").objectReferenceValue = players[i];
                choice.FindPropertyRelative("Reflection").objectReferenceValue = reflections[i];
                choice.FindPropertyRelative("Label").objectReferenceValue = labels[i];
            }

            serializedLineup.FindProperty("_selected").intValue = 0;
            serializedLineup.ApplyModifiedPropertiesWithoutUndo();
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
