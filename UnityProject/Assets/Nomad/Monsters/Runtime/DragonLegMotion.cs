using System;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters
{
    // 좌우가 서로 다른 본 축을 직접 돌리지 않고 원래 다리 길이·굽힘 방향으로 발 위치를 풉니다.
    [DefaultExecutionOrder(350)]
    public sealed class DragonLegMotion : MonoBehaviour
    {
        private Transform[] joints;
        private Quaternion[] rest;
        private Vector3[] restPositions;
        private Vector3[] restScales;
        private Leg[] legs;
        private NavMeshAgent agent;
        private Nomad.Combat.Health health;
        private float phase;
        private float gait;
        public float Gait => gait;

        private sealed class Leg
        {
            public Transform upper, lower, foot;
            public Vector3 bend;
            public float phase;
        }

        private void Awake()
        {
            Transform[] all = transform.Find("Body").GetComponentsInChildren<Transform>();
            joints = Array.FindAll(all, t => t.name.StartsWith("leg_front_") || t.name.StartsWith("leg_back_"));
            rest = Array.ConvertAll(joints, t => t.localRotation);
            restPositions = Array.ConvertAll(joints, t => t.localPosition);
            restScales = Array.ConvertAll(joints, t => t.localScale);
            legs = new Leg[4];
            string[] names = { "leg_front_", "leg_back_" };
            for (int index = 0; index < legs.Length; index++)
            {
                string prefix = names[index / 2];
                string side = index % 2 == 0 ? ".L" : ".R";
                var leg = new Leg
                {
                    upper = Array.Find(joints, t => t.name == prefix + "upper" + side),
                    lower = Array.Find(joints, t => t.name == prefix + "lower" + side),
                    foot = Array.Find(joints, t => t.name == prefix + "foot" + side),
                    phase = (index == 0 || index == 3) ? 0 : Mathf.PI
                };
                if (leg.upper == null || leg.lower == null || leg.foot == null) continue;
                Vector3 bend = Vector3.ProjectOnPlane(leg.lower.position - leg.upper.position,
                    leg.foot.position - leg.upper.position).normalized;
                leg.bend = transform.InverseTransformDirection(bend.sqrMagnitude > 0.01f ? bend : transform.forward);
                legs[index] = leg;
            }
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Nomad.Combat.Health>();
        }

        private void LateUpdate()
        {
            if (health.IsDead) return;
            float speed = agent.enabled && agent.isOnNavMesh ? Vector3.ProjectOnPlane(agent.velocity, Vector3.up).magnitude : 0f;
            if (GetComponent<MonsterTactics>()?.IsDashing == true) speed = 6f;
            gait = Mathf.MoveTowards(gait, Mathf.Clamp01(speed / 2f), Time.deltaTime * 5f);
            phase = Mathf.Repeat(phase + Mathf.Min(speed, 6f) * Time.deltaTime * 1.9f, Mathf.PI * 2);
            for (int i = 0; i < joints.Length; i++)
            {
                joints[i].localRotation = rest[i];
                joints[i].localPosition = restPositions[i];
                joints[i].localScale = restScales[i];
            }
            foreach (Leg leg in legs)
            {
                if (leg == null) continue;
                float cycle = phase + leg.phase;
                Quaternion footRotation = leg.foot.rotation;
                Vector3 target = leg.foot.position + transform.forward * (Mathf.Cos(cycle) * 0.55f * gait) +
                    Vector3.up * (Mathf.Max(0, Mathf.Sin(cycle)) * 0.18f * gait);
                PlaceFoot(leg, target);
                leg.foot.rotation = footRotation;
            }
        }

        private void PlaceFoot(Leg leg, Vector3 target)
        {
            Vector3 root = leg.upper.position;
            float upperLength = Vector3.Distance(root, leg.lower.position);
            float lowerLength = Vector3.Distance(leg.lower.position, leg.foot.position);
            if (upperLength < 0.001f || lowerLength < 0.001f) return;
            Vector3 offset = target - root;
            float distance = Mathf.Clamp(offset.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.001f,
                upperLength + lowerLength - 0.001f);
            Vector3 direction = offset.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(transform.TransformDirection(leg.bend), direction).normalized;
            float along = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2 * distance);
            Vector3 knee = root + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - along * along));

            leg.upper.rotation = Quaternion.FromToRotation(leg.lower.position - root, knee - root) * leg.upper.rotation;
            target = root + direction * distance;
            leg.lower.rotation = Quaternion.FromToRotation(leg.foot.position - leg.lower.position,
                target - leg.lower.position) * leg.lower.rotation;
        }
    }
}
