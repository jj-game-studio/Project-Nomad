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
        [SerializeField, Min(0f)] private float abandonRange = 12f;
        [SerializeField, Min(0f)] private float searchDuration = 2f;
        [SerializeField] private LayerMask sightMask = ~0;
        [SerializeField, Min(0f)] private float homeRange;

        private NavMeshAgent agent;
        private Vector3 lastSeenPosition;
        private float unseenTime;
        private float nextPathTime;
        private float combatStoppingDistance;

        public bool IsTracking { get; private set; }
        public bool HasLineOfSight { get; private set; }
        public bool PathBlocked { get; private set; }
        public bool IsReturning { get; private set; }
        public Vector3 HomePosition { get; private set; }
        public float SightRange => sightRange;
        public float AbandonRange => abandonRange;
        public float HomeRange => homeRange;

        public void Configure(float sight, float abandon, float territory)
        {
            sightRange = Mathf.Max(0f, sight);
            abandonRange = Mathf.Max(sightRange, abandon);
            homeRange = Mathf.Max(0f, territory);
        }

        public void BindTarget(Transform value)
        {
            StopTracking();
            target = value;
        }

        public void ReactToDamage()
        {
            if (!CanEngageTarget())
                return;

            // 현재 전투의 단일 지정 목표를 공격자로 취급합니다.
            lastSeenPosition = target.position;
            unseenTime = 0f;
            nextPathTime = 0f;
            IsTracking = true;
        }

        public void ResumeCombatTracking()
        {
            if (CanEngageTarget())
                IsTracking = true;
            // 공격 후 복원할 때 벽 뒤 목표의 현재 위치를 다시 읽지 않습니다.
        }

        private bool CanEngageTarget()
        {
            return target != null && target.gameObject.activeInHierarchy && !IsReturning &&
                (target.position - transform.position).sqrMagnitude <= abandonRange * abandonRange &&
                (homeRange <= 0f || Vector3.ProjectOnPlane(transform.position - HomePosition,
                    Vector3.up).sqrMagnitude < homeRange * homeRange);
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            HomePosition = transform.position;
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
            // 기존 추적은 기본값 0으로 유지하고 테스트 개체에서만 활동 반경을 켭니다.
            if (homeRange > 0f && (IsReturning ||
                Vector3.ProjectOnPlane(transform.position - HomePosition, Vector3.up).sqrMagnitude >= homeRange * homeRange))
            {
                ReturnHome();
                return;
            }

            if (IsTracking && offset.sqrMagnitude > abandonRange * abandonRange)
            {
                StopTracking();
                if (homeRange > 0f)
                    ReturnHome();
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
                if (homeRange > 0f)
                    ReturnHome();
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
            // 방향과 관계없이 거리로 탐지하되, 아래 지형 차폐 검사는 유지합니다.
            if (!IsTracking && distance > sightRange)
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

        private void ReturnHome()
        {
            if (!IsReturning)
            {
                agent.ResetPath();
                nextPathTime = 0f;
                combatStoppingDistance = agent.stoppingDistance;
                // 공격 사거리의 정지 여유가 원점 도착 반경보다 크면 복귀가 끝나지 않습니다.
                agent.stoppingDistance = Mathf.Min(combatStoppingDistance, 0.15f);
            }
            IsTracking = false;
            HasLineOfSight = false;
            IsReturning = true;
            if (Vector3.ProjectOnPlane(transform.position - HomePosition, Vector3.up).sqrMagnitude < 0.25f)
            {
                StopTracking();
                return;
            }

            if (Time.time < nextPathTime)
                return;

            nextPathTime = Time.time + 0.2f;
            var path = new NavMeshPath();
            PathBlocked = !agent.CalculatePath(HomePosition, path) || path.status != NavMeshPathStatus.PathComplete;
            if (PathBlocked)
                agent.ResetPath();
            else
                agent.SetPath(path);
        }

        private void StopTracking()
        {
            if (IsReturning && agent != null) agent.stoppingDistance = combatStoppingDistance;
            IsTracking = false;
            IsReturning = false;
            HasLineOfSight = false;
            PathBlocked = false;
            unseenTime = 0f;
            nextPathTime = 0f;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.ResetPath();
        }
    }
}
