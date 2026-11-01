// Why aren't controller models drawn? Dumps OVRControllerHelper / ControllerVisual state and the model roots.
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
var sb = new System.Text.StringBuilder();
sb.Append("headset=" + OVRPlugin.GetSystemHeadsetType() + " interactionProfile L=" + OVRPlugin.GetCurrentInteractionProfile(OVRPlugin.Hand.HandLeft)
    + " R=" + OVRPlugin.GetCurrentInteractionProfile(OVRPlugin.Hand.HandRight));
foreach (var m in UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsInactive.Include))
{
    var t = m.GetType();
    if (t.Name != "OVRControllerHelper" && t.Name != "ControllerVisual") continue;
    sb.Append("\n" + t.Name + "@" + m.transform.parent.name + "/" + m.name + " enabled=" + m.isActiveAndEnabled);
    foreach (var f in t.GetFields(bf))
    {
        var v = f.GetValue(m);
        if (v == null || f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string) || v is UnityEngine.Object)
            sb.Append(" " + f.Name + "=" + (v is UnityEngine.Object o ? (o == null ? "null" : o.name) : v));
    }
    foreach (UnityEngine.Transform c in m.transform)
        sb.Append("\n    child " + c.name + " active=" + c.gameObject.activeSelf);
}
return sb.ToString();
