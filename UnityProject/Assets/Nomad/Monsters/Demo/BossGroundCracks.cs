using Nomad.Equipment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Monsters.Demo
{
    public sealed class BossGroundCracks : MonoBehaviour
    {
        private Material material, glowMaterial;
        private LineRenderer[] glow;
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
            impact.cracks = new LineRenderer[16];
            impact.glow = new LineRenderer[16];
            impact.glowMaterial = new Material(WeaponVfx.Beam);

            for (int index = 0; index < impact.cracks.Length; index++)
            {
                var line = new GameObject("Broken stone seam").AddComponent<LineRenderer>();
                line.transform.SetParent(root.transform, false);
                line.sharedMaterial = impact.material;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.positionCount = 12;
                line.startWidth = 0.36f;
                line.endWidth = 0.035f;
                impact.cracks[index] = line;
                Vector3 direction = Quaternion.Euler(0, index * 22.5f + 13, 0) * Vector3.forward;
                Vector3 side = Vector3.Cross(Vector3.up, direction);
                for (int step = 0; step < line.positionCount; step++)
                {
                    float length = Mathf.Min(radius * 1.1f, arena.DistanceToBoundary(center, direction)) * step / (line.positionCount - 1);
                    Vector3 point = center + direction * length + side * (step == 0 ? 0 : Mathf.Sin(step * 11 + index) * 0.45f);
                    point.y = arena.GroundHeight(point) + 0.18f;
                    line.SetPosition(step, point);
                }
                var hot = new GameObject("Fresh fracture light").AddComponent<LineRenderer>();
                hot.transform.SetParent(root.transform, false);
                hot.sharedMaterial = impact.glowMaterial;
                hot.positionCount = line.positionCount;
                hot.startWidth = 0.21f; hot.endWidth = 0.025f;
                var positions = new Vector3[line.positionCount];
                line.GetPositions(positions);
                hot.SetPositions(positions);
                impact.glow[index] = hot;
            }
            BossHeavedGround.Create(parent,arena,center,radius);
            var dust = WeaponVfx.CreateParticles(root.transform, "Landing stone dust", BossFlameVfx.Smoke);
            var main = dust.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startColor = new Color(0.4f, 0.3f, 0.2f, 0.4f);
            WeaponVfx.Fade(dust,0);
            for (int index = 0; index < 100; index++)
            {
                Vector3 direction = Quaternion.Euler(0, index * 137.5f, 0) * Vector3.forward;
                Vector3 point = arena.GroundPoint(center + direction * Mathf.Min(index % 5 * 1.5f, arena.DistanceToBoundary(center, direction)));
                dust.Emit(new ParticleSystem.EmitParams { position = point + Vector3.up * 0.1f, velocity = direction * 3 + Vector3.up * 1.8f }, 1);
            }
            BossStoneDebris.Create(parent, center + Vector3.up * 0.3f);
            Destroy(root, 6);
        }

        private void Update()
        {
            float alpha = Mathf.Clamp01((6 - (Time.time - started)) / 1.5f);
            foreach (LineRenderer crack in cracks) crack.startColor = crack.endColor = new Color(0.045f, 0.025f, 0.015f, alpha);
            float flash = Mathf.Clamp01(1 - (Time.time - started) / 1.2f);
            foreach (LineRenderer line in glow) line.startColor = line.endColor = new Color(1.7f, 0.7f, 0.12f, flash);
        }

        private void OnDestroy() { if (material != null) Destroy(material); if (glowMaterial != null) Destroy(glowMaterial); }
    }
}
