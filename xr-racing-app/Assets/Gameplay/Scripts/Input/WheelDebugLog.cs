using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace XrRacing.Gameplay.Input
{
    /// <summary>
    /// Temporary CSV logger for debugging steering wheel rotation. Writes to Logs/wheel_debug.csv in the
    /// project folder when running in the Editor, or to Application.persistentDataPath on device.
    /// The file is overwritten each time play starts.
    /// </summary>
    public static class WheelDebugLog
    {
        public static bool Enabled;

        private static StreamWriter _writer;

        public static void Begin()
        {
            if (!Enabled || _writer != null)
            {
                return;
            }

#if UNITY_EDITOR
            string dir = Path.Combine(Application.dataPath, "..", "Logs");
#else
            string dir = Application.persistentDataPath;
#endif
            Directory.CreateDirectory(dir);
            string path = Path.GetFullPath(Path.Combine(dir, "wheel_debug.csv"));

            _writer = new StreamWriter(path, false, Encoding.UTF8) { AutoFlush = true };
            _writer.WriteLine("time,frame,source,event,selecting,grabPoints,wheelAngle,detail");
            Application.quitting += End;
            Debug.Log($"[WheelDebugLog] Writing to {path}");
        }

        public static void Write(string source, string evt, int selecting, int grabPoints, float wheelAngle, string detail = "")
        {
            if (!Enabled || _writer == null)
            {
                return;
            }

            _writer.WriteLine(string.Join(",",
                Time.time.ToString("F3", CultureInfo.InvariantCulture),
                Time.frameCount.ToString(CultureInfo.InvariantCulture),
                source,
                evt,
                selecting.ToString(CultureInfo.InvariantCulture),
                grabPoints.ToString(CultureInfo.InvariantCulture),
                wheelAngle.ToString("F2", CultureInfo.InvariantCulture),
                "\"" + detail + "\""));
        }

        public static string F(float value)
        {
            return value.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static void End()
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
