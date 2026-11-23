using System;
using UnityEngine;

namespace Nomad.Combat
{
    public enum DamageKind
    {
        Combat,
        Fall
    }

    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 100;

        public int MaxHealth => maxHealth;
        public int CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth == 0;
        public bool IsCombatImmune { get; set; }

        public event Action<Health> Died;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        // 살아 있을 때만 최대 체력까지 회복합니다. 실제로 회복했으면 true입니다.
        public bool RestoreFull()
        {
            if (IsDead || CurrentHealth == maxHealth)
                return false;

            CurrentHealth = maxHealth;
            return true;
        }

        public bool TryApplyDamage(int amount, DamageKind kind = DamageKind.Combat)
        {
            if (amount <= 0 || IsDead || (kind == DamageKind.Combat && IsCombatImmune))
                return false;

            if (kind == DamageKind.Combat && TryGetComponent(out ICombatDamageModifier modifier))
                amount = Mathf.Max(1, modifier.ModifyCombatDamage(amount));

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            if (IsDead)
                Died?.Invoke(this);

            return true;
        }
    }
}
