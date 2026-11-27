using System;
using Nomad.Combat;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    [RequireComponent(typeof(Health))]
    public sealed class BossArmor : MonoBehaviour, ICombatDamageModifier
    {
        public const int ReferenceHealth = 5200;
        public int BreakCount { get; private set; }
        public float DamageMultiplier => BreakCount == 0 ? 1 : BreakCount == 1 ? 1.25f : 1.55f;
        public event Action<int> Broken;
        private Health health;

        private void Awake() => health = GetComponent<Health>();
        private void OnEnable() => health.Damaged += OnDamaged;
        private void OnDisable() => health.Damaged -= OnDamaged;

        public void Configure(int maximum)
        {
            health.SetMaxHealthBonus(Mathf.Max(1, maximum) - health.BaseMaxHealth);
            health.RestoreFull();
        }

        public int ModifyCombatDamage(int amount) => Mathf.Max(1, Mathf.RoundToInt(amount * DamageMultiplier));

        private void OnDamaged(Health _, int amount, DamageKind kind)
        {
            if (health.IsDead) return;
            float ratio = health.CurrentHealth / (float)health.MaxHealth;
            // 파괴는 피해만 증가시킵니다. 공격 상태·이동·기절 시간은 건드리지 않습니다.
            while (BreakCount < 2 && ratio <= (BreakCount == 0 ? 0.7f : 0.35f))
            {
                BreakCount++;
                Broken?.Invoke(BreakCount);
            }
        }
    }
}
