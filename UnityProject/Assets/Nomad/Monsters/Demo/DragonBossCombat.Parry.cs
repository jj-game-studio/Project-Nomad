using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class DragonBossCombat
    {
        private readonly HashSet<int> protectedVolleys = new();
        private readonly Dictionary<int, int> pendingVolleyHits = new();
        private BossHazard sweep;

        public void ProtectVolley(int volley)
        {
            protectedVolleys.Add(volley);
            pendingVolleyHits.Remove(volley);
        }

        private void FlushVolleyHits()
        {
            // 모든 구체의 Update 뒤 처리해 같은 프레임의 패링이 먼저 닿은 다른 구체도 방어합니다.
            if (target != null && health != null && !health.IsDead && !EnrageTransitioning)
                foreach (var hit in pendingVolleyHits)
                    if (!protectedVolleys.Contains(hit.Key)) target.TryApplyDamage(hit.Value);
            pendingVolleyHits.Clear();
        }

        public bool SweepParryable => Pattern == DragonPattern.BodySweep;

        private void PrepareBodySweep(float windup)
        {
            sweep = Area(transform.position, Tuning.BodySweepRadius, 360, windup, 0.12f, 28);
            sweep.EmitsFlame = false;
            sweep.WarningSeconds = SweepParryable ? Mathf.Max(0.1f, windup - BossParrySignal.ReactionSeconds) : windup;
            sweep.WarningColor = SweepParryable ? new Color(1, 0.78f, 0.08f, 0.3f) : new Color(1, 0.08f, 0.015f, 0.3f);
            sweep.OnHit = () => PlayerBossReaction.Play(target, transform.position, 7, 1.35f, 0, 0.3f);
            if (SweepParryable) parrySignal.Arm(Time.time + windup, Tuning.BodySweepRadius + 0.5f);
        }

        private void PrepareRadialWarning(float windup, int volley = 0)
        {
            float impactAt = Time.time + windup;
            parrySignal.Arm(impactAt, Tuning.RadialRange);
            BossParryRangeWarning.Create(effects, arena, transform.position, direction, Tuning.RadialCount, Tuning.RadialRadius, Tuning.RadialRange,
                Mathf.Max(0.1f, windup - BossParrySignal.RadialReactionSeconds), volley * 180f / Tuning.RadialCount);
        }
    }
}
