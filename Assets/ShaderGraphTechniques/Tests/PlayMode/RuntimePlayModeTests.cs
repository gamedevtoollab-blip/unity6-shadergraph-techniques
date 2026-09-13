using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShaderGraphTechniques.Tests
{
    public sealed class RuntimePlayModeTests
    {
        static readonly string[] ShaderNames =
        {
            "Shader Graph Techniques/01 Dissolve/SG_01_Dissolve",
            "Shader Graph Techniques/02 Depth Barrier/SG_02_IntersectionShield",
            "Shader Graph Techniques/03 Heat Haze/SG_03_HeatHaze",
            "Shader Graph Techniques/04 Snow Triplanar/SG_04_TriplanarSnow",
            "Shader Graph Techniques/05 Interactive Grass/SG_05_InteractiveGrass",
            "Shader Graph Techniques/06 Scan Pulse/SG_06_ScanPulse"
        };

        [UnityTest]
        public IEnumerator AllSixRuntimeShadersResolveAndRenderAFrame()
        {
            var roots = new List<GameObject>();
            var materials = new List<Material>();
            var cameraObject = new GameObject("Test Camera");
            roots.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 1f, -12f);
            cameraObject.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.targetTexture = new RenderTexture(640, 360, 24);

            for (var index = 0; index < ShaderNames.Length; index++)
            {
                var shader = Shader.Find(ShaderNames[index]);
                Assert.That(shader, Is.Not.Null, ShaderNames[index]);
                Assert.That(shader.isSupported, Is.True, ShaderNames[index]);
                var material = new Material(shader);
                materials.Add(material);
                var item = GameObject.CreatePrimitive(index == 2 ? PrimitiveType.Quad : PrimitiveType.Sphere);
                roots.Add(item);
                item.transform.position = new Vector3((index - 2.5f) * 1.6f, 1f, 0f);
                item.GetComponent<Renderer>().sharedMaterial = material;
            }

            camera.Render();
            yield return null;
            Assert.That(camera.targetTexture.IsCreated(), Is.True);

            var renderTexture = camera.targetTexture;
            camera.targetTexture = null;
            Object.Destroy(renderTexture);
            foreach (var root in roots) Object.Destroy(root);
            foreach (var material in materials) Object.Destroy(material);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TwoDissolveObjectsOwnIndependentMaterialsAndBoundaryValues()
        {
            var shared = new Material(Shader.Find(ShaderNames[0]));
            var firstObject = CreateControlledObject<DissolveController>(shared);
            var secondObject = CreateControlledObject<DissolveController>(shared);
            yield return null;
            var controllers = new[] { firstObject.GetComponent<DissolveController>(), secondObject.GetComponent<DissolveController>() };
            controllers[0].SetProgress(0f);
            controllers[1].SetProgress(1f);
            yield return null;
            var first = firstObject.GetComponent<MaterialInstanceOwner>().Materials.Single();
            var second = secondObject.GetComponent<MaterialInstanceOwner>().Materials.Single();
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first.GetFloat("_Progress"), Is.EqualTo(0f).Within(.0001f));
            Assert.That(second.GetFloat("_Progress"), Is.EqualTo(1f).Within(.0001f));
            Assert.That(shared.GetFloat("_Progress"), Is.EqualTo(0f).Within(.0001f));
            Object.Destroy(firstObject);
            Object.Destroy(secondObject);
            Object.Destroy(shared);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GrassWithoutInteractorDisablesContactAndKeepsFiniteExpandedBounds()
        {
            var shared = new Material(Shader.Find(ShaderNames[4]));
            var root = CreateControlledObject<InteractiveGrassController>(shared);
            root.transform.localScale = new Vector3(.5f, 2f, 1f);
            yield return null;
            var controller = root.GetComponent<InteractiveGrassController>();
            controller.Interactor = null;
            controller.SetManualTime(2f);
            yield return null;
            var owner = root.GetComponent<MaterialInstanceOwner>();
            Assert.That(owner.Materials.All(material => material.GetFloat("_InteractorEnabled") == 0f), Is.True);
            foreach (var renderer in owner.Renderers)
            {
                var size = renderer.localBounds.size;
                Assert.That(float.IsNaN(size.x) || float.IsInfinity(size.x), Is.False);
                Assert.That(float.IsNaN(size.y) || float.IsInfinity(size.y), Is.False);
                Assert.That(float.IsNaN(size.z) || float.IsInfinity(size.z), Is.False);
                Assert.That(size.x, Is.GreaterThan(0f));
            }
            Object.Destroy(root);
            Object.Destroy(shared);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ScanInactiveAtRadiusResetProducesNoRingState()
        {
            var shared = new Material(Shader.Find(ShaderNames[5]));
            var root = CreateControlledObject<ScanPulseController>(shared);
            yield return null;
            var scan = root.GetComponent<ScanPulseController>();
            scan.SetState(false, 0f);
            yield return null;
            var materials = root.GetComponent<MaterialInstanceOwner>().Materials;
            Assert.That(materials, Is.Not.Empty);
            Assert.That(materials.All(material => material.GetFloat("_ScanActive") == 0f && material.GetFloat("_ScanRadius") == 0f), Is.True);
            Object.Destroy(root);
            Object.Destroy(shared);
            yield return null;
        }

        static GameObject CreateControlledObject<T>(Material shared) where T : Component
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var renderer = root.GetComponent<Renderer>();
            renderer.sharedMaterial = shared;
            var owner = root.AddComponent<MaterialInstanceOwner>();
            owner.SetRenderers(new[] { renderer });
            root.AddComponent<T>();
            return root;
        }
    }
}
