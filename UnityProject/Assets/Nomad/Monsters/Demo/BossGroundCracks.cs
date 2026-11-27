using Nomad.Equipment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Monsters.Demo
{
    public sealed class BossGroundCracks : MonoBehaviour
    {
        private Material material;
        private LineRenderer[] cracks;
        private float started;

        public static void Create(Transform parent, BossArena arena, Vector3 center, float radius)
        {
            var root = new GameObject("Slam ground fissures");
            root.transform.SetParent(parent, false);
            var impact = root.AddComponent<BossGroundCracks>();
            impact.started = Time.time;
            impact.material = new Material(WeaponVfx.Plain);
            impact.material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            impact.material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            impact.cracks = new LineRenderer[10];

            for (int index = 0; index < impact.cracks.Length; index++)
            {
                var line = new GameObject("Broken stone seam").AddComponent<LineRenderer>();
                line.transform.SetParent(root.transform, false);
                line.sharedMaterial = impact.material;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.positionCount = 8;
                line.startWidth = 0.24f;
                line.endWidth = 0.035f;
                impact.cracks[index] = line;
                Vector3 direction = Quaternion.Euler(0, index * 36 + 13, 0) * Vector3.forward;
                Vector3 side = Vector3.Cross(Vector3.up, direction);
                for (int step = 0; step < line.positionCount; step++)
                {
                    float length = Mathf.Min(radius, arena.DistanceToBoundary(center, direction)) * step / (line.positionCount - 1);
                    Vector3 point = center + direction * length + side * (step == 0 ? 0 : Mathf.Sin(step * 11 + index) * 0.45f);
                    point.y = arena.GroundHeight(point) + 0.045f;
                    line.SetPosition(step, point);
                }
            }
            var dust = WeaponVfx.CreateParticles(root.transform, "Landing stone dust", WeaponVfx.Glow);
            var main = dust.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startColor = new Color(0.4f, 0.3f, 0.2f, 0.4f);
            for (int index = 0; index < 50; index++)
            {
                Vector3 direction = Quaternion.Euler(0, index * 137.5f, 0) * Vector3.forward;
                Vector3 point = arena.GroundPoint(center + direction * Mathf.Min(index % 5 * 1.5f, arena.DistanceToBoundary(center, direction)));
                dust.Emit(new ParticleSystem.EmitParams { position = point + Vector3.up * 0.1f, velocity = direction * 3 + Vector3.up * 1.8f }, 1);
            }
            Destroy(root, 3.5f);
        }

        private void Update()
        {
            float alpha = Mathf.Clamp01((3.5f - (Time.time - started)) / 1.2f);
            foreach (LineRenderer crack in cracks) crack.startColor = crack.endColor = new Color(0.08f, 0.045f, 0.025f, alpha);
        }

        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
