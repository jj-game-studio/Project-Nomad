using UnityEngine;

namespace Nomad.Inventory
{
    // 직렬화된 값을 유지하도록 번호를 고정합니다. 4·6은 이전 얼굴·반지 자리를 바지·신발로 바꿨습니다.
    public enum EquipmentType
    {
        None = 0, Weapon = 1, Head = 2, Body = 3, Legs = 4, Accessory = 5, Feet = 6, Bag = 7
    }

    public enum WeaponAttackMode { None, Ranged, Melee, Bow, Staff }

    [CreateAssetMenu(menuName = "Nomad/Inventory/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(1)] private int maxStack = 99;
        [SerializeField] private ItemTier tier;
        [SerializeField] private EquipmentType equipmentType;
        [SerializeField, Min(0)] private int attackPower;
        [SerializeField, Min(0)] private int defense;
        [SerializeField] private WeaponAttackMode attackMode;
        [SerializeField, Min(0)] private float attackRange;
        [SerializeField, Min(0)] private float attackInterval;
        [SerializeField, Min(0)] private int manaCost;
        [SerializeField] private StatModifier[] stats = System.Array.Empty<StatModifier>();
        [SerializeField] private GameObject equippedVisual;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public int MaxStack => maxStack;
        public ItemTier Tier => tier;
        public EquipmentType EquipmentType => equipmentType;
        public int AttackPower => attackPower;
        public int Defense => defense;
        public WeaponAttackMode AttackMode => attackMode;
        public float AttackRange => attackRange;
        public float AttackInterval => attackInterval;
        public int ManaCost => manaCost;
        public StatModifier[] Stats => stats ?? System.Array.Empty<StatModifier>();
        public GameObject EquippedVisual => equippedVisual;

        // 방어력은 기존 열을, 나머지 스탯은 추가 스탯 목록을 합산합니다. 공격력은 무기 수치를 따로 읽습니다.
        public float GetStat(StatType stat)
        {
            float total = stat == StatType.Defense ? defense : 0;
            foreach (StatModifier modifier in Stats)
                if (modifier.Stat == stat)
                    total += modifier.Value;
            return total;
        }
    }
}
