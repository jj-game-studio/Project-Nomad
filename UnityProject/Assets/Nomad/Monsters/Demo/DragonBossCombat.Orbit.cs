using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class DragonBossCombat
    {
        private void PrepareOrbit(float windup)
        {
            RingBand(0, 3.8f, windup, 3.1f);
            RingBand(7, 10, windup, 3.1f);
            RingBand(14, 17, windup, 3.1f);
            BossOrbitingFlames.Create(effects, this, arena, target, windup, 8.4f).NextBands = PrepareSecondOrbitBands;
        }

        private void PrepareSecondOrbitBands()
        {
            RingBand(3.8f, 7, 0.9f, 3.5f);
            RingBand(10, 14, 0.9f, 3.5f);
            RingBand(17, 21, 0.9f, 3.5f);
        }

        private void RingBand(float inner, float outer, float delay, float duration)
        {
            BossHazard band = Area(transform.position, outer, 360, delay, duration, 10, 0.85f);
            band.InnerRadius = inner;
            band.AllowIrregular = false;
            band.IgnoreCover = true;
            band.EmitsFlame = false;
            band.WarningSeconds = delay;
            band.WarningColor = new Color(1, 0.12f, 0.025f, 0.23f);
        }
    }
}
