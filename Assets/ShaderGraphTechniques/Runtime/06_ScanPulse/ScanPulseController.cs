using UnityEngine;

namespace ShaderGraphTechniques
{
    [RequireComponent(typeof(MaterialInstanceOwner))]
    public sealed class ScanPulseController : MonoBehaviour
    {
        [SerializeField] Transform center;
        [SerializeField, Min(0f)] float radius;
        [SerializeField] bool active = true;
        [SerializeField] bool autoPlay;
        [SerializeField, Min(0.1f)] float cycleSeconds = 4f;
        [SerializeField, Min(0.01f)] float maximumRadius = 6f;
        MaterialInstanceOwner owner;

        void Awake() => owner = GetComponent<MaterialInstanceOwner>();
        void OnEnable() { if (owner == null) owner = GetComponent<MaterialInstanceOwner>(); Apply(); }
        void Update()
        {
            if (autoPlay)
            {
                var phase = Mathf.Repeat(Time.time / cycleSeconds, 1f);
                SetState(phase < 0.985f, phase * maximumRadius);
            }
            else Apply();
        }

        public void SetState(bool isActive, float newRadius)
        {
            active = isActive;
            radius = Mathf.Max(0f, newRadius);
            Apply();
        }

        public void SetCenter(Transform newCenter) { center = newCenter; Apply(); }

        void Apply()
        {
            if (owner == null || !isActiveAndEnabled) return;
            owner.SetFloat("_ScanActive", active ? 1f : 0f);
            owner.SetFloat("_ScanRadius", radius);
            owner.SetVector("_ScanCenterWS", center == null ? transform.position : center.position);
        }
    }
}
