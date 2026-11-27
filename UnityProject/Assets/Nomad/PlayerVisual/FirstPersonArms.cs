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
            root.localPosition = new Vector3(0, -1.5f, -0.15f);
            root.localScale *= 0.85f;

            animator = root.GetComponentInChildren<Animator>();
            RightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            LeftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            foreach (Transform bone in animator.GetComponentsInChildren<Transform>()) rest.Add(bone, bone.localRotation);
        }

        public void Pose(Transform camera, Vector3 right, Vector3 left)
        {
            foreach (var pair in rest) pair.Key.localRotation = pair.Value;
            Arm(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, RightHand,
                camera.TransformPoint(right), camera.TransformPoint(new Vector3(0.62f, -0.65f, 0.28f)),
                camera.TransformPoint(new Vector3(0.32f, -0.38f, 0.3f)));
            Arm(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, LeftHand,
                camera.TransformPoint(left), camera.TransformPoint(new Vector3(-0.62f, -0.65f, 0.28f)),
                camera.TransformPoint(new Vector3(-0.32f, -0.38f, 0.3f)));
        }

        private void Arm(HumanBodyBones upperId, HumanBodyBones lowerId, Transform hand, Vector3 target, Vector3 hint, Vector3 shoulder)
        {
            Transform upper = animator.GetBoneTransform(upperId);
            Transform lower = animator.GetBoneTransform(lowerId);
            // 어깨를 화면 밖 아래에 두어 전완이 화면 가장자리에서 손으로 이어지게 합니다.
            upper.position = shoulder;
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

        public void Grip(Transform camera, Quaternion pose, bool bow, bool staff)
        {
            RightHand.rotation = camera.rotation * pose * Quaternion.Euler(90, 0, 90);
            LeftHand.rotation = camera.rotation * (bow ? pose : Quaternion.identity) * Quaternion.Euler(90, 0, -90);
            Curl(RightHand, true);
            Curl(LeftHand, bow || !staff);
        }

        private static void Curl(Transform hand, bool closed)
        {
            if (!closed) return;
            float sign = hand == null || hand.name.Contains("_L") ? -1f : 1f;
            foreach (Transform finger in hand.GetComponentsInChildren<Transform>())
            {
                if ((finger.name.StartsWith("IndexFinger_") || finger.name.StartsWith("Finger_")) && !finger.name.Contains("04"))
                    finger.localRotation *= Quaternion.Euler(0, 0, sign * (finger.name.Contains("01") ? 65f : 50f));
                else if (finger.name.StartsWith("Thumb_")) finger.localRotation *= Quaternion.Euler(0, 0, sign * 25f);
            }
        }

    }
}
