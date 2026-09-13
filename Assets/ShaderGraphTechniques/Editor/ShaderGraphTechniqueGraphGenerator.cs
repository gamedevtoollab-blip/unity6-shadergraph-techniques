using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

namespace ShaderGraphTechniques.Editor
{
    /// <summary>
    /// Creates the six editable Shader Graphs used by the sample.  The graphs are
    /// intentionally made from standard Shader Graph nodes so readers can open,
    /// inspect, and modify every operation shown in the article.
    /// </summary>
    public static class ShaderGraphTechniqueGraphGenerator
    {
        const string LitTemplate = "Packages/com.unity.shadergraph/GraphTemplates/Cross Pipeline/0_Lit Basic.shadergraph";
        const string UnlitTemplate = "Packages/com.unity.shadergraph/GraphTemplates/Cross Pipeline/Unlit Simple.shadergraph";
        const string Root = "Assets/ShaderGraphTechniques/Runtime";
        const string ManualTimePath = Root + "/Common/ManualTime.shadersubgraph";

        [MenuItem("Tools/Shader Graph Techniques/Generate Shader Graphs")]
        public static void Generate()
        {
            EnsureFolders();
            RemoveSupersededGeneratedFolders();
            CreateManualTimeSubGraph();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var manualTime = AssetDatabase.LoadAssetAtPath<SubGraphAsset>(ManualTimePath);
            if (manualTime == null)
                throw new InvalidOperationException("ManualTime Sub Graph did not import: " + ManualTimePath);

            CreateDissolve();
            CreateBarrier();
            CreateHeatHaze(manualTime);
            CreateSnowTriplanar();
            CreateInteractiveGrass(manualTime);
            CreateScanPulse();

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            Debug.Log("[ShaderGraphTechniques] Generated 6 Shader Graphs and ManualTime Sub Graph.");
        }

        static void EnsureFolders()
        {
            foreach (var folder in new[]
                     {
                         "Assets/ShaderGraphTechniques",
                         Root,
                         Root + "/Common",
                         Root + "/01_Dissolve",
                         Root + "/02_IntersectionShield",
                         Root + "/03_HeatHaze",
                         Root + "/04_TriplanarSnow",
                         Root + "/05_InteractiveGrass",
                         Root + "/06_ScanPulse"
                     })
                EnsureFolder(folder);
        }

        static void RemoveSupersededGeneratedFolders()
        {
            foreach (var path in new[] { Root + "/02_DepthBarrier", Root + "/04_SnowTriplanar" })
            {
                if (AssetDatabase.IsValidFolder(path) && !AssetDatabase.DeleteAsset(path))
                    throw new IOException("Could not remove superseded generated folder: " + path);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                throw new InvalidOperationException("Invalid asset folder: " + path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        static void CreateManualTimeSubGraph()
        {
            var graph = new GraphData { isSubGraph = true, path = "Shader Graph Techniques/Common" };
            var output = new SubGraphOutputNode();
            graph.AddNode(output);
            graph.outputNode = output;
            var outputId = output.AddSlot(ConcreteSlotValueType.Vector1);
            output.FindInputSlot<MaterialSlot>(outputId).displayName = "Time";

            var builder = new Builder(graph);
            var useManual = builder.Float("Use Manual Time", "_UseManualTime", 0f, -650f, -60f, 0f, 1f);
            var manual = builder.Float("Manual Time", "_ManualTime", 0f, -650f, 100f);
            var builtIn = builder.Add<TimeNode>("Built-in Time", -650f, -230f);
            var use = builder.Saturate(useManual, "Clamp Manual Toggle", -360f, -60f);
            var selected = builder.Lerp(builder.Out(builtIn, 0), manual, use, "Stopped or Live Time", -80f, -80f);
            builder.Connect(selected, output.GetSlotReference(outputId));

            graph.ValidateGraph();
            WriteGraph(ManualTimePath, graph);
        }

        static GraphData NewGraph(string template, string path, SurfaceSettings settings)
        {
            if (!FileUtilities.TryReadGraphDataFromDisk(template, out var graph) || graph == null)
                throw new InvalidOperationException("Could not read Shader Graph template: " + template);

            graph.path = path;
            foreach (var target in graph.activeTargets.ToArray())
            {
                if (target.GetType().Name == "UnknownTarget")
                    graph.SetTargetInactive(target);
            }

            foreach (var node in graph.GetNodes<AbstractMaterialNode>().Where(node => node.canDeleteNode).ToArray())
                graph.RemoveNode(node);
            foreach (var property in graph.properties.ToArray())
                graph.RemoveGraphInput(property);
            foreach (var keyword in graph.keywords.ToArray())
                graph.RemoveGraphInput(keyword);

            ConfigureUniversalTarget(graph, settings);
            graph.ValidateGraph();

            foreach (var block in graph.GetNodes<BlockNode>())
                graph.RemoveEdges(graph.GetEdges(block.GetSlotReference(0)).ToArray());
            return graph;
        }

        static void ConfigureUniversalTarget(GraphData graph, SurfaceSettings settings)
        {
            var target = graph.activeTargets.FirstOrDefault(item => item.GetType().Name == "UniversalTarget");
            if (target == null)
                throw new InvalidOperationException("URP UniversalTarget was not found in the Shader Graph template.");

            SetEnum(target, "surfaceType", "m_SurfaceType", settings.Transparent ? "Transparent" : "Opaque");
            SetEnum(target, "alphaMode", "m_AlphaMode", "Alpha");
            SetEnum(target, "zWriteControl", "m_ZWriteControl", settings.Transparent ? "ForceDisabled" : "Auto");
            SetEnum(target, "zTestMode", "m_ZTestMode", "LEqual");
            SetEnum(target, "renderFace", "m_RenderFace", settings.DoubleSided ? "Both" : "Front");
            SetBool(target, "alphaClip", "m_AlphaClip", settings.AlphaClip);
            SetBool(target, "castShadows", "m_CastShadows", settings.CastShadows);
            SetBool(target, "receiveShadows", "m_ReceiveShadows", settings.ReceiveShadows);
        }

        static void SetEnum(object target, string propertyName, string fieldName, string value)
        {
            var type = target.GetType();
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, Enum.Parse(property.PropertyType, value));
                return;
            }

            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) throw new MissingMemberException(type.FullName, propertyName + "/" + fieldName);
            field.SetValue(target, Enum.Parse(field.FieldType, value));
        }

