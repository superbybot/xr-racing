using Oculus.Interaction;
using UnityEngine;
using XrRacing.References.KartGame.KartSystems;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using UnityEngine.SceneManagement;
#endif

namespace XrRacing.Gameplay.Diagnostics
{
    /// <summary>
    /// Diagnostic: logs the kart's vertical motion to find where "jagged / bouncy" driving comes from.
    /// Writes Logs/kart_motion.csv in the Editor (Application.persistentDataPath/kart_motion.csv on device):
    ///   SUMMARY  once a second: vertical range, max vertical speed/acceleration, up/down flips, grounded %, speed
    ///   BOUNCE / OSCILLATION / WHEEL_AIR / GROUND_CHANGE / CAM_JITTER  when something stands out
    ///   WHEEL_HANDS  once a second: where the steering wheel, head and hands sit, to tell whether the wheel drifts
    ///                away from the hands over a session or the player's body does; every 0.1 s for a few
    ///                seconds after a LANDING (all wheels off the ground, then back down)
    ///   P (physics step) and F (rendered frame) raw samples, if Log Raw Samples is on.
    /// The kart body (physics) vs the camera (rendered) columns show whether the kart itself bounces or only the
    /// view does. Compiled out of release builds; leave the component disabled unless you are investigating.
    /// </summary>
    [RequireComponent(typeof(ArcadeKart))]
    [DefaultExecutionOrder(1000)] // LateUpdate after VRCameraHeightSmoother, so the camera height is final
    public class KartMotionDebugLog : MonoBehaviour
    {
        [Tooltip("Also write every physics step (P) and rendered frame (F). Large files; off for summaries/events only.")]
        [SerializeField] private bool logRawSamples = true;
        [Tooltip("Flag BOUNCE when vertical speed exceeds this (m/s) while mostly grounded.")]
        [SerializeField, Min(0f)] private float bounceSpeed = 0.6f;
        [Tooltip("Flag BOUNCE when vertical acceleration exceeds this (m/s²) while mostly grounded.")]
        [SerializeField, Min(0f)] private float bounceAcceleration = 25f;
        [Tooltip("Flag OSCILLATION when vertical velocity changes sign at least this many times within half a second.")]
        [SerializeField, Min(2)] private int oscillationFlips = 4;
        [Tooltip("Flag CAM_JITTER when the camera's height moves this much more than the kart's in one frame (m).")]
        [SerializeField, Min(0f)] private float cameraJitter = 0.01f;

        [Header("References")]
        [Tooltip("Player head: the OVRCameraRig's center eye anchor.")]
        [SerializeField] private Transform head;
        [Tooltip("The kart's OVRCameraRig, for the hand anchors.")]
        [SerializeField] private OVRCameraRig rig;
        [Tooltip("The steering wheel: XRWheelInput's wheel transform.")]
        [SerializeField] private Transform wheel;
        [Tooltip("The steering wheel's Grabbable.")]
        [SerializeField] private Grabbable wheelGrabbable;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const float SummaryInterval = 1f;
        private const float FlipWindow = 0.5f;
        private const float MinAirTime = 0.15f;
        private const float LandingLogTime = 3f;
        private const float LandingLogInterval = 0.1f;

        private ArcadeKart _kart;
        private Rigidbody _body;
        private WheelCollider[] _wheels;
        private float _airTime;
        private float _landingLogUntil;
        private float _nextLandingLog;
        private DiagnosticCsvWriter _writer;
        private readonly StringBuilder _line = new StringBuilder(256);

        private float _lastVy;
        private bool _hasLastVy;
        private readonly float[] _flipTimes = new float[32];
        private int _flipCount;
        private int _lastGrounded = 4;
        private string _lastGroundName;
        private float _lastCameraY;
        private float _lastKartY;
        private bool _hasLastFrame;

        // Per-second summary
        private float _summaryStart;
        private float _minY = float.MaxValue, _maxY = float.MinValue, _maxAbsVy, _maxAbsAy, _groundSum, _speedSum;
        private int _steps, _flipsThisSecond, _events, _maxCameraJitterFrames;
        private float _maxCameraExtra;

        private void OnEnable()
        {
            _kart = GetComponent<ArcadeKart>();
            _body = GetComponent<Rigidbody>();
            _wheels = new[] { _kart.FrontLeftWheel, _kart.FrontRightWheel, _kart.RearLeftWheel, _kart.RearRightWheel };

            _writer = DiagnosticCsvWriter.Open("kart_motion.csv", "time,frame,kind,detail");
            _summaryStart = Time.time;
        }

        private void OnDisable()
        {
            _writer?.Dispose();
            _writer = null;
        }

