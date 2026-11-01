info = json.loads(text(call("openxr_get_session_info"))); h = info["session"]["handle"]
for hand in ("right", "left"):
    for c in ("Trigger", "Grip"): call("openxr_set_controller_input", session_handle=h, hand=hand, component=c, value=0.0, auto_release=False)
log(text(call("openxr_release_input_devices", session_handle=h))[:200])
