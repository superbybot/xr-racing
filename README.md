# xr-racing

VR kart racing for Meta Quest, built with Unity 6.

## Tech Stack

- **Engine:** Unity 6000.5.2f1, Universal Render Pipeline (URP) 17.5.0
- **Target Platform:** Meta Quest standalone (Android/ARM64)
- **XR SDKs:** Meta XR SDK 205.0.0 (Core, Interaction, Interaction.OVR, Movement), Unity XR Interaction Toolkit 3.4.1, OpenXR 1.17.1
- **Multiplayer:** Photon Fusion 2 (resolved under `Assets/Photon/Fusion`)
- **Auxiliary Packages:** UniTask, NuGetForUnity, TextMeshPro, Cinemachine, ProBuilder, Unity Input System

## Project Structure & Features

- **Driving Mechanics (`Assets/Gameplay/`):**
  - **Grabbable Steering Wheel:** Physically constrained one-grab and two-grab rotational transformer with configurable return-to-center spring and angular limits (`XRWheelInput`, `SteeringWheelTransformer`).
  - **Pedal & Input Abstraction:** Configurable finger trigger and face button mapping for throttle and brake (`DriverSettings`).
  - **Driver Ergonomics:** In-VR floating Driver Settings panel (`DriverSettingsMenu`) allowing seat height, distance, and recentering adjustments in real-time.
  - **Camera Stabilization:** Dynamic VR camera height smoothing (`VRCameraHeightSmoother`) to filter out suspension bounce and terrain jitter.
  - **PC Editor Fallback:** Keyboard input support (`KartKeyboardInput`) and dedicated test scene (`DrivingPrototype_PC.unity`) for rapid local iteration without needing a headset.
- **Reference Assets (`Assets/References/`):**
  - `Assets/References/Karting Reference/`: Ported from Unity's Karting Microgame (models, audio, tracks, and physics components).
  - `Assets/References/Input Reference/`: Grabbable cockpit rig and steering wheel meshes.
- **CI/CD Automation (`.github/workflows/`):**
  - Automated headless batch builds and ADB deployment to Meta Quest via self-hosted GitHub Actions runner (`build-and-deploy.yml`).
