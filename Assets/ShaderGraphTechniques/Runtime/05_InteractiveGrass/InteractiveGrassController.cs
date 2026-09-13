using System.Collections.Generic;
using UnityEngine;

namespace ShaderGraphTechniques
{
    [RequireComponent(typeof(MaterialInstanceOwner))]
    public sealed class InteractiveGrassController : MonoBehaviour
    {
        [SerializeField] Transform interactor;
        [SerializeField, Min(0.0001f)] float bendRadius = 1f;
        [SerializeField, Min(0f)] float bendStrength = 0.6f;
        [SerializeField, Min(0f)] float boundsPadding = 0.9f;
        [SerializeField] bool useManualTime;
        [SerializeField] float manualTime;

        MaterialInstanceOwner owner;
        readonly Dictionary<Renderer, Bounds> originalBounds = new();

        public Transform Interactor { get => interactor; set { interactor = value; Apply(); } }
        public float BendRadius => bendRadius;
        public float BendStrength => bendStrength;

        void Awake() => owner = GetComponent<MaterialInstanceOwner>();
        void OnEnable()
        {
            if (owner == null) owner = GetComponent<MaterialInstanceOwner>();
            ExpandBounds();
            Apply();
        }

        void Update() => Apply();
        void OnDisable() => RestoreBounds();

        public void SetManualTime(float seconds) { useManualTime = true; manualTime = seconds; Apply(); }
        public void UseRealtime() { useManualTime = false; Apply(); }
        public void SetBend(float radius, float strength)
        {
            bendRadius = Mathf.Max(.0001f, radius);
            bendStrength = Mathf.Max(0f, strength);
            RestoreBounds();
            ExpandBounds();
            Apply();
        }

        void Apply()
        {
            if (owner == null || !isActiveAndEnabled) return;
            owner.SetFloat("_BendRadius", bendRadius);
            owner.SetFloat("_BendStrength", bendStrength);
            owner.SetFloat("_UseManualTime", useManualTime ? 1f : 0f);
            owner.SetFloat("_ManualTime", manualTime);
            if (interactor == null)
            {
                owner.SetFloat("_InteractorEnabled", 0f);
                return;
            }
            owner.SetVector("_InteractorPositionWS", interactor.position);
            owner.SetFloat("_InteractorEnabled", 1f);
        }

        void ExpandBounds()
        {
            if (owner == null) return;
            originalBounds.Clear();
            foreach (var renderer in owner.Renderers)
            {
                if (renderer == null) continue;
                var bounds = renderer.localBounds;
                originalBounds[renderer] = bounds;
                var scale = renderer.transform.lossyScale;
                var minimumPositiveScale = Mathf.Max(0.0001f, Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                bounds.Expand(2f * boundsPadding / minimumPositiveScale);
                renderer.localBounds = bounds;
            }
        }

        void RestoreBounds()
        {
            foreach (var pair in originalBounds)
                if (pair.Key != null) pair.Key.localBounds = pair.Value;
            originalBounds.Clear();
        }
    }
}
