using System;
using Nomad.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters
{
    // 원본 리그의 본을 움직이는 임시 절차적 모션입니다. 정식 애니메이션 클립을 대체할 연결 지점입니다.
    [DefaultExecutionOrder(300)]
    public sealed class GolemMotion : MonoBehaviour
    {
        private Transform[] bones;
        private Quaternion[] rest;
        private Transform model;
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private NavMeshAgent agent;
        private MonsterMeleeAttack attack;
        private Health health;
        private float phase;
        private float death;
        private float gait;
        private Transform[] hands;
        private MonsterTactics tactics;

        private void Awake()
        {
            model = transform.Find("Body");
            basePosition = model.localPosition;
            baseRotation = model.localRotation;
            string[] names = { "upper_arm.L", "upper_arm.R", "forearm.L", "forearm.R", "thigh.L", "thigh.R", "shin.L", "shin.R" };
            Transform[] all = model.GetComponentsInChildren<Transform>();
            bones = Array.ConvertAll(names, name => Array.Find(all, bone => bone.name == name));
            hands = new[] { Array.Find(all, bone => bone.name == "hand.L"), Array.Find(all, bone => bone.name == "hand.R") };
            rest = Array.ConvertAll(bones, bone => bone != null ? bone.localRotation : Quaternion.identity);
            agent = GetComponent<NavMeshAgent>();
            attack = GetComponent<MonsterMeleeAttack>();
            health = GetComponent<Health>();
            tactics = GetComponent<MonsterTactics>();
            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator != null) animator.enabled = false;
            PoseArms(0f, 1f);
        }

        private void LateUpdate()
        {
            float speed = agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            if (tactics != null && tactics.IsDashing) speed = 6f;
            gait = Mathf.MoveTowards(gait, health.IsDead ? 0f : Mathf.Clamp01(speed / 2f), Time.deltaTime * 3f);
            phase += Time.deltaTime * (speed > 0.1f ? speed * 1.8f : 1f);
            if (health.IsDead) death = Mathf.MoveTowards(death, 1f, Time.deltaTime * 1.5f);
            float swing = Mathf.Sin(phase) * gait * 30f;
            float lift = attack.Phase == MeleePhase.Windup ? Mathf.Sin(attack.WindupProgress * Mathf.PI * 0.5f) : 0f;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null || i < 4) continue;
                float side = i % 2 == 0 ? 1f : -1f;
                Vector3 angle = i < 2 ? new Vector3(swing * side - lift * 110f, 0f, side * 65f) :
                    i < 4 ? new Vector3(-15f - lift * 45f, 0f, 0f) : i < 6 ? new Vector3(-swing * side, 0f, 0f) :
                    new Vector3(Mathf.Max(0f, swing * side) * 0.8f, 0f, 0f);
                bones[i].localRotation = Quaternion.Slerp(bones[i].localRotation,
                    rest[i] * Quaternion.Euler(angle), 1f - Mathf.Exp(-Time.deltaTime * 12f));
            }
            if (!health.IsDead) PoseArms(lift, 1f - Mathf.Exp(-Time.deltaTime * 14f));
            model.localPosition = basePosition + Vector3.up * (Mathf.Sin(phase * 2f) * 0.035f * (1f - death) - death * 0.4f);
            model.localRotation = baseRotation * Quaternion.Euler(death * 80f + (tactics != null && tactics.IsDashing ? 12f : gait * 4f),
                0f, Mathf.Sin(phase) * 3f * gait * (1f - death));
        }

        private void PoseArms(float lift, float blend)
        {
            // FBX 본의 로컬 축 대신 캐릭터 앞쪽의 손 목표점을 사용해 뒷짐 자세를 방지합니다.
            for (int side = 0; side < 2; side++)
            {
                if (bones[side] == null) continue;
                Vector3 shoulder = bones[side].position;
                float sign = Mathf.Sign(Vector3.Dot(shoulder - transform.position, transform.right));
                Vector3 neutral = shoulder - Vector3.up * 0.75f + transform.forward * 0.38f;
                Vector3 raised = shoulder + Vector3.up * 0.35f + transform.forward * 0.6f;
                Vector3 goal = Vector3.Lerp(neutral, raised, lift) + transform.forward * (Mathf.Sin(phase) * gait * sign * 0.12f);
                if (attack.Phase == MeleePhase.Windup && !attack.IsSkillAttack)
                {
                    float punch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, attack.WindupProgress));
                    bool striking = side == attack.AttackSequence % 2;
                    goal = shoulder + transform.forward * (striking ? 0.25f + punch * 0.9f : 0.3f) - Vector3.up * 0.2f;
                }
                else if (attack.Skill == MonsterSkill.Area && attack.Phase == MeleePhase.Windup)
                    goal = Vector3.Lerp(raised + Vector3.up * 0.15f,
                        shoulder + transform.forward * 0.8f - Vector3.up * 0.6f,
                        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1f, attack.WindupProgress)));
                MonsterWeaponMotion.SolveArm(bones[side], bones[side + 2], hands[side], goal,
                    transform.right * sign - Vector3.up * 0.3f, blend);
            }
        }
    }
}
