using System;
using System.Collections.Generic;
using System.Linq;
using KartGame.AI;
using KartGame.KartSystems;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using XrRacing.Gameplay.Input;
using XrRacing.Gameplay.Tracks;
using XrRacing.Gameplay.UI;
using XrRacing.Gameplay.Vehicle;

namespace XrRacing.Editor.Tracks
{
    /// <summary>
    /// Splits the tracks out of the driving scene into their own scenes, loaded additively by TrackLoader:
    /// one scene per training track prefab plus an environment-only copy of the Karting PhysicsPlayground.
    /// Each gets a TrackInfo (name, PlayerSpawn, checkpoints). The driving scene gets a TrackLoader, loses its
    /// track instances, and all scenes are put in the App build profile. Safe to re-run: it rebuilds the track
    /// scenes and replaces the TrackLoader.
    /// </summary>
    public static class TrackScenesBuilder
    {
        private const string CoreScenePath = "Assets/Gameplay/Scenes/DrivingPrototype_VR.unity";
        private const string TrackSceneFolder = "Assets/Gameplay/Scenes/Tracks";
        private const string TrainingTrackFolder = "Assets/References/Karting Reference/Prefabs/TrainingTracks/";
        private const string PlaygroundSourcePath = "Assets/References/Karting Reference/Scenes/GameplayGyms/PhysicsPlayground.unity";
        private const string BuildProfileName = "App";
        // Track loaded on first launch (before the player has picked one).
        private const string DefaultTrackName = "Playground";

        private static readonly (string Name, string Prefab)[] TrainingTracks =
        {
            ("Oval", "OvalTrack_Training.prefab"),
            ("Winding", "WindingTrack_Training.prefab"),
            ("Country", "CountryTrack_Training.prefab"),
            ("Mountain", "MountainTrack_Training.prefab"),
        };

        // Playground objects that belong to the Karting game itself rather than its environment.
        private static readonly string[] PlaygroundRootsToRemove =
        {
            "NewKartClassic_Player", "AdditionalInGameData", "CinemachineVirtualCamera", "EventSystem", "MainCamera",
            "BackgroundMusic",
        };

        [MenuItem("XR Racing/Build Track Scenes")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Report("Exit Play mode first.");
                return;
            }

            Scene core = SceneManager.GetActiveScene();
            if (core.path != CoreScenePath || core.isDirty || SceneManager.sceneCount != 1)
            {
                Report($"Open only {CoreScenePath}, saved, then run this again.");
                return;
            }

            try
            {
                ArcadeKart kart = FindInScene<ArcadeKart>(core);
                if (kart == null)
                {
                    Report("No ArcadeKart (the player kart) in the driving scene.");
                    return;
                }

                float kartHeight = MeasureHeightAboveGround(kart.transform);
                LightSettings sun = ReadPlaygroundSun();

                if (!AssetDatabase.IsValidFolder(TrackSceneFolder))
                {
                    AssetDatabase.CreateFolder("Assets/Gameplay/Scenes", "Tracks");
                }

                var entries = new List<TrackLoader.TrackEntry>();
                foreach ((string name, string prefab) in TrainingTracks)
                {
                    entries.Add(BuildTrainingTrack(name, TrainingTrackFolder + prefab, sun, kartHeight));
                }

                entries.Add(BuildPlayground(kartHeight));

                SetUpCoreScene(core, kart, entries);
                SetBuildScenes(entries);

                EditorSceneManager.SaveScene(core);
                Debug.Log($"[TrackScenesBuilder] Built {entries.Count} track scenes in {TrackSceneFolder}.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Report($"Build failed: {e.Message}");
            }
        }

        private static TrackLoader.TrackEntry BuildTrainingTrack(string name, string prefabPath, LightSettings sun, float kartHeight)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Track prefab not found: {prefabPath}");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var track = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            track.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            sun.Create(scene);
            Physics.SyncTransforms();

            DebugCheckpointRay checkpointList = track.GetComponent<DebugCheckpointRay>();
            Collider[] checkpoints = checkpointList != null && checkpointList.Colliders != null
                ? checkpointList.Colliders.Where(c => c != null).ToArray()
                : new Collider[0];

            Pose spawn = checkpoints.Length > 0
                ? SpawnFromCheckpoint(checkpoints[0].transform, scene, kartHeight)
                : new Pose(Vector3.up * kartHeight, Quaternion.identity);

            CreateTrackInfo(scene, name, spawn, checkpoints);
            return SaveTrackScene(scene, name);
        }

