namespace Nomad.Inventory
{
    // 기존 추가 API는 스택을 합치므로 분할은 빈 칸에만 수량을 전송합니다.
    public static class InventoryStackSplit
    {
        public static bool TrySplit(this PlayerInventory inventory, int source, int amount,
            ItemDefinition expectedItem, int expectedCount, out int destination, out string error)
        {
            destination = -1;
            error = string.Empty;
            if (inventory == null || (uint)source >= PlayerInventory.Capacity)
            {
                error = "분할할 아이템이 없습니다.";
                return false;
            }

            PlayerInventory.ItemStack stack = inventory.GetSlot(source);
            if (expectedItem == null || stack.Item != expectedItem || stack.Count != expectedCount)
            {
                error = "아이템이 변경되었습니다. 분할 창을 다시 열어 주세요.";
                return false;
            }

            if (amount < 1 || amount >= stack.Count || amount > stack.Item.MaxStack)
            {
                error = "분할 수량은 1개부터 현재 수량보다 1개 적게 선택하세요.";
                return false;
            }

            for (int index = 0; index < PlayerInventory.Capacity; index++)
            {
                if (inventory.GetSlot(index).Item != null)
                    continue;

                // 양쪽 변경이 끝난 상태만 한 번 알립니다. 실패 시 원본을 건드리지 않습니다.
                inventory.AddToSlotForTransfer(index, stack.Item, amount);
                inventory.RemoveFromSlotForTransfer(source, amount);
                destination = index;
                inventory.NotifyChanged();
                return true;
            }

            error = "빈 가방칸이 필요합니다.";
            return false;
        }
    }
}
