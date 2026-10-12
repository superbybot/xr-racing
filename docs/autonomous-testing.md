# Autonomous testing (agents)

How an AI agent tests xr-racing in the running Editor without a headset. Learned
in the first end-to-end run (2026-10-12, Track_Playground). Scripts live in
`tools/autotest/`; output goes to `test-captures/` (gitignored).

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
4. **Know the map before driving.** Query the scene layout (`tools/autotest/map_info.cs`)
   and keep waypoints well inside the playable area — the boundary hill and
   TrackTest fences trap the kart. Playground has no checkpoints; `LapTracker`
   is inactive there.
5. **Verify with data and pictures.** Log kart pose/speed/input alongside each
   screenshot (`why_still.cs` reports pause/lock/menu/loading/input/grab state).
   Identical screenshots or unchanged poses mean the test is broken — stop and diagnose.
6. **Calibrate steering at speed, or use the known mapping:** wheel −40° (about
   the spin axis, see `wheel_path.cs`) → `TurnInput` ≈ +0.44 = right turn.
7. **Save screenshots to `test-captures/<date>_<scene>/`**, never under `Assets/`
   (that creates `.meta` files). Use `capture_game_view` inline base64, or
   Operator's `openxr_capture_composited_image` for what the headset sees.
8. **Clean up:** release controller inputs (`op_release.py`), exit Play mode,
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
- Not registered as an MCP server here; use `tools/autotest/op.py` with the proxy
  exe and base URL `http://127.0.0.1:8720` (not `/sse`, not `localhost`).
- The live tool set (Core SDK 207) has **no hand-tracking tools** — finger-curl
  pedals can't be tested yet; use controllers.
- Wheel grab: set **both** `grip` and `aim` controller poses on the rim, then
  `Grip = 1` (expect `Grabbable.SelectingPointsCount == 2`). Aim-only doesn't grab.
  Right `Trigger` = throttle, `A` = brake/reverse.
- If Play mode quits ~4 s after start with "Playback is complete! Quitting" in the
  Simulator log, remove the `session_capture` block from
  `%APPDATA%\MetaXR\MetaXrSimulator\persistent_data.json`. Never leave one behind.

## Recipe: drive the Playground with the hands

```bash
U="$LOCALAPPDATA/Unity/bin/unity.exe"; T=tools/autotest
P=$(ls -d xr-racing-app/Library/PackageCache/com.meta.xr.sdk.core@*/Editor/MetaXROperator/Tools~/Windows/meta-xr-operator-mcp-proxy.exe | tail -1)
"$U" command editor_focus; "$U" command editor_play; sleep 8; "$U" command editor_focus
export RIM=$("$U" command eval_file --file "$T/wheel_path.cs" --result-only | python -I -c "import json,sys; print(json.load(sys.stdin)['result'])")
OUT=$(date +%F)_playground MAXT=180 PYTHONIOENCODING=utf-8 python -I -X utf8 "$T/op.py" "$P" "$T/op_drive.py"
python -I -X utf8 "$T/op.py" "$P" "$T/op_release.py"; "$U" command editor_stop
```
