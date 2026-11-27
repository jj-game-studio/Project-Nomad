using Nomad.Combat;
using Nomad.Equipment;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    [DefaultExecutionOrder(200)]
    public sealed class BossFireball : MonoBehaviour
    {
        private Transform owner;
        private Health target;
        private Vector3 start, goal;
        private float duration, elapsed;
        private bool lob, reflected, resolved;
        private int damage;
        public float Radius { get; private set; } = 0.4f;
        public bool SignalControlled { get; set; }
        private System.Action<Vector3> impact;
        private ParticleSystem core, flame;
        public bool Parryable => !lob && damage > 0 && !reflected && !resolved;
        public Health Target => target;
        public bool Reflected => reflected;

        public static BossFireball Launch(Transform parent, Transform owner, Health target, Vector3 start,
            Vector3 goal, float seconds, bool lob, System.Action<Vector3> impact = null, int damage = 25, float radius = 0.4f)
        {
            var root = new GameObject(lob ? "Lobbed flame" : "Radial flame projectile");
            root.transform.SetParent(parent, true);
            root.transform.SetPositionAndRotation(start, (goal - start).sqrMagnitude > 0.001f ? Quaternion.LookRotation(goal - start) : Quaternion.identity);
            var ball = root.AddComponent<BossFireball>();
            ball.owner = owner;
            ball.target = target;
            ball.start = start;
            ball.goal = goal;
            ball.duration = Mathf.Max(0.01f, seconds);
            ball.lob = lob;
            ball.impact = impact;
            ball.damage = damage;
            ball.Radius = radius;
            ball.BuildFlame();
            return ball;
        }

        private void BuildFlame()
        {
            core = WeaponVfx.CreateParticles(transform, "Living flame core", WeaponVfx.Glow, ParticleSystemSimulationSpace.Local);
            var main = core.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(Radius * 1.2f, Radius * 2.1f);
            main.startColor = Radius > 1 ? new Color(1.5f, 0.55f, 0.04f, 0.7f) : new Color(2.8f, 1.5f, 0.2f);
            var shape = core.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Radius * 0.35f;
            var emission = core.emission;
            emission.enabled = true;
            emission.rateOverTime = Radius > 1 ? 18 : 35;

            flame = WeaponVfx.CreateParticles(transform, "Turbulent flame", WeaponVfx.Glow);
            main = flame.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(Radius * 1.5f, Radius * 2.6f);
            main.startColor = Radius > 1
                ? new ParticleSystem.MinMaxGradient(new Color(1.2f, 0.08f, 0.005f, 0.45f), new Color(1.5f, 0.4f, 0.02f, 0.6f))
                : new ParticleSystem.MinMaxGradient(new Color(1.8f, 0.2f, 0.01f), new Color(2, 0.8f, 0.03f));
            var noise = flame.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 2;
            noise.scrollSpeed = 3;
            emission = flame.emission;
            emission.enabled = true;
            emission.rateOverTime = Radius > 1 ? 40 : 65;
            WeaponVfx.Shrink(flame);
            WeaponVfx.Fade(flame, 0);

            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = WeaponVfx.Beam;
            trail.time = 0.45f;
            trail.startWidth = Radius * 2;
            trail.endWidth = 0;
            trail.startColor = Radius > 1 ? new Color(1.2f, 0.22f, 0.015f, 0.55f) : new Color(2, 0.5f, 0.03f);
            trail.endColor = new Color(0.8f, 0.02f, 0, 0);
            trail.minVertexDistance = 0.06f;
        }

        private void Update()
        {
            if (target == null || target.IsDead) { Destroy(gameObject); return; }
            if (resolved) return;
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            Vector3 next = Vector3.Lerp(start, goal, progress);
            if (lob) next += Vector3.up * Mathf.Sin(progress * Mathf.PI) * 4;
            Vector3 delta = next - transform.position;
            Collider targetCollider = target.GetComponent<Collider>();
            if (SignalControlled && targetCollider != null && Vector3.Distance(targetCollider.ClosestPoint(next), next) <= Radius)
            {
                owner.GetComponent<DragonBossCombat>().HitRadialWave(damage);
                resolved = true;
                Explode(next);
                return;
            }
            if (!lob && damage > 0 && delta.sqrMagnitude > 0)
            {
                RaycastHit[] hits = Physics.SphereCastAll(transform.position, Radius, delta.normalized, delta.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider.transform.IsChildOf(owner) || hit.collider.GetComponentInParent<BossBreakableRock>() != null ||
                        (SignalControlled && hit.collider.GetComponentInParent<Health>() != target)) continue;
                    var parry = hit.collider.GetComponentInParent<PlayerBossParry>();
                    if (parry != null && parry.TryReflect(this, transform.position)) return;
                    resolved = true;
                    enabled = false;
                    if (hit.collider.GetComponentInParent<Health>() == target)
                    {
                        if (SignalControlled) owner.GetComponent<DragonBossCombat>().HitRadialWave(damage);
                        else if (reflected) target.TryApplyCombatImpact(damage, false, CombatImpactSource.ParriedFireball);
                        else target.TryApplyDamage(damage);
                    }
                    Explode(hit.point);
                    return;
                }
            }
            transform.position = next;
            if (progress >= 1) { resolved = true; Explode(goal); }
        }

        public bool Reflect(Transform defender)
        {
            if (!Parryable || owner == null || defender == null) return false;
            Health attacker = owner.GetComponent<Health>();
            if (attacker == null || attacker.IsDead) return false;
            reflected = true;
            SignalControlled = false;
            Radius = 0.6f;
            target = attacker;
            owner = defender;
            start = transform.position;
            goal = attacker.transform.position + Vector3.up * 1.8f;
            transform.rotation = Quaternion.LookRotation(goal - start);
            GetComponent<TrailRenderer>().startColor = new Color(0.3f, 1.2f, 2);
            var main = core.main;
            main.startColor = new Color(1, 2, 2.8f);
            main = flame.main;
            main.startColor = new Color(0.25f, 0.8f, 2);
            elapsed = 0;
            duration = Mathf.Max(0.12f, Vector3.Distance(start, goal) / 22);
            damage = 140;
            impact = null;
            return true;
        }

        public void Disperse() { resolved = true; gameObject.SetActive(false); Destroy(gameObject); }

        private void Explode(Vector3 point)
        {
            impact?.Invoke(point);
            WeaponVfx.Burst(point, new WeaponVfxStyle(new Color(1, 0.8f, 0.2f), new Color(1, 0.25f, 0.03f), 0.8f));
            Destroy(gameObject);
        }
    }
}
