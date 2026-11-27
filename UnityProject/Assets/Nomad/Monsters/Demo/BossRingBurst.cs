using Nomad.Equipment;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed class BossRingBurst : MonoBehaviour
    {
        private static Material fire;
        private float age, duration;
        private Renderer surface;
        public float Age => age;

        public static void Create(Transform parent, BossArena arena, Vector3 center, float inner, float outer, float duration)
        {
            if (fire == null)
            {
                fire = new Material(Resources.Load<Shader>("BossRingWave"));
                fire.SetTexture("_MainTex", Resources.Load<Texture2D>("BossRingReference"));
            }
            var root = new GameObject("Swirling fire annulus");
            root.transform.SetParent(parent, false);
            root.transform.position = arena.GroundPoint(center);
            var burst = root.AddComponent<BossRingBurst>(); burst.duration = duration;
            const int segments = 128, rows = 6;
            var vertices = new Vector3[(segments + 1) * (rows + 1)];
            var uv = new Vector2[vertices.Length]; var triangles = new int[segments * rows * 6];
            int triangle = 0;
            for (int row = 0; row <= rows; row++)
            for (int col = 0; col <= segments; col++)
            {
                float t = row / (float)rows, radians = col / (float)segments * Mathf.PI * 2;
                Vector3 heading = new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians));
                Vector3 point = heading * Mathf.Lerp(inner, outer, t);
                int index = row * (segments + 1) + col;
                uv[index] = new Vector2(point.x / outer, point.z / outer) * 0.5f + Vector2.one * 0.5f;
                point.y = arena.GroundHeight(center + point) - root.transform.position.y + 0.32f;
                vertices[index] = point;
                if (row == rows || col == segments) continue;
                triangles[triangle++] = index; triangles[triangle++] = index + 1; triangles[triangle++] = index + segments + 1;
                triangles[triangle++] = index + 1; triangles[triangle++] = index + segments + 2; triangles[triangle++] = index + segments + 1;
            }
            var mesh = new Mesh { name = "Safe-gap clipped fire annulus", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); BossFlameVfx.Attach(root, mesh, fire);
            burst.surface = root.GetComponent<Renderer>();
            var tint = new MaterialPropertyBlock(); tint.SetFloat("_Inner", inner / outer); burst.surface.SetPropertyBlock(tint);

            var sparks = WeaponVfx.CreateParticles(root.transform, "Annulus fire motes", WeaponVfx.Glow);
            var main = sparks.main; main.startLifetime = 0.25f; main.startSize = new ParticleSystem.MinMaxCurve(0.06f,0.18f);
            main.startColor = new Color(2.5f,0.65f,0.06f); WeaponVfx.Fade(sparks,0);
            for (int index = 0; index < Mathf.CeilToInt(outer * 5); index++)
            {
                Vector3 heading = Quaternion.Euler(0,Random.Range(0,360),0)*Vector3.forward;
                Vector3 point = arena.GroundPoint(center + heading * Random.Range(inner,outer)) + Vector3.up*0.35f;
                sparks.Emit(new ParticleSystem.EmitParams { position=point, velocity=Vector3.up*Random.Range(0.5f,2) },1);
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            var tint = new MaterialPropertyBlock(); surface.GetPropertyBlock(tint);
            tint.SetFloat("_Age", Mathf.Clamp01(age / duration)); surface.SetPropertyBlock(tint);
            if (age >= duration) Destroy(gameObject);
        }
    }
}
