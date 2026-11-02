using UnityEngine;

namespace XrRacing.Gameplay.UI
{
    /// <summary>
    /// The "MenuOverlay" layer: drawn after everything else with depth testing off (a URP Render Objects feature
    /// on the renderers, set up by "XR Racing/Build Driver Settings Menu"), so the driver menu, the pointer ray
    /// aiming at it, and the view fade are never hidden behind the kart or the track.
    /// </summary>
    public static class OverlayLayer
    {
        public const string Name = "MenuOverlay";

        /// <summary>The layer index, or -1 if the layer hasn't been set up in the project.</summary>
        public static int Index => LayerMask.NameToLayer(Name);

        /// <summary>Puts the object and all its children on the overlay layer (no-op if the layer doesn't exist).</summary>
        public static void Apply(GameObject root)
        {
            int layer = Index;
            if (layer < 0 || root == null)
            {
                return;
            }

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
        }
    }
}
