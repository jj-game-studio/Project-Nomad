using System;
using Nomad.Combat;
using Nomad.Consumables;
using Nomad.Equipment;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Inventory
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class PlayerQuickSlots : MonoBehaviour
    {
        public const int Capacity = 4;

        private PlayerInventory inventory;
        private PlayerEquipment equipment;
        private readonly ItemDefinition[] items = new ItemDefinition[Capacity];

        public event Action Changed;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            equipment = GetComponent<PlayerEquipment>();
        }

        private void OnEnable()
        {
            inventory.Changed += Refresh;
            if (equipment != null) equipment.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            inventory.Changed -= Refresh;
            if (equipment != null) equipment.Changed -= Refresh;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || keyboard.tabKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame) TryUse(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) TryUse(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) TryUse(2);
            else if (keyboard.digit4Key.wasPressedThisFrame) TryUse(3);
        }

        public bool TryUse(int index)
        {
            if (!isActiveAndEnabled || (uint)index >= Capacity || items[index] == null || Time.timeScale <= 0 ||
                inventory.BlocksTradeInput || inventory.Panel?.IsOpen == true || inventory.Panel?.BlocksSplitInput == true ||
                GetComponent<PlayerEquipmentCombat>()?.InputBlocked == true ||
                (TryGetComponent(out Health health) && health.IsDead))
                return false;

            ItemDefinition item = items[index];
            if (item.EquipmentType == EquipmentType.None)
                return item.ItemId == PlayerHealing.PotionId && GetComponent<PlayerHealing>()?.TryUse() == true;

            if (equipment == null || !equipment.isActiveAndEnabled)
                return false;

            for (int slot = 0; slot < PlayerEquipment.Capacity; slot++)
            {
                if (equipment.GetItem((EquipmentSlot)slot) != item)
                    continue;
                return item.EquipmentType != EquipmentType.Weapon || equipment.SelectWeapon((EquipmentSlot)slot);
            }

            for (int slot = 0; slot < inventory.SlotCount; slot++)
            {
                if (inventory.GetSlot(slot).Item != item)
                    continue;
                if (!equipment.TryGetAutoSlot(item.EquipmentType, out EquipmentSlot target) || !equipment.TryEquip(slot, target))
                    return false;
                return item.EquipmentType != EquipmentType.Weapon || equipment.SelectWeapon(target);
            }

            return false;
        }

        public ItemDefinition GetItem(int index) => items[index];

        public int GetCount(int index)
        {
            ItemDefinition item = items[index];
            int count = inventory.GetCount(item);
            if (item != null && equipment != null)
            {
                for (int slot = 0; slot < PlayerEquipment.Capacity; slot++)
                    if (equipment.GetItem((EquipmentSlot)slot) == item) count++;
            }
            return count;
        }

        // 가방 칸이 바뀌어도 등록을 유지하도록 위치 대신 아이템 정의를 참조합니다.
        public bool Register(int inventorySlot, int quickSlot)
        {
            if ((uint)inventorySlot >= PlayerInventory.MaxCapacity || (uint)quickSlot >= Capacity)
                return false;

            ItemDefinition item = inventory.GetSlot(inventorySlot).Item;
            if (item == null)
                return false;

            bool changed = items[quickSlot] != item;
            for (int index = 0; index < Capacity; index++)
            {
                if (index != quickSlot && items[index] == item)
                {
                    items[index] = null;
                    changed = true;
                }
            }

            // 이전 등록 제거와 새 위치 반영을 마친 뒤 완성된 상태만 알립니다.
            items[quickSlot] = item;
            if (changed)
                Changed?.Invoke();

            return true;
        }

        public bool Unregister(int index)
        {
            if ((uint)index >= Capacity || items[index] == null)
                return false;

            items[index] = null;
            Changed?.Invoke();
            return true;
        }

        private void Refresh()
        {
            for (int index = 0; index < Capacity; index++)
            {
                if (items[index] != null && GetCount(index) == 0)
                    items[index] = null;
            }

            Changed?.Invoke();
        }
    }
}
