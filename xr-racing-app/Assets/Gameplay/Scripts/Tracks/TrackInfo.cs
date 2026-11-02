using UnityEngine;

namespace XrRacing.Gameplay.Tracks
{
    /// <summary>
    /// Describes a track scene: its name, where the player kart starts, and its ordered checkpoints (for laps and
    /// AI karts). One per track scene; TrackLoader finds it after the scene loads.
    /// Built by the "XR Racing/Build Track Scenes" editor command.
    /// </summary>
    public class TrackInfo : MonoBehaviour
    {
        [Tooltip("Name shown in the driver menu.")]
        [SerializeField] private string displayName = "Track";
        [Tooltip("Where the player kart is placed when this track loads. Its forward is the starting direction.")]
        [SerializeField] private Transform playerSpawn;
        [Tooltip("The track's checkpoints in driving order (the ML-Agents training checkpoints), for laps and AI karts.")]
        [SerializeField] private Collider[] checkpoints = new Collider[0];
        [Tooltip("Number of laps to complete for this track. Defaults to 2.")]
        [SerializeField, Min(1)] private int lapsToComplete = 2;

        public string DisplayName => displayName;
        public Transform PlayerSpawn => playerSpawn;
        public Collider[] Checkpoints => checkpoints;
        public int LapsToComplete => lapsToComplete;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (playerSpawn == null)
            {
                return;
            }

            // Spawn marker: a kart-sized box with an arrow pointing the starting direction.
            Gizmos.color = Color.green;
            Gizmos.matrix = playerSpawn.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, 0.5f, 0f), new Vector3(1.2f, 1f, 2f));
            Gizmos.DrawLine(new Vector3(0f, 0.5f, 0f), new Vector3(0f, 0.5f, 2.5f));
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
