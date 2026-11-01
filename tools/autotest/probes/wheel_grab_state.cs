// Why can't a hand grab the wheel? Lists every HandGrabInteractor (state, hand connected, grab
// strength, candidate, distance to the wheel) and every HandGrabInteractable on the wheel.
var sb = new System.Text.StringBuilder();
var g = UnityEngine.Object.FindAnyObjectByType<Oculus.Interaction.Grabbable>();
var hub = g.transform.position;
sb.Append("wheel=" + g.name + " hub=" + hub.ToString("F2") + " selecting=" + g.SelectingPointsCount);
foreach (var it in UnityEngine.Object.FindObjectsByType<Oculus.Interaction.HandGrab.HandGrabInteractor>(UnityEngine.FindObjectsInactive.Include))
{
    var hand = it.Hand;
    string hs = "noHand";
    if (hand != null)
    {
        hand.GetRootPose(out var root);
        hs = "connected=" + hand.IsConnected + " tracked=" + hand.IsTrackedDataValid + " handed=" + hand.Handedness
            + " palmDistToHub=" + UnityEngine.Vector3.Distance(root.position, hub).ToString("F2")
            + " pinch=" + hand.GetFingerPinchStrength(Oculus.Interaction.Input.HandFinger.Index).ToString("F2");
    }
    sb.Append("\n  " + it.transform.parent.name + "/" + it.name + " active=" + it.isActiveAndEnabled + " state=" + it.State
        + " " + hs + " candidate=" + (it.Candidate != null ? it.Candidate.name : "none")
        + " selected=" + (it.SelectedInteractable != null ? it.SelectedInteractable.name : "none"));
}
foreach (var hi in g.GetComponentsInChildren<Oculus.Interaction.HandGrab.HandGrabInteractable>(true))
    sb.Append("\n  interactable " + hi.name + " active=" + hi.isActiveAndEnabled + " state=" + hi.State
        + " supportedGrab=" + hi.SupportedGrabTypes + " pos=" + hi.transform.position.ToString("F2")
        + " distToHub=" + UnityEngine.Vector3.Distance(hi.transform.position, hub).ToString("F2"));
return sb.ToString();
