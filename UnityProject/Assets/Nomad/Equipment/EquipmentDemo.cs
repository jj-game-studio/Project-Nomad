using Nomad.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Equipment
{
    // 정식 조작 배치가 아닌 전용 장비 검토 씬의 입력입니다.
    public sealed class EquipmentDemo : MonoBehaviour
    {
        [SerializeField] private ItemDefinition[] items;
        private PlayerEquipment equipment;
        private PlayerInventory inventory;

        private void Start()
        {
            inventory = GetComponent<PlayerInventory>();
            equipment = GetComponent<PlayerEquipment>();
            equipment.Changed += LogEquipment;
            foreach (ItemDefinition item in items)
                if (item.ItemId != "demo-unarmed")
                    inventory.Add(item.ItemId, 1);
            Equip("demo-sword", EquipmentSlot.Weapon1);
            Equip("demo-bow", EquipmentSlot.Weapon2);
            InventoryPanel.GetOrCreate(inventory);
            Debug.Log("EQUIPMENT_DEMO_READY: LMB 공격, 1 검/2 활, F5 몸통 장착/해제, F6 선택 무기 해제, F7 검 재장착, F8 활 재장착. 장비 수치·상태는 Console/API로 확인. Tab은 가방.");
        }

        private void OnDestroy()
        {
            if (equipment != null)
                equipment.Changed -= LogEquipment;
        }

        private void LogEquipment()
        {
            Debug.Log($"EQUIPMENT_CHANGED: 선택={equipment.ActiveWeaponSlot}, 무기={equipment.ActiveWeapon?.DisplayName ?? "맨손"}, 방어력={equipment.TotalDefense}, 가방={inventory.UsedSlots}/{PlayerInventory.Capacity}");
        }

        private void Update()
        {
            Keyboard keys = Keyboard.current;
            if (keys == null)
                return;

            if (keys.digit1Key.wasPressedThisFrame) equipment.SelectWeapon(EquipmentSlot.Weapon1);
            if (keys.digit2Key.wasPressedThisFrame) equipment.SelectWeapon(EquipmentSlot.Weapon2);
            if (keys.f5Key.wasPressedThisFrame)
            {
                if (equipment.GetItem(EquipmentSlot.Body) == null) Equip("demo-armor", EquipmentSlot.Body);
                else equipment.TryUnequip(EquipmentSlot.Body);
            }
            if (keys.f6Key.wasPressedThisFrame) equipment.TryUnequip(equipment.ActiveWeaponSlot);
            if (keys.f7Key.wasPressedThisFrame) Equip("demo-sword", EquipmentSlot.Weapon1);
            if (keys.f8Key.wasPressedThisFrame) Equip("demo-bow", EquipmentSlot.Weapon2);
        }

        private void Equip(string id, EquipmentSlot slot)
        {
            for (int index = 0; index < PlayerInventory.Capacity; index++)
                if (inventory.GetSlot(index).Item?.ItemId == id)
                {
                    equipment.TryEquip(index, slot);
                    return;
                }
        }
    }
}
