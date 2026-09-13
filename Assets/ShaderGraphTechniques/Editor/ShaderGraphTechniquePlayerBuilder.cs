using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShaderGraphTechniques.Editor
{
    public static class ShaderGraphTechniquePlayerBuilder
    {
        public static void Build()
        {
            ShaderGraphTechniqueProjectValidator.RunAll();
            var output = GetArgument("-buildOutput");
            if (string.IsNullOrWhiteSpace(output))
                throw new ArgumentException("The CLI must pass -buildOutput <path>.");
            output = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);

            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length != 7) throw new InvalidOperationException("Expected Gallery plus six individual scenes.");
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.StrictMode
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            var json = new BuildEvidence
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                result = summary.result.ToString(),
                outputPath = output,
                totalSize = summary.totalSize,
                totalTimeSeconds = summary.totalTime.TotalSeconds,
                totalErrors = summary.totalErrors,
                totalWarnings = summary.totalWarnings,
                unityVersion = Application.unityVersion,
                scenes = scenes
            };
            var evidenceFolder = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Artifacts", "Validation");
            Directory.CreateDirectory(evidenceFolder);
            File.WriteAllText(Path.Combine(evidenceFolder, "player-build-summary.json"), JsonUtility.ToJson(json, true), new UTF8Encoding(false));
            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Player build result was {summary.result}: errors={summary.totalErrors}, warnings={summary.totalWarnings}");
            Debug.Log($"[ShaderGraphTechniques] Player build succeeded: {output}, bytes={summary.totalSize}");
        }

        static string GetArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < args.Length; index++)
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
            return null;
        }

        [Serializable]
        sealed class BuildEvidence
        {
            public string generatedUtc;
            public string result;
            public string outputPath;
            public ulong totalSize;
            public double totalTimeSeconds;
            public int totalErrors;
            public int totalWarnings;
            public string unityVersion;
            public string[] scenes;
        }
    }
}
