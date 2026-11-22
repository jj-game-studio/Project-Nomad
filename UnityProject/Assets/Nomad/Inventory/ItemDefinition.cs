using UnityEngine;

namespace Nomad.Inventory
{
    public enum EquipmentType
    {
        None, Weapon, Head, Body, Face, Accessory, Ring, Bag, Totem
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
        [SerializeField] private EquipmentType equipmentType;
        [SerializeField, Min(0)] private int attackPower;
        [SerializeField, Min(0)] private int defense;
        [SerializeField] private WeaponAttackMode attackMode;
        [SerializeField, Min(0)] private float attackRange;
        [SerializeField, Min(0)] private float attackInterval;
        [SerializeField, Min(0)] private int manaCost;
        [SerializeField] private GameObject equippedVisual;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public int MaxStack => maxStack;
        public EquipmentType EquipmentType => equipmentType;
        public int AttackPower => attackPower;
        public int Defense => defense;
        public WeaponAttackMode AttackMode => attackMode;
        public float AttackRange => attackRange;
        public float AttackInterval => attackInterval;
        public int ManaCost => manaCost;
        public GameObject EquippedVisual => equippedVisual;
    }
}
