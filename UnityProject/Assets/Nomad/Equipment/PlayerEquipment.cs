using System;
using Nomad.Inventory;
using UnityEngine;

namespace Nomad.Equipment
{
    public enum EquipmentSlot
    {
        Weapon1, Weapon2, Head, Body, Bag, Face, Accessory, Ring, Totem1, Totem2
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class PlayerEquipment : MonoBehaviour
    {
        public const int Capacity = 10;

        private PlayerInventory inventory;
        private readonly ItemDefinition[] items = new ItemDefinition[Capacity];
        private bool changing;

        public event Action Changed;
        public EquipmentSlot ActiveWeaponSlot { get; private set; } = EquipmentSlot.Weapon1;
        public ItemDefinition ActiveWeapon => GetItem(ActiveWeaponSlot);

        public int TotalDefense
        {
            get
            {
                long total = 0;
                foreach (ItemDefinition item in items)
                    if (item != null)
                        total += item.Defense;
                return (int)Math.Min(int.MaxValue, total);
            }
        }

        private void Awake() => inventory = GetComponent<PlayerInventory>();

        public ItemDefinition GetItem(EquipmentSlot slot)
        {
            return (uint)slot < Capacity ? items[(int)slot] : null;
        }

        public bool SelectWeapon(EquipmentSlot slot)
        {
            if (!isActiveAndEnabled || changing || (slot != EquipmentSlot.Weapon1 && slot != EquipmentSlot.Weapon2))
                return false;

            if (ActiveWeaponSlot != slot)
            {
                ActiveWeaponSlot = slot;
                Publish(false);
            }
            return true;
        }

        public bool TryEquip(int inventorySlot, EquipmentSlot slot)
        {
            if (!isActiveAndEnabled || changing || (uint)inventorySlot >= PlayerInventory.Capacity || (uint)slot >= Capacity)
                return false;

            PlayerInventory.ItemStack stack = inventory.GetSlot(inventorySlot);
            if (stack.Item == null || stack.Count != 1 || stack.Item.MaxStack != 1 || !Fits(stack.Item.EquipmentType, slot))
                return false;

            ItemDefinition previous = items[(int)slot];
            inventory.RemoveFromSlotForTransfer(inventorySlot, 1);
            items[(int)slot] = stack.Item;
            if (previous != null)
                inventory.AddToSlotForTransfer(inventorySlot, previous, 1);

            Publish(true);
            return true;
        }

        public bool TryUnequip(EquipmentSlot slot)
        {
            if (!isActiveAndEnabled || changing || (uint)slot >= Capacity || items[(int)slot] == null)
                return false;

            for (int index = 0; index < PlayerInventory.Capacity; index++)
            {
                if (inventory.GetSlot(index).Item != null)
                    continue;

                inventory.AddToSlotForTransfer(index, items[(int)slot], 1);
                items[(int)slot] = null;
                Publish(true);
                return true;
            }
            return false;
        }

        public static bool Fits(EquipmentType type, EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Weapon1:
                case EquipmentSlot.Weapon2: return type == EquipmentType.Weapon;
                case EquipmentSlot.Head: return type == EquipmentType.Head;
                case EquipmentSlot.Body: return type == EquipmentType.Body;
                case EquipmentSlot.Face: return type == EquipmentType.Face;
                case EquipmentSlot.Accessory: return type == EquipmentType.Accessory;
                case EquipmentSlot.Ring: return type == EquipmentType.Ring;
                case EquipmentSlot.Bag: return type == EquipmentType.Bag;
                case EquipmentSlot.Totem1:
                case EquipmentSlot.Totem2: return type == EquipmentType.Totem;
                default: return false;
            }
        }

        private void Publish(bool inventoryChanged)
        {
            // 양쪽 상태가 완성된 뒤 알립니다. 알림 도중의 재진입으로 이중 전송하지 않습니다.
            changing = true;
            try
            {
                if (inventoryChanged)
                    inventory.NotifyChanged();
                Changed?.Invoke();
            }
            finally
            {
                changing = false;
            }
        }
    }
}
