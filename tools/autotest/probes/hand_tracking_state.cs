// Reports what the game sees from hand tracking (e.g. Simulator webcam hands): active input,
// per-hand tracking/confidence/pinch, and whether hand visuals are rendering.
var sb = new System.Text.StringBuilder();
sb.Append("active=" + OVRInput.GetActiveController() + " connected=" + OVRInput.GetConnectedControllers()
    + " handTrackingEnabled=" + OVRPlugin.GetHandTrackingEnabled());
foreach (var h in UnityEngine.Object.FindObjectsByType<OVRHand>(UnityEngine.FindObjectsInactive.Include))
{
    sb.Append(" | " + h.name + "(" + h.GetHand() + ") active=" + h.isActiveAndEnabled + " tracked=" + h.IsTracked
        + " valid=" + h.IsDataValid + " conf=" + h.HandConfidence
        + " pinchIdx=" + h.GetFingerPinchStrength(OVRHand.HandFinger.Index).ToString("F2")
        + " pos=" + h.transform.position.ToString("F2"));
}
var shown = new System.Collections.Generic.List<string>();
foreach (var r in UnityEngine.Object.FindObjectsByType<UnityEngine.SkinnedMeshRenderer>(UnityEngine.FindObjectsInactive.Exclude))
    if (r.enabled && r.name.ToLower().Contains("hand")) shown.Add(r.transform.parent.name + "/" + r.name);
sb.Append(" | visibleHandMeshes=" + (shown.Count == 0 ? "none" : string.Join(",", shown)));
return sb.ToString();
