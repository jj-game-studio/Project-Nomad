using System;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters
{
    // 원본의 다리 회전 트랙 대신 작은 범위의 사족보행을 적용합니다. 머리/날개/꼬리는 원본 애니메이션을 유지합니다.
    [DefaultExecutionOrder(350)]
    public sealed class DragonLegMotion : MonoBehaviour
    {
        private Transform[] joints;
        private Quaternion[] rest;
        private Quaternion[] pose;
        private NavMeshAgent agent;
        private Nomad.Combat.Health health;
        private float phase;
        private float gait;
        public float Gait => gait;

        private void Awake()
        {
            Transform[] all = transform.Find("Body").GetComponentsInChildren<Transform>();
            joints = Array.FindAll(all, t => t.name.StartsWith("leg_front_") || t.name.StartsWith("leg_back_"));
            rest = Array.ConvertAll(joints, t => t.localRotation);
            pose = (Quaternion[])rest.Clone();
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Nomad.Combat.Health>();
        }

        private void LateUpdate()
        {
            float speed = !health.IsDead && agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            gait = Mathf.MoveTowards(gait, Mathf.Clamp01(speed / 2f), Time.deltaTime * 3f);
            phase += speed * Time.deltaTime * 1.65f;
            float blend = 1f - Mathf.Exp(-Time.deltaTime * 14f);
            for (int i = 0; i < joints.Length; i++)
            {
                string name = joints[i].name;
                float diagonal = name.Contains("front") == name.EndsWith(".L") ? 0f : Mathf.PI;
                float step = Mathf.Sin(phase + diagonal);
                float angle = name.Contains("upper") || name.Contains("hip") || name.Contains("shoulder") ? step * 16f :
                    name.Contains("lower") ? Mathf.Max(0f, step) * 24f : -step * 8f;
                Quaternion desired = rest[i] * Quaternion.Euler(angle * gait, 0f, 0f);
                // 매 프레임 Animator가 쓴 과도한 회전을 버리고 기준 포즈 안에서만 보간합니다.
                pose[i] = Quaternion.Slerp(pose[i], desired, blend);
                joints[i].localRotation = pose[i];
            }
        }
    }
}
