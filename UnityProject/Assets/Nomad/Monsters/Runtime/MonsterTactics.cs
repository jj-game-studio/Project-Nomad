using Nomad.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters
{
    public enum MonsterSkill { None, Charge, Area }

    public sealed class MonsterTactics : MonoBehaviour
    {
        public float ChargeMin { get; private set; }
        public float ChargeMax { get; private set; }
        public float ChargeSpeed { get; private set; }
        public float ChargeCooldown { get; private set; }
        public float AreaRadius { get; private set; }
        public float AreaCooldown { get; private set; }
        public float Windup { get; private set; }
        public float Recovery { get; private set; }
        public float PlannedDistance { get; private set; }
        public float ChargeWidth => 1.4f;
        public bool IsDashing { get; private set; }
        public int ChargesStarted { get; private set; }
        public int AreasReleased { get; private set; }
        public float ChargeRemaining => Mathf.Max(0f, nextCharge - Time.time);
        public float AreaRemaining => Mathf.Max(0f, nextArea - Time.time);
        private int skillDamage;
        private float nextCharge;
        private float nextArea;
        private NavMeshAgent agent;
        private Health health;
        private Health target;
        private Vector3 direction;
        private float remaining;
        private float elapsed;

        public void Configure(Health victim, float min, float max, float speed, float chargeCooldown,
            float radius, float areaCooldown, float windup, float recovery, int damage)
        {
            target = victim;
            ChargeMin = Mathf.Max(2f, min);
            ChargeMax = Mathf.Max(ChargeMin, max);
            ChargeSpeed = Mathf.Max(1f, speed);
            ChargeCooldown = Mathf.Max(1f, chargeCooldown);
            AreaRadius = Mathf.Max(1f, radius);
            AreaCooldown = Mathf.Max(1f, areaCooldown);
            Windup = Mathf.Max(0.2f, windup);
            Recovery = Mathf.Max(0.1f, recovery);
            skillDamage = Mathf.Max(1, damage);
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
        }

        public MonsterSkill Choose(float distance, float basicRange, int attacks)
        {
            if (!enabled || target == null || target.IsDead || health.IsDead) return MonsterSkill.None;
            if (distance >= ChargeMin && distance <= ChargeMax && Time.time >= nextCharge) return MonsterSkill.Charge;
            if (distance <= AreaRadius && Time.time >= nextArea && (distance > basicRange || attacks % 3 == 2))
                return MonsterSkill.Area;
            return MonsterSkill.None;
        }

        public void Prepare(MonsterSkill skill, Vector3 aim)
        {
            PlannedDistance = Mathf.Min(ChargeMax, Vector3.ProjectOnPlane(aim - transform.position, Vector3.up).magnitude);
            if (skill == MonsterSkill.Charge) nextCharge = Time.time + ChargeCooldown;
            else if (skill == MonsterSkill.Area) nextArea = Time.time + AreaCooldown;
        }

        public bool StartCharge(Vector3 forward)
        {
            if (health.IsDead || target == null || target.IsDead || !agent.enabled || !agent.isOnNavMesh) return false;
            direction = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            remaining = PlannedDistance;
            elapsed = 0f;
            IsDashing = true;
            ChargesStarted++;
            return true;
        }

        public bool TickCharge()
        {
            if (!IsDashing || health.IsDead || target == null || target.IsDead || !agent.enabled || !agent.isOnNavMesh)
            { Stop(); return true; }
            elapsed += Time.deltaTime;
            float step = Mathf.Min(remaining, ChargeSpeed * Time.deltaTime);
            Vector3 start = agent.nextPosition;
            Vector3 destination = start + direction * step;
            bool stop = false;
            if (NavMesh.Raycast(start, destination, out NavMeshHit edge, agent.areaMask))
            {
                step = Mathf.Max(0f, Vector3.Distance(start, edge.position) - 0.08f);
                stop = true;
            }
            CapsuleCollider body = GetComponent<CapsuleCollider>();
            Vector3 bottom = transform.position + Vector3.up * (body.radius + 0.08f);
            Vector3 top = transform.position + Vector3.up * (body.height - body.radius);
            RaycastHit[] hits = Physics.CapsuleCastAll(bottom, top, body.radius, direction,
                step + 0.06f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                step = Mathf.Max(0f, hit.distance - 0.06f);
                if (hit.collider.GetComponentInParent<Health>() == target) target.TryApplyDamage(skillDamage);
                stop = true;
                break;
            }
            if (step > 0f) agent.Move(direction * step);
            remaining -= step;
            if (stop || remaining <= 0.05f || elapsed > PlannedDistance / ChargeSpeed + 0.4f)
            { Stop(); return true; }
            return false;
        }

        public void ReleaseArea()
        {
            if (health.IsDead || target == null || target.IsDead) return;
            AreasReleased++;
            Vector3 offset = target.transform.position - transform.position;
            if (offset.magnitude <= AreaRadius)
            {
                Vector3 origin = transform.position + Vector3.up;
                Vector3 ray = target.transform.position + Vector3.up - origin;
                if (Projectile.TryGetFirstHit(new Ray(origin, ray.normalized), ray.magnitude,
                    Physics.DefaultRaycastLayers, transform, out RaycastHit hit) && hit.collider.GetComponentInParent<Health>() == target)
                    target.TryApplyDamage(skillDamage);
            }
            var pulse = new GameObject("Skill impact pulse");
            pulse.transform.SetParent(transform, false);
            LineRenderer warning = System.Array.Find(GetComponentsInChildren<LineRenderer>(), line => line.name == "Attack Outline");
            if (warning != null) pulse.AddComponent<MonsterSkillPulse>().Configure(warning.sharedMaterial, AreaRadius);
            else Destroy(pulse);
        }

        public void Stop() => IsDashing = false;
        private void OnDisable() => Stop();
    }
}