        private void FixedUpdate()
        {
            if (_writer == null || _body == null)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            float y = _body.position.y;
            float vy = _body.linearVelocity.y;
            float ay = _hasLastVy ? (vy - _lastVy) / dt : 0f;
            float ground = _kart.GroundPercent;
            float speed = _body.linearVelocity.magnitude;
            bool mostlyGrounded = ground >= 0.5f;

            // Wheels: which touch the ground, how compressed each suspension is (0 = extended, 1 = fully compressed).
            int grounded = 0;
            string groundName = null;
            float groundNormalY = 1f;
            _line.Clear();
            for (int i = 0; i < _wheels.Length; i++)
            {
                WheelCollider wheel = _wheels[i];
                float compression = -1f;
                if (wheel != null && wheel.GetGroundHit(out WheelHit hit))
                {
                    grounded++;
                    float travel = -wheel.transform.InverseTransformPoint(hit.point).y - wheel.radius;
                    compression = wheel.suspensionDistance > 0f ? 1f - Mathf.Clamp01(travel / wheel.suspensionDistance) : 0f;
                    if (groundName == null && hit.collider != null)
                    {
                        groundName = hit.collider.name;
                        groundNormalY = hit.normal.y;
                    }
                }

                _line.Append(i == 0 ? "" : " ").Append(F(compression));
            }

            string compressions = _line.ToString();

            // Up/down direction changes with some real speed behind them = oscillation.
            if (_hasLastVy && Mathf.Sign(vy) != Mathf.Sign(_lastVy) && Mathf.Max(Mathf.Abs(vy), Mathf.Abs(_lastVy)) > 0.05f)
            {
                _flipTimes[_flipCount % _flipTimes.Length] = Time.time;
                _flipCount++;
                _flipsThisSecond++;
                int recent = 0;
                for (int i = 0; i < Mathf.Min(_flipCount, _flipTimes.Length); i++)
                {
                    if (Time.time - _flipTimes[i] <= FlipWindow)
                    {
                        recent++;
                    }
                }

                if (recent >= oscillationFlips)
                {
                    Event("OSCILLATION", $"flips={recent} in {F(FlipWindow)}s vy={F(vy)} y={F(y)} ground={F(ground)} susp=[{compressions}] on={groundName}");
                    _flipCount = 0; // report once per burst
                }
            }

            if (mostlyGrounded && (Mathf.Abs(vy) > bounceSpeed || Mathf.Abs(ay) > bounceAcceleration))
            {
                Event("BOUNCE", $"vy={F(vy)} ay={F(ay)} y={F(y)} speed={F(speed)} ground={F(ground)} susp=[{compressions}] on={groundName} normalY={F(groundNormalY)}");
            }

            if (grounded < _lastGrounded && grounded < 4 && speed > 0.5f)
            {
                Event("WHEEL_AIR", $"grounded {_lastGrounded}->{grounded} vy={F(vy)} speed={F(speed)} susp=[{compressions}]");
            }

            if (grounded == 0)
            {
                _airTime += dt;
            }
            else
            {
                if (_airTime >= MinAirTime)
                {
                    Event("LANDING", $"airTime={F(_airTime)} vy={F(_lastVy)} wheels={grounded} speed={F(speed)}");
                    _landingLogUntil = Time.time + LandingLogTime;
                    _nextLandingLog = Time.time;
                }

                _airTime = 0f;
            }

            if (groundName != null && _lastGroundName != null && groundName != _lastGroundName)
            {
                Event("GROUND_CHANGE", $"{_lastGroundName} -> {groundName} vy={F(vy)} ay={F(ay)} (a seam between track pieces?)");
            }

            if (logRawSamples)
            {
                Write("P", $"y={F(y)} vy={F(vy)} ay={F(ay)} ground={F(ground)} wheels={grounded} susp=[{compressions}] speed={F(speed)} normalY={F(groundNormalY)}");
            }

            _lastVy = vy;
            _hasLastVy = true;
            _lastGrounded = grounded;
            if (groundName != null)
            {
                _lastGroundName = groundName;
            }

            _steps++;
            _minY = Mathf.Min(_minY, y);
            _maxY = Mathf.Max(_maxY, y);
            _maxAbsVy = Mathf.Max(_maxAbsVy, Mathf.Abs(vy));
            _maxAbsAy = Mathf.Max(_maxAbsAy, Mathf.Abs(ay));
            _groundSum += ground;
            _speedSum += speed;
        }

