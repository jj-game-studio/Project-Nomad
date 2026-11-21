using Nomad.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters
{
    public enum MeleePhase { Idle, Windup, Recovery, Dead, Charging }

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
        private MonsterRangedAttack ranged;
        private Vector3 lockedAim;
        private MonsterTactics tactics;
        private float basicAngle = 360f;
        public void ConfigureBasicAngle(float angle) => basicAngle = Mathf.Clamp(angle, 1f, 360f);
        public MonsterSkill Skill { get; private set; }
        public int AttackSequence => attacksStarted;

        public MeleePhase Phase { get; private set; }
        public bool IsHeavyAttack { get; private set; }
        public bool IsRangedAttack { get; private set; }
        public bool IsSkillAttack => Skill != MonsterSkill.None || IsHeavyAttack || (IsRangedAttack &&
            (GetComponent<MonsterIdentity>()?.Rank ?? MonsterRank.Normal) != MonsterRank.Normal);
        public float HeavyAttackRange => heavyRange;
        public float HeavyAttackAngle => heavyAngle;
        public Vector3 AttackDirection => strikeForward;
        public float CurrentAttackRange => Skill == MonsterSkill.Charge ? tactics.PlannedDistance :
            Skill == MonsterSkill.Area ? tactics.AreaRadius : IsRangedAttack ? ranged.Range : IsHeavyAttack ? heavyRange : attackRange;
        public float CurrentAttackAngle => Skill == MonsterSkill.Area ? 360f : IsRangedAttack ? 12f : IsHeavyAttack ? heavyAngle : basicAngle;
        public float ChargeWidth => tactics != null ? tactics.ChargeWidth : 1.4f;
        public float WindupProgress { get; private set; }
        public float RecoveryProgress => Phase == MeleePhase.Recovery ? Mathf.Clamp01(phaseElapsed /
            RecoveryDuration) : 0f;
        private float RecoveryDuration => Skill != MonsterSkill.None ? tactics.Recovery : IsRangedAttack ? ranged.Recovery :
            IsHeavyAttack ? heavyRecoverySeconds : recoverySeconds;
        public float WindupDuration => Skill != MonsterSkill.None ? tactics.Windup :
            IsRangedAttack ? ranged.Windup : IsHeavyAttack ? heavyWindupSeconds : windupSeconds;

        public void ConfigureTempo(float windup, float recovery, float skillWindup, float skillRecovery)
        {
            windupSeconds = Mathf.Max(0.1f, windup);
            recoverySeconds = Mathf.Max(0.1f, recovery);
            heavyWindupSeconds = Mathf.Max(0.2f, skillWindup);
            heavyRecoverySeconds = Mathf.Max(0.1f, skillRecovery);
        }
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
            ranged = GetComponent<MonsterRangedAttack>();
            tactics = GetComponent<MonsterTactics>();
        }

        private void OnEnable()
        {
            Phase = MeleePhase.Idle;
            IsHeavyAttack = false;
            IsRangedAttack = false;
            Skill = MonsterSkill.None;
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
            tactics?.Stop();
            Skill = MonsterSkill.None;
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
                tactics?.Stop();
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

            if (Phase == MeleePhase.Charging)
            {
                if (tactics.TickCharge()) BeginRecovery();
                return;
            }

            if (Phase == MeleePhase.Windup)
            {
                // 판정과 표시가 같은 진행도를 사용합니다. 100% 프레임을 보여준 뒤 타격합니다.
                if (WindupProgress < 1f)
                {
                    phaseElapsed += Time.deltaTime * Mathf.Max(0.1f, attackSpeed);
                    WindupProgress = Mathf.Clamp01(phaseElapsed / WindupDuration);
                    return;
                }

                // 준비가 끝난 시점에 거리와 벽을 다시 확인하며 피해는 한 번만 전달합니다.
                if (Skill == MonsterSkill.Charge)
                {
                    if (tactics.StartCharge(strikeForward)) Phase = MeleePhase.Charging;
                    else BeginRecovery();
                    WindupProgress = 0f;
                    return;
                }
                if (Skill == MonsterSkill.Area)
                    tactics.ReleaseArea();
                else if (IsRangedAttack)
                    ranged.Release(target, lockedAim);
                else if (CanStrike(IsHeavyAttack))
                    target.TryApplyDamage(IsHeavyAttack ? heavyDamage : damage);

                if (health.IsDead || target.IsDead)
                    return;

                BeginRecovery();
            }
            else if (Phase == MeleePhase.Recovery)
            {
                phaseElapsed += Time.deltaTime * Mathf.Max(0.1f, attackSpeed);
                if (phaseElapsed < RecoveryDuration)
                    return;

                Phase = MeleePhase.Idle;
                IsHeavyAttack = false;
                IsRangedAttack = false;
                Skill = MonsterSkill.None;
                ResumeMovement();
            }
            else if (Phase == MeleePhase.Idle)
            {
                ResumeMovement();
                bool nextIsHeavy = enableHeavyAttack && attacksStarted % 3 == 2;
                float distance = Vector3.Distance(target.transform.position, transform.position);
                MonsterSkill nextSkill = tactics != null ? tactics.Choose(distance, attackRange, attacksStarted) : MonsterSkill.None;
                bool nextIsRanged = ranged != null && ranged.CanBegin(target, nextIsHeavy ? heavyRange : attackRange);
                if (tracker.IsTracking && tracker.HasLineOfSight && FaceTarget() &&
                    (nextSkill != MonsterSkill.None || nextIsRanged || CanStrike(nextIsHeavy)))
                {
                    Skill = nextSkill;
                    IsRangedAttack = nextSkill == MonsterSkill.None && nextIsRanged;
                    IsHeavyAttack = nextSkill == MonsterSkill.None && !nextIsRanged && nextIsHeavy;
                    attacksStarted++;
                    strikeForward = transform.forward;
                    lockedAim = target.transform.position + Vector3.up;
                    if (nextSkill == MonsterSkill.Charge)
                    {
                        strikeForward = Vector3.ProjectOnPlane(lockedAim - transform.position, Vector3.up).normalized;
                        transform.rotation = Quaternion.LookRotation(strikeForward);
                    }
                    if (nextSkill != MonsterSkill.None) tactics.Prepare(nextSkill, lockedAim);
                    PauseMovement();
                    Phase = MeleePhase.Windup;
                    WindupProgress = 0f;
                    phaseElapsed = 0f;
                }
            }
        }

        private void BeginRecovery()
        {
            Phase = MeleePhase.Recovery;
            WindupProgress = 0f;
            phaseElapsed = 0f;
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
            if (Vector3.Angle(facing, offset) > (heavy ? heavyAngle : basicAngle) * 0.5f)
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
            IsRangedAttack = false;
            tactics?.Stop();
            Skill = MonsterSkill.None;
            PauseMovement();
        }

        private void OnTargetDied(Health value)
        {
            tactics?.Stop();
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
