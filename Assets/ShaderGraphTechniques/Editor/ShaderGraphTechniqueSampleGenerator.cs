using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShaderGraphTechniques.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ShaderGraphTechniques.Editor
{
    /// <summary>Creates all checked-in binary assets, prefabs, materials, and demo scenes.</summary>
    public static class ShaderGraphTechniqueSampleGenerator
    {
        const string Root = "Assets/ShaderGraphTechniques";
        const string Runtime = Root + "/Runtime";
        const string Demo = Root + "/Demo";
        const string Scenes = Demo + "/Scenes";
        const string Presentation = Demo + "/Presentation";
        const string Materials = Presentation + "/Materials";
        const string Generated = Presentation + "/Generated";

        static readonly string[] EffectFolders =
        {
            "01_Dissolve", "02_IntersectionShield", "03_HeatHaze",
            "04_TriplanarSnow", "05_InteractiveGrass", "06_ScanPulse"
        };

        static Material dissolveMaterial;
        static Material shieldMaterial;
        static Material heatMaterial;
        static Material snowMaterial;
        static Material grassMaterial;
        static Material scanMaterial;
        static Material floorMaterial;
        static Material darkMaterial;
        static Material warmMaterial;
        static Material coolMaterial;
        static Material gridMaterial;
        static VolumeProfile volumeProfile;
        static Mesh grassMesh;

        [MenuItem("Tools/Shader Graph Techniques/Generate Complete Sample")]
        public static void GenerateAll()
        {
            ShaderGraphTechniqueGraphGenerator.Generate();
            EnsureFolders();
            ConfigureProject();
            GenerateTextures();
            GenerateMeshes();
            GenerateMaterials();
            GenerateVolumeProfile();
            GeneratePrefabs();
            GenerateScenes();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[ShaderGraphTechniques] Complete sample generation succeeded.");
        }

        static void EnsureFolders()
        {
            foreach (var folder in new[]
                     {
                         Demo, Scenes, Presentation, Materials, Generated,
                         Runtime + "/Common"
                     }.Concat(EffectFolders.Select(folder => Runtime + "/" + folder)))
                EnsureFolder(folder);
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

        static void ConfigureProject()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.companyName = "ShaderGraphTechniques";
            PlayerSettings.productName = "Shader Graph Techniques";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            QualitySettings.vSyncCount = 0;

            var assets = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
                .Select(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>)
                .Where(asset => asset != null)
                .ToArray();
            if (assets.Length == 0) throw new InvalidOperationException("No project-local URP Asset was found.");

            foreach (var asset in assets)
            {
                asset.supportsCameraDepthTexture = true;
                asset.supportsCameraOpaqueTexture = true;
                asset.msaaSampleCount = 4;
                EditorUtility.SetDirty(asset);
            }

            var renderers = AssetDatabase.FindAssets("t:UniversalRendererData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<UniversalRendererData>)
                .Where(data => data != null)
                .ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("No UniversalRendererData was found.");
            foreach (var renderer in renderers)
            {
                renderer.renderingMode = RenderingMode.Forward;
                EditorUtility.SetDirty(renderer);
            }

            GraphicsSettings.defaultRenderPipeline = assets[0];
            QualitySettings.renderPipeline = assets[0];
            EditorSceneManager.MarkAllScenesDirty();
        }

        static void GenerateTextures()
        {
            WriteTexturePng(Runtime + "/04_TriplanarSnow/TEX_SeamlessRock.png", 256, (x, y, size) =>
            {
                var u = x / (float)size;
                var v = y / (float)size;
                var a = Mathf.Sin(u * Mathf.PI * 2f * 4f) * Mathf.Cos(v * Mathf.PI * 2f * 3f);
                var b = Mathf.Sin((u + v) * Mathf.PI * 2f * 7f) * .45f;
                var c = Mathf.Cos((u - v) * Mathf.PI * 2f * 5f) * .25f;
                var value = Mathf.Clamp01(.48f + .17f * a + .13f * b + .1f * c);
                return new Color(value * .83f, value * .88f, value, 1f);
            }, true);

            WriteTexturePng(Generated + "/TEX_DemoGrid.png", 256, (x, y, size) =>
            {
                var cell = size / 8;
                var line = x % cell < 3 || y % cell < 3;
                var checker = ((x / cell) + (y / cell)) % 2 == 0;
                if (line) return new Color(.03f, .75f, 1f, 1f);
                return checker ? new Color(.06f, .08f, .13f, 1f) : new Color(.32f, .08f, .12f, 1f);
            }, true);
        }

        static void WriteTexturePng(string path, int size, Func<int, int, int, Color> pixel, bool repeat)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false) { name = Path.GetFileNameWithoutExtension(path) };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                pixels[y * size + x] = pixel(x, y, size);
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            var absolute = Path.Combine(Application.dataPath, path.Substring("Assets/".Length));
            File.WriteAllBytes(absolute, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static void GenerateMeshes()
        {
            var generated = BuildGrassCluster();
            var path = Runtime + "/05_InteractiveGrass/MESH_GrassCluster.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                grassMesh = generated;
            }
            else
            {
                EditorUtility.CopySerialized(generated, existing);
                UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(existing);
                grassMesh = existing;
            }
        }

        static Mesh BuildGrassCluster()
        {
            const int segments = 6;
            const int columns = 5;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (var z = 0; z < columns; z++)
            for (var x = 0; x < columns; x++)
            {
                var bladeIndex = z * columns + x;
                var seed = bladeIndex * 12.9898f;
                var angle = Mathf.Repeat(Mathf.Sin(seed) * 43758.5453f, 1f) * Mathf.PI;
                var right = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var center = new Vector3((x - 2f) * .28f, 0f, (z - 2f) * .28f);
                var height = .75f + Mathf.Repeat(Mathf.Sin(seed + 2.4f) * 21613.17f, 1f) * .35f;
                var baseVertex = vertices.Count;
                for (var row = 0; row <= segments; row++)
                {
                    var t = row / (float)segments;
                    var halfWidth = Mathf.Lerp(.075f, .008f, t);
                    vertices.Add(center - right * halfWidth + Vector3.up * height * t);
                    vertices.Add(center + right * halfWidth + Vector3.up * height * t);
                    uvs.Add(new Vector2(0f, t));
                    uvs.Add(new Vector2(1f, t));
                }

                for (var row = 0; row < segments; row++)
                {
                    var i = baseVertex + row * 2;
                    triangles.Add(i);
                    triangles.Add(i + 2);
                    triangles.Add(i + 1);
                    triangles.Add(i + 1);
                    triangles.Add(i + 2);
                    triangles.Add(i + 3);
                }
            }

            var mesh = new Mesh { name = "MESH_GrassCluster" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void GenerateMaterials()
        {
            var rockTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Runtime + "/04_TriplanarSnow/TEX_SeamlessRock.png");
            var gridTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Generated + "/TEX_DemoGrid.png");

            dissolveMaterial = GraphMaterial("01_Dissolve", "SG_01_Dissolve", "MAT_01_Dissolve", material =>
            {
                material.SetColor("_BaseColor", new Color(.08f, .24f, .65f, 1f));
                material.SetColor("_EdgeColor", new Color(2.3f, .16f, .015f, 1f));
                material.SetFloat("_EdgeIntensity", 3.2f);
            });
            shieldMaterial = GraphMaterial("02_IntersectionShield", "SG_02_IntersectionShield", "MAT_02_IntersectionShield", null);
            heatMaterial = GraphMaterial("03_HeatHaze", "SG_03_HeatHaze", "MAT_03_HeatHaze", null);
            snowMaterial = GraphMaterial("04_TriplanarSnow", "SG_04_TriplanarSnow", "MAT_04_TriplanarSnow", material =>
            {
                material.SetTexture("_BaseMap", rockTexture);
                material.SetColor("_BaseTint", new Color(.55f, .47f, .4f, 1f));
                material.SetFloat("_SnowAmount", .62f);
            });
            grassMaterial = GraphMaterial("05_InteractiveGrass", "SG_05_InteractiveGrass", "MAT_05_InteractiveGrass", null);
            scanMaterial = GraphMaterial("06_ScanPulse", "SG_06_ScanPulse", "MAT_06_ScanPulse", null);

            floorMaterial = LitMaterial("MAT_DemoFloor", new Color(.055f, .065f, .085f, 1f), .15f, .42f);
            darkMaterial = LitMaterial("MAT_DemoDark", new Color(.025f, .035f, .055f, 1f), .05f, .25f);
            warmMaterial = LitMaterial("MAT_DemoWarm", new Color(.64f, .11f, .055f, 1f), .05f, .35f);
            coolMaterial = LitMaterial("MAT_DemoCool", new Color(.035f, .24f, .48f, 1f), .12f, .5f);
            gridMaterial = LitMaterial("MAT_DemoGrid", Color.white, 0f, .25f);
            gridMaterial.SetTexture("_BaseMap", gridTexture);
            gridMaterial.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
            EditorUtility.SetDirty(gridMaterial);
        }

        static Material GraphMaterial(string folder, string graphName, string materialName, Action<Material> configure)
        {
            var shaderPath = $"{Runtime}/{folder}/{graphName}.shadergraph";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Missing or unsupported Shader Graph: " + shaderPath);
            var path = $"{Runtime}/{folder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = materialName };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            configure?.Invoke(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material LitMaterial(string name, Color color, float metallic, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit shader is unavailable.");
            var path = $"{Materials}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void GenerateVolumeProfile()
        {
            var path = Presentation + "/VP_Demo.asset";
            volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (volumeProfile == null)
            {
                volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(volumeProfile, path);
            }
            foreach (var component in volumeProfile.components.ToArray()) volumeProfile.Remove(component.GetType());
            var bloom = volumeProfile.Add<Bloom>(true);
            bloom.threshold.Override(1.15f);
            bloom.intensity.Override(.24f);
            bloom.scatter.Override(.58f);
            var color = volumeProfile.Add<ColorAdjustments>(true);
            color.postExposure.Override(.15f);
            color.contrast.Override(6f);
            color.saturation.Override(3f);
            EditorUtility.SetDirty(volumeProfile);
        }

        static void GeneratePrefabs()
        {
            SaveDissolvePrefab();
            SaveShieldPrefab();
            SaveHeatPrefab();
            SaveSnowPrefab();
            SaveGrassPrefab();
            SaveScanPrefab();
        }

        static void SaveDissolvePrefab()
        {
            var root = Primitive(PrimitiveType.Sphere, "Dissolve", null, Vector3.zero, Vector3.one, dissolveMaterial);
            var renderer = root.GetComponent<Renderer>();
            var owner = root.AddComponent<MaterialInstanceOwner>();
            SetRenderers(owner, renderer);
            root.AddComponent<DissolveController>();
            SavePrefab(root, Runtime + "/01_Dissolve/PF_01_Dissolve.prefab");
        }

        static void SaveShieldPrefab()
        {
            var root = Primitive(PrimitiveType.Sphere, "IntersectionShield", null, Vector3.zero, Vector3.one * 2f, shieldMaterial);
            UnityEngine.Object.DestroyImmediate(root.GetComponent<Collider>());
            SavePrefab(root, Runtime + "/02_IntersectionShield/PF_02_IntersectionShield.prefab");
        }

        static void SaveHeatPrefab()
        {
            var root = Primitive(PrimitiveType.Quad, "HeatHaze", null, Vector3.zero, new Vector3(3.2f, 3.2f, 1f), heatMaterial);
            UnityEngine.Object.DestroyImmediate(root.GetComponent<Collider>());
            var owner = root.AddComponent<MaterialInstanceOwner>();
            SetRenderers(owner, root.GetComponent<Renderer>());
            root.AddComponent<HeatHazeController>();
            SavePrefab(root, Runtime + "/03_HeatHaze/PF_03_HeatHaze.prefab");
        }

        static void SaveSnowPrefab()
        {
            var root = Primitive(PrimitiveType.Sphere, "TriplanarSnow", null, Vector3.zero, new Vector3(1.5f, 1.2f, 1.35f), snowMaterial);
            SavePrefab(root, Runtime + "/04_TriplanarSnow/PF_04_TriplanarSnow.prefab");
        }

        static void SaveGrassPrefab()
        {
            var root = new GameObject("InteractiveGrass");
            var filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = grassMesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = grassMaterial;
            var owner = root.AddComponent<MaterialInstanceOwner>();
            SetRenderers(owner, renderer);
            root.AddComponent<InteractiveGrassController>();
            SavePrefab(root, Runtime + "/05_InteractiveGrass/PF_05_InteractiveGrass.prefab");
        }

        static void SaveScanPrefab()
        {
            var root = new GameObject("ScanPulseSet");
            var renderers = new List<Renderer>();
            renderers.Add(Primitive(PrimitiveType.Cube, "Floor", root.transform, new Vector3(0f, -.15f, 0f), new Vector3(4.4f, .3f, 4.4f), scanMaterial).GetComponent<Renderer>());
            renderers.Add(Primitive(PrimitiveType.Cube, "Wall", root.transform, new Vector3(0f, 1.1f, 1.8f), new Vector3(4.4f, 2.5f, .25f), scanMaterial).GetComponent<Renderer>());
            renderers.Add(Primitive(PrimitiveType.Cube, "Step", root.transform, new Vector3(-1.2f, .35f, .2f), new Vector3(1.2f, .7f, 1.4f), scanMaterial).GetComponent<Renderer>());
            renderers.Add(Primitive(PrimitiveType.Sphere, "Sphere", root.transform, new Vector3(1.15f, .65f, .4f), Vector3.one * 1.2f, scanMaterial).GetComponent<Renderer>());
            var owner = root.AddComponent<MaterialInstanceOwner>();
            SetRenderers(owner, renderers.ToArray());
            root.AddComponent<ScanPulseController>();
            SavePrefab(root, Runtime + "/06_ScanPulse/PF_06_ScanPulse.prefab");
        }

        static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path, out var success);
            UnityEngine.Object.DestroyImmediate(root);
            if (!success) throw new InvalidOperationException("Could not save prefab: " + path);
        }

        static void SetRenderers(MaterialInstanceOwner owner, params Renderer[] renderers)
        {
            var serialized = new SerializedObject(owner);
            var property = serialized.FindProperty("targetRenderers");
            property.arraySize = renderers.Length;
            for (var index = 0; index < renderers.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = renderers[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void GenerateScenes()
        {
            CreateGalleryScene();
            CreateIndividualScene(1, "01_Dissolve", DemoPresentationController.DemoKind.Dissolve);
            CreateIndividualScene(2, "02_IntersectionShield", DemoPresentationController.DemoKind.IntersectionShield);
            CreateIndividualScene(3, "03_HeatHaze", DemoPresentationController.DemoKind.HeatHaze);
            CreateIndividualScene(4, "04_TriplanarSnow", DemoPresentationController.DemoKind.TriplanarSnow);
            CreateIndividualScene(5, "05_InteractiveGrass", DemoPresentationController.DemoKind.InteractiveGrass);
            CreateIndividualScene(6, "06_ScanPulse", DemoPresentationController.DemoKind.ScanPulse);
        }

        static Scene NewDemoScene(string name, Vector3 cameraPosition, Vector3 cameraTarget)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = name;

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.008f, .012f, .025f, 1f);
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 120f;
            camera.fieldOfView = 48f;
            camera.allowHDR = true;
            cameraObject.transform.position = cameraPosition;
            LookAt(cameraObject.transform, cameraTarget);
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.requiresDepthTexture = true;
            cameraData.requiresColorTexture = true;
            cameraData.renderType = CameraRenderType.Base;

            var sun = new GameObject("Key Light");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(.78f, .87f, 1f);
            light.intensity = 2.0f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            var fill = new GameObject("Fill Light");
            var fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.color = new Color(.14f, .4f, 1f);
            fillLight.intensity = 650f;
            fillLight.range = 18f;
            fill.transform.position = new Vector3(-4f, 5f, -4f);

            var volumeObject = new GameObject("Global Volume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = volumeProfile;
            return scene;
        }

        static void CreateGalleryScene()
        {
            var scene = NewDemoScene("Gallery", new Vector3(0f, 8.2f, -17.5f), new Vector3(0f, 1.15f, .9f));
            Primitive(PrimitiveType.Cube, "Gallery Floor", null, new Vector3(0f, -.3f, 1f), new Vector3(18f, .5f, 12f), floorMaterial);

            var dissolves = new List<DissolveController>();
            var heats = new List<HeatHazeController>();
            var grasses = new List<InteractiveGrassController>();
            var scans = new List<ScanPulseController>();
            var interactors = new List<Transform>();
            var snowObjects = new List<Transform>();

            var dissolve = InstantiatePrefab("01_Dissolve", "PF_01_Dissolve", new Vector3(-5.2f, 1.15f, -2.1f));
            dissolve.transform.localScale = Vector3.one * 1.35f;
            dissolves.Add(dissolve.GetComponent<DissolveController>());
            AddPedestal(new Vector3(-5.2f, .05f, -2.1f), new Vector3(3.1f, .3f, 3.1f));
            AddLabel("01  DISSOLVE", new Vector3(-5.2f, 2.75f, -2.3f), .18f);

            var shield = InstantiatePrefab("02_IntersectionShield", "PF_02_IntersectionShield", new Vector3(0f, 1.1f, -2.1f));
            shield.transform.localScale = Vector3.one * .83f;
            Primitive(PrimitiveType.Cube, "Shield Intersector", null, new Vector3(.15f, .65f, -2.1f), new Vector3(.65f, 2.4f, .65f), warmMaterial);
            AddPedestal(new Vector3(0f, .05f, -2.1f), new Vector3(3.3f, .3f, 3.3f));
            AddLabel("02  DEPTH BARRIER", new Vector3(0f, 2.75f, -2.3f), .18f);

            CreateHeatStation(new Vector3(5.2f, 1.2f, -2f), heats, true);
            AddLabel("03  HEAT HAZE", new Vector3(5.2f, 2.75f, -2.3f), .18f);

            var snow = InstantiatePrefab("04_TriplanarSnow", "PF_04_TriplanarSnow", new Vector3(-5.2f, 1.05f, 3.1f));
            snow.transform.localScale = new Vector3(1.25f, .95f, 1.1f);
            snow.transform.localRotation = Quaternion.Euler(15f, 20f, 22f);
            snowObjects.Add(snow.transform);
            AddPedestal(new Vector3(-5.2f, .05f, 3.1f), new Vector3(3.3f, .3f, 3.3f));
            AddLabel("04  TRIPLANAR SNOW", new Vector3(-5.2f, 2.75f, 2.9f), .18f);

            CreateGrassStation(new Vector3(0f, .15f, 3.1f), grasses, interactors);
            AddLabel("05  INTERACTIVE GRASS", new Vector3(0f, 2.75f, 2.9f), .18f);

            var scan = InstantiatePrefab("06_ScanPulse", "PF_06_ScanPulse", new Vector3(5.2f, .25f, 3.1f));
            scan.transform.localScale = Vector3.one * .72f;
            scans.Add(scan.GetComponent<ScanPulseController>());
            AddLabel("06  SCAN PULSE", new Vector3(5.2f, 2.75f, 2.9f), .18f);

            AddDemoController(DemoPresentationController.DemoKind.Gallery, dissolves, heats, grasses, scans, interactors, snowObjects);
            SaveScene(scene, Scenes + "/00_Gallery.unity");
        }

        static void CreateIndividualScene(int number, string slug, DemoPresentationController.DemoKind kind)
        {
            var scene = NewDemoScene(slug, new Vector3(0f, 3.3f, -9.2f), new Vector3(0f, 1f, .3f));
            Primitive(PrimitiveType.Cube, "Floor", null, new Vector3(0f, -.28f, .4f), new Vector3(12f, .5f, 9f), floorMaterial);
            var dissolves = new List<DissolveController>();
            var heats = new List<HeatHazeController>();
            var grasses = new List<InteractiveGrassController>();
            var scans = new List<ScanPulseController>();
            var interactors = new List<Transform>();
            var snowObjects = new List<Transform>();

            switch (number)
            {
                case 1:
                {
                    var left = InstantiatePrefab("01_Dissolve", "PF_01_Dissolve", new Vector3(-1.5f, 1.25f, .3f));
                    var right = InstantiatePrefab("01_Dissolve", "PF_01_Dissolve", new Vector3(1.5f, 1.25f, .3f));
                    left.transform.localScale = new Vector3(1.1f, 1.55f, 1.1f);
                    right.transform.localScale = new Vector3(.82f, .82f, .82f);
                    right.transform.rotation = Quaternion.Euler(0f, 25f, 18f);
                    dissolves.Add(left.GetComponent<DissolveController>());
                    dissolves.Add(right.GetComponent<DissolveController>());
                    AddLabel("OPAQUE + ALPHA CLIP", new Vector3(0f, 3.1f, .5f), .22f);
                    break;
                }
                case 2:
                {
                    var shield = InstantiatePrefab("02_IntersectionShield", "PF_02_IntersectionShield", new Vector3(0f, 1.4f, .8f));
                    shield.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
                    Primitive(PrimitiveType.Cube, "Center Pillar", null, new Vector3(0f, 1.15f, .8f), new Vector3(.8f, 3.2f, .8f), warmMaterial);
                    Primitive(PrimitiveType.Cube, "Side Pillar", null, new Vector3(1.55f, .6f, .9f), new Vector3(.55f, 1.6f, .55f), coolMaterial);
                    Primitive(PrimitiveType.Cube, "Occlusion Check", null, new Vector3(-2.3f, 1.0f, -2.2f), new Vector3(.8f, 2f, .8f), darkMaterial);
                    AddLabel("EYE-SPACE DEPTH CONTACT", new Vector3(0f, 3.55f, 1f), .21f);
                    break;
                }
                case 3:
                    CreateHeatStation(new Vector3(0f, 1.65f, .2f), heats, false);
                    AddLabel("OPAQUE TEXTURE REFRACTION", new Vector3(0f, 4.45f, .7f), .19f);
                    break;
                case 4:
                {
                    var sphere = InstantiatePrefab("04_TriplanarSnow", "PF_04_TriplanarSnow", new Vector3(-1.7f, 1.15f, .4f));
                    sphere.transform.localScale = new Vector3(1.2f, .85f, 1.05f);
                    snowObjects.Add(sphere.transform);
                    var slope = Primitive(PrimitiveType.Cube, "Sloped Rock", null, new Vector3(1.7f, .8f, .5f), new Vector3(2f, 1.5f, 1.7f), snowMaterial);
                    slope.transform.rotation = Quaternion.Euler(24f, 28f, 32f);
                    snowObjects.Add(slope.transform);
                    AddLabel("WORLD PROJECTION + UP MASK", new Vector3(0f, 3.45f, .7f), .21f);
                    break;
                }
                case 5:
                    CreateGrassStation(new Vector3(0f, .05f, .5f), grasses, interactors);
                    var secondGrass = InstantiatePrefab("05_InteractiveGrass", "PF_05_InteractiveGrass", new Vector3(2.4f, .05f, .5f));
                    secondGrass.transform.localScale = new Vector3(.5f, .5f, .5f);
                    grasses.Add(secondGrass.GetComponent<InteractiveGrassController>());
                    AddLabel("ROOT-PINNED WORLD BENDING", new Vector3(0f, 3.25f, .7f), .21f);
                    break;
                case 6:
                {
                    var scan = InstantiatePrefab("06_ScanPulse", "PF_06_ScanPulse", new Vector3(0f, .05f, .6f));
                    scan.transform.localScale = new Vector3(1.3f, .8f, 1.05f);
                    scans.Add(scan.GetComponent<ScanPulseController>());
                    AddLabel("ONE SPHERICAL WAVE, MANY SURFACES", new Vector3(0f, 3.65f, 1f), .2f);
                    break;
                }
            }

            AddDemoController(kind, dissolves, heats, grasses, scans, interactors, snowObjects);
            SaveScene(scene, $"{Scenes}/{number:00}_{slug.Substring(3)}.unity");
        }

        static void CreateHeatStation(Vector3 center, List<HeatHazeController> heats, bool compact)
        {
            var backgroundZ = center.z + (compact ? 1.4f : 2.2f);
            var grid = Primitive(PrimitiveType.Quad, "Heat Haze Grid Background", null,
                new Vector3(center.x, center.y, backgroundZ), new Vector3(compact ? 3.2f : 6.2f, compact ? 3f : 4.5f, 1f), gridMaterial);
            for (var index = -2; index <= 2; index++)
                Primitive(PrimitiveType.Cylinder, "Background Column " + index, null,
                    new Vector3(center.x + index * (compact ? .45f : .8f), center.y, backgroundZ - .35f),
                    new Vector3(.16f, compact ? 1.15f : 1.7f, .16f), index % 2 == 0 ? warmMaterial : coolMaterial);
            var haze = InstantiatePrefab("03_HeatHaze", "PF_03_HeatHaze", center);
            if (compact) haze.transform.localScale = new Vector3(.72f, .72f, .72f);
            heats.Add(haze.GetComponent<HeatHazeController>());
            AddPedestal(new Vector3(center.x, .05f, center.z), new Vector3(compact ? 3.2f : 6.6f, .3f, 2.2f));
        }

        static void CreateGrassStation(Vector3 center, List<InteractiveGrassController> grasses, List<Transform> interactors)
        {
            var grass = InstantiatePrefab("05_InteractiveGrass", "PF_05_InteractiveGrass", center);
            grass.transform.localScale = new Vector3(2f, 2f, 2f);
            var interactor = Primitive(PrimitiveType.Sphere, "Grass Interactor", grass.transform.parent,
                center + new Vector3(0f, .55f, 0f), Vector3.one * .42f, warmMaterial);
            grass.GetComponent<InteractiveGrassController>().Interactor = interactor.transform;
            grasses.Add(grass.GetComponent<InteractiveGrassController>());
            interactors.Add(interactor.transform);
            AddPedestal(new Vector3(center.x, .02f, center.z), new Vector3(3.6f, .25f, 3.2f));
        }

        static void AddDemoController(DemoPresentationController.DemoKind kind,
            List<DissolveController> dissolves, List<HeatHazeController> heats,
            List<InteractiveGrassController> grasses, List<ScanPulseController> scans,
            List<Transform> interactors, List<Transform> snowObjects)
        {
            var demo = new GameObject("Demo Presentation (not required by Runtime)");
            var controller = demo.AddComponent<DemoPresentationController>();
            controller.Configure(kind, dissolves.ToArray(), heats.ToArray(), grasses.ToArray(), scans.ToArray(), interactors.ToArray(), snowObjects.ToArray());
            demo.AddComponent<PlayerCaptureSequence>();
        }

        static GameObject InstantiatePrefab(string folder, string prefabName, Vector3 position)
        {
            var path = $"{Runtime}/{folder}/{prefabName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing prefab: " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = position;
            return instance;
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            item.transform.localScale = localScale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        static void AddPedestal(Vector3 position, Vector3 scale) =>
            Primitive(PrimitiveType.Cube, "Pedestal", null, position, scale, darkMaterial);

        static void AddLabel(string text, Vector3 position, float characterSize)
        {
            var label = new GameObject("Label " + text);
            label.transform.position = position;
            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = characterSize * .42f;
            mesh.fontSize = 56;
            mesh.color = new Color(.65f, .88f, 1f, 1f);
        }

        static void LookAt(Transform transform, Vector3 target) =>
            transform.rotation = Quaternion.LookRotation(target - transform.position, Vector3.up);

        static void SaveScene(Scene scene, string path)
        {
            if (!EditorSceneManager.SaveScene(scene, path))
                throw new IOException("Could not save scene: " + path);
        }

        static void ConfigureBuildSettings()
        {
            var paths = new[]
            {
                Scenes + "/00_Gallery.unity",
                Scenes + "/01_Dissolve.unity",
                Scenes + "/02_IntersectionShield.unity",
                Scenes + "/03_HeatHaze.unity",
                Scenes + "/04_TriplanarSnow.unity",
                Scenes + "/05_InteractiveGrass.unity",
                Scenes + "/06_ScanPulse.unity"
            };
            foreach (var path in paths)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) throw new FileNotFoundException("Missing generated scene", path);
            EditorBuildSettings.scenes = paths.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
        }
    }
}
