using UnityEngine;

namespace ShaderGraphTechniques
{
    [RequireComponent(typeof(MaterialInstanceOwner))]
    public sealed class DissolveController : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float progress;
        [SerializeField] bool autoPlay;
        [SerializeField, Min(0.1f)] float cycleSeconds = 3f;
        MaterialInstanceOwner owner;

        public float Progress => progress;

        void Awake() => owner = GetComponent<MaterialInstanceOwner>();
        void OnEnable() { if (owner == null) owner = GetComponent<MaterialInstanceOwner>(); Apply(); }
        void Update() { if (autoPlay) SetProgress(Mathf.PingPong(Time.time / cycleSeconds, 1f)); }
        void OnValidate() { if (Application.isPlaying) Apply(); }

        public void SetProgress(float value)
        {
            progress = Mathf.Clamp01(value);
            Apply();
        }

        void Apply()
        {
            if (owner != null && isActiveAndEnabled) owner.SetFloat("_Progress", progress);
        }
    }
}
