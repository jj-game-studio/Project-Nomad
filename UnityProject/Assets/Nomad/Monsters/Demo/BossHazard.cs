using Nomad.Combat;
using Nomad.Equipment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Monsters.Demo
{
    // 위치를 고정한 예고와 피해가 같은 도형을 사용합니다. 보스가 죽으면 소유자가 모두 정리합니다.
    [DefaultExecutionOrder(400)]
    public sealed partial class BossHazard : MonoBehaviour
    {
        public bool Active => elapsed >= delay;
        public float Radius { get; private set; }
        public bool HighlightActive { get; set; }
        public float ActiveOpacity { get; set; } = 0.68f;
        public bool EmitsFlame { get; set; } = true;
        public System.Action OnHit { get; set; }
        public bool IgnoreCover { get; set; }
        public Color WarningColor { get; set; } = new Color(1, 0.25f, 0.04f, 0.22f);
        public Transform BreathOrigin { get; set; }
        public float BreathTravelSeconds { get; set; } = 0.65f;
        public Vector3 LastBreathOrigin { get; private set; }
        public System.Func<Vector3, float> GroundHeight { get; set; }
        public float WarningSeconds { get; set; } = -1;
        public float JumpClearance { get; set; }
        public float InnerRadius { get; set; }
        public float VisualRangeMultiplier { get; set; } = 1;
        public bool HideWarningWhileActive { get; set; }
        public bool HideWarning { get; set; }
        public bool WarningVisible => fill != null && fill.gameObject.activeSelf;
        public void CancelDamage() => damage = 0;
        private Health target;
        private Transform owner;
        private float delay, duration, interval, elapsed, nextHit;
        private float angle;
        private int damage;
        private Mesh mesh;
        private Transform fill;
        private ParticleSystem fire;
        private Vector3[] fullFootprint, projected;
        private LineRenderer boundary;
        private bool boundaryProjected;
        private int boundaryStart;
        private bool shook;

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
            int rings = Mathf.Clamp(Mathf.CeilToInt(Radius / 1.5f), 1, 8);
            var vertices = new Vector3[1 + rings * (segments + 1)];
            var triangles = new int[segments * 3 + (rings - 1) * segments * 6];
            int triangle = 0;
            for (int ring = 1; ring <= rings; ring++)
            for (int index = 0; index <= segments; index++)
            {
                float radians = Mathf.Lerp(-angle * 0.5f, angle * 0.5f, index / (float)segments) * Mathf.Deg2Rad;
                int vertex = 1 + (ring - 1) * (segments + 1) + index;
                vertices[vertex] = new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians)) * (Radius * ring / rings);
                if (index == segments) continue;
                if (ring == 1)
                {
                    triangles[triangle++] = 0; triangles[triangle++] = vertex; triangles[triangle++] = vertex + 1;
                    continue;
                }
                int inner = vertex - segments - 1;
                triangles[triangle++] = inner; triangles[triangle++] = vertex; triangles[triangle++] = vertex + 1;
                triangles[triangle++] = inner; triangles[triangle++] = vertex + 1; triangles[triangle++] = inner + 1;
            }
            boundaryStart = 1 + (rings - 1) * (segments + 1);
            mesh = new Mesh { name = "Boss attack footprint", vertices = vertices, triangles = triangles };
            fullFootprint = vertices;
            projected = new Vector3[vertices.Length];
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
            boundary = outline;
            outline.transform.SetParent(transform, false);
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.widthMultiplier = 0.07f;
            outline.sharedMaterial = WeaponVfx.Plain;
            outline.startColor = outline.endColor = new Color(1, 0.5f, 0.1f);
            outline.positionCount = angle >= 359 ? segments : segments + 2;
            for (int index = 0; index < outline.positionCount; index++)
                outline.SetPosition(index, vertices[angle >= 359 ? boundaryStart + index : index == 0 ? 0 : boundaryStart + index - 1]);
        }

        private void Update()
        {
            if (target == null || target.IsDead) { Destroy(gameObject); return; }
            if (!innerBuilt && InnerRadius > 0) BuildBreathFootprint();
            elapsed += Time.deltaTime;
            if (!Active)
            {
                var tint = new MaterialPropertyBlock();
                tint.SetColor("_BaseColor", WarningColor);
                fill.GetComponent<Renderer>().SetPropertyBlock(tint);
                boundary.startColor = boundary.endColor = new Color(WarningColor.r, WarningColor.g, WarningColor.b);
                if (innerBoundary != null) innerBoundary.startColor = innerBoundary.endColor = boundary.startColor;
            }
            float warning = WarningSeconds >= 0 ? WarningSeconds : delay;
            float progress = warning <= 0 ? 1 : Mathf.Clamp01(elapsed / warning);
            fill.localScale = new Vector3(Mathf.Lerp(0.08f, 1, progress), 1, Mathf.Lerp(0.08f, 1, progress));
            ProjectGround(Mathf.Lerp(0.08f, 1, progress));
            if (HideWarning) { fill.gameObject.SetActive(false); boundary.gameObject.SetActive(false); if (innerBoundary != null) innerBoundary.gameObject.SetActive(false); }
            if (!Active) return;
            if (!shook && damage > 0) { shook = true; BossCombatImpact.Pulse(Radius > 12 ? 0.18f : 0.07f); }
            if (HighlightActive)
            {
                var tint = new MaterialPropertyBlock();
                tint.SetColor("_BaseColor", new Color(2, 0.22f, 0.015f, ActiveOpacity));
                fill.GetComponent<Renderer>().sharedMaterial = WeaponVfx.Plain;
                fill.GetComponent<Renderer>().SetPropertyBlock(tint);
                boundary.widthMultiplier = 0.28f;
                boundary.startColor = boundary.endColor = new Color(2.5f, 0.4f, 0.04f);
                if (innerBoundary != null) { innerBoundary.widthMultiplier = 0.28f; innerBoundary.startColor = innerBoundary.endColor = boundary.startColor; }
            }
            if (HideWarningWhileActive) { fill.gameObject.SetActive(false); boundary.gameObject.SetActive(false); }
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
                    main.startLifetime = BreathTravelSeconds;
                    main.startSpeed = 0;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
                }
                WeaponVfx.Fade(fire, 0);
                if (angle < 359) CreateBreathLayers();
                else if (duration > 1) CreateLavaSurface();
            }
            if (elapsed >= nextHit)
            {
                nextHit = elapsed + interval;
                if (damage > 0 && Contains(target.transform.position) && target.TryApplyDamage(damage)) OnHit?.Invoke();
            }
            if (elapsed >= delay + duration) Destroy(gameObject);
        }

        private void EmitBreath(bool warm)
        {
            Vector3 origin = BreathOrigin != null ? BreathOrigin.position : transform.position + Vector3.up * 1.4f + transform.forward * 1.4f;
            LastBreathOrigin = origin;
            int count = warm ? 320 : Mathf.CeilToInt(700 * Time.deltaTime);
            // 입자의 반지름까지 안쪽으로 넣어 실제 안전 구간과 사거리 밖으로 불이 넘치지 않게 합니다.
            const float margin = 0.85f;
            float inner = InnerRadius + margin;
            float outer = Mathf.Max(inner, Radius - margin);
            float halfAngle = Mathf.Max(0, angle * 0.5f - Mathf.Asin(Mathf.Min(1, margin / inner)) * Mathf.Rad2Deg);
            for (int index = 0; index < count; index++)
            {
                Vector3 heading = transform.rotation * Quaternion.Euler(0, Random.Range(-halfAngle, halfAngle), 0) * Vector3.forward;
                Vector3 start = transform.position + heading * inner;
                start.y = Mathf.Max(transform.position.y + 0.8f, origin.y - 0.4f);
                Vector3 endpoint = transform.position + heading * outer + Vector3.up * 0.9f;
                Vector3 velocity = (endpoint - start) / BreathTravelSeconds;
                float age = warm ? Random.Range(0, BreathTravelSeconds - 0.05f) : 0;
                var particle = new ParticleSystem.EmitParams
                {
                    position = start + velocity * age,
                    velocity = velocity,
                    startLifetime = BreathTravelSeconds - age,
                    startSize = Random.Range(0.65f, 1.1f)
                };
                fire.Emit(particle, 1);
                if (core != null && index % 2 == 0) core.Emit(new ParticleSystem.EmitParams {
                    position = particle.position, velocity = velocity, startLifetime = BreathTravelSeconds - age, startSize = Random.Range(0.6f, 1.05f) }, 1);
                if (smoke != null && index % 5 == 0) smoke.Emit(new ParticleSystem.EmitParams {
                    position = particle.position, velocity = Vector3.up * 0.7f, startLifetime = 0.45f, startSize = 0.65f }, 1);
            }
        }

        private bool breathWarmed;
        private void LateUpdate()
        {
            if (fire == null || Time.deltaTime <= 0) return;
            if (lavaMaterial != null) { EmitLava(); return; }
            if (angle >= 359) return;
            if (breathLight != null) { breathLight.transform.position = BreathOrigin != null ? BreathOrigin.position : transform.position + Vector3.up; breathLight.intensity = 1.5f + Mathf.Sin(Time.time * 30) * 0.25f; }
            // 애니메이션과 다리 보정이 끝난 현재 프레임의 머리를 사용합니다.
            if (!breathWarmed) { EmitBreath(true); breathWarmed = true; }
            EmitBreath(false);
        }

        public bool Contains(Vector3 point)
        {
            if (JumpClearance > 0 && GroundHeight != null && point.y - GroundHeight(point) > JumpClearance) return false;
            Vector3 offset = point - transform.position;
            float distance = Vector3.ProjectOnPlane(offset, Vector3.up).magnitude;
            if (Mathf.Abs(offset.y) > 3 || distance > EdgeRadius(Vector3.ProjectOnPlane(offset, Vector3.up)) || distance < InnerRadius) return false;
            if (angle < 359 && Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(offset, Vector3.up)) > angle * 0.5f) return false;
            if (IgnoreCover) return true;
            Vector3 ray = target.transform.position + Vector3.up - (transform.position + Vector3.up);
            return !Projectile.TryGetFirstHit(new Ray(transform.position + Vector3.up, ray.normalized), ray.magnitude,
                Physics.DefaultRaycastLayers, owner, out RaycastHit hit) || hit.collider.GetComponentInParent<Health>() == target;
        }

        private void ProjectGround(float progress)
        {
            if (GroundHeight == null) return;
            fill.localScale = Vector3.one;
            for (int index = 0; index < projected.Length; index++)
            {
                Vector3 point = fullFootprint[index] * progress;
                if (Irregular) point *= EdgeRadius(transform.TransformDirection(point)) / Radius;
                if (innerBuilt) point = fullFootprint[index].normalized * (InnerRadius + (fullFootprint[index].magnitude - InnerRadius) * progress);
                Vector3 world = transform.TransformPoint(point);
                point.y = GroundHeight(world) + 0.22f - transform.position.y;
                projected[index] = point;
            }
            mesh.vertices = projected;
            mesh.RecalculateBounds();
            if (boundaryProjected) return;
            for (int index = 0; index < boundary.positionCount; index++)
            {
                Vector3 point = fullFootprint[angle >= 359 ? boundaryStart + index : index == 0 ? 0 : boundaryStart + index - 1];
                if (Irregular) point *= EdgeRadius(transform.TransformDirection(point)) / Radius;
                Vector3 world = transform.TransformPoint(point);
                point.y = GroundHeight(world) + 0.25f - transform.position.y;
                boundary.SetPosition(index, point);
            }
            boundaryProjected = true;
        }

        private void OnDestroy() { if (mesh != null) Destroy(mesh); if (smokeMaterial != null) Destroy(smokeMaterial); if (lavaMaterial != null) Destroy(lavaMaterial); }
    }
}
