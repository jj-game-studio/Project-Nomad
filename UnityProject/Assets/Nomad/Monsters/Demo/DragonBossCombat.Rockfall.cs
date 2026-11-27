using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class DragonBossCombat
    {
        public bool AutomaticRockfall { get; set; } = true;
        private float nextRockfall;
        private Transform background;
        private readonly List<BossFallingRock> fallingRocks = new();
        private BossBreathBackstep backstep;
        public int PendingRocks { get { fallingRocks.RemoveAll(rock => rock == null); return fallingRocks.Count; } }

        private void TickIndependentHazards()
        {
            if (!PlayerInside || arena.IsLocked || Phase == DragonPhase.Waiting || Phase == DragonPhase.Enraging) return;
            if (AutomaticRockfall && Time.time >= nextRockfall) DropRocks();
        }

        public void DropRocks()
        {
            if (health == null || health.IsDead || !Introduced || !PlayerInside || arena.IsLocked || EnrageTransitioning) return;
            nextRockfall = Time.time + Tuning.RockfallInterval;
            int count = Mathf.Min(Random.Range(1, 3), BossArena.MaximumRockCount - arena.RockCount - PendingRocks);
            for (int index = 0; index < count; index++)
            {
                for (int attempt = 0; attempt < 24; attempt++)
                {
                    Vector3 requested = index == 0 && attempt == 0 ? target.transform.position :
                        arena.Center + Quaternion.Euler(0, Random.Range(0, 360), 0) * Vector3.forward * Random.Range(8, 18);
                    if (!arena.FindRockDropPoint(requested, transform.position, out Vector3 point)) continue;
                    if (fallingRocks.Exists(rock => rock != null && Vector3.Distance(rock.Landing, point) < 6)) continue;
                    BossHazard warning = BossHazard.Create(background, transform, point, Quaternion.identity, target,
                        Tuning.RockfallRadius, 360, Tuning.RockfallWarning + BossFallingRock.FallSeconds, 0.12f, ScaleDamage(22));
                    warning.GroundHeight = arena.GroundHeight;
                    warning.EmitsFlame = false;
                    warning.HighlightActive = true;
                    fallingRocks.Add(BossFallingRock.Create(background, arena, target, point, Tuning.RockfallWarning));
                    break;
                }
            }
        }

        private void ClearBackground()
        {
            if (background == null) return;
            // 구체는 보스 상태에 따라 숨기고, 진행 중 낙하는 조우 종료 때만 취소합니다.
            foreach (Transform child in background)
                if (child.GetComponent<BossOrbitingFlames>() == null) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            fallingRocks.Clear();
            nextRockfall = Time.time + Tuning.RockfallInterval;
        }

        public void FinishBreathBackstep()
        {
            if (Pattern != DragonPattern.Breath || Phase != DragonPhase.Windup) return;
            breath = Area(transform.position, Tuning.BreathRange, Tuning.BreathAngle, Remaining, 2.4f / Tempo, 12, 0.8f / Tempo);
        }
    }
}
