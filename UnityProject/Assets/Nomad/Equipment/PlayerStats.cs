using System;
using Nomad.Combat;
using Nomad.Inventory;
using Nomad.Player;
using UnityEngine;

namespace Nomad.Equipment
{
    // 기본값과 장착 장비를 합산한 플레이어 스탯입니다. 장비가 바뀌면 체력·마나·스태미나·이동에 반영합니다.
    // 수치는 임시값이며 밸런스는 #160에서 사람이 판단합니다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerEquipment))]
    public sealed class PlayerStats : MonoBehaviour
    {
        public const float CritMultiplier = 1.5f;
        public const float MaxEvasion = 60f;

        [SerializeField, Range(0, 100)] private float baseEvasion;
        [SerializeField, Range(0, 100)] private float baseCritChance = 5f;
        [SerializeField, Range(0, 100)] private float baseCritEvasion;
        [SerializeField, Min(0)] private float baseHealthRegen;
        // 받는 공격의 기본 치명타 확률입니다. 치명타 회피력만큼 줄어듭니다.
        [SerializeField, Range(0, 100)] private float incomingCritChance = 10f;

        private PlayerEquipment equipment;
        private PlayerEquipmentCombat combat;
        private PlayerInventory inventory;
        private Health health;
        private PlayerMana mana;
        private PlayerStamina stamina;
        private PlayerMovement movement;
        private float regenProgress;

        public event Action Changed;
        // 0 이상 1 미만의 난수입니다. 검증에서 치명타·회피를 고정할 때 바꿉니다.
        public Func<float> RandomSource { get; set; }
        public float IncomingCritChance => Mathf.Max(0, incomingCritChance - Get(StatType.CritEvasion));

        private void Awake()
        {
            equipment = GetComponent<PlayerEquipment>();
            TryGetComponent(out combat);
            TryGetComponent(out inventory);
            TryGetComponent(out health);
            TryGetComponent(out mana);
            TryGetComponent(out stamina);
            TryGetComponent(out movement);
        }

        private void OnEnable()
        {
            equipment.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            equipment.Changed -= Apply;
        }

        private void Update()
        {
            float regen = Get(StatType.HealthRegen);
            if (health == null || health.IsDead || regen <= 0 || health.CurrentHealth >= health.MaxHealth)
            {
                regenProgress = 0;
                return;
            }

            // 정수 체력에 맞춰 1 이상 쌓인 만큼만 회복합니다.
            regenProgress += regen * Time.deltaTime;
            int amount = Mathf.FloorToInt(regenProgress);
            if (amount > 0)
            {
                health.Heal(amount);
                regenProgress -= amount;
            }
        }

        public float Get(StatType stat)
        {
            float bonus = equipment.Sum(stat);
            switch (stat)
            {
                case StatType.Attack:
                    ItemDefinition weapon = combat != null ? combat.CurrentWeapon : equipment.ActiveWeapon;
                    return (weapon != null ? weapon.AttackPower : 0) + bonus;
                case StatType.Evasion: return Mathf.Clamp(baseEvasion + bonus, 0, MaxEvasion);
                case StatType.MoveSpeed: return Mathf.Max(10f, 100f + bonus);
                case StatType.MaxHealth: return (health != null ? health.BaseMaxHealth : 0) + bonus;
                case StatType.MaxMana: return (mana != null ? mana.BaseMaximum : 0) + bonus;
                case StatType.MaxStamina: return (stamina != null ? stamina.BaseMaximum : 0) + bonus;
                case StatType.HealthRegen: return Mathf.Max(0, baseHealthRegen + bonus);
                case StatType.ManaRegen: return Mathf.Max(0, (mana != null ? mana.BaseRecoveryPerSecond : 0) + bonus);
                case StatType.StaminaRegen: return Mathf.Max(0, (stamina != null ? stamina.BaseRecoveryPerSecond : 0) + bonus);
                case StatType.CritChance: return Mathf.Clamp(baseCritChance + bonus, 0, 100);
                case StatType.CritEvasion: return Mathf.Clamp(baseCritEvasion + bonus, 0, 100);
                case StatType.InventorySlots: return inventory != null ? inventory.SlotCount : PlayerInventory.Capacity;
                default: return bonus;
            }
        }

        // 내 공격의 피해입니다. 무기 공격력에 장비 공격력을 더하고 치명타면 1.5배로 올림합니다.
        public int RollOutgoingDamage(ItemDefinition weapon, out bool critical)
        {
            int damage = Mathf.Max(1, weapon.AttackPower + Mathf.RoundToInt(equipment.Sum(StatType.Attack)));
            critical = Roll() < Get(StatType.CritChance) / 100f;
            return critical ? Mathf.CeilToInt(damage * CritMultiplier) : damage;
        }

        // 받는 피해입니다. 회피하면 0, 아니면 치명타 판정 뒤 방어력을 빼고 최소 1을 남깁니다.
        public int ModifyIncomingDamage(int amount)
        {
            if (Roll() < Get(StatType.Evasion) / 100f)
                return 0;

            if (Roll() < IncomingCritChance / 100f)
                amount = Mathf.CeilToInt(amount * CritMultiplier);
            return Mathf.Max(1, amount - Mathf.RoundToInt(Get(StatType.Defense)));
        }

        private float Roll() => RandomSource != null ? RandomSource() : UnityEngine.Random.value;

        private void Apply()
        {
            if (health != null)
                health.SetMaxHealthBonus(Mathf.RoundToInt(equipment.Sum(StatType.MaxHealth)));
            if (mana != null)
                mana.SetBonus(Mathf.RoundToInt(equipment.Sum(StatType.MaxMana)), equipment.Sum(StatType.ManaRegen));
            if (stamina != null)
                stamina.SetBonus(Mathf.RoundToInt(equipment.Sum(StatType.MaxStamina)), equipment.Sum(StatType.StaminaRegen));
            if (movement != null)
                movement.StatSpeedMultiplier = Get(StatType.MoveSpeed) / 100f;

            Changed?.Invoke();
        }
    }
}
