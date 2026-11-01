var mirrorSpace = UnityEngine.GameObject.Find("MirrorSpace");
if (mirrorSpace != null)
{
    mirrorSpace.transform.position = new UnityEngine.Vector3(0f, 0f, 1.4f);
}
var backdrop = UnityEngine.GameObject.Find("MirrorBackdrop");
if (backdrop != null)
{
    backdrop.transform.position = new UnityEngine.Vector3(0f, 1.2f, 1.8f);
    backdrop.transform.localScale = new UnityEngine.Vector3(3f, 3f, 1f);
}
var player = UnityEngine.GameObject.Find("PlayerCharacter");
if (player != null)
{
    var transforms = player.GetComponentsInChildren<UnityEngine.Transform>();
    var head = System.Array.Find(transforms, t => t.name == "Head");
    if (head != null) head.localScale = UnityEngine.Vector3.zero;
}
var rig = UnityEngine.GameObject.Find("OVRCameraRig");
if (rig != null)
{
    rig.transform.position = new UnityEngine.Vector3(0f, -0.15f, 0f);
    rig.transform.rotation = UnityEngine.Quaternion.Euler(12f, 0f, 0f);
}
return "Mirror shot configured at 1.4m";
