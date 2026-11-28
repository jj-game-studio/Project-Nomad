using Nomad.Combat;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed class BossParrySignal : MonoBehaviour
    {
        public const float ReactionSeconds = 0.65f;
        public const float RadialReactionSeconds = 0.95f;
        public const float SlamReactionSeconds = 0.35f;
        public const float SlamInputWindow = 0.34f;
        public const float LateInputSeconds = 0.18f, DamageGrace = 0.22f;
        public static float SlamLead(DragonBossCombat owner) => Mathf.Min(SlamReactionSeconds,owner.LeapDuration*0.55f);
        private float armSpan;
        public static float BodyLead(float windup) => Mathf.Min(ReactionSeconds,Mathf.Max(0.1f,windup-0.12f));
        private float Lead => boss.Pattern == DragonPattern.RadialFireballs ? RadialReactionSeconds :
            boss.Pattern == DragonPattern.Circle ? SlamLead(boss) : BodyLead(armSpan);
        private DragonBossCombat boss;
        private Health target;
        private float impact, range;
        public bool Armed { get; private set; }
        public bool Flashing => Armed && Time.time >= impact - Lead && Time.time <= impact + LateInputSeconds;
        public float ImpactAt => impact;
        public float FlashAt => impact - Lead;
        public float CueShownAt { get; private set; } = -1;
        private bool shown;
        private readonly System.Collections.Generic.Queue<(float impact, float range)> pending = new();

        public void Configure(DragonBossCombat owner, Health player) { boss = owner; target = player; }
        public void Arm(float when, float radius)
        {
            if (Armed && when > impact + 0.25f) { pending.Enqueue((when, radius)); return; }
            armSpan = when-Time.time;
            impact = when; range = radius; Armed = true; shown = false; CueShownAt = -1;
        }
        public void Clear() { Armed = false; pending.Clear(); }

        public bool TryParry(Health player) => TryParry(player, Time.time);

        public bool TryParry(Health player, float pressedAt)
        {
            // 강타는 하강/착지 직전에 새로 누른 입력만 받습니다. 이전 가드 유지 시간은 넘겨받지 않습니다.
            if (boss.Pattern == DragonPattern.Circle && (boss.Phase != DragonPhase.Active ||
                pressedAt < impact - Mathf.Min(SlamInputWindow,SlamLead(boss)) || pressedAt > impact + LateInputSeconds)) return false;
            if ((boss.Pattern != DragonPattern.Circle && boss.Pattern != DragonPattern.BodySweep) || !Flashing || player != target || player.IsDead || boss.Phase == DragonPhase.Dead ||
                Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up).magnitude > range) return false;
            Armed = false;
            boss.ResolveParry();
            return true;
        }

        private void Update()
        {
            if (Flashing && !shown) { shown = true; CueShownAt = Time.time; BossJustGuardVfx.Cue(boss.transform); }
            if (Time.time > impact + LateInputSeconds + 0.03f)
            {
                Armed = false;
                if (pending.Count > 0) { var next = pending.Dequeue(); Arm(next.impact, next.range); }
            }
        }
    }
}
