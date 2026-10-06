using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using KartGame.KartSystems;
using UnityEngine;
using UnityEngine.SceneManagement;
using XrRacing.Gameplay.Vehicle;

namespace XrRacing.Gameplay.Tracks
{
    /// <summary>
    /// Swaps track scenes under the persistent driving scene: fade out, freeze the kart, unload the old track,
    /// load the new one additively (and make it the active scene so its lighting applies), place the kart at the
    /// track's spawn, fade back in. Remembers the last track. RequestRespawn puts the kart back at the spawn the
    /// same way. Built by "XR Racing/Build Track Scenes".
    /// RequestTrack is the local "which track" entry point; a networked session can call LoadTrackAsync after
    /// Fusion has agreed on the track instead.
    /// </summary>
    public class TrackLoader : MonoBehaviour
    {
        [Serializable]
        public class TrackEntry
        {
            [Tooltip("Name shown in the driver menu.")]
            public string displayName;
            [Tooltip("Project path of the track scene. It must be in the App build profile's scene list.")]
            public string scenePath;
        }

        private const string LastTrackKey = "xr-racing.track.last";

        [Tooltip("Tracks in menu order.")]
        [SerializeField] private TrackEntry[] tracks = new TrackEntry[0];
        [Tooltip("The player's kart; frozen during a switch and moved to the new track's spawn.")]
        [SerializeField] private ArcadeKart kart;
        [Tooltip("Provides the fade to black around a switch (the same fade as Recenter Seat).")]
        [SerializeField] private DriverSeatAdjuster fader;
        [Tooltip("Track loaded on first launch (index into Tracks).")]
        [SerializeField, Min(0)] private int defaultTrack;
        [Tooltip("Start on the last track the player picked instead of the default.")]
        [SerializeField] private bool rememberLastTrack = true;
        [Tooltip("Height of the kart's origin above the ground when it's resting on its wheels (m). The kart is placed " +
            "at this height over the ground under the spawn, so it doesn't drop onto the track.")]
        [SerializeField, Min(0f)] private float restHeight = 0.28f;

        private CancellationTokenSource _lifetime;

        public IReadOnlyList<TrackEntry> Tracks => tracks;
        public int CurrentIndex { get; private set; } = -1;
        public TrackInfo CurrentTrack { get; private set; }

        /// <summary>True during a switch; the kart ignores pedals meanwhile.</summary>
        public bool IsLoading { get; private set; }

        /// <summary>Raised with the new index once a track is loaded and the kart is on it.</summary>
        public event Action<int> TrackChanged;

        private void Awake()
        {
            _lifetime = new CancellationTokenSource();
        }

        private void Start()
        {
            if (tracks.Length == 0 || kart == null)
            {
                Debug.LogError("[TrackLoader] Needs at least one track and the player kart.", this);
                return;
            }

            int index = rememberLastTrack ? PlayerPrefs.GetInt(LastTrackKey, defaultTrack) : defaultTrack;
            LoadTrackAsync(IsValidIndex(index) ? index : 0, _lifetime.Token).Forget();
        }

        private void OnDestroy()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        /// <summary>Switch to a track (ignored while a switch is running or if it's already the current one).</summary>
        public void RequestTrack(int index)
        {
            if (IsLoading || index == CurrentIndex || !IsValidIndex(index))
            {
                return;
            }

            LoadTrackAsync(index, _lifetime.Token).Forget();
        }

        /// <summary>
        /// Put the kart back at the current track's spawn, stopped (ignored while a switch is running). Fades around
        /// it like a track switch. onMoved runs while the view is black, once the kart is in place.
        /// </summary>
        public void RequestRespawn(Action onMoved = null)
        {
            if (IsLoading || CurrentTrack == null)
            {
                return;
            }

            RespawnAsync(onMoved, _lifetime.Token).Forget();
        }

