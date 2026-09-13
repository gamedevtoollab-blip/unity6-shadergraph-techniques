using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ShaderGraphTechniques.Editor
{
    public static class ShaderGraphTechniquePackageExporter
    {
        const string Runtime = "Assets/ShaderGraphTechniques/Runtime";
        const string RuntimeAsmdef = Runtime + "/ShaderGraphTechniques.Runtime.asmdef";
        const string License = Runtime + "/Common/LICENSE.txt";

        static readonly PackageSpec[] Specs =
        {
            new("01_Dissolve", "ShaderGraphTechniques-01-Dissolve.unitypackage"),
            new("02_IntersectionShield", "ShaderGraphTechniques-02-IntersectionShield.unitypackage"),
            new("03_HeatHaze", "ShaderGraphTechniques-03-HeatHaze.unitypackage"),
            new("04_TriplanarSnow", "ShaderGraphTechniques-04-TriplanarSnow.unitypackage"),
            new("05_InteractiveGrass", "ShaderGraphTechniques-05-InteractiveGrass.unitypackage"),
            new("06_ScanPulse", "ShaderGraphTechniques-06-ScanPulse.unitypackage")
        };

        [MenuItem("Tools/Shader Graph Techniques/Export Runtime Packages")]
        public static void ExportAll()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ShaderGraphTechniqueProjectValidator.RunAll();
            var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            var output = Path.Combine(projectRoot, "Artifacts", "Packages");
            Directory.CreateDirectory(output);
            var manifest = new ExportManifest
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                packages = new List<PackageRecord>()
            };

            foreach (var spec in Specs)
            {
                var paths = BuildEffectClosure(spec.folder);
                ValidateEffectBoundary(spec.folder, paths);
                manifest.packages.Add(Export(spec.filename, paths, output, projectRoot));
            }

            var allPaths = FindAssets(Runtime).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            manifest.packages.Add(Export("ShaderGraphTechniques-Runtime-All.unitypackage", allPaths, output, projectRoot));
            File.WriteAllText(Path.Combine(output, "package-manifest.json"), JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
            Debug.Log($"[ShaderGraphTechniques] Exported {manifest.packages.Count} packages to {output}");
        }

        static string[] BuildEffectClosure(string folder)
        {
            var effectRoot = Runtime + "/" + folder;
            var seeds = FindAssets(effectRoot).ToList();
            if (!seeds.Any(path => path.EndsWith("GETTING_STARTED_JA.md", StringComparison.Ordinal)))
                throw new FileNotFoundException("Effect guide is missing", effectRoot + "/GETTING_STARTED_JA.md");
            var dependencies = AssetDatabase.GetDependencies(seeds.ToArray(), true)
                .Where(path => path.StartsWith(Runtime + "/", StringComparison.Ordinal))
                .Where(path => !AssetDatabase.IsValidFolder(path));
            var paths = new HashSet<string>(seeds.Concat(dependencies), StringComparer.Ordinal)
            {
                License
            };
            if (paths.Any(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))) paths.Add(RuntimeAsmdef);
            return paths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        static string[] FindAssets(string folder) => AssetDatabase.FindAssets("", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !AssetDatabase.IsValidFolder(path))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        static void ValidateEffectBoundary(string folder, string[] paths)
        {
            var effectRoot = Runtime + "/" + folder + "/";
            var unexpected = paths.Where(path => path.StartsWith(Runtime + "/", StringComparison.Ordinal) &&
                                                 !path.StartsWith(effectRoot, StringComparison.Ordinal) &&
                                                 !path.StartsWith(Runtime + "/Common/", StringComparison.Ordinal) &&
                                                 path != RuntimeAsmdef).ToArray();
            if (unexpected.Length > 0)
                throw new InvalidOperationException(folder + " package pulled another effect: " + string.Join(", ", unexpected));
            var forbidden = paths.Where(path => path.Contains("/Demo/", StringComparison.Ordinal) ||
                                                path.Contains("/Tests/", StringComparison.Ordinal) ||
                                                path.Contains("/Editor/", StringComparison.Ordinal) ||
                                                path.StartsWith("ProjectSettings/", StringComparison.Ordinal)).ToArray();
            if (forbidden.Length > 0)
                throw new InvalidOperationException(folder + " package contains forbidden paths: " + string.Join(", ", forbidden));
        }

        static PackageRecord Export(string filename, string[] paths, string output, string projectRoot)
        {
            foreach (var path in paths)
                if (AssetDatabase.LoadMainAssetAtPath(path) == null) throw new FileNotFoundException("Export input is missing", path);
            var outputPath = Path.Combine(output, filename);
            AssetDatabase.ExportPackage(paths, outputPath, ExportPackageOptions.Default);
            if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
                throw new IOException("Unity package was not created: " + outputPath);

            return new PackageRecord
            {
                filename = filename,
                byteSize = new FileInfo(outputPath).Length,
                sha256 = Sha256(outputPath),
                assets = paths.Select(path => new AssetRecord
                {
                    path = path,
                    guid = AssetDatabase.AssetPathToGUID(path),
                    sha256 = Sha256(Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar)))
                }).ToList()
            };
        }

        static string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var hash = SHA256.Create();
            return string.Concat(hash.ComputeHash(stream).Select(value => value.ToString("x2")));
        }

        readonly struct PackageSpec
        {
            public readonly string folder;
            public readonly string filename;
            public PackageSpec(string folder, string filename)
            {
                this.folder = folder;
                this.filename = filename;
            }
        }

        [Serializable]
        sealed class ExportManifest
        {
            public string generatedUtc;
            public string unityVersion;
            public List<PackageRecord> packages;
        }

        [Serializable]
        sealed class PackageRecord
        {
            public string filename;
            public long byteSize;
            public string sha256;
            public List<AssetRecord> assets;
        }

        [Serializable]
        sealed class AssetRecord
        {
            public string path;
            public string guid;
            public string sha256;
        }
    }
}
