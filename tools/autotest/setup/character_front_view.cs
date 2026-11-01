var rig = UnityEngine.GameObject.Find("OVRCameraRig");
if (rig != null)
{
    rig.transform.position = new UnityEngine.Vector3(-2.2f, 0f, 0.35f);
    rig.transform.rotation = UnityEngine.Quaternion.Euler(0f, 90f, 0f);
}
return "Rig moved to front view (-2.2, 0, 0.35)";
