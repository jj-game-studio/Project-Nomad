using System;
using Nomad.Combat;
using Nomad.Inventory;
using UnityEngine;

namespace Nomad.Equipment
{
    // 장비 UI의 4×2 배치 순서입니다. 무기는 장비칸 없이 가방에 둔 채 퀵바 1~2번으로 고릅니다.
    public enum EquipmentSlot
    {
        Head, Body, Legs, Feet, Bag, Accessory1, Accessory2, Accessory3
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInventory))]
    public sealed partial class PlayerEquipment : MonoBehaviour, ICombatDamageModifier
    {
        public const int Capacity = 8;

        private PlayerInventory inventory;
        private PlayerStats stats;
        private PlayerQuickSlots quickSlots;
        private ItemDefinition shownWeapon;
        private readonly ItemDefinition[] items = new ItemDefinition[Capacity];
        private bool changing;

        public event Action Changed;
        // 선택한 퀵바 무기 칸 번호(0~1)입니다. 손에 든 무기는 이 칸에 등록된 가방 무기입니다.
        public int ActiveWeaponIndex { get; private set; }
        public ItemDefinition ActiveWeapon =>
            quickSlots != null && quickSlots.isActiveAndEnabled && quickSlots.GetCount(ActiveWeaponIndex) > 0
                ? quickSlots.GetItem(ActiveWeaponIndex) : null;

        public int TotalDefense => Mathf.RoundToInt(Sum(StatType.Defense));

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            quickSlots = GetComponent<PlayerQuickSlots>();
        }

        private void OnEnable()
        {
            if (quickSlots != null) quickSlots.Changed += QuickSlotsChanged;
        }

        // 퀵바가 나중에 붙어도(검토 씬) 손에 든 무기를 따라가도록 연결합니다.
        internal void Bind(PlayerQuickSlots slots)
        {
            if (quickSlots == slots)
                return;

            if (quickSlots != null) quickSlots.Changed -= QuickSlotsChanged;
            quickSlots = slots;
            if (isActiveAndEnabled) quickSlots.Changed += QuickSlotsChanged;
        }

        private void OnDisable()
        {
            if (quickSlots != null) quickSlots.Changed -= QuickSlotsChanged;
        }

        // 퀵바 등록·가방 변화로 손에 든 무기가 바뀌었을 때만 장비 변경을 알립니다.
        private void QuickSlotsChanged()
        {
            if (!changing && ActiveWeapon != shownWeapon)
                Publish(false);
        }

        // 장착 장비의 스탯 합계입니다. 공격력은 선택한 무기에서 따로 읽습니다.
        public float Sum(StatType stat)
        {
            float total = 0;
            for (int index = 0; index < Capacity; index++)
                if (items[index] != null)
                    total += items[index].GetStat(stat);
            return total;
        }

        public int ModifyCombatDamage(int amount)
        {
            if (!isActiveAndEnabled)
                return amount;

            // 스탯 구성요소가 있으면 회피·치명타까지 계산하고, 없으면 기존 고정 방어력만 차감합니다.
            if (stats == null)
                TryGetComponent(out stats);
            if (stats != null && stats.isActiveAndEnabled)
                return stats.ModifyIncomingDamage(amount);
            return Math.Max(1, amount - TotalDefense);
        }

        public ItemDefinition GetItem(EquipmentSlot slot)
        {
            return (uint)slot < Capacity ? items[(int)slot] : null;
        }

        public bool SelectWeapon(int quickSlot)
        {
            if (!isActiveAndEnabled || changing || !PlayerQuickSlots.IsWeaponSlot(quickSlot) || quickSlot < 0)
                return false;

            if (ActiveWeaponIndex != quickSlot)
            {
                ActiveWeaponIndex = quickSlot;
                Publish(false);
            }
            return true;
        }

        // 가방 칸의 무기를 퀵바 무기 칸에 등록하고 바로 손에 듭니다.
        public bool EquipWeapon(int inventorySlot, int quickSlot = 0)
        {
            return isActiveAndEnabled && quickSlots != null && quickSlots.isActiveAndEnabled &&
                PlayerQuickSlots.IsWeaponSlot(quickSlot) && quickSlots.Register(inventorySlot, quickSlot) && SelectWeapon(quickSlot);
        }

        public bool TryEquip(int inventorySlot, EquipmentSlot slot)
        {
            if (!isActiveAndEnabled || changing || (uint)inventorySlot >= inventory.SlotCount || (uint)slot >= Capacity)
                return false;

            PlayerInventory.ItemStack stack = inventory.GetSlot(inventorySlot);
            if (stack.Item == null || stack.Count != 1 || stack.Item.MaxStack != 1 || !Fits(stack.Item.EquipmentType, slot))
                return false;

            // 더 작은 가방으로 바꿀 때는 줄어드는 칸이 비어 있어야 합니다. 이전 장비는 같은 칸으로 돌아옵니다.
            ItemDefinition previous = items[(int)slot];
            int slotCount = SlotCountWith(slot, stack.Item);
            if (!inventory.CanResize(slotCount, previous == null ? inventorySlot : -1) || (previous != null && inventorySlot >= slotCount))
                return false;

            inventory.RemoveFromSlotForTransfer(inventorySlot, 1);
            items[(int)slot] = stack.Item;
            if (previous != null)
                inventory.AddToSlotForTransfer(inventorySlot, previous, 1);

            Publish(true);
            return true;
        }

        // 더블클릭 장착용입니다. 가방 칸의 장비를 알맞은 장비칸에 넣고, 이전 장비는 같은 가방 칸으로 돌아옵니다.
        // 무기는 가방에 둔 채 퀵바 무기 칸에 등록합니다.
        public bool TryEquipAuto(int inventorySlot)
        {
            if (inventory == null || (uint)inventorySlot >= inventory.SlotCount)
                return false;

            ItemDefinition item = inventory.GetSlot(inventorySlot).Item;
            if (item != null && item.EquipmentType == EquipmentType.Weapon)
                return isActiveAndEnabled && quickSlots != null && quickSlots.RegisterWeapon(item, ActiveWeaponIndex);
            return item != null && TryGetAutoSlot(item.EquipmentType, out EquipmentSlot slot) && TryEquip(inventorySlot, slot);
        }

        // 장신구는 빈 1→2→3, 모두 차 있으면 장신구1과 교체합니다. 무기는 장비칸이 없습니다.
        public bool TryGetAutoSlot(EquipmentType type, out EquipmentSlot slot)
        {
            switch (type)
            {
                case EquipmentType.Accessory:
                    slot = FirstEmpty(EquipmentSlot.Accessory1, EquipmentSlot.Accessory3, EquipmentSlot.Accessory1);
                    return true;
                case EquipmentType.Head: slot = EquipmentSlot.Head; return true;
                case EquipmentType.Body: slot = EquipmentSlot.Body; return true;
                case EquipmentType.Legs: slot = EquipmentSlot.Legs; return true;
                case EquipmentType.Feet: slot = EquipmentSlot.Feet; return true;
                case EquipmentType.Bag: slot = EquipmentSlot.Bag; return true;
                default: slot = default; return false;
            }
        }

        private EquipmentSlot FirstEmpty(EquipmentSlot first, EquipmentSlot last, EquipmentSlot fallback)
        {
            for (EquipmentSlot slot = first; slot <= last; slot++)
                if (items[(int)slot] == null)
                    return slot;
            return fallback;
        }

        public bool TryUnequip(EquipmentSlot slot, int inventorySlot = -1)
        {
            if (!isActiveAndEnabled || changing || (uint)slot >= Capacity || items[(int)slot] == null)
                return false;

            // 가방을 벗으면 칸이 줄어듭니다. 줄어든 칸 안의 빈 자리로만 옮깁니다.
            int slotCount = SlotCountWith(slot, null);
            if (inventorySlot < -1 || inventorySlot >= slotCount || !inventory.CanResize(slotCount))
                return false;

            for (int index = 0; index < slotCount; index++)
            {
                if (inventorySlot >= 0 && index != inventorySlot)
                    continue;
                if (inventory.GetSlot(index).Item != null)
                    continue;

                ItemDefinition removed = items[(int)slot];
                items[(int)slot] = null;
                inventory.TryResize(slotCount);
                inventory.AddToSlotForTransfer(index, removed, 1);
                Publish(true);
                return true;
            }
            return false;
        }

        public static bool Fits(EquipmentType type, EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return type == EquipmentType.Head;
                case EquipmentSlot.Body: return type == EquipmentType.Body;
                case EquipmentSlot.Legs: return type == EquipmentType.Legs;
                case EquipmentSlot.Feet: return type == EquipmentType.Feet;
                case EquipmentSlot.Bag: return type == EquipmentType.Bag;
                case EquipmentSlot.Accessory1:
                case EquipmentSlot.Accessory2:
                case EquipmentSlot.Accessory3: return type == EquipmentType.Accessory;
                default: return false;
            }
        }

        // slot의 장비를 replacement로 바꿨을 때의 가방 칸 수입니다.
        private int SlotCountWith(EquipmentSlot slot, ItemDefinition replacement)
        {
            float bonus = Sum(StatType.InventorySlots);
            if (items[(int)slot] != null)
                bonus -= items[(int)slot].GetStat(StatType.InventorySlots);
            if (replacement != null)
                bonus += replacement.GetStat(StatType.InventorySlots);
            return SlotCount(bonus);
        }

        private static int SlotCount(float bonus) =>
            Mathf.Clamp(PlayerInventory.Capacity + Mathf.RoundToInt(bonus), 1, PlayerInventory.MaxCapacity);

        private void Publish(bool inventoryChanged)
        {
            // 양쪽 상태가 완성된 뒤 알립니다. 알림 도중의 재진입으로 이중 전송하지 않습니다.
            changing = true;
            try
            {
                if (inventoryChanged)
                {
                    inventory.TryResize(SlotCount(Sum(StatType.InventorySlots)));
                    inventory.NotifyChanged();
                }
                shownWeapon = ActiveWeapon;
                Changed?.Invoke();
            }
            finally
            {
                changing = false;
            }
        }
    }
}
