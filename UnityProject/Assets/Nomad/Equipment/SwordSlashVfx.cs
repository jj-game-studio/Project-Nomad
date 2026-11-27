using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Equipment
{
    // 대검 베기 궤적 VFX입니다. 공격 부채꼴을 따라 초승달 모양 면이 오른쪽에서 왼쪽으로 쓸고 지나가며 사라집니다.
    // 바깥 가장자리는 밝은 칼날빛, 안쪽은 투명하게 번지고, 칼끝에서 불꽃이 튑니다.
    public sealed class SwordSlashVfx : MonoBehaviour
    {
        private const float SweepTime = 0.2f;
        private const float FadeTime = 0.18f;
        private const float TailSegments = 18f;
        private const float InnerRatio = 0.28f;
        private const float BodyRatio = 0.78f;

        private Mesh mesh;
        private Color32[] colors;
        private Vector3[] tips;
        private ParticleSystem sparks;
        private WeaponVfxStyle style;
        private float started;
        private int segments;
        private int emittedHead = -1;

        public float Progress => Mathf.Clamp01((Time.time - started) / (SweepTime + FadeTime));

        // reaches는 부채꼴의 오른쪽(+각) 끝부터 왼쪽 끝까지 각 방향의 실제 도달 거리입니다.
        public static SwordSlashVfx Spawn(Vector3 origin, Vector3 direction, float halfAngle, float[] reaches, WeaponVfxStyle style)
        {
            var go = new GameObject("Sword Slash VFX");
            // 위에서 내려다보는 시점에서도 휘두르는 면이 보이도록 살짝 기울입니다.
            go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 0f, -12f));
            var vfx = go.AddComponent<SwordSlashVfx>();
            vfx.style = style;
            vfx.Build(halfAngle, reaches);
            return vfx;
        }

        private void Build(float halfAngle, float[] reaches)
        {
            segments = reaches.Length - 1;
            var vertices = new Vector3[reaches.Length * 3];
            var uv = new Vector2[vertices.Length];
            tips = new Vector3[reaches.Length];
            for (int index = 0; index <= segments; index++)
            {
                float angle = Mathf.Lerp(halfAngle, -halfAngle, index / (float)segments) * Mathf.Deg2Rad;
                var ray = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                float reach = Mathf.Max(0.4f, reaches[index]);
                vertices[index * 3] = ray * reach * InnerRatio;
                vertices[index * 3 + 1] = ray * reach * BodyRatio;
                vertices[index * 3 + 2] = ray * reach;
                tips[index] = ray * reach;
                for (int row = 0; row < 3; row++)
                    uv[index * 3 + row] = new Vector2(index / (float)segments, row * 0.5f);
            }

            var triangles = new int[segments * 12];
            for (int index = 0; index < segments; index++)
            for (int row = 0; row < 2; row++)
            {
                int a = index * 3 + row;
                int b = a + 3;
                int t = (index * 2 + row) * 6;
                // 위에서 내려다볼 때 앞면이 되도록 시계 방향으로 감습니다.
                triangles[t] = a;
                triangles[t + 1] = b;
                triangles[t + 2] = a + 1;
                triangles[t + 3] = b;
                triangles[t + 4] = b + 1;
                triangles[t + 5] = a + 1;
            }

            colors = new Color32[vertices.Length];
            mesh = new Mesh { name = "Sword Slash", vertices = vertices, uv = uv, triangles = triangles, colors32 = colors };
            mesh.MarkDynamic();
            mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = WeaponVfx.Plain;
            // 1인칭은 손의 짧은 칼끝 궤적으로 표현합니다. 탑다운용 넓은 면이 카메라를 덮지 않게 합니다.
            Camera camera = Camera.main;
            var firstPerson = camera != null ? camera.GetComponent<Nomad.Player.VillageFirstPersonCamera>() : null;
            renderer.enabled = firstPerson == null || !firstPerson.IsActive ||
                Vector3.Distance(camera.transform.position, transform.position) > 3f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            sparks = WeaponVfx.CreateParticles(transform, "Sparks", WeaponVfx.Glow);
            ParticleSystem.MainModule main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(style.Core, style.Glow);
            main.gravityModifier = 0.8f;
            WeaponVfx.Shrink(sparks);

            started = Time.time;
            UpdateSweep();
        }

        private void LateUpdate()
        {
            UpdateSweep();
            if (Time.time - started >= SweepTime + FadeTime + 0.5f)
                Destroy(gameObject);
        }

        private void UpdateSweep()
        {
            float elapsed = Time.time - started;
            float sweep = Mathf.Clamp01(elapsed / SweepTime);
            float head = (1f - Mathf.Pow(1f - sweep, 3f)) * segments;
            float fade = Mathf.Clamp01(1f - (elapsed - SweepTime) / FadeTime);

            Color body = style.Glow;
            Color edge = style.Core;
            for (int index = 0; index <= segments; index++)
            {
                // 칼끝(head)에 가까울수록 밝고, 지나간 자리는 꼬리처럼 빠르게 옅어집니다.
                float behind = head - index;
                float strength = behind < -0.5f ? 0f : Mathf.Clamp01(1f - behind / TailSegments) * fade;
                colors[index * 3] = Tint(body, 0f);
                colors[index * 3 + 1] = Tint(body, strength * 0.75f);
                colors[index * 3 + 2] = Tint(edge, strength);
            }
            mesh.colors32 = colors;

            // 칼끝이 지나는 칸마다 바깥쪽으로 불꽃을 튀깁니다.
            int current = Mathf.Min(segments, Mathf.FloorToInt(head));
            if (fade <= 0f)
                return;
            for (int index = emittedHead + 1; index <= current; index++)
            {
                if (index % 2 != 0)
                    continue;
                Vector3 tip = transform.TransformPoint(tips[index]);
                Vector3 outward = (tip - transform.position).normalized;
                var emit = new ParticleSystem.EmitParams
                {
                    position = tip,
                    velocity = outward * Random.Range(1.5f, 3.5f) + Vector3.up * Random.Range(0.5f, 2f),
                    applyShapeToPosition = false
                };
                sparks.Emit(emit, 2);
            }
            emittedHead = Mathf.Max(emittedHead, current);
        }

        private static Color32 Tint(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
        }
    }
}
