using System.Collections.Generic;
using Nomad.Inventory;
using UnityEngine;

namespace Nomad.Equipment
{
    public sealed partial class PlayerEquipment
    {
        public const int MaximumEnhancement = 3;
        private readonly List<ItemDefinition> enhancedItems = new();

        public int EnhancementMaterial(EquipmentSlot slot)
        {
            ItemDefinition item = GetItem(slot);
            if (item == null || item.EquipmentType == EquipmentType.Bag || item.EnhancementLevel >= MaximumEnhancement) return -1;
            for (int index = 0; index < inventory.SlotCount; index++)
            {
                ItemDefinition candidate = inventory.GetSlot(index).Item;
                if (candidate != null && candidate.ItemId == item.ItemId && candidate.EnhancementLevel == 0) return index;
            }
            return -1;
        }

        public bool TryEnhance(EquipmentSlot slot)
        {
            if (!isActiveAndEnabled || changing) return false;
            int material = EnhancementMaterial(slot);
            if (material < 0) return false;
            ItemDefinition enhanced = items[(int)slot].CreateEnhanced();
            enhancedItems.Add(enhanced);
            inventory.RemoveFromSlotForTransfer(material, 1);
            GetComponent<PlayerQuickSlots>()?.ReplaceEnhanced(items[(int)slot], enhanced);
            items[(int)slot] = enhanced;
            Publish(true);
            return true;
        }

        private void OnDestroy()
        {
            foreach (ItemDefinition item in enhancedItems) if (item != null) Destroy(item);
        }
    }
}
