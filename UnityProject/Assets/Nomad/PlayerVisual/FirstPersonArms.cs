using System.Collections.Generic;
using UnityEngine;

namespace Nomad.PlayerVisual
{
    // 독립 팔 리그를 카메라 공간의 손 목표점에 맞춥니다. 월드 캐릭터 Animator는 변경하지 않습니다.
    internal sealed class FirstPersonArms
    {
        private readonly Transform root;
        private readonly Animator animator;
        private readonly Dictionary<Transform, Quaternion> rest = new();
        public Transform RightHand { get; }
        public Transform LeftHand { get; }

        public FirstPersonArms(Transform parent, GameObject prefab)
        {
            root = Object.Instantiate(prefab, parent, false).transform;
            root.name = "First Person Arms";
            root.localPosition = new Vector3(0, -1.45f, -0.15f);
            animator = root.GetComponentInChildren<Animator>();
            RightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            LeftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            foreach (Transform bone in animator.GetComponentsInChildren<Transform>()) rest.Add(bone, bone.localRotation);
        }

        public void Pose(Transform camera, Vector3 right, Vector3 left)
        {
            foreach (var pair in rest) pair.Key.localRotation = pair.Value;
            Arm(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, RightHand,
                camera.TransformPoint(right), camera.TransformPoint(new Vector3(0.5f, -0.55f, 0.1f)));
            Arm(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, LeftHand,
                camera.TransformPoint(left), camera.TransformPoint(new Vector3(-0.5f, -0.55f, 0.1f)));
        }

        private void Arm(HumanBodyBones upperId, HumanBodyBones lowerId, Transform hand, Vector3 target, Vector3 hint)
        {
            Transform upper = animator.GetBoneTransform(upperId);
            Transform lower = animator.GetBoneTransform(lowerId);
            float a = Vector3.Distance(upper.position, lower.position);
            float b = Vector3.Distance(lower.position, hand.position);
            Vector3 direction = (target - upper.position).normalized;
            float distance = Mathf.Clamp(Vector3.Distance(target, upper.position), Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            Vector3 bend = Vector3.ProjectOnPlane(hint - upper.position, direction).normalized;
            float cosine = Mathf.Clamp((a * a + distance * distance - b * b) / (2f * a * distance), -1f, 1f);
            Vector3 elbow = upper.position + direction * (a * cosine) + bend * (a * Mathf.Sqrt(1f - cosine * cosine));
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, target - lower.position) * lower.rotation;
        }

        public void Release() { if (root != null) Object.Destroy(root.gameObject); }
    }
}
