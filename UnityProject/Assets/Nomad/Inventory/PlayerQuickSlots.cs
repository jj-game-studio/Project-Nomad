using System;
using UnityEngine;

namespace Nomad.Inventory
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class PlayerQuickSlots : MonoBehaviour
    {
        public const int Capacity = 4;

        private PlayerInventory inventory;
        private readonly ItemDefinition[] items = new ItemDefinition[Capacity];

        public event Action Changed;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
        }

        private void OnEnable()
        {
            inventory.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            inventory.Changed -= Refresh;
        }

        public ItemDefinition GetItem(int index) => items[index];

        public int GetCount(int index)
        {
            ItemDefinition item = items[index];
            if (item == null)
                return 0;

            int count = 0;
            for (int slot = 0; slot < PlayerInventory.Capacity; slot++)
            {
                PlayerInventory.ItemStack stack = inventory.GetSlot(slot);
                if (stack.Item == item)
                    count += stack.Count;
            }

            return count;
        }

        // 가방 칸이 바뀌어도 등록을 유지하도록 위치 대신 아이템 정의를 참조합니다.
        public bool Register(int inventorySlot, int quickSlot)
        {
            if ((uint)inventorySlot >= PlayerInventory.Capacity || (uint)quickSlot >= Capacity)
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
