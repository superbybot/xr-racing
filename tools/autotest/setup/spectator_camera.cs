var camGo = UnityEngine.GameObject.Find("SpectatorCamera");
if (camGo == null)
{
    camGo = new UnityEngine.GameObject("SpectatorCamera");
    var cam = camGo.AddComponent<UnityEngine.Camera>();
    cam.depth = 100; // render on top of VR camera in Game View
}
// Positioned at side-front to capture both the character's hands and the mirror reflection
camGo.transform.position = new UnityEngine.Vector3(-1.2f, 1.25f, 1.2f);
camGo.transform.LookAt(new UnityEngine.Vector3(0f, 1.15f, 1.5f));
return "SpectatorCamera set up at " + camGo.transform.position + " looking at (0, 1.15, 1.5)";
