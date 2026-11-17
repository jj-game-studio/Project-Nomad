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
        [SerializeField] private UnityEngine.UI.Text itemDetails;
        [SerializeField] private RectTransform grid;
        [SerializeField] private GameObject slotTemplate;

        private readonly SlotView[] slots = new SlotView[PlayerInventory.Capacity];
        private Canvas canvas;
        private int hoveredIndex = -1;

        private void Awake()
        {
            canvas = grid.GetComponentInParent<Canvas>(true);
            for (int index = 0; index < slots.Length; index++)
            {
                GameObject slot = Instantiate(slotTemplate, grid);
                slots[index] = new SlotView(slot);
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
            if (Keyboard.current?.tabKey.wasPressedThisFrame == true)
            {
                hoveredIndex = -1;
                panel.SetActive(!panel.activeSelf);
                if (panel.activeSelf)
                    Refresh();
            }

            if (panel.activeSelf)
                UpdateHover();
        }

        private void Refresh()
        {
            if (!panel.activeSelf)
                return;

            capacity.text = $"{inventory.UsedSlots} / {PlayerInventory.Capacity} slots";
            for (int index = 0; index < slots.Length; index++)
            {
                PlayerInventory.ItemStack stack = index < inventory.UsedSlots
                    ? inventory.GetStack(index)
                    : default;
                slots[index].Bind(stack, index == hoveredIndex);
            }

            UpdateHover(refreshDetails: true);
        }

        private void UpdateHover(bool refreshDetails = false)
        {
            int nextHoveredIndex = FindHoveredSlot();
            if (nextHoveredIndex == hoveredIndex && !refreshDetails)
                return;

            if (nextHoveredIndex != hoveredIndex)
            {
                if (hoveredIndex >= 0)
                    slots[hoveredIndex].SetHighlighted(false);
                hoveredIndex = nextHoveredIndex;
                if (hoveredIndex >= 0)
                    slots[hoveredIndex].SetHighlighted(true);
            }

            string details = string.Empty;
            if (hoveredIndex >= 0)
            {
                PlayerInventory.ItemStack stack = inventory.GetStack(hoveredIndex);
                details = $"{stack.Item.DisplayName}  ×{stack.Count}";
            }

            if (itemDetails.text != details)
                itemDetails.text = details;
        }

        private int FindHoveredSlot()
        {
            if (Mouse.current == null)
                return -1;

            Vector2 position = Mouse.current.position.ReadValue();
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            for (int index = 0; index < inventory.UsedSlots; index++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(slots[index].Rect, position, eventCamera))
                    return index;
            }

            return -1;
        }

        private sealed class SlotView
        {
            private static readonly Color EmptyFill = new Color32(26, 28, 30, 235);
            private static readonly Color OccupiedFill = new Color32(44, 47, 48, 255);
            private static readonly Color EmptyBorder = new Color32(66, 69, 68, 255);
            private static readonly Color OccupiedBorder = new Color32(101, 107, 103, 255);
            private static readonly Color HoverBorder = new Color32(224, 182, 104, 255);

            private readonly UnityEngine.UI.Image background;
            private readonly UnityEngine.UI.Outline border;
            private readonly UnityEngine.UI.Image icon;
            private readonly UnityEngine.UI.Text count;
            private readonly UnityEngine.UI.Text fallbackName;
            private bool occupied;

            public RectTransform Rect => background.rectTransform;

            public SlotView(GameObject slot)
            {
                background = slot.GetComponent<UnityEngine.UI.Image>();
                border = slot.AddComponent<UnityEngine.UI.Outline>();
                border.effectDistance = new Vector2(1, -1);
                border.useGraphicAlpha = false;

                count = slot.GetComponentInChildren<UnityEngine.UI.Text>(true);
                count.gameObject.name = "Count";

                fallbackName = Instantiate(count, slot.transform);
                fallbackName.gameObject.name = "FallbackName";
                fallbackName.alignment = TextAnchor.MiddleCenter;
                fallbackName.fontSize = 13;
                fallbackName.fontStyle = FontStyle.Normal;
                fallbackName.resizeTextForBestFit = true;
                fallbackName.resizeTextMinSize = 9;
                fallbackName.resizeTextMaxSize = 13;
                fallbackName.rectTransform.offsetMin = new Vector2(6, 23);
                fallbackName.rectTransform.offsetMax = new Vector2(-6, -7);

                icon = new GameObject("Icon", typeof(RectTransform), typeof(UnityEngine.UI.Image))
                    .GetComponent<UnityEngine.UI.Image>();
                icon.transform.SetParent(slot.transform, false);
                icon.transform.SetAsFirstSibling();
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                icon.rectTransform.anchorMin = Vector2.zero;
                icon.rectTransform.anchorMax = Vector2.one;
                icon.rectTransform.offsetMin = new Vector2(10, 16);
                icon.rectTransform.offsetMax = new Vector2(-10, -6);
            }

            public void Bind(PlayerInventory.ItemStack stack, bool highlighted)
            {
                occupied = stack.Item != null;
                background.color = occupied ? OccupiedFill : EmptyFill;
                SetHighlighted(occupied && highlighted);

                Sprite sprite = occupied ? stack.Item.Icon : null;
                icon.sprite = sprite;
                icon.enabled = sprite != null;
                fallbackName.text = occupied && sprite == null ? stack.Item.DisplayName : string.Empty;
                count.text = occupied && stack.Count > 1 ? $"×{stack.Count}" : string.Empty;
            }

            public void SetHighlighted(bool highlighted)
            {
                Color borderColor = occupied ? OccupiedBorder : EmptyBorder;
                border.effectColor = highlighted ? HoverBorder : borderColor;
            }
        }
    }
}
