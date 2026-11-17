using System.Collections.Generic;
using Nomad.Inventory;
using Nomad.Props;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Loot
{
    // 저장된 지형 통합 검토 씬의 기존 상자에만 연결합니다. 배치 정책은 변경하지 않습니다.
    public sealed class TerrainLootIntegration : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private UnityEngine.UI.Text status;
        private readonly List<LootChest> chests = new();
        private LootChest lastChest;

        public string Summary => status == null ? string.Empty : status.text;

        public void Refresh(PropScatter scatter)
        {
            chests.Clear();
            lastChest = null;
            if (scatter == null) return;

            // 도달성 복구로 제거된 큐브는 제외하고 최종 활성 배치에만 적용합니다.
            foreach (BoxCollider box in scatter.GetComponentsInChildren<BoxCollider>())
            {
                var chest = box.gameObject.AddComponent<LootChest>();
                chest.Initialize($"{scatter.GenerationId}/chest-{chests.Count}", new[] { new LootStack("scrap", 10) });
                chest.Changed += () => lastChest = chest;
                chests.Add(chest);
            }
        }

        private void Update()
        {
            if (inventory == null || !inventory.isActiveAndEnabled) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard?.fKey.wasPressedThisFrame == true)
            {
                inventory.Remove("scrap", int.MaxValue);
                inventory.Remove("field-knife", int.MaxValue);
                inventory.Add("scrap", 96);
                inventory.Add("field-knife", 19);
            }
            if (keyboard?.vKey.wasPressedThisFrame == true)
                inventory.Remove("scrap", int.MaxValue);
        }

        private void LateUpdate()
        {
            if (status == null || inventory == null) return;
            int scrap = 0;
            for (int i = 0; i < inventory.UsedSlots; i++)
                if (inventory.GetStack(i).Item.ItemId == "scrap") scrap += inventory.GetStack(i).Count;
            string result = lastChest == null ? "Approach a crate and press E"
                : $"Last chest: {lastChest.GetStack(0).Count}/10 {lastChest.Status}";
            status.text = $"E Open chest | F Fill inventory | V Free scrap | Chests {chests.Count}\n"
                + $"Scrap {scrap} | Slots {inventory.UsedSlots}/20 | {result}";
        }
    }
}
