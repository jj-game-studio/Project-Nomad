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

        private void Awake()
        {
            model = transform.Find("Body");
            basePosition = model.localPosition;
            baseRotation = model.localRotation;
            string[] names = { "upper_arm.L", "upper_arm.R", "forearm.L", "forearm.R", "thigh.L", "thigh.R" };
            Transform[] all = model.GetComponentsInChildren<Transform>();
            bones = Array.ConvertAll(names, name => Array.Find(all, bone => bone.name == name));
            rest = Array.ConvertAll(bones, bone => bone != null ? bone.localRotation : Quaternion.identity);
            agent = GetComponent<NavMeshAgent>();
            attack = GetComponent<MonsterMeleeAttack>();
            health = GetComponent<Health>();
            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator != null) animator.enabled = false;
        }

        private void LateUpdate()
        {
            float speed = agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            phase += Time.deltaTime * (speed > 0.1f ? 5f : 1.5f);
            if (health.IsDead) death = Mathf.MoveTowards(death, 1f, Time.deltaTime * 1.5f);
            float swing = Mathf.Sin(phase) * Mathf.Clamp01(speed) * 24f;
            float lift = attack.Phase == MeleePhase.Windup ? Mathf.Sin(attack.WindupProgress * Mathf.PI * 0.5f) : 0f;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                float side = i % 2 == 0 ? 1f : -1f;
                Vector3 angle = i < 2 ? new Vector3(swing * side - lift * 110f, 0f, side * 65f) :
                    i < 4 ? new Vector3(-15f - lift * 45f, 0f, 0f) : new Vector3(-swing * side, 0f, 0f);
                bones[i].localRotation = rest[i] * Quaternion.Euler(angle);
            }
            model.localPosition = basePosition + Vector3.up * (Mathf.Sin(phase * 2f) * 0.035f - death * 0.4f);
            model.localRotation = baseRotation * Quaternion.Euler(death * 80f, 0f, Mathf.Sin(phase) * 1.5f * (1f - death));
        }
    }
}