        static void SetBool(object target, string propertyName, string fieldName, bool value)
        {
            var type = target.GetType();
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value);
                return;
            }

            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) throw new MissingMemberException(type.FullName, propertyName + "/" + fieldName);
            field.SetValue(target, value);
        }

        static BlockNode Block(GraphData graph, BlockFieldDescriptor descriptor)
        {
            var block = graph.GetNodes<BlockNode>().FirstOrDefault(item => item.descriptor == descriptor);
            if (block != null) return block;
            block = new BlockNode();
            block.Init(descriptor);
            var context = descriptor.shaderStage == ShaderStage.Vertex ? graph.vertexContext : graph.fragmentContext;
            graph.AddBlock(block, context, context.blocks.Count);
            return block;
        }

        static void CreateDissolve()
        {
            var path = Root + "/01_Dissolve/SG_01_Dissolve.shadergraph";
            var graph = NewGraph(LitTemplate, "Shader Graph Techniques/01 Dissolve",
                new SurfaceSettings(alphaClip: true));
            var b = new Builder(graph);

            b.BeginGroup("01 Inputs", -1420f, -420f);
            var progress = b.Float("Progress", "_Progress", 0f, -1360f, -350f, 0f, 1f);
            var noiseScale = b.Float("Noise Scale", "_NoiseScale", 8f, -1360f, -190f, 0.01f, 32f);
            var edgeWidth = b.Float("Edge Width", "_EdgeWidth", .06f, -1360f, -30f, 0f, .5f);
            var baseColor = b.Color("Base Color", "_BaseColor", new Color(.12f, .28f, .72f, 1f), false, -1360f, 130f);
            var edgeColor = b.Color("Edge Color", "_EdgeColor", new Color(1.5f, .22f, .025f, 1f), true, -1360f, 290f);
            var edgeIntensity = b.Float("Edge Intensity", "_EdgeIntensity", 3f, -1360f, 450f, 0f, 10f);
            b.EndGroup();

            b.BeginGroup("02 Dissolve Mask", -1000f, -420f);
            var uv = b.Add<UVNode>("UV0", -940f, -300f);
            var noise = b.Add<NoiseNode>("Simple Noise", -700f, -260f);
            b.Connect(b.Out(uv, 0), b.In(noise, 0));
            b.Connect(noiseScale, b.In(noise, 1));
            var m = b.Saturate(b.Out(noise, 0), "Clamp Noise", -460f, -260f);
            var p = b.Saturate(progress, "Clamp Progress", -940f, -80f);
            var w = b.Maximum(edgeWidth, b.Const(.0001f, -940f, 80f), "Safe Edge Width", -700f, 40f);
            var low = b.Subtract(b.Multiply(w, b.Const(-1f, -700f, 180f), "-Width", -460f, 100f),
                b.Const(.0001f, -460f, 230f), "Start Below Zero", -210f, 140f);
            var cut = b.Lerp(low, b.Const(1.0001f, -210f, 280f), p, "Endpoint-safe Cut", 40f, 20f);
            var visible = b.Step(cut, m, "Visible", 300f, -180f);
            var edgeEnd = b.AddOp(cut, w, "Cut + Width", 280f, 80f);
            var edgeFade = b.Smoothstep(cut, edgeEnd, m, "Edge Fade", 540f, -20f);
            var edge = b.Multiply(visible, b.OneMinus(edgeFade, "Invert Edge Fade", 770f, -20f), "Visible Edge", 1010f, -80f);
            b.EndGroup();

            b.BeginGroup("03 Surface Output", 1240f, -260f);
            var emission = b.Multiply(b.Multiply(edgeColor, edgeIntensity, "HDR Edge", 1300f, 80f), edge, "Edge Emission", 1540f, 30f);
            b.Connect(baseColor, Block(graph, BlockFields.SurfaceDescription.BaseColor).GetSlotReference(0));
            b.Connect(emission, Block(graph, BlockFields.SurfaceDescription.Emission).GetSlotReference(0));
            b.Connect(visible, Block(graph, BlockFields.SurfaceDescription.Alpha).GetSlotReference(0));
            b.Connect(b.Const(.5f, 1530f, 260f), Block(graph, BlockFields.SurfaceDescription.AlphaClipThreshold).GetSlotReference(0));
            b.Connect(b.Const(0f, 1530f, 380f), Block(graph, BlockFields.SurfaceDescription.Metallic).GetSlotReference(0));
            b.Connect(b.Const(.35f, 1530f, 500f), Block(graph, BlockFields.SurfaceDescription.Smoothness).GetSlotReference(0));
            b.EndGroup();
            AddVertexPassthrough(graph, b, 1240f, 660f);
            FinishGraph(path, graph);
        }

        static void CreateBarrier()
        {
            var path = Root + "/02_IntersectionShield/SG_02_IntersectionShield.shadergraph";
            var graph = NewGraph(UnlitTemplate, "Shader Graph Techniques/02 Depth Barrier",
                new SurfaceSettings(transparent: true, castShadows: false, receiveShadows: false));
            var b = new Builder(graph);

            b.BeginGroup("01 Inputs", -1520f, -420f);
            var width = b.Float("Contact Width", "_ContactWidth", .15f, -1460f, -350f, .001f, 1f);
            var rimPower = b.Float("Rim Power", "_RimPower", 4f, -1460f, -190f, .1f, 12f);
            var rimIntensity = b.Float("Rim Intensity", "_RimIntensity", 2f, -1460f, -30f, 0f, 10f);
            var contactIntensity = b.Float("Contact Intensity", "_ContactIntensity", 5f, -1460f, 130f, 0f, 15f);
            var shieldColor = b.Color("Shield Color", "_ShieldColor", new Color(.02f, 1.2f, 1.55f, 1f), true, -1460f, 290f);
            var opacity = b.Float("Opacity", "_Opacity", 1f, -1460f, 450f, 0f, 1f);
            b.EndGroup();

            b.BeginGroup("02 Depth Contact", -1060f, -420f);
            var screenPosition = b.Add<ScreenPositionNode>("Screen Position (Default)", -1000f, -330f);
            screenPosition.screenSpaceType = ScreenSpaceType.Default;
            var worldPosition = b.Position("Position (World)", CoordinateSpace.World, -1000f, -150f);
            var depth = b.Add<SceneDepthDifferenceNode>("Scene Depth Difference (Eye)", -710f, -260f);
            depth.depthSamplingMode = DepthSamplingMode.Eye;
            b.Connect(b.Out(screenPosition, 0), depth.GetSlotReference(1));
            b.Connect(b.Out(worldPosition, 0), depth.GetSlotReference(2));
            var d = depth.GetSlotReference(0);
            var safeWidth = b.Maximum(width, b.Const(.0001f, -700f, 40f), "Safe Contact Width", -440f, 20f);
            var positive = b.Step(b.Const(0f, -700f, 180f), d, "Only Positive Depth", -430f, -140f);
            var falloff = b.OneMinus(b.Saturate(b.Divide(d, safeWidth, "Depth / Width", -180f, 20f), "Clamp Contact Falloff", 70f, 20f),
                "Near Is Bright", 300f, 20f);
            var contact = b.Multiply(positive, falloff, "Contact Mask", 530f, -80f);
            b.EndGroup();

            b.BeginGroup("03 Fresnel and Output", 760f, -420f);
            var normal = b.Normal("Normal Vector (World)", CoordinateSpace.World, 820f, -300f);
            var view = b.View("View Direction (World)", CoordinateSpace.World, 820f, -120f);
            var fresnel = b.Add<FresnelNode>("World-space Fresnel", 1080f, -220f);
            b.Connect(b.Out(normal, 0), b.In(fresnel, 0));
            b.Connect(b.Out(view, 0), b.In(fresnel, 1));
            b.Connect(rimPower, b.In(fresnel, 2));
            var rim = b.Out(fresnel, 0);
            var brightness = b.AddOp(b.Const(.2f, 1080f, 20f),
                b.AddOp(b.Multiply(rim, rimIntensity, "Rim Light", 1320f, -90f),
                    b.Multiply(contact, contactIntensity, "Contact Light", 1320f, 80f), "Rim + Contact", 1570f, 10f),
                "Barrier Brightness", 1810f, 10f);
            var color = b.Multiply(shieldColor, brightness, "HDR Barrier Color", 2050f, 10f);
            var alphaTerms = b.AddOp(b.Const(.04f, 1320f, 300f),
                b.AddOp(b.Multiply(rim, b.Const(.25f, 1080f, 390f), "Rim Alpha", 1570f, 260f),
                    b.Multiply(contact, b.Const(.8f, 1320f, 450f), "Contact Alpha", 1570f, 400f), "Alpha Terms", 1810f, 330f),
                "Base + Detail Alpha", 2050f, 320f);
            var alpha = b.Saturate(b.Multiply(alphaTerms, opacity, "Opacity", 2290f, 320f), "Final Alpha", 2520f, 320f);
            b.Connect(color, Block(graph, BlockFields.SurfaceDescription.BaseColor).GetSlotReference(0));
            b.Connect(alpha, Block(graph, BlockFields.SurfaceDescription.Alpha).GetSlotReference(0));
            b.EndGroup();
            AddVertexPassthrough(graph, b, 2040f, 520f);
            FinishGraph(path, graph);
        }

        static void CreateHeatHaze(SubGraphAsset manualTime)
        {
            var path = Root + "/03_HeatHaze/SG_03_HeatHaze.shadergraph";
            var graph = NewGraph(UnlitTemplate, "Shader Graph Techniques/03 Heat Haze",
                new SurfaceSettings(transparent: true, castShadows: false, receiveShadows: false));
            var b = new Builder(graph);

            b.BeginGroup("01 Inputs", -1880f, -620f);
            var noiseScale = b.Float("Noise Scale", "_NoiseScale", 6f, -1820f, -550f, .01f, 32f);
            var distortion = b.Float("Distortion", "_Distortion", .01f, -1820f, -390f, 0f, .08f);
            var opacity = b.Float("Opacity", "_Opacity", 1f, -1820f, -230f, 0f, 1f);
            var flowA = b.Vector2("Flow A", "_FlowA", new Vector2(.12f, .2f), -1820f, -70f);
            var flowB = b.Vector2("Flow B", "_FlowB", new Vector2(-.18f, .1f), -1820f, 90f);
            var screenEdgeFade = b.Float("Screen Edge Fade", "_ScreenEdgeFade", .04f, -1820f, 250f, 0f, .25f);
            var useManual = b.Float("Use Manual Time", "_UseManualTime", 0f, -1820f, 410f, 0f, 1f);
            var manual = b.Float("Manual Time", "_ManualTime", 0f, -1820f, 570f);
            b.EndGroup();

            b.BeginGroup("02 Manual Time", -1440f, 360f);
            var timeNode = b.Add<SubGraphNode>("ManualTime Sub Graph", -1380f, 420f);
            timeNode.asset = manualTime;
            b.Connect(useManual, b.InputByName(timeNode, "Use Manual Time"));
            b.Connect(manual, b.InputByName(timeNode, "Manual Time"));
            var time = b.OutputByName(timeNode, "Time");
            b.EndGroup();

            b.BeginGroup("03 Flow Noise", -1440f, -620f);
            var uv = b.Add<UVNode>("Quad UV0", -1380f, -520f);
            var scaledUv = b.Multiply(b.Out(uv, 0), noiseScale, "UV x Noise Scale", -1130f, -470f);
            var uvA = b.AddOp(scaledUv, b.Multiply(time, flowA, "Time x Flow A", -1130f, -300f), "Flowing UV A", -870f, -430f);
            var noiseA = b.Add<NoiseNode>("Simple Noise A", -620f, -420f);
            b.Connect(uvA, b.In(noiseA, 0));
            b.Connect(b.Const(1f, -850f, -210f), b.In(noiseA, 1));
            var uvB = b.AddOp(b.AddOp(scaledUv, b.Multiply(time, flowB, "Time x Flow B", -1130f, -100f), "Flowing UV B", -870f, -110f),
                b.ConstVector2(new Vector2(19.7f, 3.1f), -620f, -80f), "Offset UV B", -360f, -130f);
            var noiseB = b.Add<NoiseNode>("Simple Noise B", -100f, -130f);
            b.Connect(uvB, b.In(noiseB, 0));
            b.Connect(b.Const(1f, -350f, 40f), b.In(noiseB, 1));
            var v = b.Subtract(b.Multiply(b.Vector2From(b.Out(noiseA, 0), b.Out(noiseB, 0), "Noise Vector", 160f, -280f),
                    b.Const(2f, 160f, -100f), "Noise x 2", 410f, -250f),
                b.ConstVector2(Vector2.one, 410f, -70f), "Signed Distortion", 650f, -220f);
            b.EndGroup();

            b.BeginGroup("04 Shape and Screen Safety", -1380f, 40f);
            var centered = b.Subtract(b.Multiply(b.Out(uv, 0), b.Const(2f, -1320f, 100f), "UV x 2", -1080f, 100f),
                b.ConstVector2(Vector2.one, -1080f, 250f), "Centered UV", -820f, 140f);
            var radius = b.Length(centered, "Quad Radius", -570f, 140f);
            var shape = b.OneMinus(b.Smoothstep(b.Const(.65f, -570f, 300f), b.Const(1f, -570f, 420f), radius,
                "Soft Quad Shape", -310f, 210f), "Shape Mask", -60f, 210f);

            var screen = b.Add<ScreenPositionNode>("Screen Position (Default)", -1320f, 510f);
            screen.screenSpaceType = ScreenSpaceType.Default;
            var screenSplit = b.Split(b.Out(screen, 0), "Screen XY", -1080f, 520f);
            var minX = b.Minimum(b.Out(screenSplit, 0), b.OneMinus(b.Out(screenSplit, 0), "1 - Screen X", -820f, 650f), "X Edge Distance", -570f, 570f);
            var minY = b.Minimum(b.Out(screenSplit, 1), b.OneMinus(b.Out(screenSplit, 1), "1 - Screen Y", -820f, 790f), "Y Edge Distance", -570f, 730f);
            var edgeDistance = b.Minimum(minX, minY, "Nearest Screen Edge", -310f, 650f);
            var safeEdgeFade = b.Maximum(screenEdgeFade, b.Const(.0001f, -310f, 810f), "Safe Edge Fade", -60f, 760f);
            var screenMask = b.Smoothstep(b.Const(0f, -60f, 900f), safeEdgeFade, edgeDistance, "Screen Edge Mask", 190f, 690f);
            var screenUv = b.Vector2From(b.Out(screenSplit, 0), b.Out(screenSplit, 1), "Screen UV", 190f, 510f);
            b.EndGroup();

            b.BeginGroup("05 Distort and Sample", 890f, -300f);
            var amount = b.Multiply(b.Multiply(distortion, shape, "Shape Distortion", 950f, -190f), screenMask, "Safe Distortion", 1190f, -160f);
            var sampleUvRaw = b.AddOp(screenUv, b.Multiply(v, amount, "UV Offset", 1430f, -190f), "Distorted Screen UV", 1680f, -110f);
            var sampleUv = b.Clamp(sampleUvRaw, b.ConstVector2(new Vector2(.001f, .001f), 1430f, 40f),
                b.ConstVector2(new Vector2(.999f, .999f), 1430f, 190f), "Clamp to Screen", 1910f, 20f);
            var sceneColor = b.Add<SceneColorNode>("Scene Color", 2160f, -40f);
            b.Connect(sampleUv, sceneColor.GetSlotReference(SceneColorNode.ScreenPositionSlotId));
            var alpha = b.Multiply(shape, b.Saturate(opacity, "Clamp Opacity", 1910f, 270f), "Soft Alpha", 2160f, 250f);
            b.Connect(sceneColor.GetSlotReference(SceneColorNode.OutputSlotId), Block(graph, BlockFields.SurfaceDescription.BaseColor).GetSlotReference(0));
            b.Connect(alpha, Block(graph, BlockFields.SurfaceDescription.Alpha).GetSlotReference(0));
            b.EndGroup();
            AddVertexPassthrough(graph, b, 2160f, 430f);
            FinishGraph(path, graph);
        }

        static void CreateSnowTriplanar()
        {
            var path = Root + "/04_TriplanarSnow/SG_04_TriplanarSnow.shadergraph";
            var graph = NewGraph(LitTemplate, "Shader Graph Techniques/04 Snow Triplanar", new SurfaceSettings());
            var b = new Builder(graph);

            b.BeginGroup("01 Inputs", -1740f, -540f);
            var baseMap = b.Texture("Base Map", "_BaseMap", -1680f, -470f);
            var baseTint = b.Color("Base Tint", "_BaseTint", Color.white, false, -1680f, -310f);
            var textureScale = b.Float("Texture Scale", "_TextureScale", 1f, -1680f, -150f, .05f, 8f);
            var snowAmount = b.Float("Snow Amount", "_SnowAmount", .5f, -1680f, 10f, 0f, 1f);
            var snowColor = b.Color("Snow Color", "_SnowColor", new Color(.78f, .9f, 1f, 1f), false, -1680f, 170f);
            var snowNoiseScale = b.Float("Snow Noise Scale", "_SnowNoiseScale", 1.5f, -1680f, 330f, .05f, 12f);
            var snowSoftness = b.Float("Snow Softness", "_SnowSoftness", .12f, -1680f, 490f, 0f, .5f);
            b.EndGroup();

            b.BeginGroup("02 World Triplanar", -1280f, -540f);
            var position = b.Position("Position (World)", CoordinateSpace.World, -1220f, -450f);
            var normal = b.Normal("Normal Vector (World)", CoordinateSpace.World, -1220f, -270f);
            var normalizedNormal = b.Normalize(b.Out(normal, 0), "Normalize World Normal", -960f, -270f);
            var triplanar = b.Add<TriplanarNode>("World Triplanar", -690f, -410f);
            triplanar.textureType = TextureType.Default;
            triplanar.inputSpace = CoordinateSpace.World;
            triplanar.normalOutputSpace = CoordinateSpace.Tangent;
            triplanar.UpdateNodeAfterDeserialization();
            b.Connect(baseMap, triplanar.GetSlotReference(TriplanarNode.TextureInputId));
            b.Connect(b.Out(position, 0), triplanar.GetSlotReference(TriplanarNode.PositionInputId));
            b.Connect(normalizedNormal, triplanar.GetSlotReference(TriplanarNode.NormalInputId));
            b.Connect(textureScale, triplanar.GetSlotReference(TriplanarNode.TileInputId));
            b.Connect(b.Const(4f, -930f, -30f), triplanar.GetSlotReference(TriplanarNode.BlendInputId));
            var rock = b.Multiply(triplanar.GetSlotReference(TriplanarNode.OutputSlotId), baseTint, "Rock x Tint", -400f, -320f);
            b.EndGroup();

            b.BeginGroup("03 Snow Coverage", -1280f, 80f);
            var up = b.Dot(normalizedNormal, b.ConstVector3(Vector3.up, -1220f, 150f), "Up-facing Amount", -960f, 120f);
            var slope = b.Smoothstep(b.Const(.35f, -960f, 260f), b.Const(.85f, -960f, 380f), up, "Slope Mask", -700f, 220f);
            var positionSplit = b.Split(b.Out(position, 0), "World Position XYZ", -1220f, 510f);
            var worldXZ = b.Vector2From(b.Out(positionSplit, 0), b.Out(positionSplit, 2), "World XZ", -960f, 510f);
            var snowNoise = b.Add<NoiseNode>("Snow Simple Noise", -700f, 500f);
            b.Connect(worldXZ, b.In(snowNoise, 0));
            b.Connect(snowNoiseScale, b.In(snowNoise, 1));
            var n = b.Saturate(b.Out(snowNoise, 0), "Clamp Snow Noise", -430f, 500f);
            var a = b.Saturate(snowAmount, "Clamp Snow Amount", -700f, 690f);
            var s = b.Maximum(snowSoftness, b.Const(.0001f, -700f, 830f), "Safe Softness", -430f, 770f);
            var highCut = b.AddOp(b.AddOp(b.Const(1f, -430f, 920f), s, "1 + Softness", -180f, 870f),
                b.Const(.0001f, -180f, 1000f), "Snow Amount 0 Cut", 70f, 900f);
            var lowCut = b.Subtract(b.Multiply(s, b.Const(-1f, -430f, 1100f), "-Softness", -180f, 1080f),
                b.Const(.0001f, -180f, 1180f), "Snow Amount 1 Cut", 70f, 1080f);
            var cut = b.Lerp(highCut, lowCut, a, "Snow Cut", 320f, 970f);
            var cover = b.Smoothstep(b.Subtract(cut, s, "Cut - Softness", 560f, 850f),
                b.AddOp(cut, s, "Cut + Softness", 560f, 1040f), n, "Snow Coverage", 810f, 930f);
            var snow = b.Multiply(slope, cover, "Slope x Coverage", 1060f, 620f);
            b.EndGroup();

            b.BeginGroup("04 Surface Output", 1320f, -80f);
            var finalColor = b.Lerp(rock, snowColor, snow, "Rock / Snow", 1380f, 20f);
            var smoothness = b.Lerp(b.Const(.2f, 1380f, 210f), b.Const(.35f, 1380f, 330f), snow, "Snow Smoothness", 1630f, 250f);
            b.Connect(finalColor, Block(graph, BlockFields.SurfaceDescription.BaseColor).GetSlotReference(0));
            b.Connect(smoothness, Block(graph, BlockFields.SurfaceDescription.Smoothness).GetSlotReference(0));
            b.Connect(b.Const(0f, 1630f, 430f), Block(graph, BlockFields.SurfaceDescription.Metallic).GetSlotReference(0));
            b.EndGroup();
            AddVertexPassthrough(graph, b, 1320f, 520f);
            FinishGraph(path, graph);
        }

        static void CreateInteractiveGrass(SubGraphAsset manualTime)
        {
            var path = Root + "/05_InteractiveGrass/SG_05_InteractiveGrass.shadergraph";
            var graph = NewGraph(UnlitTemplate, "Shader Graph Techniques/05 Interactive Grass",
                new SurfaceSettings(doubleSided: true));
            var b = new Builder(graph);

            b.BeginGroup("01 Inputs", -2160f, -720f);
            var interactor = b.Vector3("Interactor Position WS", "_InteractorPositionWS", Vector3.zero, -2100f, -650f);
            var enabled = b.Float("Interactor Enabled", "_InteractorEnabled", 0f, -2100f, -490f, 0f, 1f);
            var radius = b.Float("Bend Radius", "_BendRadius", 1f, -2100f, -330f, .01f, 5f);
            var strength = b.Float("Bend Strength", "_BendStrength", .6f, -2100f, -170f, 0f, 2f);
            var windDirection = b.Vector2("Wind Direction XZ", "_WindDirectionXZ", Vector2.right, -2100f, -10f);
            var windStrength = b.Float("Wind Strength", "_WindStrength", .08f, -2100f, 150f, 0f, .5f);
            var windSpeed = b.Float("Wind Speed", "_WindSpeed", 1.5f, -2100f, 310f, 0f, 8f);
            var rootColor = b.Color("Root Color", "_RootColor", new Color(.015f, .12f, .025f, 1f), false, -2100f, 470f);
            var tipColor = b.Color("Tip Color", "_TipColor", new Color(.16f, .75f, .08f, 1f), false, -2100f, 630f);
            var useManual = b.Float("Use Manual Time", "_UseManualTime", 0f, -2100f, 790f, 0f, 1f);
            var manual = b.Float("Manual Time", "_ManualTime", 0f, -2100f, 950f);
            b.EndGroup();

            b.BeginGroup("02 Manual Time", -1720f, 760f);
            var timeNode = b.Add<SubGraphNode>("ManualTime Sub Graph", -1660f, 820f);
            timeNode.asset = manualTime;
            b.Connect(useManual, b.InputByName(timeNode, "Use Manual Time"));
            b.Connect(manual, b.InputByName(timeNode, "Manual Time"));
            var time = b.OutputByName(timeNode, "Time");
            b.EndGroup();

            b.BeginGroup("03 Root-weighted Interaction", -1720f, -720f);
            var position = b.Position("Position (World, Vertex)", CoordinateSpace.World, -1660f, -620f);
            var positionSplit = b.Split(b.Out(position, 0), "World Position XYZ", -1410f, -600f);
            var uv = b.Add<UVNode>("UV0", -1660f, -390f);
            var uvSplit = b.Split(b.Out(uv, 0), "UV XY", -1410f, -390f);
            var h = b.Saturate(b.Out(uvSplit, 1), "Height", -1160f, -390f);
            var rootMask = b.Multiply(h, h, "Root Mask (h squared)", -910f, -390f);
            var interactorSplit = b.Split(interactor, "Interactor XYZ", -1660f, -170f);
            var q = b.Vector2From(b.Subtract(b.Out(positionSplit, 0), b.Out(interactorSplit, 0), "Delta X", -1160f, -170f),
                b.Subtract(b.Out(positionSplit, 2), b.Out(interactorSplit, 2), "Delta Z", -1160f, -20f), "Horizontal Delta", -900f, -110f);
            var d = b.Length(q, "Horizontal Distance", -650f, -110f);
            var safeD = b.Maximum(d, b.Const(.0001f, -650f, 40f), "Safe Distance", -400f, -20f);
            var outward = b.Divide(q, safeD, "Outward Direction", -150f, -100f);
            var safeRadius = b.Maximum(radius, b.Const(.0001f, -650f, 170f), "Safe Radius", -400f, 160f);
            var weight = b.Multiply(b.OneMinus(b.Smoothstep(b.Const(0f, -400f, 300f), safeRadius, d,
                    "Bend Falloff", -150f, 210f), "Near Interactor", 100f, 210f),
                b.Saturate(enabled, "Clamp Interactor Toggle", -150f, 370f), "Interaction Weight", 350f, 260f);
            var interaction = b.Multiply(b.Multiply(outward, weight, "Outward x Weight", 600f, 50f), strength, "Bend Offset", 850f, 40f);
            b.EndGroup();

            b.BeginGroup("04 Wind and Vertex Output", -900f, 430f);
            var safeWindLength = b.Maximum(b.Length(windDirection, "Wind Direction Length", -840f, 500f),
                b.Const(.0001f, -840f, 640f), "Safe Wind Length", -590f, 570f);
            var windDir = b.Divide(windDirection, safeWindLength, "Normalized Wind Direction", -340f, 530f);
            var phase = b.AddOp(b.AddOp(b.Multiply(b.Out(positionSplit, 0), b.Const(.7f, -590f, 760f), "World X Phase", -340f, 740f),
                    b.Multiply(b.Out(positionSplit, 2), b.Const(.9f, -590f, 900f), "World Z Phase", -340f, 880f), "Spatial Phase", -80f, 800f),
                b.Multiply(time, windSpeed, "Time Phase", -340f, 1030f), "Wind Phase", 180f, 860f);
            var wind = b.Multiply(b.Multiply(windDir, b.Sine(phase, "Wind Sine", 430f, 830f), "Directional Sine", 680f, 690f),
                windStrength, "Wind Offset", 930f, 690f);
            var move = b.Multiply(b.AddOp(interaction, wind, "Interaction + Wind", 1180f, 310f), rootMask, "Root-weighted Move", 1430f, 330f);
            var moveSplit = b.Split(move, "Move XZ", 1680f, 330f);
            var moved = b.Vector3From(b.AddOp(b.Out(positionSplit, 0), b.Out(moveSplit, 0), "Moved X", 1930f, 230f),
                b.Out(positionSplit, 1), b.AddOp(b.Out(positionSplit, 2), b.Out(moveSplit, 1), "Moved Z", 1930f, 430f),
                "Moved Position WS", 2190f, 330f);
            var transform = b.Add<TransformNode>("World to Object (Position)", 2440f, 330f);
            transform.conversion = new CoordinateSpaceConversion(CoordinateSpace.World, CoordinateSpace.Object);
            transform.conversionType = ConversionType.Position;
            transform.normalize = false;
            b.Connect(moved, b.In(transform, 0));
            b.Connect(b.Out(transform, 0), Block(graph, BlockFields.VertexDescription.Position).GetSlotReference(0));
            b.EndGroup();

            b.BeginGroup("05 Fragment Gradient", 1040f, -400f);
            var fragmentUv = b.Add<UVNode>("UV0 (Fragment)", 1100f, -320f);
            var fragmentSplit = b.Split(b.Out(fragmentUv, 0), "Fragment UV", 1350f, -320f);
            var gradient = b.Lerp(rootColor, tipColor, b.Saturate(b.Out(fragmentSplit, 1), "Clamp Height", 1600f, -320f),
                "Root to Tip Color", 1850f, -250f);
            b.Connect(gradient, Block(graph, BlockFields.SurfaceDescription.BaseColor).GetSlotReference(0));
            b.Connect(b.Const(1f, 1850f, -80f), Block(graph, BlockFields.SurfaceDescription.Alpha).GetSlotReference(0));
            b.EndGroup();
            FinishGraph(path, graph);
        }

        static void CreateScanPulse()
        {
            var path = Root + "/06_ScanPulse/SG_06_ScanPulse.shadergraph";
            var graph = NewGraph(LitTemplate, "Shader Graph Techniques/06 Scan Pulse", new SurfaceSettings());
            var b = new Builder(graph);

            b.BeginGroup("01 Inputs", -1460f, -430f);
            var center = b.Vector3("Scan Center WS", "_ScanCenterWS", Vector3.zero, -1400f, -360f);
            var radius = b.Float("Scan Radius", "_ScanRadius", 0f, -1400f, -200f, 0f, 30f);
            var width = b.Float("Scan Width", "_ScanWidth", .25f, -1400f, -40f, .001f, 2f);
            var active = b.Float("Scan Active", "_ScanActive", 0f, -1400f, 120f, 0f, 1f);
            var scanColor = b.Color("Scan Color", "_ScanColor", new Color(.01f, 1.3f, 1.8f, 1f), true, -1400f, 280f);
            var scanIntensity = b.Float("Scan Intensity", "_ScanIntensity", 3f, -1400f, 440f, 0f, 12f);
            var baseColor = b.Color("Base Color", "_BaseColor", new Color(.2f, .24f, .29f, 1f), false, -1400f, 600f);
            b.EndGroup();

            b.BeginGroup("02 Spherical Ring", -980f, -430f);
            var position = b.Position("Position (World)", CoordinateSpace.World, -920f, -330f);
            var distance = b.Distance(b.Out(position, 0), center, "Distance to Center", -660f, -250f);
            var safeRadius = b.Maximum(radius, b.Const(0f, -920f, -60f), "Non-negative Radius", -660f, -40f);
            var safeWidth = b.Maximum(width, b.Const(.0001f, -920f, 100f), "Safe Ring Width", -660f, 120f);
            var shellDistance = b.Absolute(b.Subtract(distance, safeRadius, "Distance - Radius", -390f, -130f), "Distance to Shell", -140f, -130f);
            var ringShape = b.OneMinus(b.Smoothstep(b.Const(0f, -390f, 250f), safeWidth, shellDistance,
                "Ring Falloff", 110f, 40f), "Bright Shell", 360f, 40f);
            var ring = b.Multiply(ringShape, b.Saturate(active, "Clamp Active", 110f, 250f), "Active Ring", 610f, 100f);
            b.EndGroup();

            b.BeginGroup("03 Surface Output", 860f, -260f);
            var emission = b.Multiply(b.Multiply(scanColor, scanIntensity, "HDR Scan", 920f, -120f), ring, "Scan Emission", 1170f, -50f);
            b.Connect(baseColor, Block(graph, BlockFields.SurfaceDescription.BaseColor).GetSlotReference(0));
            b.Connect(emission, Block(graph, BlockFields.SurfaceDescription.Emission).GetSlotReference(0));
            b.Connect(b.Const(0f, 1160f, 170f), Block(graph, BlockFields.SurfaceDescription.Metallic).GetSlotReference(0));
            b.Connect(b.Const(.25f, 1160f, 290f), Block(graph, BlockFields.SurfaceDescription.Smoothness).GetSlotReference(0));
            b.EndGroup();
            AddVertexPassthrough(graph, b, 860f, 420f);
            FinishGraph(path, graph);
        }

        static void AddVertexPassthrough(GraphData graph, Builder builder, float x, float y)
        {
            builder.BeginGroup("00 Vertex Passthrough", x, y);
            var position = builder.Position("Position (Object)", CoordinateSpace.Object, x + 60f, y + 70f);
            builder.Connect(builder.Out(position, 0), Block(graph, BlockFields.VertexDescription.Position).GetSlotReference(0));
            builder.EndGroup();
        }

        static void FinishGraph(string path, GraphData graph)
        {
            graph.ValidateGraph();
            WriteGraph(path, graph);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("Generated Shader Graph did not import as a supported shader: " + path);
        }

        static void WriteGraph(string path, GraphData graph)
        {
            // Write in place so the .meta GUID remains stable across regeneration.
            // Heat Haze and Grass reference ManualTime by GUID, and package exports
            // must preserve that common dependency identity.
            if (FileUtilities.WriteShaderGraphToDisk(path, graph) == null)
                throw new IOException("Failed to write Shader Graph: " + path);
        }

        readonly struct SurfaceSettings
        {
            public readonly bool Transparent;
            public readonly bool AlphaClip;
            public readonly bool DoubleSided;
            public readonly bool CastShadows;
            public readonly bool ReceiveShadows;

            public SurfaceSettings(bool transparent = false, bool alphaClip = false, bool doubleSided = false,
                bool castShadows = true, bool receiveShadows = true)
            {
                Transparent = transparent;
                AlphaClip = alphaClip;
                DoubleSided = doubleSided;
                CastShadows = castShadows;
                ReceiveShadows = receiveShadows;
            }
        }

        sealed class Builder
        {
            readonly GraphData graph;
            GroupData currentGroup;

            public Builder(GraphData graphData) => graph = graphData;

            public void BeginGroup(string title, float x, float y)
            {
                currentGroup = new GroupData(title, new Vector2(x, y));
                graph.CreateGroup(currentGroup);
            }

            public void EndGroup() => currentGroup = null;

            public T Add<T>(string name, float x, float y) where T : AbstractMaterialNode, new()
            {
                var node = new T { name = name, precision = Precision.Single };
                var state = node.drawState;
                state.position = new Rect(x, y, 220f, 120f);
                node.drawState = state;
                graph.AddNode(node);
                if (currentGroup != null) graph.SetGroup(node, currentGroup);
                return node;
            }

            public SlotReference Float(string displayName, string referenceName, float value, float x, float y,
                float? min = null, float? max = null)
            {
                var property = new Vector1ShaderProperty
                {
                    displayName = displayName,
                    overrideReferenceName = referenceName,
                    value = value,
                    generatePropertyBlock = true
                };
                if (min.HasValue && max.HasValue)
                {
                    property.floatType = FloatType.Slider;
                    property.rangeValues = new Vector2(min.Value, max.Value);
                }
                SetPerMaterial(property);
                graph.AddGraphInput(property);
                return PropertyNode(property, displayName, x, y);
            }

            public SlotReference Vector2(string displayName, string referenceName, Vector2 value, float x, float y)
            {
                var property = new Vector2ShaderProperty
                {
                    displayName = displayName,
                    overrideReferenceName = referenceName,
                    value = new Vector4(value.x, value.y, 0f, 0f),
                    generatePropertyBlock = true
                };
                SetPerMaterial(property);
                graph.AddGraphInput(property);
                return PropertyNode(property, displayName, x, y);
            }

            public SlotReference Vector3(string displayName, string referenceName, Vector3 value, float x, float y)
            {
                var property = new Vector3ShaderProperty
                {
                    displayName = displayName,
                    overrideReferenceName = referenceName,
                    value = new Vector4(value.x, value.y, value.z, 0f),
                    generatePropertyBlock = true
                };
                SetPerMaterial(property);
                graph.AddGraphInput(property);
                return PropertyNode(property, displayName, x, y);
            }

            public SlotReference Color(string displayName, string referenceName, Color value, bool hdr, float x, float y)
            {
                var property = new ColorShaderProperty
                {
                    displayName = displayName,
                    overrideReferenceName = referenceName,
                    value = value,
                    colorMode = hdr ? ColorMode.HDR : ColorMode.Default,
                    generatePropertyBlock = true
                };
                SetPerMaterial(property);
                graph.AddGraphInput(property);
                return PropertyNode(property, displayName, x, y);
            }

            public SlotReference Texture(string displayName, string referenceName, float x, float y)
            {
                var property = new Texture2DShaderProperty
                {
                    displayName = displayName,
                    overrideReferenceName = referenceName,
                    generatePropertyBlock = true,
                    useTilingAndOffset = false,
                    useTexelSize = false,
                    defaultType = Texture2DShaderProperty.DefaultType.Grey
                };
                SetPerMaterial(property);
                graph.AddGraphInput(property);
                return PropertyNode(property, displayName, x, y);
            }

            SlotReference PropertyNode(AbstractShaderProperty property, string name, float x, float y)
            {
                var node = Add<PropertyNode>(name, x, y);
                node.property = property;
                node.UpdateNodeAfterDeserialization();
                return Out(node, 0);
            }

            static void SetPerMaterial(AbstractShaderProperty property)
            {
                property.overrideHLSLDeclaration = true;
                property.hlslDeclarationOverride = HLSLDeclaration.UnityPerMaterial;
            }

            public PositionNode Position(string name, CoordinateSpace space, float x, float y)
            {
                var node = Add<PositionNode>(name, x, y);
                SetGeometrySpace(node, space);
                return node;
            }

            public NormalVectorNode Normal(string name, CoordinateSpace space, float x, float y)
            {
                var node = Add<NormalVectorNode>(name, x, y);
                SetGeometrySpace(node, space);
                return node;
            }

            public ViewDirectionNode View(string name, CoordinateSpace space, float x, float y)
            {
                var node = Add<ViewDirectionNode>(name, x, y);
                SetGeometrySpace(node, space);
                return node;
            }

            static void SetGeometrySpace(GeometryNode node, CoordinateSpace space)
            {
                var field = typeof(GeometryNode).GetField("m_Space", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) throw new MissingFieldException(typeof(GeometryNode).FullName, "m_Space");
                field.SetValue(node, space);
                node.UpdateNodeAfterDeserialization();
            }

            public SlotReference Const(float value, float x, float y)
            {
                var node = Add<Vector1Node>(value.ToString("0.####"), x, y);
                node.FindInputSlot<Vector1MaterialSlot>(Vector1Node.InputSlotXId).value = value;
                return node.GetSlotReference(Vector1Node.OutputSlotId);
            }

            public SlotReference ConstVector2(Vector2 value, float x, float y)
            {
                var node = Add<Vector2Node>("Vector2 " + value, x, y);
                node.FindInputSlot<Vector1MaterialSlot>(Vector2Node.InputSlotXId).value = value.x;
                node.FindInputSlot<Vector1MaterialSlot>(Vector2Node.InputSlotYId).value = value.y;
                return node.GetSlotReference(Vector2Node.OutputSlotId);
            }

            public SlotReference ConstVector3(Vector3 value, float x, float y)
            {
                var node = Add<Vector3Node>("Vector3 " + value, x, y);
                node.FindInputSlot<Vector1MaterialSlot>(Vector3Node.InputSlotXId).value = value.x;
                node.FindInputSlot<Vector1MaterialSlot>(Vector3Node.InputSlotYId).value = value.y;
                node.FindInputSlot<Vector1MaterialSlot>(Vector3Node.InputSlotZId).value = value.z;
                return node.GetSlotReference(Vector3Node.OutputSlotId);
            }

            public SlotReference Vector2From(SlotReference xValue, SlotReference yValue, string name, float x, float y)
            {
                var node = Add<Vector2Node>(name, x, y);
                Connect(xValue, node.GetSlotReference(Vector2Node.InputSlotXId));
                Connect(yValue, node.GetSlotReference(Vector2Node.InputSlotYId));
                return node.GetSlotReference(Vector2Node.OutputSlotId);
            }

            public SlotReference Vector3From(SlotReference xValue, SlotReference yValue, SlotReference zValue, string name, float x, float y)
            {
                var node = Add<Vector3Node>(name, x, y);
                Connect(xValue, node.GetSlotReference(Vector3Node.InputSlotXId));
                Connect(yValue, node.GetSlotReference(Vector3Node.InputSlotYId));
                Connect(zValue, node.GetSlotReference(Vector3Node.InputSlotZId));
                return node.GetSlotReference(Vector3Node.OutputSlotId);
            }

            public SplitNode Split(SlotReference input, string name, float x, float y)
            {
                var node = Add<SplitNode>(name, x, y);
                Connect(input, In(node, 0));
                return node;
            }

            public SlotReference AddOp(SlotReference a, SlotReference b, string name, float x, float y) => Binary<AddNode>(a, b, name, x, y);
            public SlotReference Subtract(SlotReference a, SlotReference b, string name, float x, float y) => Binary<SubtractNode>(a, b, name, x, y);
            public SlotReference Multiply(SlotReference a, SlotReference b, string name, float x, float y) => Binary<MultiplyNode>(a, b, name, x, y);
            public SlotReference Divide(SlotReference a, SlotReference b, string name, float x, float y) => Binary<DivideNode>(a, b, name, x, y);
            public SlotReference Maximum(SlotReference a, SlotReference b, string name, float x, float y) => Binary<MaximumNode>(a, b, name, x, y);
            public SlotReference Minimum(SlotReference a, SlotReference b, string name, float x, float y) => Binary<MinimumNode>(a, b, name, x, y);
            public SlotReference Distance(SlotReference a, SlotReference b, string name, float x, float y) => Binary<DistanceNode>(a, b, name, x, y);
            public SlotReference Dot(SlotReference a, SlotReference b, string name, float x, float y) => Binary<DotProductNode>(a, b, name, x, y);

            SlotReference Binary<T>(SlotReference a, SlotReference b, string name, float x, float y) where T : AbstractMaterialNode, new()
            {
                var node = Add<T>(name, x, y);
                Connect(a, In(node, 0));
                Connect(b, In(node, 1));
                return Out(node, 0);
            }

            public SlotReference Lerp(SlotReference a, SlotReference b, SlotReference t, string name, float x, float y)
            {
                var node = Add<LerpNode>(name, x, y);
                Connect(a, In(node, 0));
                Connect(b, In(node, 1));
                Connect(t, In(node, 2));
                return Out(node, 0);
            }

            public SlotReference Smoothstep(SlotReference edge1, SlotReference edge2, SlotReference input, string name, float x, float y)
            {
                var node = Add<SmoothstepNode>(name, x, y);
                Connect(edge1, In(node, 0));
                Connect(edge2, In(node, 1));
                Connect(input, In(node, 2));
                return Out(node, 0);
            }

            public SlotReference Clamp(SlotReference input, SlotReference min, SlotReference max, string name, float x, float y)
            {
                var node = Add<ClampNode>(name, x, y);
                Connect(input, In(node, 0));
                Connect(min, In(node, 1));
                Connect(max, In(node, 2));
                return Out(node, 0);
            }

            public SlotReference Step(SlotReference edge, SlotReference input, string name, float x, float y)
            {
                var node = Add<StepNode>(name, x, y);
                Connect(edge, In(node, 0));
                Connect(input, In(node, 1));
                return Out(node, 0);
            }

            public SlotReference Saturate(SlotReference input, string name, float x, float y) => Unary<SaturateNode>(input, name, x, y);
            public SlotReference OneMinus(SlotReference input, string name, float x, float y) => Unary<OneMinusNode>(input, name, x, y);
            public SlotReference Length(SlotReference input, string name, float x, float y) => Unary<LengthNode>(input, name, x, y);
            public SlotReference Normalize(SlotReference input, string name, float x, float y) => Unary<NormalizeNode>(input, name, x, y);
            public SlotReference Absolute(SlotReference input, string name, float x, float y) => Unary<AbsoluteNode>(input, name, x, y);
            public SlotReference Sine(SlotReference input, string name, float x, float y) => Unary<SineNode>(input, name, x, y);

            SlotReference Unary<T>(SlotReference input, string name, float x, float y) where T : AbstractMaterialNode, new()
            {
                var node = Add<T>(name, x, y);
                Connect(input, In(node, 0));
                return Out(node, 0);
            }

            public SlotReference InputByName(AbstractMaterialNode node, string name)
            {
                var slot = node.GetInputSlots<MaterialSlot>().FirstOrDefault(item => item.RawDisplayName() == name);
                if (slot == null) throw new InvalidOperationException($"Input '{name}' was not found on {node.name}.");
                return node.GetSlotReference(slot.id);
            }

            public SlotReference OutputByName(AbstractMaterialNode node, string name)
            {
                var slot = node.GetOutputSlots<MaterialSlot>().FirstOrDefault(item => item.RawDisplayName() == name);
                if (slot == null) throw new InvalidOperationException($"Output '{name}' was not found on {node.name}.");
                return node.GetSlotReference(slot.id);
            }

            public SlotReference In(AbstractMaterialNode node, int index) =>
                node.GetSlotReference(node.GetInputSlots<MaterialSlot>().ElementAt(index).id);

            public SlotReference Out(AbstractMaterialNode node, int index) =>
                node.GetSlotReference(node.GetOutputSlots<MaterialSlot>().ElementAt(index).id);

            public void Connect(SlotReference output, SlotReference input) => graph.Connect(output, input);
        }
    }
}
