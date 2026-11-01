var rig = UnityEngine.GameObject.Find("OVRCameraRig");
if (rig != null)
{
    rig.transform.position = new UnityEngine.Vector3(-1.8f, 0f, 1.5f);
    rig.transform.rotation = UnityEngine.Quaternion.Euler(5f, 90f, 0f);
}
var player = UnityEngine.GameObject.Find("PlayerCharacter");
if (player != null)
{
    var transforms = player.GetComponentsInChildren<UnityEngine.Transform>();
    var head = System.Array.Find(transforms, t => t.name == "Head");
    if (head != null) head.localScale = UnityEngine.Vector3.one;
}
return "Side view configured with full body visible";
