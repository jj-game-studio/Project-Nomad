using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters.Demo
{
    public sealed partial class DragonBossCombat
    {
        public const float MinimumChargeDistance = 32;
        public const float ChargeOvershoot = 14;
        private float dashRemaining, dashStarted;
        private bool dashHitPlayer;

        private bool TickBossCharge()
        {
            if (!agent.enabled || !agent.isOnNavMesh) return true;
            float speed = Tuning.ChargeSpeed * Tempo;
            float step = Mathf.Min(dashRemaining, speed * Time.deltaTime);
            Vector3 start = agent.nextPosition;
            bool stopped = false;
            if (NavMesh.Raycast(start, start + direction * step, out NavMeshHit edge, agent.areaMask))
            {
                step = Mathf.Max(0, Vector3.Distance(start, edge.position) - 0.08f);
                stopped = true;
            }

            CapsuleCollider body = GetComponent<CapsuleCollider>();
            Vector3 bottom = transform.position + Vector3.up * (body.radius + 0.08f);
            Vector3 top = transform.position + Vector3.up * (body.height - body.radius);
            RaycastHit[] hits = Physics.CapsuleCastAll(bottom, top, body.radius, direction, step + 0.06f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.collider.GetComponentInParent<Nomad.Combat.Health>() == target)
                {
                    if (!dashHitPlayer) { dashHitPlayer = true; target.TryApplyDamage(ScaleDamage(28)); }
                    continue;
                }
                step = Mathf.Max(0, hit.distance - 0.06f);
                stopped = true;
                break;
            }

            if (step > 0) agent.Move(direction * step);
            dashRemaining -= step;
            return stopped || dashRemaining <= 0.05f || Time.time - dashStarted > PlannedChargeDistance / speed + 0.5f;
        }
    }
}
