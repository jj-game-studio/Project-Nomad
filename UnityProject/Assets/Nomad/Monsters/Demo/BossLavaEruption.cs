using Nomad.Equipment;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed class BossLavaEruption : MonoBehaviour
    {
        private float burstAt, expires;
        private bool erupted;
        private float radius;

        public static void Create(Transform parent, Vector3 point, float warning, float radius)
        {
            var root = new GameObject("Lava fissure eruption");
            root.transform.SetParent(parent, false);
            root.transform.position = point + Vector3.up * 0.2f;
            var eruption = root.AddComponent<BossLavaEruption>();
            eruption.burstAt = Time.time + warning;
            eruption.expires = eruption.burstAt + 1.8f;
            eruption.radius = radius;
        }

        private void Update()
        {
            if (!erupted && Time.time >= burstAt)
            {
                erupted = true;
                BossStoneDebris.Create(transform, transform.position);
                var plume = WeaponVfx.CreateParticles(transform, "Molten eruption jet", WeaponVfx.Glow);
                plume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = plume.main;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 1.1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(5, 11);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(3, 1.4f, 0.12f), new Color(2, 0.22f, 0.01f));
                main.gravityModifier = 1.4f;
                var shape = plume.shape;
                shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 18; shape.radius = radius * 0.42f;
                plume.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                var emit = plume.emission;
                emit.enabled = true; emit.rateOverTime = 0;
                emit.SetBursts(new[] { new ParticleSystem.Burst(0, 100), new ParticleSystem.Burst(0.2f, 55) });
                WeaponVfx.Fade(plume, 0.1f);
                plume.Play();
                WeaponVfx.Burst(transform.position, new WeaponVfxStyle(new Color(3, 1.1f, 0.08f), new Color(1, 0.15f, 0.01f), 1.5f));
            }
            if (Time.time >= expires) Destroy(gameObject);
        }
    }
}
