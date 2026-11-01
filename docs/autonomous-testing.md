# Autonomous testing (agents)

How an AI agent tests xr-racing in the running Editor without a headset. Learned
in the first end-to-end run (2026-10-12, Track_Playground). Scripts live in
`tools/autotest/` (see the index below); screenshots go to `_temp/captures/`, videos to
`_temp/videos/` (both gitignored).

## The stack — use the right layer

| Need | Tool |
|---|---|
| Compile check, Play/Stop, scene/asset edits, game-state queries (`eval`), Game-view screenshot | **Unity CLI + Pipeline** (`%LOCALAPPDATA%\Unity\bin\unity.exe`, port 7800) |
| VR input: headset pose, controllers on the wheel, buttons/trigger, headset (composited) screenshot | **Meta XR Operator** via its stdio proxy, with the **Meta XR Simulator** as runtime (port 8720, Play mode only) |
| Repeatable regression checks | In-game code / PlayMode tests — not external polling loops |

## Rules

1. **Test through the real input path.** For driving, use the simulated
   controllers on the steering wheel (Operator) — not `simulate_key` into
   `KartKeyboardInput`, and never scripts that set `Rigidbody` velocity or
   transforms directly. Keyboard is only a fallback, and say so in the report.
2. **Don't add test-only MonoBehaviours to gameplay scenes.** If a test needs an
   in-game helper, keep it in an Editor/test assembly or add it at runtime and
   leave the scene file untouched. Remove anything temporary before finishing.
3. **External control loops are slow** (~1 s per Unity CLI call, ~0.3–0.6 s per
   Operator step). Fine for steering toward waypoints; too slow for anything
   frame-timed. Frame-accurate input sequences must run inside the game.
4. **Know the map before driving.** Query the scene layout (`tools/autotest/probes/track_layout.cs`)
   and keep waypoints well inside the playable area — the boundary hill and
   TrackTest fences trap the kart. Playground has no checkpoints; `LapTracker`
   is inactive there.
5. **Verify with data and pictures.** Log kart pose/speed/input alongside each
   screenshot (`probes/kart_drive_state.cs` reports pause/lock/menu/loading/input/grab state).
   Identical screenshots or unchanged poses mean the test is broken — stop and diagnose.
6. **Calibrate steering at speed, or use the known mapping:** wheel −40° (about
   the spin axis, see `probes/wheel_rim_points.cs`) → `TurnInput` ≈ +0.44 = right turn.
7. **Save screenshots to `_temp/captures/<date>_<scene>/`** and videos to `_temp/videos/`,
   never inside `xr-racing-app/`
   (that creates `.meta` files). Use `capture_game_view` inline base64, or
   Operator's `openxr_capture_composited_image` for what the headset sees.
8. **Clean up:** release controller inputs (`operator/release_controllers.py`), exit Play mode,
   and report anything you changed outside the repo.
9. **Report honestly:** what drove the kart (hands vs keyboard), waypoints reached,
   where it got stuck and why, and which failures were the test's fault vs the game's.

## Gotchas

**Unity CLI**
- Run `unity command editor_focus` first; an unfocused Editor gives
  "Main thread operation timed out after 5000ms".
- PowerShell 5.1 mangles quotes in `eval --code` → put C# in a file, use `eval_file --file`.
- `editor_pause` only toggles → set `UnityEditor.EditorApplication.isPaused` via eval.
  Play mode can start paused if it was left paused.
- `delete_asset <path> --confirm true`; list params with `unity command --query <name> --detail full --format json`.

**Meta XR Operator / Simulator**
- Not registered as an MCP server here; use `tools/autotest/operator/operator_client.py` with the proxy
  exe and base URL `http://127.0.0.1:8720` (not `/sse`, not `localhost`).
- The live tool set (Core SDK 207) has **no hand-tracking tools** — finger-curl
  pedals can't be tested yet; use controllers.
- Wheel grab: set **both** `grip` and `aim` controller poses on the rim, then
  `Grip = 1` (expect `Grabbable.SelectingPointsCount == 2`). Aim-only doesn't grab.
  Right `Trigger` = throttle, `A` = brake/reverse.
- If Play mode quits ~4 s after start with "Playback is complete! Quitting" in the
  Simulator log, remove the `session_capture` block from
  `%APPDATA%\MetaXR\MetaXrSimulator\persistent_data.json`. Never leave one behind.
- With controllers, the rig draws no hands or controllers, so nothing visible holds
  the wheel. A white-outlined hand on the rim is an Interaction SDK authoring ghost
  (`OpenXRGhost-Hand*(Clone)`) that got saved into a prefab. Delete it from the
  prefab; it was removed from `HandGrabInteractable_Wheel.prefab` on 2026-10-12.
