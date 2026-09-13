using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShaderGraphTechniques.Demo
{
    /// <summary>
    /// Presentation-only automation. Runtime effects remain usable without this component.
    /// All movement is derived from one resettable clock so stills and frame sequences are repeatable.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class DemoPresentationController : MonoBehaviour
    {
        public enum DemoKind
        {
            Gallery,
            Dissolve,
            IntersectionShield,
            HeatHaze,
            TriplanarSnow,
            InteractiveGrass,
            ScanPulse
        }

        [SerializeField] DemoKind kind;
        [SerializeField] bool manualMode;
        [SerializeField, Min(0f)] float manualTime = 1.5f;
        [SerializeField] bool showOverlay = true;
        [SerializeField] DissolveController[] dissolves = Array.Empty<DissolveController>();
        [SerializeField] HeatHazeController[] heatHazes = Array.Empty<HeatHazeController>();
        [SerializeField] InteractiveGrassController[] grasses = Array.Empty<InteractiveGrassController>();
        [SerializeField] ScanPulseController[] scans = Array.Empty<ScanPulseController>();
        [SerializeField] Transform[] interactors = Array.Empty<Transform>();
        [SerializeField] Transform[] snowShowpieces = Array.Empty<Transform>();

        float resetTime;
        bool showcaseMode;
        Camera presentationCamera;
        Vector3 cameraBasePosition;
        Quaternion cameraBaseRotation;
        float cameraBaseFieldOfView;
        bool cameraBaseCached;
        Transform shieldSurface;
        Transform[] shieldIntersectors = Array.Empty<Transform>();
        Transform[] shieldReferenceObjects = Array.Empty<Transform>();
        Transform[] heatColumns = Array.Empty<Transform>();
        Transform[] scanCenters = Array.Empty<Transform>();
        Material[] snowMaterials = Array.Empty<Material>();
        readonly Dictionary<Transform, Vector3> baseLocalPositions = new();
        readonly Dictionary<Transform, Quaternion> baseLocalRotations = new();
        readonly Dictionary<Transform, Vector3> baseLocalScales = new();
        readonly Dictionary<GameObject, bool> baseActiveStates = new();
        readonly List<Material> ownedShowcaseMaterials = new();
        GUIStyle titleStyle;
        GUIStyle bodyStyle;

        public DemoKind Kind => kind;
        public bool ManualMode => manualMode;
        public float EvaluationTime => manualMode ? manualTime : Mathf.Max(0f, Time.unscaledTime - resetTime);

        public void Configure(DemoKind demoKind, DissolveController[] dissolveControllers,
            HeatHazeController[] heatHazeControllers, InteractiveGrassController[] grassControllers,
            ScanPulseController[] scanControllers, Transform[] movingInteractors, Transform[] snowObjects)
        {
            kind = demoKind;
            dissolves = dissolveControllers ?? Array.Empty<DissolveController>();
            heatHazes = heatHazeControllers ?? Array.Empty<HeatHazeController>();
            grasses = grassControllers ?? Array.Empty<InteractiveGrassController>();
            scans = scanControllers ?? Array.Empty<ScanPulseController>();
            interactors = movingInteractors ?? Array.Empty<Transform>();
            snowShowpieces = snowObjects ?? Array.Empty<Transform>();
        }

        void Awake()
        {
            resetTime = Time.unscaledTime;
            Cursor.visible = showOverlay;
            CachePresentationState();
        }

        void Update() => Evaluate(EvaluationTime);

        void LateUpdate()
        {
            // Keep the authored scene framing authoritative even if another
            // presentation component updates a camera later in the frame.
            if (showcaseMode) LockCamera();
        }

        public void Evaluate(float seconds)
        {
            var t = Mathf.Max(0f, seconds);
            for (var index = 0; index < dissolves.Length; index++)
            {
                var controller = dissolves[index];
                if (controller == null) continue;
                var phase = showcaseMode
                    ? Mathf.Repeat((t + index * .85f) / 5.8f, 1f)
                    : Mathf.Repeat(t / 4f, 1f);
                var progress = phase < .12f ? 0f :
                    phase < .43f ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.12f, .43f, phase)) :
                    phase < .56f ? 1f :
                    phase < .87f ? Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(.56f, .87f, phase)) : 0f;
                controller.SetProgress(progress);

                if (showcaseMode && baseLocalRotations.TryGetValue(controller.transform, out var baseRotation))
                {
                    var turn = t * (24f + index * 8f);
                    var tilt = Mathf.Sin(t * 1.4f + index) * 7f;
                    controller.transform.localRotation = baseRotation * Quaternion.Euler(tilt, turn, -tilt * .35f);
                }
            }

            foreach (var controller in heatHazes.Where(item => item != null))
            {
                controller.SetManualTime(showcaseMode ? t * 1.8f : t);
                if (!showcaseMode)
                {
                    controller.SetOpacity(1f);
                    continue;
                }

                var comparePhase = Mathf.Repeat(t, 6.4f);
                var opacity = comparePhase < .8f ? Mathf.SmoothStep(0f, 1f, comparePhase / .8f) :
                    comparePhase < 4.5f ? 1f :
                    comparePhase < 5.1f ? Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(4.5f, 5.1f, comparePhase)) :
                    comparePhase < 5.55f ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5.55f, 6.4f, comparePhase));
                controller.SetOpacity(opacity);
                var owner = controller.GetComponent<MaterialInstanceOwner>();
                if (owner != null)
                    owner.SetFloat("_Distortion", Mathf.Lerp(.012f, .028f, .5f + .5f * Mathf.Sin(t * 1.65f)));
            }

            for (var index = 0; index < interactors.Length; index++)
            {
                var interactor = interactors[index];
                if (interactor == null) continue;
                var origin = baseLocalPositions.TryGetValue(interactor, out var basePosition) ? basePosition : interactor.localPosition;
                var phase = t * (showcaseMode ? 1.28f : .72f) + index * 1.7f;
                var amplitude = showcaseMode ? 2.25f : 1.2f;
                interactor.localPosition = origin + new Vector3(
                    Mathf.Sin(phase) * amplitude,
                    showcaseMode ? Mathf.Sin(phase * 1.7f) * .16f : 0f,
                    Mathf.Sin(phase * 2f) * (showcaseMode ? .78f : .7f));
            }

            foreach (var controller in grasses.Where(item => item != null))
            {
                if (showcaseMode)
                {
                    if (controller.Interactor == null && interactors.Length > 0) controller.Interactor = interactors[0];
                    controller.SetBend(1.25f, .95f);
                }
                controller.SetManualTime(t);
            }

            foreach (var controller in scans.Where(item => item != null))
            {
                var phase = Mathf.Repeat(t / (showcaseMode ? 2.85f : 4.2f), 1f);
                controller.SetState(phase < .965f, phase * (showcaseMode ? 9f : 8f));
            }

            for (var index = 0; index < snowShowpieces.Length; index++)
            {
                var item = snowShowpieces[index];
                if (item == null) continue;
                var baseRotation = baseLocalRotations.TryGetValue(item, out var savedRotation) ? savedRotation : Quaternion.identity;
                var speed = showcaseMode ? 24f + index * 9f : 7f + index;
                item.localRotation = baseRotation * Quaternion.Euler(
                    showcaseMode ? Mathf.Sin(t * 1.15f + index) * 10f : 0f,
                    t * speed,
                    showcaseMode ? Mathf.Cos(t * .9f + index) * 6f : 0f);
            }

            if (showcaseMode)
            {
                AnimateSnowCoverage(t);
                AnimateShield(t);
                AnimateHeatBackground(t);
                AnimateScanCenters(t);
                LockCamera();
            }
        }

        public void SetShowcaseMode(bool enabled)
        {
            showcaseMode = enabled;
            if (enabled)
            {
                EnsureShowcaseResources();
                foreach (var item in shieldReferenceObjects.Where(item => item != null))
                    item.gameObject.SetActive(false);
                foreach (var grass in grasses.Where(item => item != null))
                    if (grass.Interactor == null && interactors.Length > 0) grass.Interactor = interactors[0];
            }
            else RestorePresentationState();
            Evaluate(EvaluationTime);
        }

        public void SetManualMode(bool enabled)
        {
            manualMode = enabled;
            if (!enabled) resetTime = Time.unscaledTime - manualTime;
            Evaluate(EvaluationTime);
        }

        public void SetManualTime(float seconds)
        {
            manualTime = Mathf.Max(0f, seconds);
            manualMode = true;
            Evaluate(manualTime);
        }

        public void ResetDemo()
        {
            resetTime = Time.unscaledTime;
            manualTime = 0f;
            Evaluate(0f);
        }

        public void SetOverlayVisible(bool visible)
        {
            showOverlay = visible;
            Cursor.visible = visible;
        }

        void CachePresentationState()
        {
            CachePresentationCamera();

            var sceneTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            shieldSurface = sceneTransforms.FirstOrDefault(item =>
                item.name.EndsWith("IntersectionShield", StringComparison.Ordinal));
            shieldIntersectors = sceneTransforms.Where(item => item.name == "Center Pillar").ToArray();
            shieldReferenceObjects = sceneTransforms.Where(item =>
                item.name == "Side Pillar" || item.name == "Occlusion Check").ToArray();
            heatColumns = sceneTransforms.Where(item => item.name.StartsWith("Background Column ", StringComparison.Ordinal)).ToArray();

            Remember(shieldSurface);
            foreach (var item in dissolves.Where(item => item != null)) Remember(item.transform);
            foreach (var item in interactors.Where(item => item != null)) Remember(item);
            foreach (var item in snowShowpieces.Where(item => item != null)) Remember(item);
            foreach (var item in shieldIntersectors) Remember(item);
            foreach (var item in shieldReferenceObjects.Where(item => item != null))
                baseActiveStates[item.gameObject] = item.gameObject.activeSelf;
            foreach (var item in heatColumns) Remember(item);
        }

        void Remember(Transform item)
        {
            if (item == null || baseLocalPositions.ContainsKey(item)) return;
            baseLocalPositions[item] = item.localPosition;
            baseLocalRotations[item] = item.localRotation;
            baseLocalScales[item] = item.localScale;
        }

        void EnsureShowcaseResources()
        {
            if (snowMaterials.Length == 0 && snowShowpieces.Length > 0)
            {
                snowMaterials = snowShowpieces
                    .Where(item => item != null)
                    .SelectMany(item => item.GetComponentsInChildren<Renderer>(true))
                    .Where(renderer => renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_SnowAmount"))
                    .Select(renderer =>
                    {
                        var material = new Material(renderer.sharedMaterial) { name = renderer.sharedMaterial.name + " (Showcase)" };
                        renderer.sharedMaterial = material;
                        ownedShowcaseMaterials.Add(material);
                        return material;
                    })
                    .ToArray();
            }

            if (scanCenters.Length == 0 && scans.Length > 0)
            {
                scanCenters = new Transform[scans.Length];
                for (var index = 0; index < scans.Length; index++)
                {
                    var scan = scans[index];
                    if (scan == null) continue;
                    var center = new GameObject("Showcase Scan Center " + index).transform;
                    center.position = scan.transform.position;
                    scan.SetCenter(center);
                    scanCenters[index] = center;
                    Remember(center);
                }
            }
        }

        void AnimateSnowCoverage(float t)
        {
            var phase = Mathf.Repeat(t / 7.4f, 1f);
            var amount = phase < .12f ? 0f :
                phase < .52f ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.12f, .52f, phase)) :
                phase < .72f ? 1f : Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(.72f, 1f, phase));
            foreach (var material in snowMaterials)
                if (material != null) material.SetFloat("_SnowAmount", amount);
        }

        void AnimateShield(float t)
        {
            if (shieldSurface != null && baseLocalScales.TryGetValue(shieldSurface, out var baseScale))
                // Enlarge the presentation-only shield so the cyan contact rim
                // remains readable after the scene is placed in a 1080p montage.
                shieldSurface.localScale = baseScale * (1.28f + Mathf.Sin(t * 2.1f) * .04f);

            for (var index = 0; index < shieldIntersectors.Length; index++)
            {
                var item = shieldIntersectors[index];
                if (item == null || !baseLocalPositions.TryGetValue(item, out var origin)) continue;

                // Move the only visible pillar completely outside the shield, pass it
                // through the center, hold the contact, then continue to the far side.
                var phase = Mathf.Repeat(t / 5f, 1f);
                var travel = phase < .14f ? -1f :
                    phase < .34f ? Mathf.SmoothStep(-1f, .22f, Mathf.InverseLerp(.14f, .34f, phase)) :
                    phase < .50f ? .22f :
                    phase < .70f ? Mathf.SmoothStep(.22f, 1f, Mathf.InverseLerp(.50f, .70f, phase)) :
                    phase < .82f ? 1f :
                    Mathf.SmoothStep(1f, -1f, Mathf.InverseLerp(.82f, 1f, phase));
                item.localPosition = origin + new Vector3(travel * 2.25f, 0f, 0f);

                if (baseLocalRotations.TryGetValue(item, out var baseRotation))
                    item.localRotation = baseRotation * Quaternion.Euler(0f, t * 18f, 0f);
            }
        }

        void AnimateHeatBackground(float t)
        {
            for (var index = 0; index < heatColumns.Length; index++)
            {
                var item = heatColumns[index];
                if (item == null || !baseLocalPositions.TryGetValue(item, out var origin)) continue;
                item.localPosition = origin + new Vector3(
                    Mathf.Sin(t * 1.55f + index * .82f) * .12f,
                    Mathf.Sin(t * 1.17f + index * 1.1f) * .09f,
                    0f);
            }
        }

        void AnimateScanCenters(float t)
        {
            for (var index = 0; index < scanCenters.Length; index++)
            {
                var item = scanCenters[index];
                if (item == null || !baseLocalPositions.TryGetValue(item, out var origin)) continue;
                item.localPosition = origin + new Vector3(
                    Mathf.Sin(t * .72f + index) * .8f,
                    .15f + Mathf.Sin(t * 1.05f + index) * .12f,
                    Mathf.Cos(t * .58f + index) * .55f);
            }
        }

        void LockCamera()
        {
            // Camera.main can briefly resolve to the outgoing scene while the
            // capture runner loads a requested scene. Reacquire by scene so the
            // lock always targets the camera that actually renders this demo.
            if (presentationCamera == null || presentationCamera.gameObject.scene != gameObject.scene)
                CachePresentationCamera();
            if (presentationCamera == null || !cameraBaseCached) return;
            presentationCamera.transform.SetPositionAndRotation(cameraBasePosition, cameraBaseRotation);
            presentationCamera.fieldOfView = cameraBaseFieldOfView;
        }

        void CachePresentationCamera()
        {
            var scene = gameObject.scene;
            presentationCamera = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(item => item != null && item.gameObject.scene == scene && item.CompareTag("MainCamera"))
                ?? FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(item => item != null && item.gameObject.scene == scene);
            if (presentationCamera == null) return;

            cameraBasePosition = presentationCamera.transform.position;
            cameraBaseRotation = presentationCamera.transform.rotation;
            cameraBaseFieldOfView = presentationCamera.fieldOfView;
            cameraBaseCached = true;
        }

        void RestorePresentationState()
        {
            foreach (var pair in baseLocalPositions)
                if (pair.Key != null) pair.Key.localPosition = pair.Value;
            foreach (var pair in baseLocalRotations)
                if (pair.Key != null) pair.Key.localRotation = pair.Value;
            foreach (var pair in baseLocalScales)
                if (pair.Key != null) pair.Key.localScale = pair.Value;
            foreach (var pair in baseActiveStates)
                if (pair.Key != null) pair.Key.SetActive(pair.Value);
            if (presentationCamera != null)
            {
                presentationCamera.transform.SetPositionAndRotation(cameraBasePosition, cameraBaseRotation);
                presentationCamera.fieldOfView = cameraBaseFieldOfView;
            }
        }

        void OnDestroy()
        {
            foreach (var material in ownedShowcaseMaterials)
                if (material != null) Destroy(material);
            ownedShowcaseMaterials.Clear();
        }

        void OnGUI()
        {
            if (!showOverlay) return;
            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(20, Screen.height / 38),
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(14, Screen.height / 58),
                normal = { textColor = new Color(.83f, .9f, 1f) }
            };

            var panel = new Rect(24f, 20f, Mathf.Min(720f, Screen.width - 48f), 126f);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 10f, panel.width - 36f, 34f),
                "Shader Graph Techniques — " + kind, titleStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 48f, panel.width - 36f, 28f),
                $"{(manualMode ? "Manual" : "Auto")}  t={EvaluationTime:0.00}s  |  all animation uses the same capture clock", bodyStyle);

            if (GUI.Button(new Rect(panel.x + 18f, panel.y + 86f, 110f, 28f), manualMode ? "Resume" : "Freeze"))
                SetManualMode(!manualMode);
            if (GUI.Button(new Rect(panel.x + 138f, panel.y + 86f, 110f, 28f), "Reset")) ResetDemo();
            if (GUI.Button(new Rect(panel.x + 258f, panel.y + 86f, 110f, 28f), "Hide UI")) SetOverlayVisible(false);
        }
    }
}
