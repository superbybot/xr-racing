var c = System.AppDomain.CurrentDomain.GetData("progressRecorder") as UnityEditor.Recorder.RecorderController;
c?.StopRecording();
System.AppDomain.CurrentDomain.SetData("progressRecorder", null);
return c == null ? "no recorder" : "stopped";
