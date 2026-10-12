// Rim grab points on the wheel's real plane, as OpenXR local_floor coords (Unity tracking-local, z negated).
// Output: hub|axis|R:<angle>:x,y,z|L:<angle>:x,y,z ...  angles applied about the wheel's spin axis.
var inv = System.Globalization.CultureInfo.InvariantCulture;
var ts = UnityEngine.GameObject.Find("TrackingSpace").transform;
var g = UnityEngine.Object.FindFirstObjectByType<Oculus.Interaction.Grabbable>();
var w = g.transform;
var kart = UnityEngine.Object.FindFirstObjectByType<KartGame.KartSystems.ArcadeKart>().transform;
var axis = (w.parent != null ? w.parent.TransformDirection(w.localRotation * UnityEngine.Vector3.right) : w.right).normalized;
var hub = w.position;
var side = UnityEngine.Vector3.ProjectOnPlane(kart.right, axis).normalized;
float radius = 0.17f;
System.Func<UnityEngine.Vector3, string> O = v => { var p = ts.InverseTransformPoint(v); return string.Format(inv, "{0:F3},{1:F3},{2:F3}", p.x, p.y, -p.z); };
var sb = new System.Text.StringBuilder();
sb.Append("hub=" + O(hub) + "|axisWorld=" + axis.ToString("F2") + "|kartFwd=" + kart.forward.ToString("F2"));
for (int a = -90; a <= 90; a += 15)
{
    var q = UnityEngine.Quaternion.AngleAxis(a, axis);
    sb.Append("|R" + a + "=" + O(hub + q * side * radius));
    sb.Append("|L" + a + "=" + O(hub - q * side * radius));
}
return sb.ToString();
