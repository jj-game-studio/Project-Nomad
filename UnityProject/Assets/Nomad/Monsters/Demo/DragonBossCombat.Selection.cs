using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class DragonBossCombat
    {
        private DragonPattern? queuedPattern;

        public DragonPattern ChooseNextPattern(float targetDistance)
        {
            var choices = new List<DragonPattern>();
            foreach (DragonPattern pattern in Rotation)
                if (AttacksStarted == 0 || pattern != Pattern) choices.Add(pattern);
            if (targetDistance <= Tuning.BodySweepRadius + 1 && Pattern != DragonPattern.BodySweep)
                choices.Add(DragonPattern.BodySweep);
            if (Time.time >= nextCharge && Pattern != DragonPattern.Charge) choices.Add(DragonPattern.Charge);
            if (GetComponent<BossArmor>().BreakCount > 0 && Pattern != DragonPattern.LavaEruption) choices.Add(DragonPattern.LavaEruption);
            return choices[Random.Range(0, choices.Count)];
        }

        private void BeginNext()
        {
            queuedPattern ??= ChooseNextPattern(Vector3.Distance(transform.position, target.transform.position));
            DragonPattern next = queuedPattern.Value;
            float engageRange = next == DragonPattern.Charge ? float.PositiveInfinity :
                next == DragonPattern.Circle ? 5.5f : next == DragonPattern.BodySweep ? Tuning.BodySweepRadius : 9;
            if (Vector3.Distance(transform.position, target.transform.position) > engageRange)
            {
                if (pursuitStarted < 0) pursuitStarted = Time.time;
                if (Time.time - pursuitStarted < 2.5f)
                {
                    PursuePlayer();
                    until = Time.time + 0.08f;
                    return;
                }
                next = DragonPattern.RadialFireballs;
            }
            pursuitStarted = -1;
            queuedPattern = null;
            BeginPattern(next);
        }
    }
}
