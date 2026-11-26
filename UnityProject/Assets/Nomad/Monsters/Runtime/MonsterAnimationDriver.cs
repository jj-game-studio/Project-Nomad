using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters
{
    [DefaultExecutionOrder(250)]
    public sealed class MonsterAnimationDriver : MonoBehaviour
    {
        private Animator animator;
        private NavMeshAgent agent;
        private Nomad.Combat.Health health;
        private MonsterHitFeedback feedback;
        private MonsterMeleeAttack attack;
        private string current;
        private float attackClipLength = 1f;
        private int playedAttack = -1;
        public string ActionState { get; set; }
        public float ActionSpeed { get; set; } = 1;
        public float ActionNormalizedTime { get; set; } = -1;
        public int ActionSequence { get; set; }
        private int playedAction = -1;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Nomad.Combat.Health>();
            feedback = GetComponent<MonsterHitFeedback>();
            attack = GetComponent<MonsterMeleeAttack>();
            if (animator != null && animator.runtimeAnimatorController != null)
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                    if (clip.name.ToLowerInvariant().Contains("attack")) attackClipLength = clip.length;
        }

        private void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            float speed = agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            if (GetComponent<MonsterTactics>()?.IsDashing == true) speed = 5f;
            string state = health.IsDead ? "Death_A" : feedback.IsFlashing ? "Hit_A" :
                speed > 2.5f ? "Running_A" : speed > 0.1f ? "Walking_A" : "Idle_A";
            if (!health.IsDead && attack != null && (attack.Phase == MeleePhase.Windup ||
                (attack.Phase == MeleePhase.Recovery && attack.RecoveryProgress < 0.5f)) &&
                animator.HasState(0, Animator.StringToHash("Base Layer.Attack_A")))
                state = "Attack_A";
            if (!health.IsDead && !string.IsNullOrEmpty(ActionState)) state = ActionState;
            if (!animator.HasState(0, Animator.StringToHash("Base Layer." + state))) state = "Idle_A";
            animator.speed = state == "Attack_A" ? Mathf.Clamp(attackClipLength /
                (attack.WindupDuration / attack.AttackSpeed + 0.12f), 0.5f, 6f) : 1f;
            if (!health.IsDead && ActionState != null) animator.speed = ActionSpeed;
            if (!health.IsDead && ActionState != null && ActionNormalizedTime >= 0)
            {
                animator.speed = 0;
                animator.Play(Animator.StringToHash("Base Layer." + state), 0, ActionNormalizedTime);
                current = state;
                playedAction = ActionSequence;
                return;
            }
            if (state == current && (state != "Attack_A" || playedAttack == attack.AttackSequence) && playedAction == ActionSequence) return;
            int hash = Animator.StringToHash("Base Layer." + state);
            if (!animator.HasState(0, hash)) return;
            animator.applyRootMotion = false;
            animator.CrossFadeInFixedTime(hash, state == "Attack_A" ? 0.04f : 0.12f, 0);
            if (state == "Attack_A") playedAttack = attack.AttackSequence;
            playedAction = ActionSequence;
            current = state;
        }
    }
}
