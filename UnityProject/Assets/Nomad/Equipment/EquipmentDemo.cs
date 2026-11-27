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
        private PlayerQuickSlots quickSlots;

        private void Start()
        {
            if (!TryGetComponent<EquipmentCombatVisuals>(out _)) gameObject.AddComponent<EquipmentCombatVisuals>();
            if (!TryGetComponent<EquipmentPanel>(out _)) gameObject.AddComponent<EquipmentPanel>();
            inventory = GetComponent<PlayerInventory>();
            equipment = GetComponent<PlayerEquipment>();
            // 검토 씬에는 퀵바가 없어 무기 선택용으로 붙입니다. 1/2키 선택도 퀵바가 처리합니다.
            if (!TryGetComponent(out quickSlots)) quickSlots = gameObject.AddComponent<PlayerQuickSlots>();
            equipment.Changed += LogEquipment;
            foreach (ItemDefinition item in items)
                if (item.ItemId != "demo-unarmed")
                    inventory.Add(item.ItemId, item.ItemId == PlayerEquipmentCombat.ArrowItemId ? 30 : 1);

            RegisterWeapon("demo-sword", 0);
            RegisterWeapon("demo-bow", 1);
            InventoryPanel.GetOrCreate(inventory);
            Debug.Log("EQUIPMENT_DEMO_READY: Tab 장비8+가방20, 가방→장비 드래그/클릭 장착, 장비 우클릭 해제. LMB 공격, 1 검/2 활. F5갑옷/F6무기 등록 해제/F7검/F8활은 검토용 보조키.");
        }

        private void OnDestroy()
        {
            if (equipment != null)
                equipment.Changed -= LogEquipment;
        }

        private void LogEquipment()
        {
            Debug.Log($"EQUIPMENT_CHANGED: 선택=퀵바{equipment.ActiveWeaponIndex + 1}, 무기={equipment.ActiveWeapon?.DisplayName ?? "맨손"}, 방어력={equipment.TotalDefense}, 가방={inventory.UsedSlots}/{inventory.SlotCount}");
        }

        private void Update()
        {
            Keyboard keys = Keyboard.current;
            if (keys == null)
                return;

            if (keys.f5Key.wasPressedThisFrame)
            {
                if (equipment.GetItem(EquipmentSlot.Body) == null) Equip("gear-chain-mail", EquipmentSlot.Body);
                else equipment.TryUnequip(EquipmentSlot.Body);
            }
            if (keys.f6Key.wasPressedThisFrame) quickSlots?.Unregister(equipment.ActiveWeaponIndex);
            if (keys.f7Key.wasPressedThisFrame) RegisterWeapon("demo-sword", 0);
            if (keys.f8Key.wasPressedThisFrame) RegisterWeapon("demo-bow", 1);
        }

        private void RegisterWeapon(string id, int quickSlot)
        {
            for (int index = 0; index < PlayerInventory.MaxCapacity; index++)
                if (inventory.GetSlot(index).Item?.ItemId == id)
                {
                    quickSlots.Register(index, quickSlot);
                    return;
                }
        }

        private void Equip(string id, EquipmentSlot slot)
        {
            for (int index = 0; index < PlayerInventory.MaxCapacity; index++)
                if (inventory.GetSlot(index).Item?.ItemId == id)
                {
                    equipment.TryEquip(index, slot);
                    return;
                }
        }
    }
}
