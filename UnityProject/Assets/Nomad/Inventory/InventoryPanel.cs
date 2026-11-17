using Nomad.Interaction;
using Nomad.Loot;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Nomad.Inventory
{
    [DisallowMultipleComponent]
    public sealed class InventoryPanel : MonoBehaviour
    {
        private const float DragThreshold = 5f;

        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private GameObject panel;
        [SerializeField] private UnityEngine.UI.Text capacity;
        [SerializeField] private UnityEngine.UI.Text itemDetails;
        [SerializeField] private RectTransform grid;
        [SerializeField] private GameObject slotTemplate;

        private readonly SlotView[] slots = new SlotView[PlayerInventory.Capacity];
        private Canvas canvas;
        private SlotView dragPreview;
        private Mouse dragMouse;
        private Vector2 pressPosition;
        private int hoveredIndex = -1;
        private int dragSource = -1;
        private ChestLootPanel chestPanel;
        private GameObject lootEventSystem;
        private Vector2 backpackPosition;
        private bool runtimeInstance;

        internal bool HasOpenChest => chestPanel != null && chestPanel.IsOpen;
        internal LootChest OpenChest => chestPanel?.Chest;

        internal static InventoryPanel GetOrCreate(PlayerInventory target)
        {
            if (target.Panel != null)
                return target.Panel;

            GameObject asset = Resources.Load<GameObject>("NomadInventory");
            if (asset == null || !asset.TryGetComponent(out InventoryPanel prefab))
            {
                Debug.LogError("NomadInventory UI 프리팹을 읽을 수 없습니다.", target);
                return null;
            }

            InventoryPanel view = Instantiate(prefab);
            view.gameObject.name = prefab.gameObject.name;
            view.runtimeInstance = true;
            view.inventory = target;
            view.Subscribe();
            return view;
        }

        private Camera EventCamera => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        private void Awake()
        {
            canvas = grid.GetComponentInParent<Canvas>(true);
            backpackPosition = ((RectTransform)panel.transform).anchoredPosition;
            for (int index = 0; index < slots.Length; index++)
            {
                GameObject slot = Instantiate(slotTemplate, grid);
                slots[index] = new SlotView(slot);
                slot.SetActive(true);
            }

            GameObject preview = Instantiate(slotTemplate, panel.transform);
            preview.name = "DragPreview";
            dragPreview = new SlotView(preview);
            dragPreview.Group.alpha = 0.85f;
            dragPreview.Group.blocksRaycasts = false;
            preview.SetActive(false);
            panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (inventory != null)
                Subscribe();
        }

        private void Subscribe()
        {
            inventory.Panel = this;
            inventory.Changed += Refresh;
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.Changed -= Refresh;
                if (inventory.Panel == this)
                    inventory.Panel = null;
            }

            CloseChest();
            CancelDrag();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                CancelDrag();
        }

        private void Update()
        {
            if (inventory == null)
            {
                if (runtimeInstance)
                    Destroy(gameObject);
                return;
            }

            if (HasOpenChest && (Keyboard.current?.tabKey.wasPressedThisFrame == true ||
                                 Keyboard.current?.escapeKey.wasPressedThisFrame == true))
            {
                CloseChest();
                return;
            }

            if (Keyboard.current?.tabKey.wasPressedThisFrame == true)
            {
                CancelDrag();
                hoveredIndex = -1;
                panel.SetActive(!panel.activeSelf);
                if (panel.activeSelf)
                    Refresh();
            }

            if (panel.activeSelf)
                ProcessPointer();
        }

        internal void ShowChest(LootChest chest, PlayerInteraction interaction)
        {
            if (!interaction.CanAccessChest(chest))
                return;

            if (EventSystem.current == null)
            {
                if (lootEventSystem == null)
                {
                    lootEventSystem = new GameObject("Loot Event System");
                    lootEventSystem.SetActive(false);
                    lootEventSystem.transform.SetParent(transform);
                    lootEventSystem.AddComponent<EventSystem>();
                    lootEventSystem.AddComponent<InputSystemUIInputModule>();
                }

                lootEventSystem.SetActive(true);
            }

            var backpack = (RectTransform)panel.transform;
            chestPanel ??= new ChestLootPanel(backpack, capacity.font, inventory, CloseChest);
            backpack.anchoredPosition = backpackPosition - Vector2.right * chestPanel.BackpackOffset;
            panel.SetActive(true);
            CancelDrag();
            chest.Inspect();
            if (!interaction.CanAccessChest(chest))
            {
                backpack.anchoredPosition = backpackPosition;
                panel.SetActive(false);
                if (lootEventSystem != null)
                    lootEventSystem.SetActive(false);
                return;
            }

            chestPanel.Show(chest, interaction);
            Refresh();
        }

        internal void CloseChest()
        {
            if (!HasOpenChest)
                return;

            chestPanel.Hide();
            CancelDrag();
            hoveredIndex = -1;
            ((RectTransform)panel.transform).anchoredPosition = backpackPosition;
            panel.SetActive(false);
            if (lootEventSystem != null)
                lootEventSystem.SetActive(false);
        }

        private void Refresh()
        {
            if (!panel.activeSelf)
                return;

            // 획득이나 제거로 내용이 바뀌면 기존 드래그를 취소합니다.
            CancelDrag();
            capacity.text = $"{inventory.UsedSlots} / {PlayerInventory.Capacity} slots";
            for (int index = 0; index < slots.Length; index++)
                slots[index].Bind(inventory.GetSlot(index), index == hoveredIndex);

            UpdateHover(FindPointerSlot(Mouse.current), refreshDetails: true);
        }

        private void ProcessPointer()
        {
            Mouse mouse = Mouse.current;
            int pointerSlot = FindPointerSlot(mouse);
            if (dragSource >= 0 && (mouse != dragMouse || Keyboard.current?.escapeKey.wasPressedThisFrame == true))
                CancelDrag();

            if (mouse != null)
            {
                Vector2 position = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame && pointerSlot >= 0 &&
                    inventory.GetSlot(pointerSlot).Item != null)
                {
                    dragSource = pointerSlot;
                    dragMouse = mouse;
                    pressPosition = position;
                }

                if (dragSource >= 0)
                    UpdateDrag(mouse, position, pointerSlot);
            }

            UpdateHover(pointerSlot);
        }

        private void UpdateDrag(Mouse mouse, Vector2 position, int pointerSlot)
        {
            GameObject preview = dragPreview.Rect.gameObject;
            if (!preview.activeSelf && (position - pressPosition).sqrMagnitude >= DragThreshold * DragThreshold)
            {
                dragPreview.Bind(inventory.GetSlot(dragSource), false);
                dragPreview.Rect.sizeDelta = slots[dragSource].Rect.rect.size;
                slots[dragSource].Group.alpha = 0.45f;
                preview.SetActive(true);
            }

            if (preview.activeSelf && RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)panel.transform, position, EventCamera, out Vector3 worldPoint))
            {
                dragPreview.Rect.position = worldPoint;
            }

            if (mouse.leftButton.isPressed)
                return;

            int source = dragSource;
            bool drop = preview.activeSelf && mouse.leftButton.wasReleasedThisFrame && pointerSlot >= 0;
            CancelDrag();
            if (drop)
                inventory.MoveStack(source, pointerSlot);
        }

        private void CancelDrag()
        {
            if (dragSource < 0)
                return;

            slots[dragSource].Group.alpha = 1f;
            dragPreview.Rect.gameObject.SetActive(false);
            dragSource = -1;
            dragMouse = null;
        }

        private void UpdateHover(int pointerSlot, bool refreshDetails = false)
        {
            bool dragging = dragPreview.Rect.gameObject.activeSelf;
            PlayerInventory.ItemStack stack = pointerSlot >= 0 ? inventory.GetSlot(pointerSlot) : default;
            int nextHoveredIndex = stack.Item != null || dragging ? pointerSlot : -1;
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

            itemDetails.text = stack.Item != null ? $"{stack.Item.DisplayName}  ×{stack.Count}" : string.Empty;
        }

        private int FindPointerSlot(Mouse mouse)
        {
            if (mouse == null)
                return -1;

            Vector2 position = mouse.position.ReadValue();
            Camera eventCamera = EventCamera;
            for (int index = 0; index < slots.Length; index++)
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
            public CanvasGroup Group { get; }

            public SlotView(GameObject slot)
            {
                background = slot.GetComponent<UnityEngine.UI.Image>();
                Group = slot.AddComponent<CanvasGroup>();
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
