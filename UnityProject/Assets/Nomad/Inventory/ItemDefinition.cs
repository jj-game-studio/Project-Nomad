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
        public int EnhancementLevel { get; private set; }
        public ItemDefinition BaseItem { get; private set; }
        public string DisplayName => EnhancementLevel > 0 ? $"{displayName} +{EnhancementLevel}" : displayName;
        public string UnlockedSkill => EnhancementLevel < 1 || equipmentType != EquipmentType.Weapon ? string.Empty :
            attackMode == WeaponAttackMode.Bow ? "관통 사격" : attackMode == WeaponAttackMode.Staff ? "폭발 마법" : "전방 충격파";
        public string Description => string.IsNullOrEmpty(UnlockedSkill) ? description : description + "\nC · " + UnlockedSkill + " (재사용 8초)";
        public Sprite Icon => icon;
        public int MaxStack => maxStack;
        public ItemTier Tier => tier;
        public EquipmentType EquipmentType => equipmentType;
        public int AttackPower => Mathf.RoundToInt(attackPower * (1 + EnhancementLevel * 0.15f));
        public int Defense => Mathf.RoundToInt(defense * (1 + EnhancementLevel * 0.15f));
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
            return stat == StatType.InventorySlots ? total : total * (1 + EnhancementLevel * 0.15f);
        }

        // 원본 에셋을 수정하지 않습니다. 이 인스턴스가 장비·가방 사이를 이동하며 강화를 유지합니다.
        internal ItemDefinition CreateEnhanced()
        {
            ItemDefinition copy = Instantiate(BaseItem != null ? BaseItem : this);
            copy.BaseItem = BaseItem != null ? BaseItem : this;
            copy.EnhancementLevel = EnhancementLevel + 1;
            copy.name = DisplayName + " enhanced";
            return copy;
        }
    }
}
