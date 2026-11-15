using Nomad.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters
{
    public enum MeleePhase { Idle, Windup, Recovery, Dead }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(MonsterTracker))]
    public sealed class MonsterMeleeAttack : MonoBehaviour
    {
        [SerializeField] private Health target;
        [SerializeField, Min(1)] private int damage = 20;
        [SerializeField, Min(0.1f)] private float attackRange = 1.8f;
        [SerializeField, Min(0.05f)] private float windupSeconds = 0.5f;
        [SerializeField, Min(0.05f)] private float recoverySeconds = 1f;

        private Health health;
        private MonsterTracker tracker;
        private NavMeshAgent agent;
        private float phaseEnd;
        private bool ownsPause;
        private bool trackerWasEnabled;

        public MeleePhase Phase { get; private set; }

        private void Awake()
        {
            health = GetComponent<Health>();
            tracker = GetComponent<MonsterTracker>();
            agent = GetComponent<NavMeshAgent>();
        }

        private void OnEnable()
        {
            Phase = MeleePhase.Idle;
            health.Died += OnDied;
            if (target != null)
                target.Died += OnTargetDied;
        }

        private void OnDisable()
        {
            health.Died -= OnDied;
            if (target != null)
                target.Died -= OnTargetDied;

            Phase = health.IsDead ? MeleePhase.Dead : MeleePhase.Idle;
            ResumeMovement();
        }

        private void Update()
        {
            if (health.IsDead)
            {
                OnDied(health);
                return;
            }

            if (target == null || !target.isActiveAndEnabled || target.IsDead)
            {
                Phase = MeleePhase.Idle;
                PauseMovement();
                return;
            }

            if (Phase == MeleePhase.Windup && Time.time >= phaseEnd)
            {
                // 준비가 끝난 시점에 거리와 벽을 다시 확인하며 피해는 한 번만 전달합니다.
                if (CanStrike())
                    target.TryApplyDamage(damage);

                if (health.IsDead || target.IsDead)
                    return;

                Phase = MeleePhase.Recovery;
                phaseEnd = Time.time + recoverySeconds;
            }
            else if (Phase == MeleePhase.Recovery && Time.time >= phaseEnd)
            {
                Phase = MeleePhase.Idle;
                ResumeMovement();
            }
            else if (Phase == MeleePhase.Idle)
            {
                ResumeMovement();
                if (tracker.IsTracking && tracker.HasLineOfSight && CanStrike())
                {
                    PauseMovement();
                    Phase = MeleePhase.Windup;
                    phaseEnd = Time.time + windupSeconds;
                }
            }
        }

        private bool CanStrike()
        {
            if (target == null || !target.isActiveAndEnabled || target.IsDead ||
                (target.transform.position - transform.position).sqrMagnitude > attackRange * attackRange)
                return false;

            Vector3 origin = transform.position + Vector3.up;
            Vector3 direction = target.transform.position + Vector3.up - origin;
            return Projectile.TryGetFirstHit(new Ray(origin, direction.normalized), direction.magnitude,
                Physics.DefaultRaycastLayers, transform, out RaycastHit hit) &&
                hit.collider.GetComponentInParent<Health>() == target;
        }

        private void OnDied(Health value)
        {
            Phase = MeleePhase.Dead;
            PauseMovement();
        }

        private void OnTargetDied(Health value)
        {
            Phase = MeleePhase.Idle;
            PauseMovement();
        }

        private void PauseMovement()
        {
            if (!ownsPause)
            {
                trackerWasEnabled = tracker.enabled;
                ownsPause = true;
            }

            tracker.enabled = false;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
        }

        private void ResumeMovement()
        {
            if (!ownsPause || health.IsDead || target == null || target.IsDead)
                return;

            ownsPause = false;
            tracker.enabled = trackerWasEnabled;
            if (agent.enabled && agent.isOnNavMesh)
                agent.isStopped = false;
        }
    }
}
