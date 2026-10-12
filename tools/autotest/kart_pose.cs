var k = UnityEngine.Object.FindFirstObjectByType<KartGame.KartSystems.ArcadeKart>();
if (k == null || !UnityEngine.Application.isPlaying) return "none";
var p = k.transform.position;
return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F2},{1:F2},{2:F2},{3:F1},{4:F2},{5:F1}",
    p.x, p.y, p.z, k.transform.eulerAngles.y, k.Rigidbody.linearVelocity.magnitude, UnityEngine.Time.time);
