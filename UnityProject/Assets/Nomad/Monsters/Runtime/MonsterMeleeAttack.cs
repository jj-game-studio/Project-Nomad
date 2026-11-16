using Nomad.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters
{
    public enum MeleePhase { Idle, Windup, Recovery, Dead }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Health), typeof(MonsterTracker))]
    public sealed class MonsterMeleeAttack : MonoBehaviour
    {
        [SerializeField] private Health target;
        [SerializeField, Min(1)] private int damage = 20;
        [SerializeField, Min(0.1f)] private float attackRange = 1.8f;
        [SerializeField, Min(0.05f)] private float windupSeconds = 0.5f;
        [SerializeField, Min(0.05f)] private float recoverySeconds = 1f;
        [SerializeField, Min(0.1f)] private float attackSpeed = 1f;

        [SerializeField] private bool enableHeavyAttack;
        [SerializeField, Min(1)] private int heavyDamage = 40;
        [SerializeField, Min(0.1f)] private float heavyRange = 2.6f;
        [SerializeField, Range(1f, 360f)] private float heavyAngle = 90f;
        [SerializeField, Min(0.05f)] private float heavyWindupSeconds = 1.2f;
        [SerializeField, Min(0.05f)] private float heavyRecoverySeconds = 1.8f;

        private Health health;
        private MonsterTracker tracker;
        private NavMeshAgent agent;
        private float phaseElapsed;
        private bool ownsPause;
        private bool trackerWasEnabled;
        private bool resumeTracking;
        private int previousHealth;
        private int attacksStarted;
        private Vector3 strikeForward;

        public MeleePhase Phase { get; private set; }
        public bool IsHeavyAttack { get; private set; }
        public float HeavyAttackRange => heavyRange;
        public float HeavyAttackAngle => heavyAngle;
        public Vector3 AttackDirection => strikeForward;
        public float CurrentAttackRange => IsHeavyAttack ? heavyRange : attackRange;
        public float CurrentAttackAngle => IsHeavyAttack ? heavyAngle : 360f;
        public float WindupProgress { get; private set; }
        public void Configure(Health victim, bool heavy, int lightDamage, int strongDamage,
            float range, float strongRange, float speed)
        {
            if (target != null)
                target.Died -= OnTargetDied;

            target = victim;
            if (isActiveAndEnabled && target != null)
                target.Died += OnTargetDied;

            enableHeavyAttack = heavy;
            damage = Mathf.Max(1, lightDamage);
            heavyDamage = Mathf.Max(1, strongDamage);
            attackRange = Mathf.Max(0.1f, range);
            heavyRange = Mathf.Max(attackRange, strongRange);
            AttackSpeed = speed;
        }

        public float AttackSpeed
        {
            get => attackSpeed;
            set => attackSpeed = Mathf.Max(0.1f, value);
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            tracker = GetComponent<MonsterTracker>();
            agent = GetComponent<NavMeshAgent>();
        }

        private void OnEnable()
        {
            Phase = MeleePhase.Idle;
            IsHeavyAttack = false;
            attacksStarted = 0;
            WindupProgress = 0f;
            previousHealth = health.CurrentHealth;
            resumeTracking = false;
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
            WindupProgress = 0f;
            IsHeavyAttack = false;
            ResumeMovement();
        }

        private void Update()
        {
            bool wasHit = health.CurrentHealth < previousHealth;
            previousHealth = health.CurrentHealth;
            if (health.IsDead)
            {
                OnDied(health);
                return;
            }

            if (target == null || !target.isActiveAndEnabled || target.IsDead)
            {
                Phase = MeleePhase.Idle;
                WindupProgress = 0f;
                PauseMovement();
                return;
            }

            if (wasHit)
            {
                if (ownsPause)
                    resumeTracking = true;
                else
                    tracker.ReactToDamage();
            }

            if (Time.deltaTime <= 0f)
                return;

            if (Phase == MeleePhase.Windup)
            {
                // 판정과 표시가 같은 진행도를 사용합니다. 100% 프레임을 보여준 뒤 타격합니다.
                if (WindupProgress < 1f)
                {
                    phaseElapsed += Time.deltaTime * Mathf.Max(0.1f, attackSpeed);
                    float duration = IsHeavyAttack ? heavyWindupSeconds : windupSeconds;
                    WindupProgress = Mathf.Clamp01(phaseElapsed / duration);
                    return;
                }

                // 준비가 끝난 시점에 거리와 벽을 다시 확인하며 피해는 한 번만 전달합니다.
                if (CanStrike(IsHeavyAttack))
                    target.TryApplyDamage(IsHeavyAttack ? heavyDamage : damage);

                if (health.IsDead || target.IsDead)
                    return;

                Phase = MeleePhase.Recovery;
                WindupProgress = 0f;
                phaseElapsed = 0f;
            }
            else if (Phase == MeleePhase.Recovery)
            {
                phaseElapsed += Time.deltaTime * Mathf.Max(0.1f, attackSpeed);
                if (phaseElapsed < (IsHeavyAttack ? heavyRecoverySeconds : recoverySeconds))
                    return;

                Phase = MeleePhase.Idle;
                IsHeavyAttack = false;
                ResumeMovement();
            }
            else if (Phase == MeleePhase.Idle)
            {
                ResumeMovement();
                bool nextIsHeavy = enableHeavyAttack && attacksStarted % 3 == 2;
                if (tracker.IsTracking && tracker.HasLineOfSight && FaceTarget() && CanStrike(nextIsHeavy))
                {
                    IsHeavyAttack = nextIsHeavy;
                    attacksStarted++;
                    strikeForward = transform.forward;
                    PauseMovement();
                    Phase = MeleePhase.Windup;
                    WindupProgress = 0f;
                    phaseElapsed = 0f;
                }
            }
        }

        private bool CanStrike(bool heavy)
        {
            float range = heavy ? heavyRange : attackRange;
            if (target == null || !target.isActiveAndEnabled || target.IsDead ||
                (target.transform.position - transform.position).sqrMagnitude > range * range)
                return false;

            // 강공격은 준비 시작 방향을 고정하여 옆으로 피할 수 있게 합니다.
            Vector3 facing = Phase == MeleePhase.Windup ? strikeForward : transform.forward;
            Vector3 offset = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up);
            if (heavy && Vector3.Angle(facing, offset) > heavyAngle * 0.5f)
                return false;

            Vector3 origin = transform.position + Vector3.up;
            Vector3 direction = target.transform.position + Vector3.up - origin;
            return Projectile.TryGetFirstHit(new Ray(origin, direction.normalized), direction.magnitude,
                Physics.DefaultRaycastLayers, transform, out RaycastHit hit) &&
                hit.collider.GetComponentInParent<Health>() == target;
        }

        private bool FaceTarget()
        {
            Vector3 direction = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up);
            if (direction.sqrMagnitude < 0.0001f)
                return true;

            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction), agent.angularSpeed * Time.deltaTime);
            return Vector3.Angle(transform.forward, direction) <= 5f;
        }

        private void OnDied(Health value)
        {
            Phase = MeleePhase.Dead;
            WindupProgress = 0f;
            IsHeavyAttack = false;
            PauseMovement();
        }

        private void OnTargetDied(Health value)
        {
            Phase = MeleePhase.Idle;
            WindupProgress = 0f;
            IsHeavyAttack = false;
            PauseMovement();
        }

        private void PauseMovement()
        {
            if (!ownsPause)
            {
                trackerWasEnabled = tracker.enabled;
                resumeTracking = tracker.IsTracking;
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
            if (trackerWasEnabled && resumeTracking)
                tracker.ResumeCombatTracking();
            resumeTracking = false;
        }
    }
}
