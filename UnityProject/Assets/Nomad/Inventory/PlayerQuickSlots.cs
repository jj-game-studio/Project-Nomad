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
        public const int Capacity = 6;
        // 1~2번은 무기 전용, 3~6번은 전투 중 사용하는 소모품 전용입니다.
        public const int WeaponSlotCount = 2;

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
            else if (keyboard.digit5Key.wasPressedThisFrame) TryUse(4);
            else if (keyboard.digit6Key.wasPressedThisFrame) TryUse(5);
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

        public static bool IsWeaponSlot(int index) => index < WeaponSlotCount;

        // 빈 칸(null)은 어느 칸에나 둘 수 있어 교환 검사에 그대로 씁니다.
        public static bool Accepts(int index, ItemDefinition item)
        {
            if ((uint)index >= Capacity)
                return false;
            if (item == null)
                return true;

            return IsWeaponSlot(index) ? item.EquipmentType == EquipmentType.Weapon : IsCombatConsumable(item);
        }

        // 현재 전투 중 "사용"이 구현된 소모품은 회복약뿐입니다. 새 소모품은 여기에 추가합니다.
        public static bool IsCombatConsumable(ItemDefinition item) =>
            item != null && item.EquipmentType == EquipmentType.None && item.ItemId == PlayerHealing.PotionId;

        internal void ReplaceEnhanced(ItemDefinition previous, ItemDefinition replacement)
        {
            for (int index = 0; index < Capacity; index++)
                if (items[index] == previous) items[index] = replacement;
        }

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
            return RegisterItem(item, quickSlot);
        }

        public bool RegisterEquipped(EquipmentSlot equipmentSlot, int quickSlot)
        {
            return RegisterItem(equipment != null ? equipment.GetItem(equipmentSlot) : null, quickSlot);
        }

        private bool RegisterItem(ItemDefinition item, int quickSlot)
        {
            if (item == null || !Accepts(quickSlot, item))
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

        public bool Move(int source, int target)
        {
            if ((uint)source >= Capacity || (uint)target >= Capacity || source == target || items[source] == null)
                return false;

            // 무기 칸과 소모품 칸 사이로는 옮기거나 교환할 수 없습니다.
            if (!Accepts(target, items[source]) || !Accepts(source, items[target]))
                return false;

            (items[source], items[target]) = (items[target], items[source]);
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
