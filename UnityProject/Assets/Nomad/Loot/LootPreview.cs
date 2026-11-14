using Nomad.Inventory;
using UnityEngine;

namespace Nomad.Loot
{
    // A 입력 연결 전 B 소유 로직을 Inspector 메뉴에서만 직접 확인하는 검토 도구입니다.
    public sealed class LootPreview : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private LootChest chest;

        [ContextMenu("B Review/1 Fill inventory (3 scrap spaces)")]
        private void Fill()
        {
            if (!Application.isPlaying) return;
            inventory.Remove("scrap", int.MaxValue);
            inventory.Remove("field-knife", int.MaxValue);
            inventory.Add("scrap", 96);
            inventory.Add("field-knife", 19);
            Debug.Log("B REVIEW: 20 slots, scrap 96/99. Chest has 10; loot must leave 7.");
        }

        [ContextMenu("B Review/2 Direct loot (not player interaction)")]
        private void Loot()
        {
            if (!Application.isPlaying) return;
            Debug.Log($"B REVIEW: added={chest.TryLoot(inventory)}, remaining={chest.GetStack(0).Count}, {chest.Message}");
        }

        [ContextMenu("B Review/3 Remove scrap to free space")]
        private void Remove()
        {
            if (Application.isPlaying) inventory.Remove("scrap", int.MaxValue);
        }
    }
}
