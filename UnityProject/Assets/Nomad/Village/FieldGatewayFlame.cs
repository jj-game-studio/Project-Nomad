using Nomad.Equipment;
using UnityEngine;

namespace Nomad.Village
{
    public sealed class FieldGatewayFlame : MonoBehaviour
    {
        private void Awake()
        {
            ParticleSystem fire = WeaponVfx.CreateParticles(transform, "Flame", WeaponVfx.Glow);
            fire.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var main = fire.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, 0.35f, 0.08f), new Color(1, 0.8f, 0.3f));
            var emission = fire.emission;
            emission.enabled = true;
            emission.rateOverTime = 24;
            var shape = fire.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12;
            shape.radius = 0.13f;
            WeaponVfx.Shrink(fire);
            WeaponVfx.Fade(fire, 0);
        }
    }
}
