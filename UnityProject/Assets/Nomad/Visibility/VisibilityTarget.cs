using System.Collections.Generic;
using Nomad.Combat;
using Nomad.Monsters;
using UnityEngine;

namespace Nomad.Visibility
{
    [DefaultExecutionOrder(1000)]
    public sealed class VisibilityTarget : MonoBehaviour
    {
        internal static readonly HashSet<VisibilityTarget> Active = new();
        public static int ActiveCount => Active.Count;
        [SerializeField, Range(0f, 0.2f)] private float hideDelay = 0.08f;
        private readonly Dictionary<Renderer, bool> renderers = new();
        private readonly Dictionary<Behaviour, bool> displays = new();
        private float lastSeen = float.NegativeInfinity;
        private PlayerVisibility owner;
        private int revision = -1;
        private Health health;

        public bool IsVisible { get; private set; }
        public bool Controlled { get; private set; }
        // 시체는 파밍할 수 있도록 시야와 관계없이 항상 보입니다.
        public bool IsCorpse => health != null && health.IsDead;

        private void Awake() => health = GetComponent<Health>();

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            Active.Add(this);
            owner = FindFirstObjectByType<PlayerVisibility>();
            if (owner != null && owner.Available) Evaluate(owner);
        }

        public void Evaluate(PlayerVisibility source)
        {
            owner = source;
            if (!source.Available || IsCorpse) { Release(); return; }
            if (revision != source.Revision)
            {
                lastSeen = float.NegativeInfinity;
                revision = source.Revision;
            }

            Capture();
            Bounds bounds = new Bounds(transform.position + Vector3.up, new Vector3(0.6f, 1.8f, 0.6f));
            Collider body = GetComponent<Collider>();
            if (body != null) bounds = body.bounds;
            bool foundModel = false;
            Transform modelRoot = transform.Find("Body");
            foreach (var item in renderers)
            {
                Renderer renderer = item.Key;
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || item.Value ||
                    (modelRoot != null && !renderer.transform.IsChildOf(modelRoot)) ||
                    (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer)) continue;
                if (foundModel) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; foundModel = true; }
            }
            Vector3 middle = bounds.center;
            Vector3 side = Vector3.Cross(Vector3.up, source.Forward).normalized * Mathf.Min(0.3f, bounds.extents.x);
            bool seen = source.CanSee(middle, transform) ||
                source.CanSee(middle + Vector3.up * bounds.extents.y * 0.95f, transform) ||
                source.CanSee(middle - side, transform) || source.CanSee(middle + side, transform);
            if (seen) lastSeen = Time.unscaledTime;

            IsVisible = seen || (Controlled && Time.unscaledTime - lastSeen < hideDelay);
            Controlled = true;
            Apply();
        }

        private void Capture()
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                if (!renderers.ContainsKey(renderer)) renderers.Add(renderer, renderer.forceRenderingOff);
            CaptureDisplay(GetComponent<SegmentedHealthBar>());
            CaptureDisplay(GetComponent<MonsterHitFeedback>());
        }

        private void CaptureDisplay(Behaviour display)
        {
            if (display != null && !displays.ContainsKey(display)) displays.Add(display, display.enabled);
        }

        private void LateUpdate()
        {
            if (!Controlled) return;
            if (owner == null || !owner.Available || IsCorpse) { Release(); return; }
            // 공격 표시/피격 파편처럼 나중에 생긴 Renderer도 숨깁니다.
            Capture();
            Apply();
        }

        private void Apply()
        {
            // 시야 밖 적은 그림자까지 완전히 숨겨 위치가 드러나지 않게 합니다.
            foreach (var item in renderers)
                if (item.Key != null) item.Key.forceRenderingOff = item.Value || !IsVisible;
            foreach (var item in displays)
                if (item.Key != null) item.Key.enabled = item.Value && IsVisible;
        }

        public void Release()
        {
            foreach (var item in renderers)
                if (item.Key != null) item.Key.forceRenderingOff = item.Value;
            foreach (var item in displays)
                if (item.Key != null) item.Key.enabled = item.Value;
            renderers.Clear();
            displays.Clear();
            Controlled = false;
            IsVisible = true;
            lastSeen = float.NegativeInfinity;
        }

        private void OnDisable() { Active.Remove(this); Release(); }
    }
}
