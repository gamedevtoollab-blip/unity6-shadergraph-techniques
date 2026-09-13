using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShaderGraphTechniques.Tests
{
    public sealed class RuntimeAssetTests
    {
        const string Runtime = "Assets/ShaderGraphTechniques/Runtime";

        [Test]
        public void SixRuntimePrefabsContainNoPresentationComponentsOrMissingScripts()
        {
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { Runtime })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
            Assert.That(paths, Has.Length.EqualTo(6));
            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var components = prefab.GetComponentsInChildren<Component>(true);
                Assert.That(components, Has.None.Null, path + " has a missing script");
                Assert.That(components.OfType<Camera>(), Is.Empty, path);
                Assert.That(components.OfType<Light>(), Is.Empty, path);
                Assert.That(components.OfType<Volume>(), Is.Empty, path);
                Assert.That(components.OfType<Canvas>(), Is.Empty, path);
                Assert.That(components.Any(item => item.GetType().Namespace?.StartsWith("ShaderGraphTechniques.Demo", StringComparison.Ordinal) == true), Is.False, path);
            }
        }

        [Test]
        public void MaterialOwnerClonesOnceReusesAndRestoresOnlyItsOwnMaterial()
        {
            var shared = AssetDatabase.LoadAssetAtPath<Material>(Runtime + "/01_Dissolve/MAT_01_Dissolve.mat");
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                var renderer = root.GetComponent<Renderer>();
                renderer.sharedMaterial = shared;
                var owner = root.AddComponent<MaterialInstanceOwner>();
                owner.SetRenderers(new[] { renderer });
                var first = owner.Materials.Single();
                var again = owner.Materials.Single();
                Assert.That(first, Is.SameAs(again), "Material must be reused, not recreated per access/frame.");
                Assert.That(first, Is.Not.SameAs(shared));
                Assert.That(renderer.sharedMaterial, Is.SameAs(first));
                owner.SetFloat("_Progress", .72f);
                Assert.That(first.GetFloat("_Progress"), Is.EqualTo(.72f).Within(.0001f));
                Assert.That(shared.GetFloat("_Progress"), Is.Not.EqualTo(.72f).Within(.0001f));
                owner.Release();
                Assert.That(renderer.sharedMaterial, Is.SameAs(shared));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GrassMeshHasSixVerticalSegmentsAndRootTipUvContract()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Runtime + "/05_InteractiveGrass/MESH_GrassCluster.asset");
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.uv, Has.Length.EqualTo(mesh.vertexCount));
            var levels = mesh.uv.Select(uv => uv.y).Distinct().OrderBy(value => value).ToArray();
            Assert.That(levels, Has.Length.EqualTo(7));
            Assert.That(levels.First(), Is.EqualTo(0f));
            Assert.That(levels.Last(), Is.EqualTo(1f));
        }

        [Test]
        public void RuntimeAssemblyHasNoPresentationDependency()
        {
            var asmdef = File.ReadAllText(Path.GetFullPath(Runtime + "/ShaderGraphTechniques.Runtime.asmdef"));
            Assert.That(asmdef, Does.Not.Contain("Demo"));
            Assert.That(asmdef, Does.Not.Contain("Editor"));
            Assert.That(asmdef, Does.Not.Contain("Tests"));

            var assets = AssetDatabase.FindAssets("", new[] { Runtime }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !AssetDatabase.IsValidFolder(path)).ToArray();
            var dependencies = AssetDatabase.GetDependencies(assets, true);
            Assert.That(dependencies.Any(path => path.StartsWith("Assets/ShaderGraphTechniques/Demo/", StringComparison.Ordinal) ||
                                                 path.StartsWith("Assets/ShaderGraphTechniques/Editor/", StringComparison.Ordinal) ||
                                                 path.StartsWith("Assets/ShaderGraphTechniques/Tests/", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void BuildContainsGalleryAndSixIndividualScenes()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            Assert.That(scenes, Has.Length.EqualTo(7));
            Assert.That(scenes[0], Does.EndWith("00_Gallery.unity"));
            Assert.That(scenes.All(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null), Is.True);
        }

        [TestCase("Uniform1x", 1f, 1f, 1f)]
        [TestCase("Uniform0_5x", .5f, .5f, .5f)]
        [TestCase("Uniform2x", 2f, 2f, 2f)]
        [TestCase("NonUniformPositive", .5f, 2f, 1.25f)]
        public void RuntimePrefabsRemainFiniteUnderMovedRotatedParentAndPositiveScale(
            string caseName, float scaleX, float scaleY, float scaleZ)
        {
            var prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { Runtime })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
            var parent = new GameObject("Transform Case " + caseName);
            var interactor = new GameObject("World-space Interactor");
            try
            {
                parent.transform.position = new Vector3(7.5f, 1.25f, -3.75f);
                parent.transform.rotation = Quaternion.Euler(17f, 38f, 11f);
                parent.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
                interactor.transform.position = new Vector3(8.2f, 1.8f, -3.1f);
                Assert.That(parent.transform.localToWorldMatrix.determinant, Is.GreaterThan(0f), caseName);

                foreach (var path in prefabPaths)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instance.transform.SetParent(parent.transform, false);
                    instance.transform.localPosition = new Vector3(1.1f, .4f, -.7f);
                    instance.transform.localRotation = Quaternion.Euler(9f, 23f, 14f);

                    foreach (var owner in instance.GetComponentsInChildren<MaterialInstanceOwner>(true))
                        owner.EnsureInitialized();
                    foreach (var controller in instance.GetComponentsInChildren<MonoBehaviour>(true)
                                 .Where(item => item != null && item.GetType().Name.EndsWith("Controller", StringComparison.Ordinal)))
                    {
                        InvokeLifecycle(controller, "Awake");
                        InvokeLifecycle(controller, "OnEnable");
                    }

                    var dissolve = instance.GetComponentInChildren<DissolveController>(true);
                    if (dissolve != null) dissolve.SetProgress(.63f);
                    var heat = instance.GetComponentInChildren<HeatHazeController>(true);
                    if (heat != null) { heat.SetManualTime(1.25f); heat.SetOpacity(.8f); }
                    var grass = instance.GetComponentInChildren<InteractiveGrassController>(true);
                    if (grass != null) { grass.Interactor = interactor.transform; grass.SetBend(1.4f, .75f); grass.SetManualTime(1.25f); }
                    var scan = instance.GetComponentInChildren<ScanPulseController>(true);
                    if (scan != null) { scan.SetCenter(interactor.transform); scan.SetState(true, 2.25f); }

                    foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    {
                        Assert.That(renderer.sharedMaterials, Has.None.Null, path + " / " + caseName);
                        Assert.That(renderer.sharedMaterials.All(material => material.shader != null && material.shader.isSupported), Is.True, path + " / " + caseName);
                        var bounds = renderer.bounds;
                        AssertFinitePositive(bounds.size.x, path + " bounds.x / " + caseName);
                        AssertFinitePositive(bounds.size.y, path + " bounds.y / " + caseName);
                        AssertFinitePositive(bounds.size.z, path + " bounds.z / " + caseName);
                    }

                    if (grass != null)
                    {
                        var materials = grass.GetComponent<MaterialInstanceOwner>().Materials;
                        Assert.That(materials.All(material => material.GetFloat("_InteractorEnabled") == 1f), Is.True, caseName);
                        var position = materials.First().GetVector("_InteractorPositionWS");
                        Assert.That(position.x, Is.EqualTo(interactor.transform.position.x).Within(.0001f), caseName);
                        Assert.That(position.y, Is.EqualTo(interactor.transform.position.y).Within(.0001f), caseName);
                        Assert.That(position.z, Is.EqualTo(interactor.transform.position.z).Within(.0001f), caseName);
                    }
                    if (scan != null)
                    {
                        var material = scan.GetComponent<MaterialInstanceOwner>().Materials.First();
                        var position = material.GetVector("_ScanCenterWS");
                        Assert.That(position.x, Is.EqualTo(interactor.transform.position.x).Within(.0001f), caseName);
                        Assert.That(position.y, Is.EqualTo(interactor.transform.position.y).Within(.0001f), caseName);
                        Assert.That(position.z, Is.EqualTo(interactor.transform.position.z).Within(.0001f), caseName);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(interactor);
            }
        }

        static void InvokeLifecycle(MonoBehaviour component, string methodName)
        {
            component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
        }

        static void AssertFinitePositive(float value, string message)
        {
            Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False, message);
            Assert.That(value, Is.GreaterThan(0f), message);
        }
    }
}
