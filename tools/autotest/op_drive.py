# Drive the kart with simulated Touch controllers on the steering wheel (Meta XR Operator).
# Needs env RIM = result of `unity command eval_file --file wheel_path.cs` (taken in Play mode, kart at rest).
# Env: MAXT (seconds), OUT (capture subfolder). Headset screenshot every 10 s. Waypoints are for Track_Playground.
import os, math, time
S = os.path.dirname(__file__); MAXT = float(os.environ.get("MAXT", "150"))
parts = dict(kv.split("=", 1) for kv in os.environ["RIM"].split("|"))
def v3(s): return [float(t) for t in s.split(",")]
R = {int(k[1:]): v3(v) for k, v in parts.items() if k[0] == "R" and k[1:].lstrip("-").isdigit()}
L = {int(k[1:]): v3(v) for k, v in parts.items() if k[0] == "L" and k[1:].lstrip("-").isdigit()}
def rim(table, a):
    a = max(-90, min(90, a)); lo = int(math.floor(a / 15) * 15); hi = min(90, lo + 15); t = 0 if hi == lo else (a - lo) / 15
    return [table[lo][i] + (table[hi][i] - table[lo][i]) * t for i in range(3)]

h = json.loads(text(call("openxr_get_session_info")))["session"]["handle"]
ident = [0, 0, 0, 1]
def hands(a, dur=0.0):
    for hand, tab in (("right", R), ("left", L)):
        for pt in ("grip", "aim"):
            call("openxr_set_controller_pose", session_handle=h, hand=hand, pose_type=pt, position=rim(tab, a),
                 orientation=ident, base_space="local_floor", duration_seconds=dur)
def kart():
    t = text(call("unity_get_world_pose", path="KartClassic_Player"))
    m = json.loads(t)["matches"][0]
    p, r = m["position"], m["rotation"]
    return [p["x"], p["y"], p["z"]], (r if "w" in r else [r["x"], r["y"], r["z"]]), t
shots = [0]
def shot(tag):
    save_images(call("openxr_capture_composited_image", session_handle=h, eye="left"), f"drive{shots[0]:02d}_{tag}"); shots[0] += 1

p, r, raw = kart(); log("kart pose raw:", raw[:300])
def yaw_of(r):
    if isinstance(r, dict) and "w" in r:  # quaternion -> yaw
        x, y, z, w = r["x"], r["y"], r["z"], r["w"]
        return math.degrees(math.atan2(2 * (w * y + x * z), 1 - 2 * (y * y + x * x))) % 360
    return r[1] % 360

# grab: approach from behind the rim, then onto it, squeeze both grips
hands(0, 0.4); sleep(0.6)
for hand in ("right", "left"):
    call("openxr_set_controller_input", session_handle=h, hand=hand, component="Grip", value=1.0, auto_release=False)
sleep(0.4); shot("grabbed")
import subprocess
UNITY = os.path.join(os.environ["LOCALAPPDATA"], "Unity", "bin", "unity.exe")
def ustate():
    r = subprocess.run([UNITY, "command", "eval_file", "--file", os.path.join(S, "why_still.cs"), "--timeout", "20000", "--result-only"], capture_output=True, text=True, encoding="utf-8")
    try: return json.loads(r.stdout)["result"]
    except Exception: return r.stdout[:200]
log("after grab:", ustate())

# calibrate steering sign: wheel to -40 for 2 s at part throttle, watch yaw change
call("openxr_set_controller_input", session_handle=h, hand="right", component="Trigger", value=0.8, auto_release=False)
sleep(1.5); _, r0, _ = kart(); y0 = yaw_of(r0)
hands(-40, 0.3); sleep(2.0); log("calib mid:", ustate()); _, r1, _ = kart(); y1 = yaw_of(r1); hands(0, 0.3)
dyaw = (y1 - y0 + 540) % 360 - 180
sign = 1 if dyaw > 0 else -1   # wheel angle -40 gave dyaw: yaw increases => turning right
log(f"calibration: wheel -40 -> yaw {y0:.0f} -> {y1:.0f} (d={dyaw:.0f}); right-turn wheel sign = {-sign if False else ('negative' if dyaw > 0 else 'positive')}")
right_turn_angle_sign = -1  # measured: wheel -40 -> kart TurnInput +0.44 (right)
shot("after_calibration")

wps = [("south of spawn", 170, -100), ("south-west", 92, -100), ("west side", 92, 70),
       ("over TrackTest", 160, 75), ("north-east", 205, 72), ("east corridor", 226, 0),
       ("south-east", 226, -100), ("home", 170, -100)]
i = 0; t0 = time.time(); next_shot = 10; wheel = 0.0; it = 0; hist = []
while i < len(wps) and time.time() - t0 < MAXT:
    p, r, _ = kart(); y = yaw_of(r); it += 1; now = time.time() - t0
    hist.append((now, p[0], p[2])); hist = [q for q in hist if now - q[0] < 3.5]
    name, wx, wz = wps[i]; dx, dz = wx - p[0], wz - p[2]; dist = math.hypot(dx, dz)
    if dist < 12: log(f"{now:5.1f}s reached {name} at ({p[0]:.0f},{p[2]:.0f})"); i += 1; continue
    err = (math.degrees(math.atan2(dx, dz)) - y + 540) % 360 - 180      # + = target to the right
    moved = math.hypot(hist[-1][1] - hist[0][1], hist[-1][2] - hist[0][2]) if len(hist) > 1 else 99
    if now > 6 and hist[-1][0] - hist[0][0] > 3 and moved < 1.5:
        log(f"{now:5.1f}s stuck at ({p[0]:.0f},{p[2]:.0f}) -> reverse (A button) with opposite lock")
        call("openxr_set_controller_input", session_handle=h, hand="right", component="Trigger", value=0.0, auto_release=False)
        hands(60 if err > 0 else -60, 0.2)
        call("openxr_set_controller_input", session_handle=h, hand="right", component="A", value=1, auto_release=False)
        sleep(2.2)
        call("openxr_set_controller_input", session_handle=h, hand="right", component="A", value=0, auto_release=False)
        hist = []; continue
    want = max(-90, min(90, err * 1.2)) * right_turn_angle_sign
    wheel += max(-35, min(35, want - wheel))
    hands(wheel, 0.12)
    call("openxr_set_controller_input", session_handle=h, hand="right", component="Trigger", value=(0.9 if abs(err) < 35 else 0.55), auto_release=False)
    if now >= next_shot:
        log(f"{now:5.1f}s pos=({p[0]:.0f},{p[2]:.0f}) yaw={y:.0f} -> {name} dist={dist:.0f} err={err:.0f} wheel={wheel:.0f} iters={it}")
        shot(f"{int(now):03d}s"); next_shot += 10
log(f"done: reached {i}/{len(wps)} in {time.time()-t0:.0f}s, {it} control steps")
call("openxr_set_controller_input", session_handle=h, hand="right", component="Trigger", value=0.0, auto_release=False)
hands(0, 0.3); sleep(1.5); shot("final")
