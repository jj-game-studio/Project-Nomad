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
        private bool lob;
        private int damage;
        private System.Action<Vector3> impact;
        private bool reflected, resolved;
        private Material meteorMaterial;
        public bool Parryable => !lob && damage > 0 && !reflected && !resolved;
        public Health Target => target;
        public bool Reflected => reflected;

        public static BossFireball Launch(Transform parent, Transform owner, Health target, Vector3 start,
            Vector3 goal, float seconds, bool lob, System.Action<Vector3> impact = null, int damage = 25)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = lob ? "Lobbed fireball" : "Three way fireball";
            root.transform.SetParent(parent, true);
            root.transform.position = start;
            if ((goal - start).sqrMagnitude > 0.001f) root.transform.rotation = Quaternion.LookRotation(goal - start);
            root.transform.localScale = Vector3.one * (lob ? 0.8f : 1.1f);
            root.GetComponent<Collider>().enabled = false;
            Destroy(root.GetComponent<Collider>());
            root.GetComponent<Renderer>().sharedMaterial = WeaponVfx.Glow;
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", new Color(1, 0.32f, 0.035f));
            root.GetComponent<Renderer>().SetPropertyBlock(tint);
            var ball = root.AddComponent<BossFireball>();
            ball.owner = owner;
            ball.target = target;
            ball.start = start;
            ball.goal = goal;
            ball.duration = seconds;
            ball.lob = lob;
            ball.impact = impact;
            ball.damage = damage;
            if (!lob && damage > 0) ball.CreateMeteorTrail();
            ParticleSystem flames = WeaponVfx.CreateParticles(root.transform, "Fire trail", WeaponVfx.Glow);
            var main = flames.main;
            main.startLifetime = 0.45f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.65f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, 0.3f, 0.02f), new Color(1, 0.85f, 0.2f));
            var emission = flames.emission;
            emission.enabled = true;
            emission.rateOverTime = 45;
            WeaponVfx.Shrink(flames);
            WeaponVfx.Fade(flames, 0);
            return ball;
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
            if (!lob && damage > 0)
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
                    if (hit.collider.GetComponentInParent<Health>() == target) target.TryApplyDamage(damage);
                    Explode(hit.point);
                    return;
                }
            }
            transform.position = next;
            if (progress >= 1) Explode(goal);
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
            var trail = GetComponent<TrailRenderer>();
            if (trail != null) trail.startColor = new Color(0.3f, 1.2f, 2);
            elapsed = 0;
            duration = Mathf.Max(0.12f, Vector3.Distance(start, goal) / 22);
            damage = 140;
            impact = null;
            return true;
        }

        private void CreateMeteorTrail()
        {
            meteorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            meteorMaterial.color = new Color(0.12f, 0.045f, 0.02f);
            meteorMaterial.EnableKeyword("_EMISSION");
            meteorMaterial.SetColor("_EmissionColor", new Color(1.3f, 0.16f, 0.01f));
            GetComponent<Renderer>().sharedMaterial = meteorMaterial;
            GetComponent<Renderer>().SetPropertyBlock(null);

            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = WeaponVfx.Beam;
            trail.time = 0.65f;
            trail.startWidth = 1.15f;
            trail.endWidth = 0;
            trail.startColor = new Color(2, 0.65f, 0.04f);
            trail.endColor = new Color(0.6f, 0.03f, 0, 0);
            trail.minVertexDistance = 0.08f;

            var embers = WeaponVfx.CreateParticles(transform, "Meteor embers", WeaponVfx.Glow);
            var main = embers.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.65f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.3f);
            main.startSpeed = 1.8f;
            main.startColor = new Color(1.6f, 0.45f, 0.02f);
            var emission = embers.emission;
            emission.enabled = true;
            emission.rateOverTime = 85;
            WeaponVfx.Shrink(embers);
        }

        private void OnDestroy() { if (meteorMaterial != null) Destroy(meteorMaterial); }

        private void Explode(Vector3 point)
        {
            impact?.Invoke(point);
            WeaponVfx.Burst(point, new WeaponVfxStyle(new Color(1, 0.8f, 0.2f), new Color(1, 0.25f, 0.03f), 0.8f));
            Destroy(gameObject);
        }
    }
}
