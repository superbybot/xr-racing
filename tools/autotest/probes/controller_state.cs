// What the game sees from the controllers, and whether anything draws them.
var sb = new System.Text.StringBuilder();
sb.Append("active=" + OVRInput.GetActiveController() + " connected=" + OVRInput.GetConnectedControllers());
foreach (var c in new[] { OVRInput.Controller.LTouch, OVRInput.Controller.RTouch })
    sb.Append(" | " + c + " pos=" + OVRInput.GetLocalControllerPosition(c).ToString("F2")
        + " posTracked=" + OVRInput.GetControllerPositionTracked(c)
        + " grip=" + OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, c).ToString("F2")
        + " trig=" + OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, c).ToString("F2"));
var g = UnityEngine.Object.FindAnyObjectByType<Oculus.Interaction.Grabbable>();
sb.Append(" | wheelSelecting=" + g.SelectingPointsCount);
sb.Append("\nvisuals:");
foreach (var r in UnityEngine.Object.FindObjectsByType<UnityEngine.Renderer>(UnityEngine.FindObjectsInactive.Include))
{
    var path = r.transform.parent != null ? r.transform.parent.name + "/" + r.name : r.name;
    var lower = path.ToLower();
    if (lower.Contains("controller") || lower.Contains("hand") || lower.Contains("touch"))
        sb.Append("\n  " + path + " drawn=" + (r.enabled && r.gameObject.activeInHierarchy));
}
var ovrVis = new System.Collections.Generic.List<string>();
foreach (var m in UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsInactive.Include))
{
    var n = m.GetType().Name;
    if (n.Contains("ControllerVisual") || n.Contains("ControllerHelper") || n == "OVRControllerPrefab" || n.Contains("HandVisual"))
        ovrVis.Add(n + "@" + m.name + (m.isActiveAndEnabled ? "" : "(off)"));
}
sb.Append("\nvisualComponents: " + (ovrVis.Count == 0 ? "none" : string.Join(", ", ovrVis)));
return sb.ToString();
