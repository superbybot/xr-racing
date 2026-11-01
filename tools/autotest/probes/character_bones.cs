var go = UnityEngine.GameObject.Find("PlayerCharacter");
if (go == null) return "PlayerCharacter not found";
var sb = new System.Text.StringBuilder();
foreach (var t in go.GetComponentsInChildren<UnityEngine.Transform>())
{
    sb.AppendLine(t.name);
}
return sb.ToString();
