using System.Globalization;
using UnityEngine;

namespace XrRacing.Gameplay.Diagnostics
{
    /// <summary>CSV log of the steering wheel's rotation and grabs (Logs/wheel_debug.csv). Editor and development builds only, and only while XRWheelInput's Debug Log is on.</summary>
    public static class WheelDebugLog
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static bool Enabled;

        private static DiagnosticCsvWriter _writer;

        public static void Begin()
        {
            if (!Enabled || _writer != null)
            {
                return;
            }

            _writer = DiagnosticCsvWriter.Open("wheel_debug.csv", "time,frame,source,event,selecting,grabPoints,wheelAngle,detail", autoFlush: true);
            Application.quitting += End;
        }

        public static void Write(string source, string evt, int selecting, int grabPoints, float wheelAngle, string detail = "")
        {
            if (!Enabled || _writer == null)
            {
                return;
            }

            _writer.WriteRow(string.Join(",", source, evt, selecting.ToString(CultureInfo.InvariantCulture), grabPoints.ToString(CultureInfo.InvariantCulture), DiagnosticCsvWriter.Format(wheelAngle, "F2"), DiagnosticCsvWriter.Quote(detail)));
        }

        public static string F(float value)
        {
            return DiagnosticCsvWriter.Format(value, "F2");
        }

        private static void End()
        {
            _writer?.Dispose();
            _writer = null;
        }
#else
        // Release builds: every call compiles away to a no-op.
        public static bool Enabled { get => false; set { } }
        public static void Begin() { }
        public static void Write(string source, string evt, int selecting, int grabPoints, float wheelAngle, string detail = "") { }
        public static string F(float value) => string.Empty;
#endif
    }
}
