using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Inventory
{
    [DisallowMultipleComponent]
    public sealed class InventoryPanel : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private GameObject panel;
        [SerializeField] private UnityEngine.UI.Text capacity;
        [SerializeField] private RectTransform grid;
        [SerializeField] private GameObject slotTemplate;

        private readonly UnityEngine.UI.Text[] slots = new UnityEngine.UI.Text[PlayerInventory.Capacity];

        private void Awake()
        {
            for (int index = 0; index < slots.Length; index++)
            {
                GameObject slot = Instantiate(slotTemplate, grid);
                slots[index] = slot.GetComponentInChildren<UnityEngine.UI.Text>(true);
                slot.SetActive(true);
            }

            panel.SetActive(false);
        }

        private void OnEnable()
        {
            inventory.Changed += Refresh;
        }

        private void OnDisable()
        {
            inventory.Changed -= Refresh;
        }

        private void Update()
        {
            if (Keyboard.current?.tabKey.wasPressedThisFrame != true)
                return;

            panel.SetActive(!panel.activeSelf);
            if (panel.activeSelf)
                Refresh();
        }

        private void Refresh()
        {
            if (!panel.activeSelf)
                return;

            capacity.text = $"{inventory.UsedSlots} / {PlayerInventory.Capacity} slots";
            for (int index = 0; index < slots.Length; index++)
            {
                if (index >= inventory.UsedSlots)
                {
                    slots[index].text = "—";
                    slots[index].color = new Color(0.54f, 0.61f, 0.64f);
                    continue;
                }

                PlayerInventory.ItemStack stack = inventory.GetStack(index);
                slots[index].text = $"{stack.Item.DisplayName}   ×{stack.Count}\n{stack.Item.ItemId}";
                slots[index].color = new Color(0.95f, 0.96f, 0.95f);
            }
        }
    }
}
