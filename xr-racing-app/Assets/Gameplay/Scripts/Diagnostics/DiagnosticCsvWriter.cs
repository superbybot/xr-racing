#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace XrRacing.Gameplay.Diagnostics
{
    /// <summary>
    /// CSV file shared by the diagnostic loggers. In the Editor it lands in the project's Logs/ folder (gitignored);
    /// on a device in Application.persistentDataPath. Development builds and the Editor only.
    /// </summary>
    public sealed class DiagnosticCsvWriter : IDisposable
    {
        private readonly StreamWriter _writer;

        private DiagnosticCsvWriter(StreamWriter writer)
        {
            _writer = writer;
        }

        public static DiagnosticCsvWriter Open(string fileName, string header, bool autoFlush = false)
        {
#if UNITY_EDITOR
            string directory = Path.Combine(Application.dataPath, "..", "Logs");
#else
            string directory = Application.persistentDataPath;
#endif
            Directory.CreateDirectory(directory);
            string path = Path.GetFullPath(Path.Combine(directory, fileName));
            var writer = new StreamWriter(path, false, Encoding.UTF8) { AutoFlush = autoFlush };
            writer.WriteLine(header);
            Debug.Log($"[Diagnostics] Writing to {path}");
            return new DiagnosticCsvWriter(writer);
        }

        /// <summary>Writes a "time,frame,kind,detail" row.</summary>
        public void WriteEvent(string kind, string detail)
        {
            _writer.WriteLine(string.Join(",", Time(), Frame(), kind, Quote(detail)));
        }

        /// <summary>Writes a row whose columns after time and frame are already joined with commas.</summary>
        public void WriteRow(string columns)
        {
            _writer.WriteLine(string.Join(",", Time(), Frame(), columns));
        }

        public void Flush()
        {
            _writer.Flush();
        }

        public void Dispose()
        {
            _writer.Flush();
            _writer.Dispose();
        }

        public static string Format(float value, string format = "F3")
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        public static string Quote(string text)
        {
            return "\"" + text + "\"";
        }

        private static string Time()
        {
            return Format(UnityEngine.Time.time);
        }

        private static string Frame()
        {
            return UnityEngine.Time.frameCount.ToString(CultureInfo.InvariantCulture);
        }
    }
}
#endif
