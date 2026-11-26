using Nomad.Combat;
using Nomad.Equipment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Monsters.Demo
{
    // 위치를 고정한 예고와 피해가 같은 도형을 사용합니다. 보스가 죽으면 소유자가 모두 정리합니다.
    [DefaultExecutionOrder(200)]
    public sealed class BossHazard : MonoBehaviour
    {
        public bool Active => elapsed >= delay;
        public float Radius { get; private set; }
        public bool EmitsFlame { get; set; } = true;
        private Health target;
        private Transform owner;
        private float delay, duration, interval, elapsed, nextHit;
        private float angle;
        private int damage;
        private Mesh mesh;
        private Transform fill;
        private ParticleSystem fire;

        public static BossHazard Create(Transform parent, Transform owner, Vector3 point, Quaternion facing, Health target,
            float radius, float angle, float delay, float duration, int damage, float interval = 0.75f)
        {
            var root = new GameObject("Boss ground attack");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(point + Vector3.up * 0.08f, facing);
            var hazard = root.AddComponent<BossHazard>();
            hazard.target = target;
            hazard.owner = owner;
            hazard.Radius = radius;
            hazard.angle = angle;
            hazard.delay = delay;
            hazard.duration = duration;
            hazard.damage = damage;
            hazard.interval = interval;
            hazard.nextHit = delay;
            hazard.BuildVisual();
            return hazard;
        }

        private void BuildVisual()
        {
            const int segments = 64;
            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];
            for (int index = 0; index <= segments; index++)
            {
                float radians = Mathf.Lerp(-angle * 0.5f, angle * 0.5f, index / (float)segments) * Mathf.Deg2Rad;
                vertices[index + 1] = new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians)) * Radius;
                if (index == segments) continue;
                triangles[index * 3] = 0;
                triangles[index * 3 + 1] = index + 1;
                triangles[index * 3 + 2] = index + 2;
            }
            mesh = new Mesh { name = "Boss attack footprint", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            var go = new GameObject("Warning fill");
            fill = go.transform;
            fill.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = WeaponVfx.Plain;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", new Color(1, 0.25f, 0.04f, 0.22f));
            renderer.SetPropertyBlock(tint);

            var outline = new GameObject("Warning boundary").AddComponent<LineRenderer>();
            outline.transform.SetParent(transform, false);
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.widthMultiplier = 0.07f;
            outline.sharedMaterial = WeaponVfx.Plain;
            outline.startColor = outline.endColor = new Color(1, 0.5f, 0.1f);
            outline.positionCount = angle >= 359 ? vertices.Length - 1 : vertices.Length;
            for (int index = 0; index < outline.positionCount; index++)
                outline.SetPosition(index, vertices[angle >= 359 ? index + 1 : index]);
        }

        private void Update()
        {
            if (target == null || target.IsDead) { Destroy(gameObject); return; }
            elapsed += Time.deltaTime;
            float progress = delay <= 0 ? 1 : Mathf.Clamp01(elapsed / delay);
            fill.localScale = new Vector3(Mathf.Lerp(0.08f, 1, progress), 1, Mathf.Lerp(0.08f, 1, progress));
            if (!Active) return;
            if (fire == null && EmitsFlame)
            {
                fire = WeaponVfx.CreateParticles(transform, "Burning embers", WeaponVfx.Glow);
                var main = fire.main;
                main.startLifetime = 0.5f;
                main.startSize = 0.25f;
                main.startColor = new Color(1, 0.3f, 0.04f);
                var shape = fire.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = Radius;
                shape.arc = angle;
                fire.transform.localRotation = Quaternion.Euler(-90, -angle * 0.5f, 0);
                var emission = fire.emission;
                emission.enabled = true;
                emission.rateOverTime = Radius * 20;
                if (angle < 359)
                {
                    // 입자도 판정과 같은 수평 원뿔 안에서만 퍼집니다.
                    shape.enabled = false;
                    emission.enabled = false;
                    main.startLifetime = 0.65f;
                    main.startSpeed = 0;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
                }
                WeaponVfx.Fade(fire, 0);
                if (angle < 359) EmitBreath(true);
            }
            if (fire != null && angle < 359) EmitBreath(false);
            if (elapsed >= nextHit)
            {
                nextHit = elapsed + interval;
                if (Contains(target.transform.position)) target.TryApplyDamage(damage);
            }
            if (elapsed >= delay + duration) Destroy(gameObject);
        }

        private void EmitBreath(bool warm)
        {
            int count = warm ? 64 : Mathf.CeilToInt(220 * Time.deltaTime);
            for (int index = 0; index < count; index++)
            {
                Vector3 heading = transform.rotation * Quaternion.Euler(0, Random.Range(-angle * 0.5f, angle * 0.5f), 0) * Vector3.forward;
                Vector3 velocity = heading * ((Radius - 1.4f) / 0.65f);
                float age = warm ? Random.Range(0, 0.6f) : 0;
                var particle = new ParticleSystem.EmitParams
                {
                    position = transform.position + Vector3.up * 1.4f + transform.forward * 1.4f + velocity * age,
                    velocity = velocity,
                    startLifetime = 0.65f - age,
                    startSize = Random.Range(0.4f, 0.9f)
                };
                fire.Emit(particle, 1);
            }
        }

        public bool Contains(Vector3 point)
        {
            Vector3 offset = point - transform.position;
            if (Mathf.Abs(offset.y) > 3 || Vector3.ProjectOnPlane(offset, Vector3.up).magnitude > Radius) return false;
            if (angle < 359 && Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(offset, Vector3.up)) > angle * 0.5f) return false;
            Vector3 ray = target.transform.position + Vector3.up - (transform.position + Vector3.up);
            return !Projectile.TryGetFirstHit(new Ray(transform.position + Vector3.up, ray.normalized), ray.magnitude,
                Physics.DefaultRaycastLayers, owner, out RaycastHit hit) || hit.collider.GetComponentInParent<Health>() == target;
        }

        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
