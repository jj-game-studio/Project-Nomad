using System;
using UnityEngine;

namespace Nomad.Combat
{
    public enum DamageKind
    {
        Combat,
        Fall
    }

    public enum CombatImpactSource { Basic, Skill, ParriedFireball, ParriedSlam }

    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 100;

        private int maxHealthBonus;

        public int BaseMaxHealth => maxHealth;
        public int MaxHealth => Mathf.Max(1, maxHealth + maxHealthBonus);
        public int CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth == 0;
        public bool IsCombatImmune { get; set; }
        // 구르기처럼 한 프레임마다 갱신되는 무적과 독립적인 디버그 무적입니다.
        public bool IsInvulnerable { get; set; }
        public bool LastHitCritical { get; private set; }
        public CombatImpactSource LastHitSource { get; private set; }

        public event Action<Health> Died;
        public event Action<Health> Evaded;
        public event Action<Health, int, DamageKind> Damaged;

        private void Awake()
        {
            CurrentHealth = MaxHealth;
        }

        // 장비로 바뀐 최대 체력입니다. 늘어나도 현재 체력은 그대로, 줄어들면 새 최대치로 맞춥니다.
        public void SetMaxHealthBonus(int bonus)
        {
            maxHealthBonus = bonus;
            CurrentHealth = Mathf.Min(CurrentHealth, MaxHealth);
        }

        // 살아 있을 때만 최대 체력까지 회복합니다. 실제로 회복했으면 true입니다.
        public bool Heal(int amount)
        {
            if (amount <= 0 || IsDead || CurrentHealth >= MaxHealth)
                return false;

            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            return true;
        }

        // 살아 있을 때만 최대 체력까지 회복합니다. 실제로 회복했으면 true입니다.
        public bool RestoreFull()
        {
            if (IsDead || CurrentHealth == MaxHealth)
                return false;

            CurrentHealth = MaxHealth;
            return true;
        }

        public bool TryApplyDamage(int amount, DamageKind kind = DamageKind.Combat) => ApplyDamage(amount, kind, false, CombatImpactSource.Basic);

        public bool TryApplyCombatDamage(int amount, bool critical) => ApplyDamage(amount, DamageKind.Combat, critical, CombatImpactSource.Basic);

        public bool TryApplyCombatImpact(int amount, bool critical, CombatImpactSource source) => ApplyDamage(amount, DamageKind.Combat, critical, source);

        private bool ApplyDamage(int amount, DamageKind kind, bool critical, CombatImpactSource source)
        {
            if (amount <= 0 || IsDead || IsInvulnerable || (kind == DamageKind.Combat && IsCombatImmune))
                return false;

            // 보정 결과가 0 이하이면 회피한 공격으로 보고 피해를 적용하지 않습니다.
            if (kind == DamageKind.Combat && TryGetComponent(out ICombatDamageModifier modifier))
            {
                amount = modifier.ModifyCombatDamage(amount);
                if (amount <= 0)
                {
                    Evaded?.Invoke(this);
                    return false;
                }
            }

            int before = CurrentHealth;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            LastHitCritical = critical;
            LastHitSource = source;
            Damaged?.Invoke(this, before - CurrentHealth, kind);
            if (IsDead)
                Died?.Invoke(this);

            return true;
        }
    }
}
