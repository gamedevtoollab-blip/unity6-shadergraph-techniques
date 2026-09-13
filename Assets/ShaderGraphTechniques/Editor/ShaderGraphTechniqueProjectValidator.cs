using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Graphing;
using UnityEditor.PackageManager;
using UnityEditor.Rendering;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShaderGraphTechniques.Editor
{
    public static class ShaderGraphTechniqueProjectValidator
    {
        const string Root = "Assets/ShaderGraphTechniques";
        const string Runtime = Root + "/Runtime";

        static readonly GraphExpectation[] Expectations =
        {
            new("01_Dissolve/SG_01_Dissolve.shadergraph", new[] { "_Progress", "_NoiseScale", "_EdgeWidth", "_BaseColor", "_EdgeColor", "_EdgeIntensity" }, new[] { "NoiseNode", "StepNode", "SmoothstepNode" }),
            new("02_IntersectionShield/SG_02_IntersectionShield.shadergraph", new[] { "_ContactWidth", "_RimPower", "_RimIntensity", "_ContactIntensity", "_ShieldColor", "_Opacity" }, new[] { "SceneDepthDifferenceNode", "FresnelNode" }),
            new("03_HeatHaze/SG_03_HeatHaze.shadergraph", new[] { "_NoiseScale", "_Distortion", "_Opacity", "_FlowA", "_FlowB", "_ScreenEdgeFade", "_UseManualTime", "_ManualTime" }, new[] { "SceneColorNode", "ScreenPositionNode", "SubGraphNode" }),
            new("04_TriplanarSnow/SG_04_TriplanarSnow.shadergraph", new[] { "_BaseMap", "_BaseTint", "_TextureScale", "_SnowAmount", "_SnowColor", "_SnowNoiseScale", "_SnowSoftness" }, new[] { "TriplanarNode", "NormalVectorNode", "NoiseNode" }),
            new("05_InteractiveGrass/SG_05_InteractiveGrass.shadergraph", new[] { "_InteractorPositionWS", "_InteractorEnabled", "_BendRadius", "_BendStrength", "_WindDirectionXZ", "_WindStrength", "_WindSpeed", "_RootColor", "_TipColor", "_UseManualTime", "_ManualTime" }, new[] { "TransformNode", "PositionNode", "SubGraphNode" }),
            new("06_ScanPulse/SG_06_ScanPulse.shadergraph", new[] { "_ScanCenterWS", "_ScanRadius", "_ScanWidth", "_ScanActive", "_ScanColor", "_ScanIntensity", "_BaseColor" }, new[] { "DistanceNode", "AbsoluteNode", "SmoothstepNode" })
        };

        [MenuItem("Tools/Shader Graph Techniques/Validate Project")]
        public static void RunAll()
        {
            var result = new ValidationReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                checks = new List<ValidationCheck>()
            };

            Check(result, "editor.version", Application.unityVersion == "6000.3.22f1", Application.unityVersion);
            CheckPackage(result, "com.unity.render-pipelines.universal", "17.3.0");
            CheckPackage(result, "com.unity.shadergraph", "17.3.0");
            CheckProjectSettings(result);
            CheckGraphs(result);
            CheckMaterials(result);
            CheckPrefabs(result);
            CheckRuntimeBoundary(result);
            CheckGrassMesh(result);
            CheckBuildScenes(result);

            result.passed = result.checks.Count(check => check.passed);
            result.failed = result.checks.Count - result.passed;
            var outputFolder = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Artifacts", "Validation");
            Directory.CreateDirectory(outputFolder);
            File.WriteAllText(Path.Combine(outputFolder, "project-validation.json"), JsonUtility.ToJson(result, true), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(outputFolder, "project-validation.md"), ToMarkdown(result), new UTF8Encoding(false));
            Debug.Log($"[ShaderGraphTechniques] Validation: {result.passed} passed, {result.failed} failed.");
            if (result.failed > 0)
                throw new InvalidOperationException($"Project validation failed ({result.failed} checks). See Artifacts/Validation/project-validation.md");
        }

        static void CheckPackage(ValidationReport report, string packageName, string expectedVersion)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + packageName);
            Check(report, "package." + packageName, package != null && package.version == expectedVersion,
                package == null ? "not resolved" : package.version);
        }

        static void CheckProjectSettings(ValidationReport report)
        {
            Check(report, "project.colorSpace.linear", PlayerSettings.colorSpace == ColorSpace.Linear, PlayerSettings.colorSpace.ToString());
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Check(report, "project.urp.active", pipeline != null, pipeline == null ? "none" : AssetDatabase.GetAssetPath(pipeline));
            if (pipeline != null)
            {
                Check(report, "project.depthTexture", pipeline.supportsCameraDepthTexture, pipeline.supportsCameraDepthTexture.ToString());
                Check(report, "project.opaqueTexture", pipeline.supportsCameraOpaqueTexture, pipeline.supportsCameraOpaqueTexture.ToString());
            }

            var renderers = AssetDatabase.FindAssets("t:UniversalRendererData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<UniversalRendererData>)
                .Where(item => item != null).ToArray();
            Check(report, "project.renderer.forward", renderers.Length > 0 && renderers.All(item => item.renderingMode == RenderingMode.Forward),
                string.Join(", ", renderers.Select(item => item.renderingMode.ToString())));
        }

        static void CheckGraphs(ValidationReport report)
        {
            var manualTime = AssetDatabase.LoadAssetAtPath<SubGraphAsset>(Runtime + "/Common/ManualTime.shadersubgraph");
            Check(report, "subgraph.manualTime.imported", manualTime != null, Runtime + "/Common/ManualTime.shadersubgraph");

            foreach (var expectation in Expectations)
            {
                var path = Runtime + "/" + expectation.relativePath;
                var id = Path.GetFileNameWithoutExtension(path);
                if (!FileUtilities.TryReadGraphDataFromDisk(path, out var graph) || graph == null)
                {
                    Check(report, "graph." + id + ".read", false, path);
                    continue;
                }

                Check(report, "graph." + id + ".read", true, $"{graph.GetNodes<AbstractMaterialNode>().Count()} nodes");
                var propertyNames = graph.properties.Select(property => property.referenceName).ToArray();
                var missingProperties = expectation.properties.Except(propertyNames).ToArray();
                Check(report, "graph." + id + ".properties", missingProperties.Length == 0,
                    missingProperties.Length == 0 ? string.Join(", ", expectation.properties) : "missing: " + string.Join(", ", missingProperties));

                var typeNames = graph.GetNodes<AbstractMaterialNode>().Select(node => node.GetType().Name).ToArray();
                var missingNodes = expectation.nodeTypes.Where(required => !typeNames.Contains(required)).ToArray();
                Check(report, "graph." + id + ".standardNodes", missingNodes.Length == 0 && !typeNames.Contains("CustomFunctionNode"),
                    missingNodes.Length == 0 ? "required standard nodes present; CustomFunctionNode absent" : "missing: " + string.Join(", ", missingNodes));

                var groups = graph.groups.Select(group => group.title).ToArray();
                Check(report, "graph." + id + ".groups", groups.Length >= 3,
                    groups.Length + " groups: " + string.Join(" | ", groups));

                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                var messages = shader == null ? Array.Empty<ShaderMessage>() : ShaderUtil.GetShaderMessages(shader);
                var errors = messages.Where(message => message.severity == ShaderCompilerMessageSeverity.Error).ToArray();
                Check(report, "graph." + id + ".shaderCompile", shader != null && shader.isSupported && errors.Length == 0,
                    shader == null ? "shader asset missing" : errors.Length == 0 ? "supported; no compiler errors" : string.Join(" | ", errors.Select(error => error.message)));
            }

            var barrierPath = Runtime + "/02_IntersectionShield/SG_02_IntersectionShield.shadergraph";
            if (FileUtilities.TryReadGraphDataFromDisk(barrierPath, out var barrier))
            {
                var depth = barrier.GetNodes<SceneDepthDifferenceNode>().SingleOrDefault();
                Check(report, "graph.barrier.depthModeEye", depth != null && depth.depthSamplingMode == DepthSamplingMode.Eye,
                    depth == null ? "node missing" : depth.depthSamplingMode.ToString());
            }

            var snowPath = Runtime + "/04_TriplanarSnow/SG_04_TriplanarSnow.shadergraph";
            if (FileUtilities.TryReadGraphDataFromDisk(snowPath, out var snow))
            {
                var tri = snow.GetNodes<TriplanarNode>().SingleOrDefault();
                Check(report, "graph.snow.triplanarWorld", tri != null && tri.inputSpace == CoordinateSpace.World,
                    tri == null ? "node missing" : tri.inputSpace.ToString());
            }

            var grassPath = Runtime + "/05_InteractiveGrass/SG_05_InteractiveGrass.shadergraph";
            if (FileUtilities.TryReadGraphDataFromDisk(grassPath, out var grass))
            {
                var transform = grass.GetNodes<TransformNode>().SingleOrDefault();
                var conversion = transform?.conversion;
                Check(report, "graph.grass.worldToObjectPosition",
                    transform != null && conversion.HasValue && conversion.Value.from == CoordinateSpace.World && conversion.Value.to == CoordinateSpace.Object && transform.conversionType == ConversionType.Position,
                    transform == null ? "node missing" : $"{conversion?.from}->{conversion?.to}, {transform.conversionType}");
            }
        }

        static void CheckMaterials(ValidationReport report)
        {
            foreach (var expectation in Expectations)
            {
                var folder = expectation.relativePath.Split('/')[0];
                var number = folder.Substring(0, 2);
                var materialPath = AssetDatabase.FindAssets("t:Material", new[] { Runtime + "/" + folder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.Ordinal)
                    .SingleOrDefault();
                var material = string.IsNullOrEmpty(materialPath) ? null : AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                Check(report, "material." + number + ".exists", material != null, materialPath ?? "missing");
                if (material != null)
                {
                    var missing = expectation.properties.Where(reference => !material.HasProperty(reference)).ToArray();
                    Check(report, "material." + number + ".properties", missing.Length == 0,
                        missing.Length == 0 ? "all article references available" : "missing: " + string.Join(", ", missing));
                }
            }
        }

        static void CheckPrefabs(ValidationReport report)
        {
            var prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { Runtime }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
            Check(report, "prefab.count", prefabPaths.Length == 6, prefabPaths.Length.ToString());
            foreach (var path in prefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var components = prefab.GetComponentsInChildren<Component>(true);
                var forbidden = components.Where(component => component != null &&
                    (component is Camera || component is Light || component is Volume || component is Canvas ||
                     component.GetType().Namespace?.StartsWith("ShaderGraphTechniques.Demo", StringComparison.Ordinal) == true)).ToArray();
                Check(report, "prefab." + prefab.name + ".runtimeOnly", forbidden.Length == 0 && components.All(component => component != null),
                    forbidden.Length == 0 ? "no Camera/Light/Volume/Canvas/Demo/missing script" : string.Join(", ", forbidden.Select(item => item.GetType().Name)));
            }
        }

        static void CheckRuntimeBoundary(ValidationReport report)
        {
            var paths = AssetDatabase.FindAssets("", new[] { Runtime }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !AssetDatabase.IsValidFolder(path)).ToArray();
            var dependencies = AssetDatabase.GetDependencies(paths, true);
            var invalid = dependencies.Where(path => path.StartsWith(Root + "/Demo", StringComparison.Ordinal) ||
                                                     path.StartsWith(Root + "/Editor", StringComparison.Ordinal) ||
                                                     path.StartsWith(Root + "/Tests", StringComparison.Ordinal)).ToArray();
            Check(report, "runtime.dependencyDirection", invalid.Length == 0,
                invalid.Length == 0 ? "Runtime has no Demo/Editor/Tests dependency" : string.Join(", ", invalid));

            var forbiddenTokens = new[] { "Camera.main", "Resources.Load", "Shader.SetGlobal", "UnityEditor", "GameObject.Find(" };
            var sourceProblems = new List<string>();
            foreach (var path in paths.Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            {
                var text = File.ReadAllText(Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, path));
                foreach (var token in forbiddenTokens.Where(text.Contains)) sourceProblems.Add(path + ": " + token);
            }
            Check(report, "runtime.hiddenAssumptions", sourceProblems.Count == 0,
                sourceProblems.Count == 0 ? "no Camera.main/Resources/SetGlobal/GameObject.Find/UnityEditor" : string.Join(" | ", sourceProblems));
        }

        static void CheckGrassMesh(ValidationReport report)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Runtime + "/05_InteractiveGrass/MESH_GrassCluster.asset");
            Check(report, "grass.mesh.exists", mesh != null, mesh == null ? "missing" : $"{mesh.vertexCount} vertices");
            if (mesh == null) return;
            var uv = mesh.uv;
            var y = uv.Select(item => item.y).ToArray();
            Check(report, "grass.mesh.uvContract", uv.Length == mesh.vertexCount && y.Min() == 0f && y.Max() == 1f && y.Distinct().Count() >= 7,
                $"uv={uv.Length}, minY={y.Min():0.###}, maxY={y.Max():0.###}, levels={y.Distinct().Count()}");
        }

        static void CheckBuildScenes(ValidationReport report)
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            Check(report, "buildScenes.count", scenes.Length == 7, scenes.Length + ": " + string.Join(", ", scenes));
            Check(report, "buildScenes.galleryFirst", scenes.FirstOrDefault()?.EndsWith("00_Gallery.unity", StringComparison.Ordinal) == true,
                scenes.FirstOrDefault() ?? "none");
            var missing = scenes.Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null).ToArray();
            Check(report, "buildScenes.assets", missing.Length == 0, missing.Length == 0 ? "all scene assets resolve" : string.Join(", ", missing));
        }

        static void Check(ValidationReport report, string id, bool passed, string detail) =>
            report.checks.Add(new ValidationCheck { id = id, passed = passed, detail = detail });

        static string ToMarkdown(ValidationReport report)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Deterministic project validation");
            builder.AppendLine();
            builder.AppendLine($"- Generated UTC: `{report.generatedUtc}`");
            builder.AppendLine($"- Unity: `{report.unityVersion}`");
            builder.AppendLine($"- Result: `{report.passed} passed / {report.failed} failed`");
            builder.AppendLine();
            builder.AppendLine("| Result | Check | Detail |");
            builder.AppendLine("| --- | --- | --- |");
            foreach (var check in report.checks)
                builder.AppendLine($"| {(check.passed ? "PASS" : "FAIL")} | `{check.id}` | {check.detail.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ")} |");
            return builder.ToString();
        }

        readonly struct GraphExpectation
        {
            public readonly string relativePath;
            public readonly string[] properties;
            public readonly string[] nodeTypes;
            public GraphExpectation(string relativePath, string[] properties, string[] nodeTypes)
            {
                this.relativePath = relativePath;
                this.properties = properties;
                this.nodeTypes = nodeTypes;
            }
        }

        [Serializable]
        sealed class ValidationReport
        {
            public string generatedUtc;
            public string unityVersion;
            public int passed;
            public int failed;
            public List<ValidationCheck> checks;
        }

        [Serializable]
        sealed class ValidationCheck
        {
            public string id;
            public bool passed;
            public string detail;
        }
    }
}
