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
        private System.Action<Vector3> impact;
        private ParticleSystem core, flame;
        public bool Parryable => !lob && damage > 0 && !reflected && !resolved;
        public Health Target => target;
        public bool Reflected => reflected;

        public static BossFireball Launch(Transform parent, Transform owner, Health target, Vector3 start,
            Vector3 goal, float seconds, bool lob, System.Action<Vector3> impact = null, int damage = 25)
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
            ball.BuildFlame();
            return ball;
        }

        private void BuildFlame()
        {
            core = WeaponVfx.CreateParticles(transform, "Living flame core", WeaponVfx.Glow, ParticleSystemSimulationSpace.Local);
            var main = core.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startColor = new Color(2.8f, 1.5f, 0.2f);
            var shape = core.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.14f;
            var emission = core.emission;
            emission.enabled = true;
            emission.rateOverTime = 35;

            flame = WeaponVfx.CreateParticles(transform, "Turbulent flame", WeaponVfx.Glow);
            main = flame.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.8f, 0.2f, 0.01f), new Color(2, 0.8f, 0.03f));
            var noise = flame.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 2;
            noise.scrollSpeed = 3;
            emission = flame.emission;
            emission.enabled = true;
            emission.rateOverTime = 65;
            WeaponVfx.Shrink(flame);
            WeaponVfx.Fade(flame, 0);

            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = WeaponVfx.Beam;
            trail.time = 0.45f;
            trail.startWidth = 0.75f;
            trail.endWidth = 0;
            trail.startColor = new Color(2, 0.5f, 0.03f);
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
            if (!lob && damage > 0 && delta.sqrMagnitude > 0)
            {
                RaycastHit[] hits = Physics.SphereCastAll(transform.position, 0.4f, delta.normalized, delta.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider.transform.IsChildOf(owner)) continue;
                    var parry = hit.collider.GetComponentInParent<PlayerBossParry>();
                    if (parry != null && parry.TryReflect(this, transform.position)) return;
                    resolved = true;
                    enabled = false;
                    if (hit.collider.GetComponentInParent<Health>() == target)
                    {
                        if (reflected) target.TryApplyCombatImpact(damage, false, CombatImpactSource.ParriedFireball);
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

        private void Explode(Vector3 point)
        {
            impact?.Invoke(point);
            WeaponVfx.Burst(point, new WeaponVfxStyle(new Color(1, 0.8f, 0.2f), new Color(1, 0.25f, 0.03f), 0.8f));
            Destroy(gameObject);
        }
    }
}