        // Environment-only copy of the Karting PhysicsPlayground, so its baked lighting and post-processing carry over.
        private static TrackLoader.TrackEntry BuildPlayground(float kartHeight)
        {
            const string name = "Playground";
            string path = ScenePath(name);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            if (!AssetDatabase.CopyAsset(PlaygroundSourcePath, path))
            {
                throw new InvalidOperationException($"Couldn't copy {PlaygroundSourcePath}.");
            }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            // The Playground's own kart marks where driving starts; keep its spot for our spawn.
            Pose kartStart = new Pose(Vector3.up * kartHeight, Quaternion.identity);
            ArcadeKart playgroundKart = FindInScene<ArcadeKart>(scene);
            if (playgroundKart != null)
            {
                kartStart = new Pose(playgroundKart.transform.position, playgroundKart.transform.rotation);
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (PlaygroundRootsToRemove.Contains(root.name) || IsGameplayOnly(root))
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                // Karting game scripts that weren't ported; prefab instances can't drop components here, so skip them.
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                    {
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                    }
                }
            }

            Physics.SyncTransforms();
            Pose spawn = SnapToGround(kartStart.position, kartStart.forward, scene, kartHeight);
            CreateTrackInfo(scene, name, spawn, new Collider[0]);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
            return new TrackLoader.TrackEntry { displayName = name, scenePath = path };
        }

        // Anything that would clash with the persistent driving scene: other karts, cameras, listeners, event systems.
        private static bool IsGameplayOnly(GameObject root)
        {
            return root.GetComponentInChildren<ArcadeKart>(true) != null
                || root.GetComponentInChildren<Camera>(true) != null
                || root.GetComponentInChildren<AudioListener>(true) != null
                || root.GetComponentInChildren<EventSystem>(true) != null;
        }

        private static void CreateTrackInfo(Scene scene, string name, Pose spawn, Collider[] checkpoints)
        {
            var infoObject = new GameObject("TrackInfo");
            SceneManager.MoveGameObjectToScene(infoObject, scene);
            TrackInfo info = infoObject.AddComponent<TrackInfo>();

            var spawnObject = new GameObject("PlayerSpawn");
            spawnObject.transform.SetParent(infoObject.transform, false);
            spawnObject.transform.SetPositionAndRotation(spawn.position, spawn.rotation);

            var serialized = new SerializedObject(info);
            serialized.FindProperty("displayName").stringValue = name;
            serialized.FindProperty("playerSpawn").objectReferenceValue = spawnObject.transform;
            SerializedProperty list = serialized.FindProperty("checkpoints");
            list.arraySize = checkpoints.Length;
            for (int i = 0; i < checkpoints.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = checkpoints[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TrackLoader.TrackEntry SaveTrackScene(Scene scene, string name)
        {
            string path = ScenePath(name);
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new InvalidOperationException($"Couldn't save {path}.");
            }

            EditorSceneManager.CloseScene(scene, true);
            return new TrackLoader.TrackEntry { displayName = name, scenePath = path };
        }

        // Checkpoints face along the track (see DebugCheckpointRay), so the first one gives position and direction.
        private static Pose SpawnFromCheckpoint(Transform checkpoint, Scene scene, float kartHeight)
        {
            return SnapToGround(checkpoint.position, checkpoint.forward, scene, kartHeight);
        }

        private static Pose SnapToGround(Vector3 position, Vector3 forward, Scene scene, float kartHeight)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            Quaternion rotation = flatForward.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(flatForward.normalized, Vector3.up)
                : Quaternion.identity;

            // Only this track's colliders: the driving scene may still hold other tracks at this spot.
            RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * 3f, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit? ground = hits.Where(h => h.collider.gameObject.scene == scene)
                .OrderBy(h => h.distance)
                .Select(h => (RaycastHit?)h)
                .FirstOrDefault();

            Vector3 spawnPosition = ground.HasValue ? ground.Value.point + Vector3.up * kartHeight : position;
            return new Pose(spawnPosition, rotation);
        }

        // How high the kart's origin sits above the ground under it now, so spawns keep the same clearance.
        private static float MeasureHeightAboveGround(Transform kart)
        {
            const float fallback = 0.3f;
            Physics.SyncTransforms();
            RaycastHit[] hits = Physics.RaycastAll(kart.position + Vector3.up * 2f, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits.OrderBy(h => h.distance))
            {
                if (!hit.collider.transform.IsChildOf(kart))
                {
                    return Mathf.Clamp(kart.position.y - hit.point.y, 0f, 2f);
                }
            }

            return fallback;
        }

        private static void SetUpCoreScene(Scene core, ArcadeKart kart, List<TrackLoader.TrackEntry> entries)
        {
            // The tracks now live in their own scenes.
            var trackPrefabPaths = new HashSet<string>(TrainingTracks.Select(t => TrainingTrackFolder + t.Prefab));
            foreach (GameObject root in core.GetRootGameObjects())
            {
                if (PrefabUtility.IsAnyPrefabInstanceRoot(root) &&
                    trackPrefabPaths.Contains(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root)))
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            foreach (TrackLoader old in FindAllInScene<TrackLoader>(core))
            {
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }

            var loaderObject = new GameObject("TrackLoader");
            SceneManager.MoveGameObjectToScene(loaderObject, core);
            TrackLoader loader = loaderObject.AddComponent<TrackLoader>();

            var serialized = new SerializedObject(loader);
            SerializedProperty tracks = serialized.FindProperty("tracks");
            tracks.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                SerializedProperty entry = tracks.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("displayName").stringValue = entries[i].displayName;
                entry.FindPropertyRelative("scenePath").stringValue = entries[i].scenePath;
            }

            serialized.FindProperty("defaultTrack").intValue = Math.Max(0, entries.FindIndex(e => e.displayName == DefaultTrackName));
            serialized.FindProperty("kart").objectReferenceValue = kart;
            serialized.FindProperty("fader").objectReferenceValue = FindInScene<DriverSeatAdjuster>(core);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SetReference(FindInScene<XRWheelInput>(core), "trackLoader", loader);
            SetReference(FindInScene<DriverSettingsMenu>(core), "trackLoader", loader);
            EditorSceneManager.MarkSceneDirty(core);
        }

        private static void SetBuildScenes(List<TrackLoader.TrackEntry> entries)
        {
            EditorBuildSettingsScene[] scenes = new[] { CoreScenePath }
                .Concat(entries.Select(e => e.scenePath))
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToArray();

            EditorBuildSettings.scenes = scenes;

            BuildProfile profile = BuildProfile.GetAllBuildProfiles().FirstOrDefault(p => p.name == BuildProfileName);
            if (profile == null)
            {
                Debug.LogWarning($"[TrackScenesBuilder] No '{BuildProfileName}' build profile; only Build Settings were updated.");
                return;
            }

            profile.overrideGlobalScenes = true;
            profile.scenes = scenes;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            if (target == null)
            {
                return;
            }

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property != null)
            {
                property.objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static LightSettings ReadPlaygroundSun()
        {
            Scene playground = EditorSceneManager.OpenScene(PlaygroundSourcePath, OpenSceneMode.Additive);
            try
            {
                Light sun = FindAllInScene<Light>(playground).FirstOrDefault(l => l.type == LightType.Directional);
                return sun != null ? LightSettings.From(sun) : LightSettings.Default;
            }
            finally
            {
                EditorSceneManager.CloseScene(playground, true);
            }
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            return FindAllInScene<T>(scene).FirstOrDefault();
        }

        private static IEnumerable<T> FindAllInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
        }

        private static string ScenePath(string trackName)
        {
            return $"{TrackSceneFolder}/Track_{trackName}.unity";
        }

        private static void Report(string message)
        {
            if (Application.isBatchMode)
            {
                Debug.LogError("[TrackScenesBuilder] " + message);
            }
            else
            {
                EditorUtility.DisplayDialog("Build Track Scenes", message, "OK");
            }
        }

        // The Playground's sun, re-created in the training track scenes (they have no light of their own).
        private readonly struct LightSettings
        {
            private readonly Quaternion _rotation;
            private readonly Color _color;
            private readonly float _intensity;
            private readonly LightShadows _shadows;

            private LightSettings(Quaternion rotation, Color color, float intensity, LightShadows shadows)
            {
                _rotation = rotation;
                _color = color;
                _intensity = intensity;
                _shadows = shadows;
            }

            public static LightSettings Default =>
                new LightSettings(Quaternion.Euler(50f, -30f, 0f), Color.white, 1f, LightShadows.Soft);

            public static LightSettings From(Light light) =>
                new LightSettings(light.transform.rotation, light.color, light.intensity, light.shadows);

            public void Create(Scene scene)
            {
                var go = new GameObject("Sun");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.transform.rotation = _rotation;
                Light light = go.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = _color;
                light.intensity = _intensity;
                light.shadows = _shadows;
            }
        }
    }
}
