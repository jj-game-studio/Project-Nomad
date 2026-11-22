using Nomad.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Visibility
{
    // 플레이어의 표시용 시야입니다. 몬스터 AI의 감지는 변경하지 않습니다.
    [DefaultExecutionOrder(500)]
    public sealed class PlayerVisibility : MonoBehaviour
    {
        [SerializeField] private Transform observer;
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(1f)] private float distance = 24f;
        [SerializeField, Range(1f, 360f)] private float angle = 120f;
        [SerializeField, Min(0.1f)] private float eyeHeight = 1.2f;
        [SerializeField] private LayerMask blockers = Physics.DefaultRaycastLayers;
        [SerializeField, Range(0.02f, 0.2f)] private float interval = 0.08f;

        private readonly RaycastHit[] hits = new RaycastHit[64];
        private float nextSample;
        private Vector3 forward = Vector3.forward;
        private Transform previousObserver;
        private Vector3 previousPosition;

        public Transform Observer => observer;
        public Vector3 Eye => observer.position + Vector3.up * eyeHeight;
        public Vector3 Forward => forward;
        public float Distance => distance;
        public float Angle => angle;
        public bool Available => isActiveAndEnabled && observer != null && observer.gameObject.activeInHierarchy;
        public int Revision { get; private set; }

        public void Configure(Transform source, Camera camera, float range = 24f, float degrees = 120f)
        {
            observer = source;
            viewCamera = camera;
            distance = Mathf.Max(1f, range);
            angle = Mathf.Clamp(degrees, 1f, 360f);
            forward = source != null ? source.forward : Vector3.forward;
            nextSample = 0f;
        }

        private void LateUpdate()
        {
            if (!Available)
            {
                foreach (VisibilityTarget target in VisibilityTarget.Active) target.Release();
                return;
            }
            UpdateDirection();

            // 순간이동/대상 교체는 표시 유예를 초기화합니다.
            if (previousObserver != observer || Vector3.Distance(previousPosition, observer.position) > 3f)
                Revision++;
            previousObserver = observer;
            previousPosition = observer.position;

            if (Time.unscaledTime < nextSample) return;
            nextSample = Time.unscaledTime + interval;
            foreach (VisibilityTarget target in VisibilityTarget.Active) target.Evaluate(this);
        }

        public void SetDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) forward = direction.normalized;
        }

        private void UpdateDirection()
        {
            Camera camera = viewCamera != null ? viewCamera : Camera.main;
            if (camera == null || Mouse.current == null || Time.timeScale <= 0f) return;
            Vector2 pointer = Mouse.current.position.ReadValue();
            if (!camera.pixelRect.Contains(pointer)) return;

            Ray ray = camera.ScreenPointToRay(pointer);
            Plane plane = new Plane(Vector3.up, observer.position + Vector3.up * eyeHeight);
            if (plane.Raycast(ray, out float length)) SetDirection(ray.GetPoint(length) - Eye);
        }

        public bool InCone(Vector3 point)
        {
            if (!Available) return false;
            Vector3 offset = point - observer.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= distance * distance &&
                (offset.sqrMagnitude < 0.0001f || angle >= 359.9f ||
                 Vector3.Dot(forward, offset.normalized) >= Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad));
        }

        public bool CanSee(Vector3 point, Transform target = null)
        {
            if (!InCone(point)) return false;
            Vector3 ray = point - Eye;
            return ClearDistance(Eye, ray.normalized, ray.magnitude, target) >= ray.magnitude - 0.01f;
        }

        public float ClearDistance(Vector3 origin, Vector3 direction, float length, Transform target = null)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hits, length, blockers, QueryTriggerInteraction.Ignore);
            RaycastHit[] candidates = hits;
            if (count == hits.Length)
            {
                // 포화된 배열에는 가장 가까운 충돌이 빠질 수 있으므로 다시 확인합니다.
                candidates = Physics.RaycastAll(origin, direction, length, blockers, QueryTriggerInteraction.Ignore);
                count = candidates.Length;
            }

            float nearest = length;
            for (int index = 0; index < count; index++)
            {
                Collider collider = candidates[index].collider;
                Transform item = collider.transform;
                if (item.IsChildOf(observer) || (target != null && item.IsChildOf(target))) continue;
                if (collider.GetComponentInParent<Health>() != null) continue;
                VisibilityFoliage foliage = collider.GetComponentInParent<VisibilityFoliage>();
                if (foliage != null && !foliage.BlocksSight) continue;
                nearest = Mathf.Min(nearest, candidates[index].distance);
            }
            return nearest;
        }

        private void OnDisable()
        {
            foreach (VisibilityTarget target in VisibilityTarget.Active) target.Release();
        }
    }
}
