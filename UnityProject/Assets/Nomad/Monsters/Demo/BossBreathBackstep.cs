using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters.Demo
{
    [DefaultExecutionOrder(320)]
    public sealed class BossBreathBackstep : MonoBehaviour
    {
        public const float Seconds = 0.55f;
        public bool Airborne { get; private set; }
        public int Landings { get; private set; }
        private DragonBossCombat boss;
        private BossArena arena;
        private NavMeshAgent agent;
        private CapsuleCollider capsule;
        private Transform body;
        private DragonLegMotion feet;
        private Vector3 rest, restCollider, start, goal;
        private Quaternion restRotation;
        private float since;
        private bool running;

        public void Configure(DragonBossCombat owner, BossArena room)
        {
            boss = owner; arena = room; agent = GetComponent<NavMeshAgent>();
            capsule = GetComponent<CapsuleCollider>();
            body = transform.Find("Body"); feet = GetComponent<DragonLegMotion>();
            rest = body.localPosition; restRotation = body.localRotation; restCollider = capsule.center;
        }

        public void Begin(Vector3 backward, float distance)
        {
            ResetPose();
            start = transform.position;
            distance = Mathf.Min(distance, Mathf.Max(0, arena.DistanceToBoundary(start, backward) - capsule.radius - 0.5f));
            if (NavMesh.Raycast(start, start + backward * distance, out NavMeshHit edge, agent.areaMask))
                distance = Mathf.Min(distance, Mathf.Max(0, Vector3.Distance(start, edge.position) - 0.25f));
            if (Physics.CapsuleCast(start + Vector3.up * capsule.radius, start + Vector3.up * (capsule.height - capsule.radius),
                capsule.radius, backward, out RaycastHit solid, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!solid.collider.transform.IsChildOf(transform)) distance = Mathf.Min(distance, Mathf.Max(0, solid.distance - 0.2f));
            goal = arena.GroundPoint(start + backward * distance);
            since = Time.time;
            running = true;
        }

        private void LateUpdate()
        {
            if (!running) return;
            if (boss.Pattern != DragonPattern.Breath || boss.Phase != DragonPhase.Windup)
            { ResetPose(); return; }
            float t = Mathf.Clamp01((Time.time - since) / Seconds);
            Vector3 desired = Vector3.Lerp(start, goal, Mathf.SmoothStep(0, 1, t));
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.Move(Vector3.ProjectOnPlane(desired - transform.position, Vector3.up));
                transform.position = agent.nextPosition;
            }
            float lift = Mathf.Sin(t * Mathf.PI) * 1.8f;
            body.localPosition = rest + Vector3.up * lift;
            body.localRotation = restRotation * Quaternion.Euler(-10 * Mathf.Sin(t * Mathf.PI), 0, 0);
            capsule.center = restCollider + Vector3.up * lift;
            Airborne = t < 1;
            if (feet != null) feet.enabled = !Airborne;
            if (t < 1) return;
            Landings++;
            ResetPose();
            boss.FinishBreathBackstep();
        }

        private void ResetPose()
        {
            running = false; Airborne = false;
            if (body != null) { body.localPosition = rest; body.localRotation = restRotation; }
            if (capsule != null) capsule.center = restCollider;
        }

        private void OnDisable() => ResetPose();
    }
}
