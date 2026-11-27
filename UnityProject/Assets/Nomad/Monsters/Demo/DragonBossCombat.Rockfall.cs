using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class DragonBossCombat
    {
        private float nextRockfall;
        private BossFallingRock fallingRock;
        private BossBreathBackstep backstep;

        private void PrepareRockfall(float windup)
        {
            nextRockfall = Time.time + Tuning.RockfallInterval;
            if (!arena.FindRockDropPoint(target.transform.position, transform.position, out Vector3 point)) return;
            BossHazard warning = Area(point, Tuning.RockfallRadius, 360, windup + BossFallingRock.FallSeconds, 0.12f, 22);
            warning.EmitsFlame = false;
            fallingRock = BossFallingRock.Create(effects, arena, target, point, windup);
        }

        public void FinishBreathBackstep()
        {
            if (Pattern != DragonPattern.Breath || Phase != DragonPhase.Windup) return;
            breath = Area(transform.position, Tuning.BreathRange, Tuning.BreathAngle, Remaining, 2.4f / Tempo, 12, 0.8f / Tempo);
        }
    }
}
