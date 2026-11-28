using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nomad.Game.Editor
{
    internal static class BossKnockdownPoseAuthoring
    {
        public static void Write(GameObject prefab, AnimationClip clip)
        {
            GameObject model = Object.Instantiate(prefab);
            model.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Animator animator = model.GetComponentInChildren<Animator>();
                Transform[] bones = animator.GetComponentsInChildren<Transform>();
                var rotations = bones.ToDictionary(bone => bone, bone => bone.localRotation);
                var feet = bones.Where(bone => bone.name.Contains("leg_") && bone.name.Contains("foot"))
                    .ToDictionary(bone => bone, bone => (bone.position, bone.rotation));
                Transform root = bones.First(bone => bone.name == "base");
                Vector3 resting = root.localPosition;
                root.position -= Vector3.up * 0.75f;
                root.rotation = Quaternion.AngleAxis(-8, model.transform.forward) * root.rotation;

                // 발은 원래 바닥에 둔 채 무릎을 바깥으로 접어 가슴부터 내려앉습니다.
                foreach (var pair in feet)
                {
                    Transform foot = pair.Key;
                    Transform lower = foot.parent;
                    Transform upper = lower.parent;
                    Vector3 outward = model.transform.right * (foot.name.EndsWith(".L") ? -1 : 1);
                    BendLeg(upper, lower, foot, pair.Value.position, outward);
                    foot.rotation = pair.Value.rotation;
                }
                Transform neck = bones.First(bone => bone.name == "neck1");
                Transform jaw = bones.First(bone => bone.name == "jaw_upper");
                Vector3 head = jaw.position - neck.position;
                Vector3 lowered = Vector3.ProjectOnPlane(head, Vector3.up) - Vector3.up * head.magnitude * 0.2f;
                neck.rotation = Quaternion.FromToRotation(head, lowered) * neck.rotation;

                clip.ClearCurves();
                foreach (Transform bone in bones)
                {
                    Quaternion rest = rotations[bone];
                    Quaternion down = bone.localRotation;
                    if (Quaternion.Angle(rest, down) < 0.01f) continue;
                    string binding = AnimationUtility.CalculateTransformPath(bone, animator.transform);
                    Quaternion brace = Quaternion.Slerp(rest, down, 0.2f);
                    for (int axis = 0; axis < 4; axis++)
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(binding, typeof(Transform), "m_LocalRotation." + "xyzw"[axis]),
                            new AnimationCurve(new Keyframe(0, rest[axis]), new Keyframe(0.18f, brace[axis]),
                                new Keyframe(0.65f, down[axis]), new Keyframe(3.5f, down[axis]),
                                new Keyframe(4.1f, brace[axis]), new Keyframe(4.8f, rest[axis])));
                }
                string rootPath = AnimationUtility.CalculateTransformPath(root, animator.transform);
                for (int axis = 0; axis < 3; axis++)
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(rootPath, typeof(Transform), "m_LocalPosition." + "xyz"[axis]),
                        new AnimationCurve(new Keyframe(0, resting[axis]), new Keyframe(0.65f, root.localPosition[axis]),
                            new Keyframe(3.5f, root.localPosition[axis]), new Keyframe(4.8f, resting[axis])));
                clip.EnsureQuaternionContinuity();
            }
            finally { Object.DestroyImmediate(model); }
        }

        private static void BendLeg(Transform upper, Transform lower, Transform foot, Vector3 target, Vector3 outward)
        {
            float a = Vector3.Distance(upper.position, lower.position);
            float b = Vector3.Distance(lower.position, foot.position);
            Vector3 heading = (target - upper.position).normalized;
            float distance = Mathf.Clamp(Vector3.Distance(target, upper.position), Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            float along = (a * a - b * b + distance * distance) / (2 * distance);
            float bend = Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            Vector3 knee = upper.position + heading * along + Vector3.ProjectOnPlane(outward, heading).normalized * bend;
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, knee - upper.position) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(foot.position - lower.position, target - lower.position) * lower.rotation;
        }
    }
}
