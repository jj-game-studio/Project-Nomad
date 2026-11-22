using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Equipment
{
    // 지팡이 발사체를 따라가는 마법탄 VFX입니다(빛나는 핵·꼬리·흩날리는 입자·주변광).
    // 발사체의 자식이 아니라 별도 오브젝트라서 발사체가 사라져도 꼬리와 입자가 자연스럽게 사라집니다.
    public sealed class MagicBoltVfx : MonoBehaviour
    {
        private const float LingerTime = 0.5f;

        private Transform target;
        private ParticleSystem core;
        private ParticleSystem sparks;
        private TrailRenderer trail;
        private Light glowLight;
        private float lightIntensity;
        private float releasedAt = -1f;

        public bool Following => target != null;

        public static MagicBoltVfx Attach(Transform bolt, WeaponVfxStyle style)
        {
            // 기존 발사체 외형(작은 구)은 VFX 핵으로 대체합니다.
            foreach (Renderer renderer in bolt.GetComponentsInChildren<Renderer>())
                renderer.enabled = false;

            var vfx = new GameObject("Magic Bolt VFX").AddComponent<MagicBoltVfx>();
            vfx.transform.SetPositionAndRotation(bolt.position, bolt.rotation);
            vfx.target = bolt;
            vfx.Build(style);
            return vfx;
        }

        private void Build(WeaponVfxStyle style)
        {
            float size = style.Size;

            core = WeaponVfx.CreateParticles(transform, "Core", WeaponVfx.Glow, ParticleSystemSimulationSpace.Local);
            ParticleSystem.MainModule main = core.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f * size, 0.7f * size);
            main.startColor = new ParticleSystem.MinMaxGradient(style.Core, Color.Lerp(style.Core, style.Glow, 0.5f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            ParticleSystem.EmissionModule emission = core.emission;
            emission.enabled = true;
            emission.rateOverTime = 70f;
            WeaponVfx.Fade(core, 0.4f);

            sparks = WeaponVfx.CreateParticles(transform, "Sparks", WeaponVfx.Glow);
            main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f * size, 0.12f * size);
            main.startColor = new ParticleSystem.MinMaxGradient(style.Core, style.Glow);
            main.gravityModifier = -0.15f;
            emission = sparks.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 10f;
            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f * size;
            WeaponVfx.Shrink(sparks);

            trail = new GameObject("Trail").AddComponent<TrailRenderer>();
            trail.transform.SetParent(transform, false);
            trail.sharedMaterial = WeaponVfx.Beam;
            trail.time = 0.2f;
            trail.minVertexDistance = 0.05f;
            trail.numCapVertices = 4;
            trail.widthCurve = AnimationCurve.EaseInOut(0f, 0.36f * size, 1f, 0f);
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(style.Core, 0f), new GradientColorKey(style.Glow, 0.35f), new GradientColorKey(style.Glow, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.35f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;

            glowLight = new GameObject("Glow Light").AddComponent<Light>();
            glowLight.transform.SetParent(transform, false);
            glowLight.type = LightType.Point;
            glowLight.color = style.Glow;
            glowLight.range = 3.5f * size;
            glowLight.intensity = lightIntensity = 2.5f;
            glowLight.shadows = LightShadows.None;
        }

        private void LateUpdate()
        {
            if (target != null)
            {
                transform.SetPositionAndRotation(target.position, target.rotation);
                return;
            }

            // 발사체가 명중하거나 사거리 끝에서 사라지면 방출을 멈추고 남은 꼬리·입자만 사라지게 둡니다.
            if (releasedAt < 0f)
            {
                releasedAt = Time.time;
                core.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                trail.emitting = false;
            }

            float progress = (Time.time - releasedAt) / LingerTime;
            glowLight.intensity = lightIntensity * Mathf.Clamp01(1f - progress * 4f);
            if (progress >= 1f)
                Destroy(gameObject);
        }
    }
}
