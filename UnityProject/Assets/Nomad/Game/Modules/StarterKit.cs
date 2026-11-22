using System;
using Nomad.Equipment;
using Nomad.Inventory;
using UnityEngine;

namespace Nomad.Game.Modules
{
    // 메인 게임 시작 시 한 번 지급하는 임시 시작 장비입니다. 지급 목록은 기획 확정 전 임시값입니다(#89 판단 필요).
    [RequireComponent(typeof(PlayerInventory), typeof(PlayerEquipment))]
    public sealed class StarterKit : MonoBehaviour
    {
        [Serializable]
        private struct Entry
        {
            public string itemId;
            public int count;
            [Tooltip("지급 직후 slot 칸에 장착합니다.")]
            public bool equip;
            public EquipmentSlot slot;
        }

        [SerializeField] private Entry[] items = Array.Empty<Entry>();

        private bool granted;

        // 지역 전환으로 다시 활성화되어도 한 번만 지급합니다.
        private void Start()
        {
            if (granted)
                return;

            granted = true;
            var inventory = GetComponent<PlayerInventory>();
            var equipment = GetComponent<PlayerEquipment>();

            foreach (Entry entry in items)
            {
                if (inventory.Add(entry.itemId, Mathf.Max(1, entry.count)) <= 0 || !entry.equip)
                    continue;

                int index = FindSlot(inventory, entry.itemId);
                if (index >= 0)
                    equipment.TryEquip(index, entry.slot);
            }
        }

        private static int FindSlot(PlayerInventory inventory, string itemId)
        {
            for (int index = 0; index < PlayerInventory.Capacity; index++)
            {
                if (inventory.GetSlot(index).Item?.ItemId == itemId)
                    return index;
            }

            return -1;
        }
    }
}
