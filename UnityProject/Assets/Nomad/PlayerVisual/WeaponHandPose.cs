using Nomad.Inventory;
using UnityEngine;

namespace Nomad.PlayerVisual
{
    // Humanoid IK로 활을 쥔 손·시전하는 손을 맞춥니다. 위치 이동과 공격 판정은 변경하지 않습니다.
    public sealed class WeaponHandPose : MonoBehaviour
    {
        public PlayerSwordAnimation Owner { private get; set; }
        private Animator animator;
        private float staffCastBlend;

        private void Awake() => animator = GetComponent<Animator>();

        private void Update()
        {
            float target = Owner != null && Owner.HoldingStaff && Owner.IsSwinging ? 1 : 0;
            staffCastBlend = Mathf.MoveTowards(staffCastBlend, target, Time.deltaTime * 8f);
        }

        private void OnAnimatorIK(int layer)
        {
            if ((layer != 0 && layer != 1) || Owner == null) return;
            bool staff = Owner.HoldingStaff;
            WeaponAttackMode mode = Owner.AnimatedMode;
            bool posing = layer == 1 && Owner.IsSwinging && (mode == WeaponAttackMode.Bow || mode == WeaponAttackMode.Ranged);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, staff ? 1 : 0);
            if (staff) { PoseStaff(); return; }
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, posing ? 1 : 0);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, posing ? 1 : 0);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, posing ? 0.8f : 0);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, posing ? 0.8f : 0);
            if (!posing) return;

            Vector3 origin = Owner.transform.position;
            Quaternion facing = Owner.AttackFacing;
            Vector3 left = mode == WeaponAttackMode.Bow ? new Vector3(-0.12f, 1.38f, 0.52f) : new Vector3(-0.04f, 1.3f, 0.45f);
            Vector3 right = mode == WeaponAttackMode.Bow ? new Vector3(0.18f, 1.43f, 0.1f) : new Vector3(0.17f, 1.35f, 0.44f);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, origin + facing * left);
            animator.SetIKPosition(AvatarIKGoal.RightHand, origin + facing * right);
            Vector3 leftElbow = mode == WeaponAttackMode.Bow ? new Vector3(-0.28f, 1.2f, 0.24f) : new Vector3(-0.28f, 1.17f, -0.06f);
            Vector3 rightElbow = mode == WeaponAttackMode.Bow ? new Vector3(0.48f, 1.38f, -0.2f) : new Vector3(0.36f, 1.1f, 0.1f);
            animator.SetIKHintPosition(AvatarIKHint.LeftElbow, origin + facing * leftElbow);
            animator.SetIKHintPosition(AvatarIKHint.RightElbow, origin + facing * rightElbow);
        }

        private void PoseStaff()
        {
            Vector3 origin = Owner.transform.position;
            Quaternion facing = Owner.IsSwinging ? Owner.AttackFacing : Owner.transform.rotation;
            Vector3 hand = Vector3.Lerp(new Vector3(0.28f, 1f, 0.12f), new Vector3(0.24f, 1.22f, 0.4f), staffCastBlend);
            hand.y += Mathf.Sin(Time.time * 4f) * 0.01f * (1f - staffCastBlend);
            Vector3 elbow = Vector3.Lerp(new Vector3(0.4f, 1.1f, -0.08f), new Vector3(0.36f, 1.1f, 0.1f), staffCastBlend);

            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKPosition(AvatarIKGoal.RightHand, origin + facing * hand);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0.85f);
            animator.SetIKHintPosition(AvatarIKHint.RightElbow, origin + facing * elbow);
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            animator.SetIKRotation(AvatarIKGoal.RightHand, Owner.StaffGripRotation(right.rotation,
                animator.GetIKRotation(AvatarIKGoal.RightHand), staffCastBlend));

            // 대기·걷기에서는 보조 팔의 원래 움직임을 살리고, 시전 중에만 몸 옆으로 내립니다.
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, staffCastBlend);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, origin + facing * new Vector3(-0.28f, 0.95f, 0.08f));
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, staffCastBlend * 0.8f);
            animator.SetIKHintPosition(AvatarIKHint.LeftElbow, origin + facing * new Vector3(-0.28f, 1.17f, -0.06f));
        }
    }
}
