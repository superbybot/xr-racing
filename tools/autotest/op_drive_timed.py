# Timed free drive with simulated Touch controllers on the steering wheel (Meta XR Operator).
# No waypoints: straight, then a steady right-hand circle so the kart stays in open ground.
# Needs env RIM = result of `unity command eval_file --file wheel_path.cs` (Play mode, kart at rest).
# Env: DRIVE (seconds, default 30), SHOT (screenshot interval, default 5), OUT (capture subfolder).
import math, time
DRIVE = float(os.environ.get("DRIVE", "30")); EVERY = float(os.environ.get("SHOT", "5"))
parts = dict(kv.split("=", 1) for kv in os.environ["RIM"].split("|"))
def v3(s): return [float(t) for t in s.split(",")]
R = {int(k[1:]): v3(v) for k, v in parts.items() if k[0] == "R" and k[1:].lstrip("-").isdigit()}
L = {int(k[1:]): v3(v) for k, v in parts.items() if k[0] == "L" and k[1:].lstrip("-").isdigit()}
def rim(table, a):
    a = max(-90, min(90, a)); lo = int(math.floor(a / 15) * 15); hi = min(90, lo + 15); t = 0 if hi == lo else (a - lo) / 15
    return [table[lo][i] + (table[hi][i] - table[lo][i]) * t for i in range(3)]

h = json.loads(text(call("openxr_get_session_info")))["session"]["handle"]
def hands(a, dur=0.0):
    for hand, tab in (("right", R), ("left", L)):
        for pt in ("grip", "aim"):
            call("openxr_set_controller_pose", session_handle=h, hand=hand, pose_type=pt, position=rim(tab, a),
                 orientation=[0, 0, 0, 1], base_space="local_floor", duration_seconds=dur)
def inp(hand, comp, v): call("openxr_set_controller_input", session_handle=h, hand=hand, component=comp, value=v, auto_release=False)
def pos():
    m = json.loads(text(call("unity_get_world_pose", path="KartClassic_Player")))["matches"][0]["position"]
    return m["x"], m["z"]
def shot(name): save_images(call("openxr_capture_composited_image", session_handle=h, eye="left"), name)

hands(0, 0.4); sleep(0.6)
inp("right", "Grip", 1.0); inp("left", "Grip", 1.0); sleep(0.4)
shot("t00s_grabbed")
inp("right", "Trigger", 0.8)

# wheel angle over time: negative = right turn (see docs/autonomous-testing.md)
def plan(t): return 0 if t < 6 else -35
t0 = time.time(); wheel = None; n = 1; last = None
while True:
    t = time.time() - t0
    if t >= DRIVE: break
    a = plan(t)
    if a != wheel: hands(a, 0.5); wheel = a          # only send poses when the angle changes
    if t >= n * EVERY:
        x, z = pos(); spd = 0 if last is None else math.hypot(x - last[1], z - last[2]) / (t - last[0])
        log(f"{t:5.1f}s pos=({x:.0f},{z:.0f}) ~{spd:.1f} m/s wheel={wheel}")
        shot(f"t{int(n * EVERY):02d}s"); last = (t, x, z); n += 1
    sleep(0.1)

inp("right", "Trigger", 0.0); hands(0, 0.3); sleep(0.5)
x, z = pos(); log(f"done after {DRIVE:.0f}s at ({x:.0f},{z:.0f})")
