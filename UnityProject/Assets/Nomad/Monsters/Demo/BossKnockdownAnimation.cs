using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed class BossKnockdownAnimation : MonoBehaviour
    {
        private Animator animator;
        private RuntimeAnimatorController original;
        private RuntimeAnimatorController controller;
        public AnimationClip Clip { get; private set; }

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            Clip = FindFirstObjectByType<BossEncounterContent>()?.Knockdown ?? Resources.Load<AnimationClip>("BossKnockdown");
            if (animator == null || Clip == null || animator.runtimeAnimatorController == null) return;
            original = animator.runtimeAnimatorController;
            controller = Resources.Load<RuntimeAnimatorController>("BossCombat");
            if (controller == null) return;
            animator.runtimeAnimatorController = controller;
        }

        private void OnDestroy()
        {
            if (animator != null && animator.runtimeAnimatorController == controller) animator.runtimeAnimatorController = original;
        }
    }
}
