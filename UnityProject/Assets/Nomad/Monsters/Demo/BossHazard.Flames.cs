using Nomad.Equipment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Monsters.Demo
{
    public sealed partial class BossHazard
    {
        private ParticleSystem core, smoke;
        private Material smokeMaterial;
        private Light breathLight;

        private void CreateBreathLayers()
        {
            CreateContinuousJet();
            core = WeaponVfx.CreateParticles(transform, "Hot flame core", WeaponVfx.Glow);
            var main = core.main;
            main.startColor = new Color(1.8f, 0.65f, 0.08f);
            main.maxParticles = 600;
            var renderer = core.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1f;
            renderer.velocityScale = 0.03f;
            WeaponVfx.Fade(core, 0);

            smokeMaterial = new Material(WeaponVfx.Glow);
            smokeMaterial.name = "Breath smoke alpha";
            smokeMaterial.SetFloat("_Blend", 0);
            smokeMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            smokeMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            smoke = WeaponVfx.CreateParticles(transform, "Breath smoke", smokeMaterial);
            main = smoke.main;
            main.startColor = new Color(0.16f, 0.12f, 0.1f, 0.24f);
            main.maxParticles = 128;
            var grow = smoke.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.4f, 1, 1.5f));
            WeaponVfx.Fade(smoke, 0.2f);

            var lamp = new GameObject("Breath light");
            lamp.transform.SetParent(transform, false);
            breathLight = lamp.AddComponent<Light>();
            breathLight.type = LightType.Point;
            breathLight.color = new Color(1, 0.35f, 0.05f);
            breathLight.range = 7;
            breathLight.shadows = LightShadows.None;
        }
    }
}
