using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class MonsterTracker : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float sightRange = 8f;
        [SerializeField, Range(0f, 360f)] private float sightAngle = 120f;
        [SerializeField, Min(0f)] private float proximityRange = 2f;
        [SerializeField, Min(0f)] private float abandonRange = 12f;
        [SerializeField, Min(0f)] private float searchDuration = 2f;
        [SerializeField] private LayerMask sightMask = ~0;

        private NavMeshAgent agent;
        private Vector3 lastSeenPosition;
        private float unseenTime;
        private float nextPathTime;

        public bool IsTracking { get; private set; }
        public bool HasLineOfSight { get; private set; }
        public bool PathBlocked { get; private set; }

        public void BindTarget(Transform value)
        {
            StopTracking();
            target = value;
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        private void OnDisable()
        {
            StopTracking();
        }

        private void Update()
        {
            HasLineOfSight = false;
            if (target == null || !target.gameObject.activeInHierarchy ||
                !agent.enabled || !agent.isOnNavMesh)
            {
                StopTracking();
                return;
            }

            Vector3 offset = target.position - transform.position;
            if (IsTracking && offset.sqrMagnitude > abandonRange * abandonRange)
            {
                StopTracking();
                return;
            }

            HasLineOfSight = CanSee(offset);
            if (HasLineOfSight)
            {
                IsTracking = true;
                lastSeenPosition = target.position;
                unseenTime = 0f;
            }
            else
            {
                unseenTime += Time.deltaTime;
            }

            if (!IsTracking)
                return;

            // 가려진 목표의 실시간 위치를 읽지 않고 마지막 목격 위치까지만 이동합니다.
            if (!HasLineOfSight && unseenTime >= searchDuration)
            {
                StopTracking();
                return;
            }

            if (Time.time < nextPathTime)
                return;

            nextPathTime = Time.time + 0.2f;
            // 같은 경로를 계속 덮어쓰면 계단 모서리에서 감속과 회전이 반복될 수 있습니다.
            if (agent.hasPath && (agent.destination - lastSeenPosition).sqrMagnitude < 0.01f)
                return;

            var path = new NavMeshPath();
            PathBlocked = !agent.CalculatePath(lastSeenPosition, path) ||
                path.status != NavMeshPathStatus.PathComplete;

            if (PathBlocked)
            {
                // 끊긴 경로를 강제로 직선 이동하거나 무한 재시도하지 않습니다.
                agent.ResetPath();
                return;
            }

            agent.SetPath(path);
        }

        private bool CanSee(Vector3 offset)
        {
            float distance = offset.magnitude;
            bool nearby = distance <= proximityRange;
            Vector3 planar = Vector3.ProjectOnPlane(offset, Vector3.up);
            if (!nearby && (distance > sightRange ||
                Vector3.Angle(transform.forward, planar) > sightAngle * 0.5f))
                return false;

            Vector3 eye = transform.position + Vector3.up;
            Vector3 destination = target.position + Vector3.up;
            Vector3 ray = destination - eye;
            foreach (RaycastHit hit in Physics.RaycastAll(eye, ray.normalized, ray.magnitude,
                sightMask, QueryTriggerInteraction.Ignore))
            {
                Transform obstacle = hit.transform;
                if (obstacle == transform || obstacle.IsChildOf(transform) ||
                    obstacle == target || obstacle.IsChildOf(target))
                    continue;

                return false;
            }

            return true;
        }

        private void StopTracking()
        {
            IsTracking = false;
            HasLineOfSight = false;
            PathBlocked = false;
            unseenTime = 0f;
            nextPathTime = 0f;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.ResetPath();
        }
    }
}
