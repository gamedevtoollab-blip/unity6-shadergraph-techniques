using UnityEngine;

namespace ShaderGraphTechniques
{
    [RequireComponent(typeof(MaterialInstanceOwner))]
    public sealed class HeatHazeController : MonoBehaviour
    {
        [SerializeField] bool useManualTime;
        [SerializeField] float manualTime;
        [SerializeField, Range(0f, 1f)] float opacity = 1f;
        MaterialInstanceOwner owner;

        void Awake() => owner = GetComponent<MaterialInstanceOwner>();
        void OnEnable() { if (owner == null) owner = GetComponent<MaterialInstanceOwner>(); Apply(); }
        void Update() { if (!useManualTime) return; Apply(); }

        public void SetManualTime(float seconds)
        {
            useManualTime = true;
            manualTime = seconds;
            Apply();
        }

        public void UseRealtime() { useManualTime = false; Apply(); }
        public void SetOpacity(float value) { opacity = Mathf.Clamp01(value); Apply(); }

        void Apply()
        {
            if (owner == null || !isActiveAndEnabled) return;
            owner.SetFloat("_UseManualTime", useManualTime ? 1f : 0f);
            owner.SetFloat("_ManualTime", manualTime);
            owner.SetFloat("_Opacity", opacity);
        }
    }
}
