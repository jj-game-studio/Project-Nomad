using Nomad.Equipment;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed class BossMouthPreparation : MonoBehaviour
    {
        private DragonBossCombat boss;
        private ParticleSystem gathering;

        public void Configure(DragonBossCombat owner, Transform mouth)
        {
            boss = owner;
            gathering = WeaponVfx.CreateParticles(mouth, "Mouth fire preparation", WeaponVfx.Glow, ParticleSystemSimulationSpace.Local);
            var main = gathering.main;
            main.startLifetime = 0.35f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startSpeed = 0.1f;
            main.startColor = new Color(1, 0.35f, 0.02f);

            var shape = gathering.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            var emission = gathering.emission;
            emission.rateOverTime = 40;
            WeaponVfx.Fade(gathering, 0);
        }

        private void LateUpdate()
        {
            if (boss == null || gathering == null) return;
            bool preparing = boss.Phase == DragonPhase.Windup &&
                (boss.Pattern == DragonPattern.Breath || boss.Pattern == DragonPattern.RadialFireballs);
            var emission = gathering.emission;
            emission.enabled = preparing;
            if (!preparing) gathering.Clear();
        }
    }
}
