using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class DragonBossCombat
    {
        private void PrepareOrbit(float windup) => BossRingSequence.Create(effects, this, arena, target, windup);

        private void PrepareLavaEruption(float windup)
        {
            int count = Random.Range(2, 4);
            var points = new System.Collections.Generic.List<Vector3>();
            for (int index = 0; index < count; index++)
            {
                Vector3 point = ClampPoint(target.transform.position);
                for (int attempt = 0; attempt < 48; attempt++)
                {
                    if (!points.Exists(existing => Vector3.Distance(existing, point) < Tuning.LavaEruptionRadius * 2 + 0.5f)) break;
                    Vector3 center = attempt < 12 ? transform.position : arena.Center;
                    point = ClampPoint(center + Quaternion.Euler(0, Random.Range(0, 360), 0) * Vector3.forward * Random.Range(7, 16));
                }
                points.Add(point);
                BossHazard eruption = Area(point, Tuning.LavaEruptionRadius, 360, windup, 0.55f, 26);
                eruption.EmitsFlame = false;
                eruption.HideWarningWhileActive = true;
                BossLavaEruption.Create(effects, point, windup, Tuning.LavaEruptionRadius);
                BossHazard heat = Area(point, Tuning.LavaEruptionRadius, 360, windup + 0.55f, 3.5f, 7);
                heat.EmitsFlame = false;
                heat.HideWarning = true;
            }
        }
    }
}
