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

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Nomad.Combat.Health>();
            feedback = GetComponent<MonsterHitFeedback>();
            attack = GetComponent<MonsterMeleeAttack>();
        }

        private void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            float speed = agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            string state = health.IsDead ? "Death_A" : feedback.IsFlashing ? "Hit_A" :
                speed > 2.5f ? "Running_A" : speed > 0.1f ? "Walking_A" : "Idle_A";
            if (!health.IsDead && attack != null && attack.Phase == MeleePhase.Windup &&
                animator.HasState(0, Animator.StringToHash("Base Layer.Attack_A")))
                state = "Attack_A";
            if (!animator.HasState(0, Animator.StringToHash("Base Layer." + state))) state = "Idle_A";
            if (state == current) return;
            int hash = Animator.StringToHash("Base Layer." + state);
            if (!animator.HasState(0, hash)) return;
            animator.applyRootMotion = false;
            animator.CrossFadeInFixedTime(hash, 0.08f, 0);
            current = state;
        }
    }
}
