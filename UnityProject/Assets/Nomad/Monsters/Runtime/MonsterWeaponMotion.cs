using System;
using UnityEngine;

namespace Nomad.Monsters
{
    [DefaultExecutionOrder(340)]
    public sealed class MonsterWeaponMotion : MonoBehaviour
    {
        private Transform[] arms;
        private Transform[] hands;
        private MonsterMeleeAttack attack;
        private MonsterIdentity identity;
        private Nomad.Combat.Health health;
        private Transform weapon;
        private Quaternion weaponRest;
        public float PoseWeight { get; private set; }

        private void Awake()
        {
            attack = GetComponent<MonsterMeleeAttack>();
            identity = GetComponent<MonsterIdentity>();
            health = GetComponent<Nomad.Combat.Health>();
            Transform[] all = transform.Find("Body").GetComponentsInChildren<Transform>();
            string[] names = { "upperarm.r", "lowerarm.r", "upperarm.l", "lowerarm.l" };
            arms = Array.ConvertAll(names, name => Array.Find(all, t => t.name == name));
            hands = new[] { Array.Find(all, t => t.name == "hand.r"), Array.Find(all, t => t.name == "hand.l") };
            weapon = Array.Find(all, t => t.name == "Ranged weapon");
            if (weapon != null) weaponRest = weapon.localRotation;
        }

        private void LateUpdate()
        {
            bool preparing = attack.Phase == MeleePhase.Windup;
            bool recovering = attack.Phase == MeleePhase.Recovery;
            float desired = !health.IsDead && (preparing || recovering) ? 1f : 0f;
            PoseWeight = Mathf.MoveTowards(PoseWeight, desired, Time.deltaTime * 8f);
            if (PoseWeight <= 0f)
            {
                if (weapon != null) weapon.localRotation = weaponRest;
                return;
            }
            float progress = attack.WindupProgress;
            float recovery = attack.RecoveryProgress;
            bool sword = identity.Weapon == MonsterWeaponStyle.Sword;
            // 검은 준비 마지막 구간에 실제 타격선을 통과하고, 석궁은 조준 자세에서 발사 후 반동을 줍니다.
            float slash = sword ? (preparing ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1f, progress)) :
                1f - Mathf.SmoothStep(0f, 1f, recovery)) : 0f;
            float recoil = recovering ? Mathf.Exp(-recovery * 18f) : 0f;
            if (arms[0] == null || arms[2] == null) return;
            Vector3 chest = (arms[0].position + arms[2].position) * 0.5f;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            Vector3 rightGoal, leftGoal;
            Quaternion facing = transform.rotation;
            if (sword)
            {
                rightGoal = Vector3.Lerp(chest + right * 0.32f + Vector3.up * 0.4f,
                    chest - right * 0.28f + forward * 0.45f - Vector3.up * 0.2f, slash);
                leftGoal = chest - right * 0.3f - Vector3.up * 0.2f;
                facing = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, forward, slash));
            }
            else if (identity.Weapon == MonsterWeaponStyle.Crossbow)
            {
                rightGoal = chest + right * 0.12f + forward * (0.43f - recoil * 0.07f) - Vector3.up * 0.12f;
                leftGoal = chest - right * 0.08f + forward * 0.53f - Vector3.up * 0.15f;
            }
            else
            {
                rightGoal = chest + right * 0.28f + Vector3.up * (progress * 0.2f);
                leftGoal = chest - right * 0.15f + forward * 0.48f;
                facing *= Quaternion.Euler(-progress * 12f, 0f, 0f);
            }
            Reach(arms[0], arms[1], hands[0], rightGoal, right - Vector3.up);
            Reach(arms[2], arms[3], hands[1], leftGoal, -right - Vector3.up);
            if (weapon != null)
                weapon.rotation = Quaternion.Slerp(weapon.parent.rotation * weaponRest, facing, PoseWeight);
        }

        private void Reach(Transform upper, Transform lower, Transform hand, Vector3 goal, Vector3 pole)
            => SolveArm(upper, lower, hand, goal, pole, PoseWeight);

        public static void SolveArm(Transform upper, Transform lower, Transform hand, Vector3 goal, Vector3 pole, float blend)
        {
            if (upper == null || lower == null || hand == null) return;
            float a = Vector3.Distance(upper.position, lower.position);
            float b = Vector3.Distance(lower.position, hand.position);
            if (a < 0.001f || b < 0.001f) return;
            Vector3 delta = goal - upper.position;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            Vector3 direction = delta.normalized;
            float cosine = Mathf.Clamp((a * a + distance * distance - b * b) / (2f * a * distance), -1f, 1f);
            Vector3 bend = Vector3.ProjectOnPlane(pole, direction).normalized;
            Vector3 elbow = upper.position + direction * (a * cosine) + bend * (a * Mathf.Sqrt(1f - cosine * cosine));
            Quaternion start = upper.rotation;
            Quaternion desired = Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position) * start;
            upper.rotation = Quaternion.Slerp(start, desired, blend);
            start = lower.rotation;
            desired = Quaternion.FromToRotation(hand.position - lower.position, goal - lower.position) * start;
            lower.rotation = Quaternion.Slerp(start, desired, blend);
        }
    }
}
