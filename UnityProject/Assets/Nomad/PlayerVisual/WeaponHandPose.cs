using Nomad.Inventory;
using UnityEngine;

namespace Nomad.PlayerVisual
{
    // Humanoid IK로 활을 쥔 손·시전하는 손을 맞춥니다. 위치 이동과 공격 판정은 변경하지 않습니다.
    public sealed class WeaponHandPose : MonoBehaviour
    {
        public PlayerSwordAnimation Owner { private get; set; }
        private Animator animator;

        private void Awake() => animator = GetComponent<Animator>();

        private void OnAnimatorIK(int layer)
        {
            if (layer != 1 || Owner == null) return;
            WeaponAttackMode mode = Owner.AnimatedMode;
            bool posing = Owner.IsSwinging && (mode == WeaponAttackMode.Bow || mode == WeaponAttackMode.Staff || mode == WeaponAttackMode.Ranged);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, posing ? 1 : 0);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, posing ? 1 : 0);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, posing ? 0.8f : 0);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, posing ? 0.8f : 0);
            if (!posing) return;

            Vector3 origin = Owner.transform.position;
            Quaternion facing = Owner.AttackFacing;
            Vector3 left = mode == WeaponAttackMode.Bow ? new Vector3(-0.15f, 1.38f, 0.55f) :
                mode == WeaponAttackMode.Staff ? new Vector3(-0.24f, 1f, 0.12f) : new Vector3(-0.04f, 1.3f, 0.45f);
            Vector3 right = mode == WeaponAttackMode.Bow ? new Vector3(0.14f, 1.38f, 0.04f) :
                mode == WeaponAttackMode.Staff ? new Vector3(0.24f, 1.36f, 0.52f) : new Vector3(0.17f, 1.35f, 0.44f);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, origin + facing * left);
            animator.SetIKPosition(AvatarIKGoal.RightHand, origin + facing * right);
            animator.SetIKHintPosition(AvatarIKHint.LeftElbow, origin + facing * new Vector3(-0.45f, 1.15f, 0.22f));
            animator.SetIKHintPosition(AvatarIKHint.RightElbow, origin + facing * new Vector3(0.5f, 1.35f, -0.1f));
        }
    }
}
