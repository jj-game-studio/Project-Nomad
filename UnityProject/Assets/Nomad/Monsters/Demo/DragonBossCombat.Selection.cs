using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class DragonBossCombat
    {
        private DragonPattern? queuedPattern;
        public const float MaximumParryGap = 10;
        private float lastParryPatternAt;
        public float ParryPatternGap => Mathf.Max(0, Time.time - lastParryPatternAt);
        public static bool IsParryPattern(DragonPattern pattern) => pattern == DragonPattern.Circle ||
            pattern == DragonPattern.RadialFireballs || pattern == DragonPattern.BodySweep || pattern == DragonPattern.TrackingFireball;

        private DragonPattern GuaranteedParry(float distance)
        {
            var choices = new List<DragonPattern> { DragonPattern.RadialFireballs, DragonPattern.TrackingFireball };
            if (distance <= Tuning.BodySweepRadius) choices.Add(DragonPattern.BodySweep);
            if (distance <= 5.5f) choices.Add(DragonPattern.Circle);
            choices.Remove(Pattern);
            return choices[Random.Range(0, choices.Count)];
        }

        public DragonPattern ChooseNextPattern(float targetDistance)
        {
            if (ParryPatternGap >= MaximumParryGap) return GuaranteedParry(targetDistance);
            var choices = new List<DragonPattern>();
            foreach (DragonPattern pattern in Rotation)
                if (AttacksStarted == 0 || pattern != Pattern) choices.Add(pattern);
            if (targetDistance <= Tuning.BodySweepRadius)
            {
                if (Pattern != DragonPattern.BodySweep) choices.Add(DragonPattern.BodySweep);
                if (Pattern != DragonPattern.RedSweep) choices.Add(DragonPattern.RedSweep);
            }
            else
            {
                if (Pattern != DragonPattern.TrackingFireball) choices.Add(DragonPattern.TrackingFireball);
                if (Pattern != DragonPattern.AimedBreath) choices.Add(DragonPattern.AimedBreath);
            }
            if (Time.time >= nextCharge && Pattern != DragonPattern.Charge) choices.Add(DragonPattern.Charge);
            if (GetComponent<BossArmor>().BreakCount > 0 && Pattern != DragonPattern.LavaEruption)
            {
                choices.Add(DragonPattern.LavaEruption);
                choices.Add(DragonPattern.LavaEruption);
                choices.Add(DragonPattern.LavaEruption);
                choices.Add(DragonPattern.LavaEruption);
                choices.Add(DragonPattern.LavaEruption);
            }
            return choices[Random.Range(0, choices.Count)];
        }

        private void BeginNext()
        {
            if (ParryPatternGap >= MaximumParryGap) queuedPattern = GuaranteedParry(Vector3.Distance(transform.position,target.transform.position));
            queuedPattern ??= ChooseNextPattern(Vector3.Distance(transform.position, target.transform.position));
            DragonPattern next = queuedPattern.Value;
            float engageRange = next == DragonPattern.Charge || next == DragonPattern.TrackingFireball || next == DragonPattern.AimedBreath ? float.PositiveInfinity :
                next == DragonPattern.Circle ? 5.5f : next == DragonPattern.BodySweep || next == DragonPattern.RedSweep ? Tuning.BodySweepRadius : 9;
            if (Vector3.Distance(transform.position, target.transform.position) > engageRange)
            {
                if (pursuitStarted < 0) pursuitStarted = Time.time;
                if (Time.time - pursuitStarted < 2.5f)
                {
                    PursuePlayer();
                    until = Time.time + 0.08f;
                    return;
                }
                next = Random.value < 0.5f ? DragonPattern.TrackingFireball : DragonPattern.AimedBreath;
            }
            pursuitStarted = -1;
            queuedPattern = null;
            BeginPattern(next);
        }
    }
}
