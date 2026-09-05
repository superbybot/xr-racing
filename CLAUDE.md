# xr-racing

## Reference project: xr-sandbox

This project's implementation leans heavily on
`/Users/byronbautista/xr-sandbox/xr-sandbox-app` — it's a sister sandbox
project with the XR driving rig, Meta XR SDK setup, and a Unity Karting
Microgame reference already worked out, and is where the assets currently in
`xr-racing-app/Assets/App/References/` were ported from.

Don't hesitate to look there when deciding how to implement something here —
check its scripts, prefabs, and package setup for existing patterns before
building from scratch. Treat it as a reference/source, not something to edit.

## CI/CD & Agent Guardrails

1. Never create/move/rename/delete a Unity asset without its .meta file, and never change existing GUIDs.
2. Never stage or edit generated files under xr-racing-app/Library, Temp, Obj, Build, Builds, Logs, or UserSettings.
3. Builds and deploys to the Quest happen only via the self-hosted GitHub Actions runner on the Windows machine, triggered by push to main — never by invoking Unity.exe or other Windows binaries directly from a macOS/non-Windows session.
4. Editor-only C# outside the Assets/Editor folder must be wrapped in #if UNITY_EDITOR.
