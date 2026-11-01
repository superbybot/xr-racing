// Starts a Game View MP4 recording into <repo>/_temp/videos/progress_<timestamp>.mp4. Stop it with stop_recording.cs.
var settings = UnityEngine.ScriptableObject.CreateInstance<UnityEditor.Recorder.RecorderControllerSettings>();
var movie = UnityEngine.ScriptableObject.CreateInstance<UnityEditor.Recorder.MovieRecorderSettings>();
movie.name = "Progress";
movie.Enabled = true;
movie.OutputFormat = UnityEditor.Recorder.MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
movie.ImageInputSettings = new UnityEditor.Recorder.Input.GameViewInputSettings { OutputWidth = 1280, OutputHeight = 720 };
var videos = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "..", "_temp", "videos"));
System.IO.Directory.CreateDirectory(videos);
movie.OutputFile = System.IO.Path.Combine(videos, "progress_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss")).Replace('\', '/');
settings.AddRecorderSettings(movie);
settings.SetRecordModeToManual();
settings.FrameRate = 30f;
var controller = new UnityEditor.Recorder.RecorderController(settings);
controller.PrepareRecording();
System.AppDomain.CurrentDomain.SetData("progressRecorder", controller);
return "recording=" + controller.StartRecording() + " file=" + movie.OutputFile + ".mp4";
