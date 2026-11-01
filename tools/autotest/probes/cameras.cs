var sb = new System.Text.StringBuilder();
foreach (var c in UnityEngine.Object.FindObjectsByType<UnityEngine.Camera>(UnityEngine.FindObjectsInactive.Include))
{
    sb.AppendLine("Camera: " + c.name + " tag=" + c.tag + " enabled=" + c.enabled + " pos=" + c.transform.position + " rot=" + c.transform.rotation.eulerAngles);
}
return sb.ToString();
