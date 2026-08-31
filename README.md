# xr-racing

VR kart racing for Meta Quest, built in Unity.

## Tech stack

- **Engine**: Unity 6000.5.2f1, Universal Render Pipeline 17.5.0
- **Target**: VR, Meta Quest (standalone), grabbable steering-wheel driving
- **XR**: Meta XR SDK (Core / Interaction / Interaction.OVR) 205.0.0, Meta XR
  Movement SDK, Unity XR Interaction Toolkit 3.4.1, OpenXR 1.16.1
- **Multiplayer**: Photon Fusion 2 (manual account/App ID setup, not yet
  imported)
- **Other**: Cinemachine, ML-Agents (kart AI), ProBuilder, Unity Input System

## Assets in use

- XR input rig (grabbable steering wheel, joystick) — ported from the
  `xr-sandbox` Car Demo (`Assets/App/References/Input Reference`); no scripts
  ported yet
- Kart/track/AI reference content — Unity's official **Karting Microgame**
  asset (`Assets/App/References/Karting Reference`; see that folder's
  `ThirdPartyNotice.txt` for attribution)
