using System.Collections.Generic;
using Nomad.Combat;
using Nomad.Equipment;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    [DefaultExecutionOrder(450)]
    public sealed partial class PlayerBossReaction : MonoBehaviour
    {
        private Health health;
        private CharacterController controller;
        private Transform visual;
        private Animator avatar;
        private readonly Dictionary<Transform, Quaternion> boneRest = new();
        private Vector3 originalPosition, pushDirection;
        private Quaternion originalRotation;
        private float start, end, delay, pushDistance, pushed;
        private float fallHeight = 0.35f;
        private float pushSeconds = 0.6f;
        private bool posed;
        public bool Locked => isActiveAndEnabled && health != null && !health.IsDead && Time.time < end;
        public float KnockdownWeight { get; private set; }
        public bool HasVisual => visual != null;

        public static PlayerBossReaction Play(Health player, Vector3 source, float distance, float seconds, float delay = 0, float pushSeconds = 0.6f)
        {
            var reaction = player.GetComponent<PlayerBossReaction>() ?? player.gameObject.AddComponent<PlayerBossReaction>();
            reaction.Restore();
            reaction.start = Time.time;
            reaction.end = Time.time + seconds;
            reaction.delay = delay;
            reaction.pushSeconds = Mathf.Max(0.1f, pushSeconds);
            reaction.KnockdownWeight = 0;
            reaction.pushDistance = distance;
            reaction.pushed = 0;
            reaction.pushDirection = Vector3.ProjectOnPlane(player.transform.position - source, Vector3.up).normalized;
            if (reaction.pushDirection.sqrMagnitude < 0.01f) reaction.pushDirection = -player.transform.forward;
            player.GetComponent<PlayerEquipmentCombat>()?.InterruptForParry();
            return reaction;
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            controller = GetComponent<CharacterController>();
            avatar = GetComponentInChildren<Animator>();
            if (avatar != null && avatar.transform != transform) visual = avatar.transform;
            if (visual == null) CreateFallbackVisual();
        }

        private void Update()
        {
            Restore();
            if (!Locked) { KnockdownWeight = 0; return; }
            float age = Time.time - start - delay;
            if (age <= 0) return;
            float reach = pushDistance * (1 - Mathf.Pow(1 - Mathf.Clamp01(age / pushSeconds), 3));
            float move = reach - pushed;
            pushed = reach;
            if (controller != null && controller.enabled)
            {
                // CharacterController로 충돌을 처리하므로 벽이나 바위 뒤로 순간 이동하지 않습니다.
                int steps = Mathf.Max(1, Mathf.CeilToInt(move / 0.25f));
                for (int step = 0; step < steps; step++) controller.Move(pushDirection * (move / steps) + Vector3.down * 0.01f);
            }
        }

        private void LateUpdate()
        {
            if (!Locked) return;
            float age = Time.time - start - delay;
            if (age <= 0) return;
            float falling = Mathf.SmoothStep(0, 1, age / 0.3f);
            float standing = Mathf.SmoothStep(0, 1, (end - Time.time) / 0.85f);
            KnockdownWeight = Mathf.Min(falling, standing);
            if (visual == null) return;
            RefreshFallbackTint();
            originalRotation = visual.localRotation;
            originalPosition = visual.localPosition;
            visual.localRotation *= Quaternion.Euler(-76 * KnockdownWeight, 0, 7 * KnockdownWeight);
            visual.localPosition += Vector3.up * (fallHeight * KnockdownWeight);
            Bend(HumanBodyBones.Spine, 18, transform.right);
            Bend(HumanBodyBones.LeftUpperLeg, -42, transform.right);
            Bend(HumanBodyBones.RightUpperLeg, -48, transform.right);
            Bend(HumanBodyBones.LeftLowerLeg, 75, transform.right);
            Bend(HumanBodyBones.RightLowerLeg, 85, transform.right);
            Bend(HumanBodyBones.LeftUpperArm, -30, transform.forward);
            Bend(HumanBodyBones.RightUpperArm, 30, transform.forward);
            Bend(HumanBodyBones.LeftLowerArm, -25, transform.right);
            Bend(HumanBodyBones.RightLowerArm, -25, transform.right);
            posed = true;
        }

        private void Bend(HumanBodyBones boneId, float degrees, Vector3 axis)
        {
            if (avatar == null || !avatar.isHuman) return;
            Transform bone = avatar.GetBoneTransform(boneId);
            if (bone == null) return;
            boneRest[bone] = bone.localRotation;
            bone.rotation = Quaternion.AngleAxis(degrees * KnockdownWeight, axis) * bone.rotation;
        }

        private void Restore()
        {
            if (posed && visual != null)
            {
                visual.localRotation = originalRotation;
                visual.localPosition = originalPosition;
            }
            foreach (var pair in boneRest) if (pair.Key != null) pair.Key.localRotation = pair.Value;
            boneRest.Clear();
            posed = false;
        }

        public void Cancel() { end = 0; KnockdownWeight = 0; Restore(); }
        private void OnDisable() => Cancel();
    }
}
