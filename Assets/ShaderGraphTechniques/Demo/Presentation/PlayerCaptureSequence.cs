using System;
using System.Collections;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShaderGraphTechniques.Demo
{
    /// <summary>
    /// Optional player-side fixed-step capture. Invoke the built player with
    /// -captureSequence -captureOutput &lt;folder&gt; [-captureFrames 180] [-captureFps 60].
    /// It captures actual Unity frames and quits after the sequence.
    /// </summary>
    public sealed class PlayerCaptureSequence : MonoBehaviour
    {
        static bool captureOwnerExists;

        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Contains("-captureSequence", StringComparer.OrdinalIgnoreCase)) yield break;
            if (captureOwnerExists)
            {
                // The presentation controller shares this GameObject in every demo
                // scene. Remove only the duplicate capture runner after a scene load;
                // destroying the GameObject would also remove the scene choreography.
                Destroy(this);
                yield break;
            }
            captureOwnerExists = true;
            DontDestroyOnLoad(gameObject);

            var requestedScene = GetArgument(args, "-captureScene");
            if (!string.IsNullOrWhiteSpace(requestedScene) &&
                !string.Equals(SceneManager.GetActiveScene().name, requestedScene, StringComparison.OrdinalIgnoreCase))
            {
                // Only the capture runner must survive. The presentation component on
                // the startup scene would otherwise keep drawing its overlay above the
                // requested scene and compete for the camera.
                var startupPresentation = GetComponent<DemoPresentationController>();
                if (startupPresentation != null)
                {
                    startupPresentation.SetOverlayVisible(false);
                    startupPresentation.enabled = false;
                    Destroy(startupPresentation);
                }

                var load = SceneManager.LoadSceneAsync(requestedScene, LoadSceneMode.Single);
                if (load == null) throw new InvalidOperationException("Could not load capture scene: " + requestedScene);
                while (!load.isDone) yield return null;
            }

            var output = GetArgument(args, "-captureOutput") ?? Path.Combine(Application.persistentDataPath, "ShaderGraphTechniqueCaptures");
            var frameCount = ParsePositiveInt(GetArgument(args, "-captureFrames"), 180);
            var fps = ParsePositiveInt(GetArgument(args, "-captureFps"), 60);
            var width = ParsePositiveInt(GetArgument(args, "-captureWidth"), 1920);
            var height = ParsePositiveInt(GetArgument(args, "-captureHeight"), 1080);
            var driveShowcaseTime = args.Contains("-showcaseMotion", StringComparer.OrdinalIgnoreCase);
            Directory.CreateDirectory(output);
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            Time.captureFramerate = fps;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            yield return null;
            yield return new WaitForEndOfFrame();
            var activeScene = SceneManager.GetActiveScene();
            var presentation = FindObjectsByType<DemoPresentationController>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(item => item != null && item.gameObject.scene == activeScene)
                ?? GetComponent<DemoPresentationController>();
            if (presentation != null)
            {
                presentation.ResetDemo();
                if (driveShowcaseTime)
                {
                    presentation.SetShowcaseMode(true);
                    presentation.SetManualTime(0f);
                }
                if (args.Contains("-captureHideUi", StringComparer.OrdinalIgnoreCase)) presentation.SetOverlayVisible(false);
            }
            var camera = Camera.main;
            if (camera != null && args.Contains("-orthographic", StringComparer.OrdinalIgnoreCase))
            {
                camera.orthographic = true;
                camera.orthographicSize = ParsePositiveFloat(GetArgument(args, "-orthographicSize"), 4.2f);
            }
            var cameraTrace = new StringBuilder("frame,positionX,positionY,positionZ,rotationX,rotationY,rotationZ,rotationW,fieldOfView\n");
            for (var frame = 0; frame < frameCount; frame++)
            {
                // Time.captureFramerate does not guarantee that unscaledTime advances in a
                // screenshot coroutine. Drive the demo clock explicitly so every output
                // frame represents its exact timeline position.
                if (driveShowcaseTime && presentation != null)
                    presentation.SetManualTime(frame / (float)fps);

                yield return new WaitForEndOfFrame();
                camera = Camera.main;
                if (camera != null)
                {
                    var position = camera.transform.position;
                    var rotation = camera.transform.rotation;
                    cameraTrace.Append(frame).Append(',')
                        .Append(position.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(position.y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(position.z.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(rotation.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(rotation.y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(rotation.z.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(rotation.w.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(camera.fieldOfView.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
                }
                var filename = $"{SceneManager.GetActiveScene().name}_{frame:0000}.png";
                var screenshot = ScreenCapture.CaptureScreenshotAsTexture(1);
                try
                {
                    File.WriteAllBytes(Path.Combine(output, filename), screenshot.EncodeToPNG());
                }
                finally
                {
                    Destroy(screenshot);
                }
                yield return null;
            }

            File.WriteAllText(Path.Combine(output, "camera-trace.csv"), cameraTrace.ToString());
            File.WriteAllText(Path.Combine(output, "capture-complete.txt"),
                $"scene={SceneManager.GetActiveScene().name}\nwidth={width}\nheight={height}\nfps={fps}\nframes={frameCount}\nfixedStep=true\nshowcaseMotion={driveShowcaseTime}\npresentationFound={presentation != null}\ncaptureMode=synchronous-texture\n");
            Application.Quit(0);
        }

        static string GetArgument(string[] args, string name)
        {
            for (var index = 0; index + 1 < args.Length; index++)
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
            return null;
        }

        static int ParsePositiveInt(string value, int fallback) =>
            int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;

        static float ParsePositiveFloat(string value, float fallback) =>
            float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0f ? parsed : fallback;
    }
}
