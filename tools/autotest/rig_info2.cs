var sb = new System.Text.StringBuilder();
System.Func<UnityEngine.Transform, string> path = null;
path = t => t.parent == null ? t.name : path(t.parent) + "/" + t.name;
foreach (var i in UnityEngine.Object.FindObjectsByType<Oculus.Interaction.HandGrab.HandGrabInteractor>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
    sb.AppendLine("HGInteractor " + path(i.transform).Replace("KartClassic_Player/[BuildingBlock] Camera Rig/[BuildingBlock] OVRComprehensiveInteractionRig/", "") + " activeInHierarchy=" + i.gameObject.activeInHierarchy + " enabled=" + i.enabled);
var l = UnityEngine.GameObject.Find("LeftControllerAnchor"); var r = UnityEngine.GameObject.Find("RightControllerAnchor");
if (l != null) sb.AppendLine("LeftControllerAnchor " + l.transform.position.ToString("F3"));
if (r != null) sb.AppendLine("RightControllerAnchor " + r.transform.position.ToString("F3"));
sb.AppendLine("activeController=" + OVRInput.GetActiveController() + " connected=" + OVRInput.GetConnectedControllers());
var ts = UnityEngine.GameObject.Find("TrackingSpace");
if (ts != null) sb.AppendLine("TrackingSpace pos=" + ts.transform.position.ToString("F3") + " rot=" + ts.transform.rotation.ToString("F4") + " euler=" + ts.transform.eulerAngles.ToString("F1"));
return sb.ToString();
