var k = UnityEngine.Object.FindFirstObjectByType<KartGame.KartSystems.ArcadeKart>();
var wi = UnityEngine.Object.FindFirstObjectByType<XrRacing.Gameplay.Input.XRWheelInput>();
var tl = UnityEngine.Object.FindFirstObjectByType<XrRacing.Gameplay.Tracks.TrackLoader>();
var g = UnityEngine.Object.FindFirstObjectByType<Oculus.Interaction.Grabbable>();
var d = wi.GenerateInput();
return "paused=" + UnityEditor.EditorApplication.isPaused + " t=" + UnityEngine.Time.time.ToString("F1") + " timeScale=" + UnityEngine.Time.timeScale
 + " locked=" + XrRacing.Gameplay.Race.RaceManager.IsControlsLocked + " menuOpen=" + XrRacing.Gameplay.UI.DriverSettingsMenu.IsOpen
 + " loading=" + (tl != null && tl.IsLoading) + " kinematic=" + k.Rigidbody.isKinematic + " kartEnabled=" + k.enabled
 + " wheelInput(acc=" + d.Accelerate.ToString("F2") + ",turn=" + d.TurnInput.ToString("F2") + ") kartInput(acc=" + k.Input.Accelerate.ToString("F2") + ",turn=" + k.Input.TurnInput.ToString("F2") + ")"
 + " rawTrigR=" + OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch).ToString("F2")
 + " selecting=" + g.SelectingPointsCount + " speed=" + k.Rigidbody.linearVelocity.magnitude.ToString("F2");