        public async UniTask RespawnAsync(Action onMoved, CancellationToken cancellationToken)
        {
            if (IsLoading || CurrentTrack == null)
            {
                return;
            }

            IsLoading = true;
            Rigidbody body = kart.GetComponent<Rigidbody>();
            bool wasKinematic = body.isKinematic;
            bool wasKartEnabled = kart.enabled;

            try
            {
                if (fader != null)
                {
                    await fader.FadeAsync(1f, cancellationToken);
                }

                // Same freeze as a track switch, so nothing (ArcadeKart, interpolation) drags the kart off the spawn.
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                kart.enabled = false;
                body.isKinematic = true;
                PlaceKart(CurrentTrack);

                // A frame in the new spot while black, so the camera rig's smoothing catches up before fading in.
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                onMoved?.Invoke();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
            finally
            {
                if (body != null)
                {
                    body.isKinematic = wasKinematic;
                    if (!wasKinematic)
                    {
                        body.linearVelocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                }

                if (kart != null)
                {
                    kart.enabled = wasKartEnabled;
                }

                IsLoading = false;
            }

            if (fader != null)
            {
                await fader.FadeAsync(0f, cancellationToken);
            }
        }

        public async UniTask LoadTrackAsync(int index, CancellationToken cancellationToken)
        {
            if (IsLoading || !IsValidIndex(index))
            {
                return;
            }

            IsLoading = true;
            Rigidbody body = kart.GetComponent<Rigidbody>();
            bool wasKinematic = body.isKinematic;
            bool wasKartEnabled = kart.enabled;

            try
            {
                if (fader != null)
                {
                    await fader.FadeAsync(1f, cancellationToken);
                }

                // Frozen so it doesn't fall while no track is loaded. ArcadeKart is paused too, since it sets the
                // body's velocity every physics step and that isn't allowed on a kinematic body.
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                kart.enabled = false;
                body.isKinematic = true;

                string path = tracks[index].scenePath;
                await UnloadOtherTracksAsync(path, cancellationToken);

                Scene scene = SceneManager.GetSceneByPath(path);
                if (!scene.isLoaded) // already open, e.g. added to the hierarchy while editing
                {
                    await SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive).ToUniTask(cancellationToken: cancellationToken);
                    scene = SceneManager.GetSceneByPath(path);
                }

                SceneManager.SetActiveScene(scene);
                CurrentTrack = FindTrackInfo(scene);
                PlaceKart(CurrentTrack);

                CurrentIndex = index;
                PlayerPrefs.SetInt(LastTrackKey, index);
                PlayerPrefs.Save();
                TrackChanged?.Invoke(index);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
            finally
            {
                if (body != null)
                {
                    body.isKinematic = wasKinematic;
                }

                if (kart != null)
                {
                    kart.enabled = wasKartEnabled;
                }

                IsLoading = false;
            }

            if (fader != null)
            {
                await fader.FadeAsync(0f, cancellationToken);
            }
        }

        private async UniTask UnloadOtherTracksAsync(string keepPath, CancellationToken cancellationToken)
        {
            bool anyUnloaded = false;
            foreach (TrackEntry track in tracks)
            {
                if (track.scenePath == keepPath)
                {
                    continue;
                }

                Scene scene = SceneManager.GetSceneByPath(track.scenePath);
                if (scene.isLoaded)
                {
                    await SceneManager.UnloadSceneAsync(scene).ToUniTask(cancellationToken: cancellationToken);
                    anyUnloaded = true;
                }
            }

            if (anyUnloaded)
            {
                await Resources.UnloadUnusedAssets().ToUniTask(cancellationToken: cancellationToken);
            }
        }

        private static TrackInfo FindTrackInfo(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                TrackInfo info = root.GetComponentInChildren<TrackInfo>(true);
                if (info != null)
                {
                    return info;
                }
            }

            Debug.LogWarning($"[TrackLoader] No TrackInfo in scene '{scene.path}'; the kart stays where it is.");
            return null;
        }

        private void PlaceKart(TrackInfo track)
        {
            if (track == null || track.PlayerSpawn == null)
            {
                return;
            }

            Transform spawn = track.PlayerSpawn;
            Vector3 position = spawn.position;

            // Set the kart down at its resting height on the ground under the spawn, so it doesn't drop and bounce.
            RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * 2f, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                if (!hit.collider.transform.IsChildOf(kart.transform) && hit.distance < nearest)
                {
                    nearest = hit.distance;
                    position.y = hit.point.y + restHeight;
                }
            }

            kart.transform.SetPositionAndRotation(position, spawn.rotation);
            Physics.SyncTransforms();
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < tracks.Length;
        }
    }
}
