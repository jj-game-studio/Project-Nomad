using Nomad.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Loot
{
    // B 검토 전용 수량 준비 도구입니다. 실제 획득 입력은 PlayerInteraction이 처리합니다.
    public sealed class LootPreview : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private LootChest chest;

        [SerializeField] private bool keyboardControls;
        [SerializeField] private UnityEngine.UI.Text status;

        private void Start()
        {
            if (keyboardControls) Fill();
        }

        private void Update()
        {
            if (!keyboardControls) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard?.fKey.wasPressedThisFrame == true) Fill();
            if (keyboard?.gKey.wasPressedThisFrame == true) Remove();

            if (status != null)
                status.text = $"Chest: {chest.CountItems()} items  ·  {chest.Status}";
        }

        [ContextMenu("B Review/1 Fill inventory (3 scrap spaces)")]
        private void Fill()
        {
            if (!Application.isPlaying) return;
            inventory.Remove("scrap", int.MaxValue);
            inventory.Remove("field-knife", int.MaxValue);
            inventory.Remove("field-vest", int.MaxValue);
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
