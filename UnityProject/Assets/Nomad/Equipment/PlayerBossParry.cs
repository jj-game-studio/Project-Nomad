using Nomad.Combat;
using Nomad.CombatFeedback;
using Nomad.Monsters;
using Nomad.Monsters.Demo;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Equipment
{
    [RequireComponent(typeof(PlayerEquipmentCombat))]
    public sealed class PlayerBossParry : MonoBehaviour
    {
        public const float Window = 0.65f;
        public const float Cooldown = 0.75f;
        private float until, next;
        private bool consumed;
        private PlayerEquipmentCombat combat;
        public int Reflections { get; private set; }
        public int Successes { get; private set; }
        public float PressedAt { get; private set; } = float.NegativeInfinity;
        public bool IsReady => Time.time >= next;
        public bool Active => !consumed && Time.time < until && combat.CanParry;

        private void Awake() => combat = GetComponent<PlayerEquipmentCombat>();
        public bool TryBegin()
        {
            if (!combat.CanParry || !IsReady) return false;
            combat.InterruptForParry();
            consumed = false;
            PressedAt = Time.time;
            float hold = Window;
            foreach (BossParrySignal signal in FindObjectsByType<BossParrySignal>(FindObjectsSortMode.None))
                if (signal.Flashing && signal.GetComponent<DragonBossCombat>().Pattern == DragonPattern.RadialFireballs)
                {
                    var boss = signal.GetComponent<DragonBossCombat>();
                    // 느린 탄속/긴 사거리로 디버그 조정해도 신호를 보고 누른 가드를 탄 도달까지 유지합니다.
                    float travel = (boss.Tuning.RadialRange - 3) / (boss.Tuning.RadialSpeed * boss.Tempo);
                    hold = Mathf.Max(hold, signal.ImpactAt - Time.time + travel + 0.1f);
                }
            until = Time.time + hold;
            next = Time.time + Cooldown;
            return true;
        }

        private void Update()
        {
            if (Keyboard.current?.fKey.wasPressedThisFrame == true) TryBegin();
            if (!Active) return;

            foreach (BossParrySignal signal in FindObjectsByType<BossParrySignal>(FindObjectsSortMode.None))
                if (signal.TryParry(GetComponent<Health>(), PressedAt))
                {
                    if (signal.GetComponent<DragonBossCombat>().Pattern == DragonPattern.RadialFireballs) Reflections++;
                    Succeed(transform.position + Vector3.up * 1.3f);
                    return;
                }

            BossFireball nearest = null;
            float distance = float.PositiveInfinity;
            foreach (BossFireball ball in FindObjectsByType<BossFireball>(FindObjectsSortMode.None))
            {
                if (!ball.Parryable || ball.Target != GetComponent<Health>()) continue;
                float candidate = Vector3.Distance(ball.transform.position, transform.position + Vector3.up * 1.3f);
                if (candidate < distance) { nearest = ball; distance = candidate; }
            }
            if (nearest != null) TryReflect(nearest, nearest.transform.position);
        }

        public bool TryReflect(BossFireball ball, Vector3 point)
        {
            if (!Active || !ball.Parryable || ball.Target != GetComponent<Health>()) return false;
            Vector3 delta = point - (transform.position + Vector3.up * 1.3f);
            if (delta.magnitude > ball.Radius + 1.6f || Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(delta, Vector3.up)) > 105) return false;
            if (!ball.Reflect(transform)) return false;

            Reflections++;
            Succeed(point);
            return true;
        }

        private void Succeed(Vector3 point)
        {
            consumed = true;
            Successes++;
            BossJustGuardVfx.Success(transform, point);
            BossParrySlowMotion.Pulse();
            HitCameraImpulse.Pulse(0.13f, 0.12f);
            CombatImpactAudio.Play(GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>(), true, 0.25f);
        }

        private void OnDisable() { until = 0; consumed = true; }
    }
}
