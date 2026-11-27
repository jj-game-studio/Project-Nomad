using Nomad.Combat;
using Nomad.Monsters;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters.Demo
{
    [DefaultExecutionOrder(275)]
    public sealed class BossDeathPresentation : MonoBehaviour
    {
        public const float Seconds = 2.6f;
        public bool Playing { get; private set; }
        public float Progress => Playing ? Mathf.Clamp01((Time.time - started) / Seconds) : 0;
        private Health health;
        private Animator animator;
        private float started;

        private void Awake()
        {
            health = GetComponent<Health>();
            animator = GetComponentInChildren<Animator>(true);
        }

        private void Update()
        {
            if (!health.IsDead || animator == null) return;
            if (!Playing)
            {
                Playing = true;
                started = Time.time;
                GetComponent<MonsterAnimationDriver>().enabled = false;
                var feet = GetComponent<DragonLegMotion>();
                if (feet != null) feet.enabled = false;
                GetComponent<Collider>().enabled = false;
                var agent = GetComponent<NavMeshAgent>();
                if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            }
            animator.speed = 0;
            animator.Play("Base Layer.BossDeath", 0, Progress);
        }
    }
}
