using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShaderGraphTechniques
{
    /// <summary>
    /// Owns one material clone per renderer slot. It never edits the shared material asset,
    /// reuses clones for the lifetime of this component, restores the original references,
    /// and destroys only clones it created.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MaterialInstanceOwner : MonoBehaviour
    {
        [SerializeField] Renderer[] targetRenderers = Array.Empty<Renderer>();

        readonly List<Material> ownedMaterials = new();
        Material[][] originalMaterials = Array.Empty<Material[]>();
        bool initialized;

        public IReadOnlyList<Material> Materials
        {
            get
            {
                EnsureInitialized();
                return ownedMaterials;
            }
        }

        public IReadOnlyList<Renderer> Renderers
        {
            get
            {
                ResolveRenderers();
                return targetRenderers;
            }
        }

        void OnEnable() => EnsureInitialized();
        void OnDisable() => Release();
        void OnDestroy() => Release();

        public void SetRenderers(Renderer[] renderers)
        {
            Release();
            targetRenderers = renderers ?? Array.Empty<Renderer>();
            EnsureInitialized();
        }

        public void SetFloat(string referenceName, float value)
        {
            foreach (var material in Materials)
                if (material != null && material.HasProperty(referenceName)) material.SetFloat(referenceName, value);
        }

        public void SetVector(string referenceName, Vector4 value)
        {
            foreach (var material in Materials)
                if (material != null && material.HasProperty(referenceName)) material.SetVector(referenceName, value);
        }

        public void SetColor(string referenceName, Color value)
        {
            foreach (var material in Materials)
                if (material != null && material.HasProperty(referenceName)) material.SetColor(referenceName, value);
        }

        public void EnsureInitialized()
        {
            if (initialized) return;
            ResolveRenderers();
            originalMaterials = new Material[targetRenderers.Length][];
            ownedMaterials.Clear();

            for (var rendererIndex = 0; rendererIndex < targetRenderers.Length; rendererIndex++)
            {
                var renderer = targetRenderers[rendererIndex];
                if (renderer == null)
                {
                    originalMaterials[rendererIndex] = Array.Empty<Material>();
                    continue;
                }

                var originals = renderer.sharedMaterials;
                originalMaterials[rendererIndex] = originals;
                var instances = new Material[originals.Length];
                for (var materialIndex = 0; materialIndex < originals.Length; materialIndex++)
                {
                    var original = originals[materialIndex];
                    if (original == null) continue;
                    var instance = new Material(original)
                    {
                        name = original.name + " (Owned by " + name + ")",
                        hideFlags = HideFlags.DontSave
                    };
                    instances[materialIndex] = instance;
                    ownedMaterials.Add(instance);
                }
                renderer.sharedMaterials = instances;
            }
            initialized = true;
        }

        public void Release()
        {
            if (!initialized) return;
            for (var index = 0; index < targetRenderers.Length && index < originalMaterials.Length; index++)
                if (targetRenderers[index] != null) targetRenderers[index].sharedMaterials = originalMaterials[index];

            foreach (var material in ownedMaterials)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
            ownedMaterials.Clear();
            originalMaterials = Array.Empty<Material[]>();
            initialized = false;
        }

        void ResolveRenderers()
        {
            if (targetRenderers != null && targetRenderers.Length > 0) return;
            targetRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }
}
