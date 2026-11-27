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

        private void PrepareBodySweep(float windup)
        {
            sweep = Area(transform.position, Tuning.BodySweepRadius, 360, windup, 0.12f, 28);
            sweep.EmitsFlame = false;
            sweep.WarningColor = new Color(1, 0.78f, 0.08f, 0.3f);
            sweep.OnHit = () => PlayerBossReaction.Play(target, transform.position, 7, 1.7f);
            parrySignal.Arm(Time.time + windup, Tuning.BodySweepRadius + 0.5f);
        }

        private void PrepareRadialWarning(float windup)
        {
            float distance = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up).magnitude;
            float travel = Mathf.Max(0, distance - 3 - Tuning.RadialRadius) / (Tuning.RadialSpeed * Tempo);
            BossParryRangeWarning.Create(effects, transform.position, Tuning.RadialRange, windup + travel, arena.GroundHeight, target.transform, heading => arena.DistanceToBoundary(transform.position, heading));
        }
    }
}
