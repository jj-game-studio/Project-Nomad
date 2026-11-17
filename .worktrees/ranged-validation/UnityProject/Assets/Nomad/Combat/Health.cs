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

        public bool TryApplyDamage(int amount, DamageKind kind = DamageKind.Combat)
        {
            if (amount <= 0 || IsDead || (kind == DamageKind.Combat && IsCombatImmune))
                return false;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            if (IsDead)
                Died?.Invoke(this);

            return true;
        }
    }
}
