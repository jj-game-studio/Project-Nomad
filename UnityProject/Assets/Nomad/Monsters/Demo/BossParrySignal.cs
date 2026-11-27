using Nomad.Combat;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed class BossParrySignal : MonoBehaviour
    {
        public const float ReactionSeconds = 0.35f;
        private DragonBossCombat boss;
        private Health target;
        private float impact, range;
        public bool Armed { get; private set; }
        public bool Flashing => Armed && Time.time >= impact - ReactionSeconds && Time.time <= impact + 0.12f;
        public float ImpactAt => impact;
        public float FlashAt => impact - ReactionSeconds;
        private bool shown;
        private readonly System.Collections.Generic.Queue<(float impact, float range)> pending = new();

        public void Configure(DragonBossCombat owner, Health player) { boss = owner; target = player; }
        public void Arm(float when, float radius)
        {
            if (Armed && when > impact + 0.25f) { pending.Enqueue((when, radius)); return; }
            impact = when; range = radius; Armed = true; shown = false;
        }
        public void Clear() { Armed = false; pending.Clear(); }

        public bool TryParry(Health player)
        {
            if ((boss.Pattern != DragonPattern.Circle && boss.Pattern != DragonPattern.BodySweep) || !Flashing || player != target || player.IsDead || boss.Phase == DragonPhase.Dead ||
                Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up).magnitude > range) return false;
            Armed = false;
            boss.ResolveParry();
            return true;
        }

        private void Update()
        {
            if (Flashing && !shown) { shown = true; BossJustGuardVfx.Cue(boss.transform); }
            if (Time.time > impact + 0.15f)
            {
                Armed = false;
                if (pending.Count > 0) { var next = pending.Dequeue(); Arm(next.impact, next.range); }
            }
        }
    }
}
