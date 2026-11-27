using System;
using Nomad.Combat;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    [RequireComponent(typeof(Health))]
    public sealed class BossArmor : MonoBehaviour, ICombatDamageModifier
    {
        public const int ReferenceHealth = 4200;
        public int BreakCount { get; private set; }
        public const float DurabilityPerLayer = 1450;
        public float Durability { get; private set; } = DurabilityPerLayer;
        public float RemainingRatio => BreakCount >= 2 ? 0 : Durability / DurabilityPerLayer;
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
            if (health.IsDead || kind != DamageKind.Combat) return;
            if (health.LastHitSource == CombatImpactSource.ParriedFireball || health.LastHitSource == CombatImpactSource.ParriedSlam)
                DamageArmor(DurabilityPerLayer * 0.45f);
            else if (health.LastHitSource == CombatImpactSource.Basic) DamageArmor(Mathf.Min(12, amount / DamageMultiplier * 0.15f));
        }

        public void DamageArmor(float amount)
        {
            if (health.IsDead || BreakCount >= 2 || amount <= 0) return;
            Durability = Mathf.Max(0, Durability - amount);
            if (Durability <= 0)
            {
                BreakCount++;
                Durability = BreakCount < 2 ? DurabilityPerLayer : 0;
                Broken?.Invoke(BreakCount);
            }
        }
    }
}
