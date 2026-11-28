using System.Collections.Generic;
using Nomad.Combat;
using UnityEngine;

namespace Nomad.Equipment
{
    public sealed class SkillProjectile : MonoBehaviour
    {
        private Transform owner;
        private int damage;
        private bool explosive;
        private bool critical;
        private float distance;
        private readonly HashSet<Health> struck = new();

        public static void Launch(Transform owner, Vector3 position, Vector3 direction, int damage, bool explosive, bool critical = false)
        {
            var root = new GameObject(explosive ? "Enhanced magic bolt" : "Piercing arrow");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, owner.gameObject.scene);
            root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            var shot = root.AddComponent<SkillProjectile>();
            shot.owner = owner;
            shot.damage = damage;
            shot.explosive = explosive;
            shot.critical = critical;
            var trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = WeaponVfx.Beam;
            trail.time = 0.18f;
            trail.startWidth = explosive ? 0.35f : 0.18f;
            trail.endWidth = 0;
            trail.startColor = explosive ? new Color(0.4f, 0.9f, 1) : new Color(1, 0.85f, 0.3f);
            trail.endColor = new Color(1, 1, 1, 0);
        }

        private void FixedUpdate()
        {
            if (owner == null || !owner.gameObject.activeInHierarchy || owner.GetComponent<Health>().IsDead) { Destroy(gameObject); return; }
            float step = 28 * Time.fixedDeltaTime;
            RaycastHit[] hits = Physics.SphereCastAll(transform.position, explosive ? 0.3f : 0.16f, transform.forward,
                step, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(owner)) continue;
                Health target = hit.collider.GetComponentInParent<Health>();
                if (target != null && struck.Contains(target)) continue;
                if (explosive)
                {
                    DamageArea(hit.point, transform.forward, 3.5f, 360, damage, owner, critical);
                    WeaponVfx.Burst(hit.point, new WeaponVfxStyle(Color.white, new Color(0.3f, 0.8f, 1), 2));
                    Destroy(gameObject);
                    return;
                }
                if (target != null) { struck.Add(target); target.TryApplyCombatImpact(damage, critical, CombatImpactSource.Skill); }
                else { Destroy(gameObject); return; }
            }
            transform.position += transform.forward * step;
            distance += step;
            if (distance >= 24) Destroy(gameObject);
        }

        public static void DamageArea(Vector3 center, Vector3 forward, float radius, float angle, int damage, Transform owner, bool critical = false)
        {
            var hit = new HashSet<Health>();
            foreach (Collider collider in Physics.OverlapSphere(center + Vector3.up, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                Health target = collider.GetComponentInParent<Health>();
                if (target == null || collider.transform.IsChildOf(owner) || !hit.Add(target)) continue;
                Vector3 delta = target.transform.position - center;
                if (angle < 359 && Vector3.Angle(forward, Vector3.ProjectOnPlane(delta, Vector3.up)) > angle * 0.5f) continue;
                Vector3 ray = target.transform.position + Vector3.up - (center + Vector3.up);
                if (Projectile.TryGetFirstHit(new Ray(center + Vector3.up, ray.normalized), ray.magnitude,
                    Physics.DefaultRaycastLayers, owner, out RaycastHit wall) && wall.collider.GetComponentInParent<Health>() != target) continue;
                target.TryApplyCombatImpact(damage, critical, CombatImpactSource.Skill);
            }
        }
    }
}
