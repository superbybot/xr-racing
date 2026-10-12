var sb = new System.Text.StringBuilder();
var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("Track_Playground");
foreach (var root in scene.GetRootGameObjects())
{
    var rs = root.GetComponentsInChildren<UnityEngine.Renderer>();
    if (rs.Length == 0) { sb.AppendLine(root.name + " (no renderers) pos=" + root.transform.position.ToString("F0")); continue; }
    var b = rs[0].bounds;
    foreach (var r in rs) b.Encapsulate(r.bounds);
    sb.AppendLine(root.name + " children=" + root.transform.childCount + " center=" + b.center.ToString("F0") + " size=" + b.size.ToString("F0"));
    if (root.transform.childCount <= 40)
        foreach (UnityEngine.Transform c in root.transform)
        {
            var cr = c.GetComponentsInChildren<UnityEngine.Renderer>();
            if (cr.Length == 0) continue;
            var cb = cr[0].bounds; foreach (var r in cr) cb.Encapsulate(r.bounds);
            sb.AppendLine("   " + c.name + " center=" + cb.center.ToString("F0") + " size=" + cb.size.ToString("F0"));
        }
}
var t = UnityEngine.Object.FindFirstObjectByType<XrRacing.Gameplay.Tracks.TrackInfo>();
if (t != null && t.PlayerSpawn != null) sb.AppendLine("spawn " + t.PlayerSpawn.position.ToString("F1") + " yaw=" + t.PlayerSpawn.eulerAngles.y.ToString("F0"));
return sb.ToString();
