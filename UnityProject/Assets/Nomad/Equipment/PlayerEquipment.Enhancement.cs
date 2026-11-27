using System.Collections.Generic;
using Nomad.Inventory;
using UnityEngine;

namespace Nomad.Equipment
{
    public sealed partial class PlayerEquipment
    {
        public const int MaximumEnhancement = 3;
        private readonly List<ItemDefinition> enhancedItems = new();

        public int EnhancementMaterial(EquipmentSlot slot) => FindMaterial(GetItem(slot), -1);

        // 손에 든 무기는 가방에 있으므로 무기 자신의 칸은 재료에서 뺍니다.
        public int WeaponEnhancementMaterial() => FindMaterial(ActiveWeapon, WeaponBagSlot());

        private int FindMaterial(ItemDefinition item, int exclude)
        {
            if (item == null || item.EquipmentType == EquipmentType.Bag || item.EnhancementLevel >= MaximumEnhancement) return -1;
            for (int index = 0; index < inventory.SlotCount; index++)
            {
                ItemDefinition candidate = inventory.GetSlot(index).Item;
                if (index != exclude && candidate != null && candidate.ItemId == item.ItemId && candidate.EnhancementLevel == 0) return index;
            }
            return -1;
        }

        private int WeaponBagSlot()
        {
            ItemDefinition weapon = ActiveWeapon;
            for (int index = 0; weapon != null && index < inventory.SlotCount; index++)
                if (inventory.GetSlot(index).Item == weapon) return index;
            return -1;
        }

        public bool TryEnhance(EquipmentSlot slot)
        {
            if (!isActiveAndEnabled || changing) return false;
            int material = EnhancementMaterial(slot);
            if (material < 0) return false;
            ItemDefinition enhanced = CreateEnhanced(items[(int)slot]);
            inventory.RemoveFromSlotForTransfer(material, 1);
            items[(int)slot] = enhanced;
            Publish(true);
            return true;
        }

        // 선택한 퀵바 무기를 강화합니다. 강화품은 원래 무기가 있던 가방 칸에 그대로 놓입니다.
        public bool TryEnhanceWeapon()
        {
            if (!isActiveAndEnabled || changing) return false;
            int material = WeaponEnhancementMaterial();
            int source = WeaponBagSlot();
            if (material < 0 || source < 0) return false;
            ItemDefinition enhanced = CreateEnhanced(ActiveWeapon);
            inventory.RemoveFromSlotForTransfer(material, 1);
            inventory.RemoveFromSlotForTransfer(source, 1);
            inventory.AddToSlotForTransfer(source, enhanced, 1);
            Publish(true);
            return true;
        }

        // 퀵바 등록을 먼저 강화품으로 바꿔야 가방 갱신 때 등록이 지워지지 않습니다.
        private ItemDefinition CreateEnhanced(ItemDefinition item)
        {
            ItemDefinition enhanced = item.CreateEnhanced();
            enhancedItems.Add(enhanced);
            quickSlots?.ReplaceEnhanced(item, enhanced);
            return enhanced;
        }

        private void OnDestroy()
        {
            foreach (ItemDefinition item in enhancedItems) if (item != null) Destroy(item);
        }
    }
}
