using UnityEngine;

namespace Nomad.Interaction
{
    // 거리/벽 판정은 InteractionPoint, 화면 표시는 머리 위, 조준은 몸 중심을 사용합니다.
    internal static class InteractionAnchors
    {
        private static Transform Head(IInteractable target)
        {
            if (target is not Component owner) return null;
            Animator animator = owner.GetComponentInChildren<Animator>();
            return animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        }

        public static Vector3 Display(IInteractable target)
        {
            Transform head = Head(target);
            return head != null ? head.position + Vector3.up * 0.25f : target.InteractionPoint;
        }

        public static Vector3 Focus(IInteractable target)
        {
            Transform head = Head(target);
            return head != null ? head.position - Vector3.up * 0.3f : target.InteractionPoint;
        }
    }
}
