using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class DragonBossCombat
    {
        private BossLineBreath aimedBreath;

        private void PrepareAdditionalPattern(DragonPattern pattern, float windup)
        {
            if (pattern == DragonPattern.RedSweep) PrepareBodySweep(windup);
            if (pattern == DragonPattern.AimedBreath)
                aimedBreath = BossLineBreath.Create(effects, this, arena, target, windup);
            if (pattern != DragonPattern.InnerBlast && pattern != DragonPattern.OuterBlast) return;

            bool safeInside = pattern == DragonPattern.OuterBlast;
            float outer = safeInside ? 19 : 10;
            float inner = safeInside ? 7 : 0;
            var blast = Area(transform.position, outer, 360, windup, 0.2f, 24);
            blast.InnerRadius = inner;
            blast.AllowIrregular = false;
            blast.IgnoreCover = true;
            blast.EmitsFlame = false;
            blast.HighlightActive = true;
            blast.gameObject.AddComponent<BossDelayedRing>().Configure(arena, transform.position, inner, outer, windup);
        }

        private void ReleaseAdditionalPattern()
        {
            if (Pattern == DragonPattern.TrackingFireball)
            {
                var ball = BossFireball.Launch(effects, transform, target, Mouth.position,
                    target.transform.position + Vector3.up * 1.2f, 4, false, damage: ScaleDamage(24), radius: 0.85f);
                ball.Homing = true;
                ball.TravelSpeed = 10;
                BossJustGuardVfx.Cue(transform);
            }
            if (Pattern != DragonPattern.Circle) BossCombatImpact.Pulse(0.1f);
        }

        private void TickAimedBreath()
        {
            if (Pattern != DragonPattern.AimedBreath || Phase != DragonPhase.Windup || aimedBreath == null) return;
            if (Remaining > 0.4f)
            {
                direction = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up).normalized;
                aimedBreath.SetHeading(direction);
            }
        }
    }
}