        // Rendered frames: the kart (interpolated) and the camera after the rig's height smoothing.
        private void LateUpdate()
        {
            if (_writer == null)
            {
                return;
            }

            float kartY = transform.position.y;
            float cameraY = head != null ? head.position.y : 0f;

            // A big one-frame jump is the kart being placed (track load / respawn), not motion.
            if (_hasLastFrame && Mathf.Abs(kartY - _lastKartY) > 0.5f)
            {
                _hasLastFrame = false;
                _hasLastVy = false;
            }

            if (_hasLastFrame)
            {
                float kartStep = kartY - _lastKartY;
                float cameraStep = cameraY - _lastCameraY;
                float extra = Mathf.Abs(cameraStep - kartStep);
                _maxCameraExtra = Mathf.Max(_maxCameraExtra, extra);

                if (extra > cameraJitter)
                {
                    _maxCameraJitterFrames++;
                    Event("CAM_JITTER", $"camera moved {F(cameraStep)} vs kart {F(kartStep)} this frame (dt={F(Time.deltaTime)})");
                }

                if (logRawSamples)
                {
                    Write("F", $"kartY={F(kartY)} camY={F(cameraY)} kartStep={F(kartStep)} camStep={F(cameraStep)} dt={F(Time.deltaTime)}");
                }
            }

            _lastKartY = kartY;
            _lastCameraY = cameraY;
            _hasLastFrame = true;

            if (Time.time < _landingLogUntil && Time.time >= _nextLandingLog)
            {
                _nextLandingLog = Time.time + LandingLogInterval;
                LogWheelAndHands();
            }

            if (Time.time - _summaryStart >= SummaryInterval && _steps > 0)
            {
                Write("SUMMARY",
                    $"track={SceneManager.GetActiveScene().name} yRange={F(_maxY - _minY)} maxVy={F(_maxAbsVy)} maxAy={F(_maxAbsAy)} " +
                    $"upDownFlips={_flipsThisSecond} grounded={F(_groundSum / _steps)} avgSpeed={F(_speedSum / _steps)} " +
                    $"events={_events} camJitterFrames={_maxCameraJitterFrames} maxCamExtra={F(_maxCameraExtra)}");
                LogWheelAndHands();
                _writer.Flush();

                _summaryStart = Time.time;
                _minY = float.MaxValue;
                _maxY = float.MinValue;
                _maxAbsVy = _maxAbsAy = _groundSum = _speedSum = _maxCameraExtra = 0f;
                _steps = _flipsThisSecond = _events = _maxCameraJitterFrames = 0;
            }
        }

        // Kart-space positions show what moved relative to the kart (the wheel, or the player's head); hub-relative
        // positions in the wheel mount's space (its plane is x/y, as the player sees it) show whether the hands are
        // centered on the wheel. Grab points are where the (snapped) hands hold the wheel.
        private void LogWheelAndHands()
        {
            if (wheel == null || wheel.parent == null)
            {
                return;
            }

            Transform mount = wheel.parent;
            Vector3 FromHub(Vector3 world) => mount.InverseTransformPoint(world) - wheel.localPosition;

            _line.Clear();
            _line.Append("hubInKart=").Append(V(transform.InverseTransformPoint(wheel.position)))
                .Append(" mountTiltLag=").Append(F(Vector3.Angle(mount.up, transform.up)))
                .Append(" kartTilt=").Append(F(Vector3.Angle(transform.up, Vector3.up)));

            if (head != null)
            {
                _line.Append(" headInKart=").Append(V(transform.InverseTransformPoint(head.position)));
            }

            if (rig != null)
            {
                Vector3 left = FromHub(rig.leftHandAnchor.position);
                Vector3 right = FromHub(rig.rightHandAnchor.position);
                _line.Append(" leftFromHub=").Append(V(left))
                    .Append(" rightFromHub=").Append(V(right))
                    .Append(" midFromHub=").Append(V((left + right) * 0.5f));
            }

            if (wheelGrabbable != null)
            {
                _line.Append(" held=").Append(wheelGrabbable.SelectingPointsCount);
                for (int i = 0; i < wheelGrabbable.GrabPoints.Count; i++)
                {
                    _line.Append(" grab").Append(i).Append("FromHub=").Append(V(FromHub(wheelGrabbable.GrabPoints[i].position)));
                }
            }

            Write("WHEEL_HANDS", _line.ToString());
        }

        private static string V(Vector3 v)
        {
            return $"({F(v.x)} {F(v.y)} {F(v.z)})";
        }

        private void Event(string kind, string detail)
        {
            _events++;
            Write(kind, detail);
        }

        private void Write(string kind, string detail)
        {
            _writer.WriteEvent(kind, detail);
        }

        private static string F(float value)
        {
            return DiagnosticCsvWriter.Format(value);
        }
#endif
    }
}