- Seen once (2026-10-12): wheel input and `kartInput` both read throttle 0.58, but
  the kart crept at ~0.4 m/s for a whole run. It drove normally on the next Play.
  Cause unknown; if it happens, restart Play mode and note it in the report.

## Recipe: drive the Playground with the hands

From the git root, in Git Bash. Paths must be absolute: `eval_file` resolves
relative paths against `xr-racing-app/`, and the proxy fails to launch from a relative path.

```bash
R=$(pwd -W); U="$LOCALAPPDATA/Unity/bin/unity.exe"; T="$R/tools/autotest"
P="$R/$(ls -d xr-racing-app/Library/PackageCache/com.meta.xr.sdk.core@*/Editor/MetaXROperator/Tools~/Windows/meta-xr-operator-mcp-proxy.exe | tail -1)"
"$U" command editor_focus; "$U" command editor_play; sleep 10
for i in 1 2 3 4 5; do "$U" command editor_focus && break; sleep 3; done   # Editor is busy right after entering Play
export RIM=$("$U" command eval_file --file "$T/probes/wheel_rim_points.cs" --result-only | python -I -c "import json,sys; print(json.load(sys.stdin)['result'])")
# Quick check: timed free drive, screenshot every SHOT seconds (no waypoints)
OUT=$(date +%F)_drive DRIVE=30 SHOT=5 PYTHONIOENCODING=utf-8 python -I -X utf8 "$T/operator/operator_client.py" "$P" "$T/operator/drive_timed_circle.py"
# Or the waypoint tour: OUT=... MAXT=180 ... "$T/operator/drive_waypoints.py"
python -I -X utf8 "$T/operator/operator_client.py" "$P" "$T/operator/release_controllers.py"; "$U" command editor_stop
```

`drive_timed_circle.py` holds a steady right-hand circle after 6 s. Its lap is about
5 s, so positions sampled every 5 s can look stuck while the kart is moving. Check
`speed` in `kart_drive_state.cs` before calling it a stall.

## Script index (`tools/autotest/`)

Run the `.cs` files with `unity command eval_file --file <absolute path>`; the
`operator/` scripts with `python -I -X utf8 operator/operator_client.py <proxy.exe> <script>`.

| Script | What it does |
|---|---|
| `operator/operator_client.py` | Minimal MCP client for the Meta XR Operator proxy; runs a script with `call()`, `save_images()`, `sleep()`, `log()`. Saves to `_temp/captures/<OUT>/`. |
| `operator/drive_timed_circle.py` | Grabs the wheel with both controllers and drives straight, then a steady right circle; headset screenshot every `SHOT` s. |
| `operator/drive_waypoints.py` | Waypoint tour of Track_Playground with the controllers on the wheel. |
| `operator/release_controllers.py` | Releases every injected controller input. Run after each drive. |
| `probes/kart_pose.cs` | Kart position, yaw, speed, time as one CSV line. |
| `probes/kart_drive_state.cs` | Why the kart isn't moving: pause, control lock, menu, loading, wheel/kart input, grab, speed. |
| `probes/wheel_rim_points.cs` | Rim grab points in OpenXR coordinates (the `RIM` env var for the drive scripts). |
| `probes/wheel_grab_state.cs` | Every HandGrabInteractor near the wheel and why it does or doesn't grab. |
| `probes/controller_state.cs` | Controller connection, pose, grip/trigger, and which controller visuals render. |
| `probes/controller_visuals.cs` | Why controller models aren't drawn (OVRControllerHelper / ControllerVisual state). |
| `probes/hand_tracking_state.cs` | Hand tracking state per hand (tracked, confidence, pinch). |
| `probes/track_layout.cs` | Track_Playground root objects with bounds, for planning waypoints. |
| `probes/cameras.cs` | All cameras with tag, enabled state and pose. |
| `probes/character_bones.cs` | Bone names under `PlayerCharacter`. |
| `setup/activate_xr_simulator.cs` | Turns on the Meta XR Simulator (same as its menu item). |
| `setup/rig_reset.cs` | Puts `OVRCameraRig` back at the origin. |
| `setup/spectator_camera.cs` | Adds a side-on spectator camera for Game View captures (CharacterTest). |
| `setup/character_front_view.cs`, `character_side_view.cs`, `character_mirror_view.cs` | Stage the rig/mirror for CharacterTest screenshots. |
| `setup/refresh_assets.cs` | `AssetDatabase.Refresh()`. |
| `recording/start_recording.cs`, `stop_recording.cs` | Unity Recorder MP4 of the Game View into `_temp/videos/progress_<time>.mp4`. |
