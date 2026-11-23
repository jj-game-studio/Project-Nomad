using System;
using System.Globalization;

namespace Nomad.Inventory
{
    // 플레이어 스탯 13종입니다. 장비의 추가 스탯과 스탯 창이 같은 순서·이름을 사용합니다.
    public enum StatType
    {
        Attack, Defense, Evasion, MoveSpeed, MaxHealth, MaxMana, MaxStamina,
        HealthRegen, ManaRegen, StaminaRegen, CritChance, CritEvasion, InventorySlots
    }

    [Serializable]
    public struct StatModifier
    {
        public StatType Stat;
        public float Value;

        public StatModifier(StatType stat, float value)
        {
            Stat = stat;
            Value = value;
        }
    }

    public static class StatFormat
    {
        private static readonly string[] Names =
        {
            "공격력", "방어력", "회피력", "이동속도", "최대 체력", "최대 마나", "최대 스태미나",
            "체력 재생", "마나 재생", "스태미나 재생", "치명타 확률", "치명타 회피력", "인벤토리 용량"
        };

        public static string Name(StatType stat) => Names[(int)stat];

        // 회피·이동속도·치명타는 %, 재생은 초당 수치, 가방은 칸 수로 표시합니다.
        public static string Value(StatType stat, float value)
        {
            switch (stat)
            {
                case StatType.Evasion:
                case StatType.MoveSpeed:
                case StatType.CritChance:
                case StatType.CritEvasion:
                    return Number(value) + "%";
                case StatType.HealthRegen:
                case StatType.ManaRegen:
                case StatType.StaminaRegen:
                    return Number(value) + "/초";
                case StatType.InventorySlots:
                    return Number(value) + "칸";
                default:
                    return Number(value);
            }
        }

        public static string Bonus(StatType stat, float value) => (value >= 0 ? "+" : "-") + Value(stat, Math.Abs(value));

        public static string Number(float value) =>
            Math.Abs(value - Math.Round(value)) < 0.05f
                ? Math.Round(value).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.0", CultureInfo.InvariantCulture);
    }
}

namespace Nomad.Inventory
{
    // 기획자가 붙이는 아이템 등급입니다. 오른쪽일수록 높고, None은 화살처럼 등급이 없는 아이템입니다.
    public enum ItemTier { None, C, B, A, S, SS, SSS }

    public static class TierStyle
    {
        private static readonly UnityEngine.Color32[] Colors =
        {
            new(101, 107, 103, 255), // None: 기존 칸 테두리
            new(168, 172, 170, 255), // C 회색
            new(98, 196, 112, 255),  // B 초록
            new(84, 150, 235, 255),  // A 파랑
            new(176, 108, 232, 255), // S 보라
            new(240, 150, 56, 255),  // SS 주황
            new(238, 74, 84, 255),   // SSS 빨강
        };

        public static UnityEngine.Color Color(ItemTier tier) => Colors[(int)tier];

        public static string Hex(ItemTier tier) => UnityEngine.ColorUtility.ToHtmlStringRGB(Color(tier));

        public static string Label(ItemTier tier) => tier == ItemTier.None ? string.Empty : tier.ToString();
    }
}
