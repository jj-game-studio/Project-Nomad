using Nomad.Interaction;
using Nomad.Hud;
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
        private const float StoreDoubleClickInterval = 0.3f;

        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private GameObject panel;
        [SerializeField] private UnityEngine.UI.Text capacity;
        [SerializeField] private UnityEngine.UI.Text itemDetails;
        [SerializeField] private RectTransform grid;
        [SerializeField] private GameObject slotTemplate;

        private readonly SlotView[] slots = new SlotView[PlayerInventory.MaxCapacity];
        private Canvas canvas;
        private InventoryTooltip tooltip;
        private InventorySplitDialog splitDialog;
        private bool pointerFocused = true;
        private SlotView dragPreview;
        private Mouse dragMouse;
        private Vector2 pressPosition;
        private bool shiftAtPress;
        private int previousStoreClickSlot = -1;
        private float previousStoreClickTime;
        private Mouse previousStoreClickMouse;
        private int hoveredIndex = -1;
        private int dragSource = -1;
        private int quickDragSource = -1;
        private ItemDefinition quickDragItem;
        private int lootDragSource = -1;
        private ItemDefinition lootDragItem;
        private ChestLootPanel chestPanel;
        private GameObject lootEventSystem;
        private Vector2 backpackPosition;
        private bool runtimeInstance;
        private PlayerHud dragHud;

        internal bool HasOpenChest => chestPanel != null && chestPanel.IsOpen;
        internal LootChest OpenChest => chestPanel?.Chest;
        internal bool BlocksSplitInput => splitDialog != null && splitDialog.BlocksInput;

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

            // 지역 씬이 바뀌어도 가방 UI가 플레이어와 함께 남도록 플레이어와 같은 씬에 둡니다.
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(view.gameObject, target.gameObject.scene);
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
            tooltip = new InventoryTooltip((RectTransform)canvas.transform, capacity.font);
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

            splitDialog?.Close();
            tooltip?.Hide();
            CloseChest();
            CancelDrag();
        }

        private void OnDestroy() => splitDialog?.Dispose();

        private void OnApplicationFocus(bool hasFocus)
        {
            pointerFocused = hasFocus;
            if (!hasFocus)
                splitDialog?.Close();
            if (!hasFocus)
                tooltip?.Hide();
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

            if (inventory.BlocksTradeInput)
                return;

            if (splitDialog?.IsOpen == true)
            {
                if (!panel.activeInHierarchy)
                    splitDialog.Close();
                else if (Keyboard.current?.tabKey.wasPressedThisFrame == true)
                {
                    splitDialog.Close();
                    if (HasOpenChest) CloseChest();
                    else panel.SetActive(false);
                }
                else splitDialog.Tick();
                return;
            }
            if (BlocksSplitInput)
                return;

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

            if (panel.activeSelf && lootDragSource < 0)
                ProcessPointer();
        }

        private void LateUpdate()
        {
            if (!panel.activeInHierarchy)
                splitDialog?.Close();

            Mouse mouse = Mouse.current;
            if (BlocksSplitInput || inventory == null || !panel.activeInHierarchy || !pointerFocused || mouse == null ||
                mouse.leftButton.isPressed || dragSource >= 0 || quickDragSource >= 0 || lootDragSource >= 0)
            {
                tooltip.Hide();
                return;
            }

            int index = FindPointerSlot(mouse);
            PlayerInventory.ItemStack stack = index >= 0 ? inventory.GetSlot(index) : default;
            tooltip.Show(stack.Item, stack.Count, mouse.position.ReadValue(), EventCamera);
        }

        internal void CloseForTrade()
        {
            splitDialog?.Close();
            CloseChest();
            CancelDrag();
            tooltip.Hide();
            panel.SetActive(false);
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
            capacity.text = $"{inventory.UsedSlots} / {inventory.SlotCount} slots";
            for (int index = 0; index < slots.Length; index++)
            {
                // 가방 장비로 열리지 않은 칸은 숨겨 드래그·툴팁 대상에서도 빠집니다.
                slots[index].Rect.gameObject.SetActive(index < inventory.SlotCount);
                slots[index].Bind(inventory.GetSlot(index), index == hoveredIndex);
            }

            UpdateHover(FindPointerSlot(Mouse.current), refreshDetails: true);
        }

        private void ProcessPointer()
        {
            Mouse mouse = Mouse.current;
            int pointerSlot = FindPointerSlot(mouse);
            Keyboard splitKeyboard = Keyboard.current;
            if (pointerFocused && mouse?.leftButton.wasPressedThisFrame == true && pointerSlot >= 0 &&
                splitKeyboard != null && (splitKeyboard.leftCtrlKey.isPressed || splitKeyboard.rightCtrlKey.isPressed))
            {
                CancelDrag();
                if (inventory.GetSlot(pointerSlot).Count >= 2)
                {
                    tooltip.Hide();
                    splitDialog ??= new InventorySplitDialog((RectTransform)canvas.transform);
                    splitDialog.Open(inventory, pointerSlot);
                }
                return;
            }

            if ((dragSource >= 0 || quickDragSource >= 0) &&
                (mouse != dragMouse || Keyboard.current?.escapeKey.wasPressedThisFrame == true))
                CancelDrag();

            if (quickDragSource >= 0 && (dragHud == null || !dragHud.isActiveAndEnabled ||
                !dragHud.QuickSlots.isActiveAndEnabled || dragHud.QuickSlots.GetItem(quickDragSource) != quickDragItem))
                CancelDrag();

            if (dragHud == null)
                dragHud = inventory.GetComponentInChildren<PlayerHud>();

            if (mouse != null)
            {
                Vector2 position = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (pointerSlot != previousStoreClickSlot)
                        previousStoreClickSlot = -1;

                    if (pointerSlot >= 0 && inventory.GetSlot(pointerSlot).Item != null)
                    {
                        dragSource = pointerSlot;
                        dragMouse = mouse;
                        pressPosition = position;
                        Keyboard keyboard = Keyboard.current;
                        shiftAtPress = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
                    }
                    else if (!ContainsBackpack(position) && dragHud != null)
                    {
                        int source = dragHud.FindQuickSlot(position);
                        if (source >= 0 && dragHud.QuickSlots.GetItem(source) != null)
                        {
                            quickDragSource = source;
                            quickDragItem = dragHud.QuickSlots.GetItem(source);
                            dragMouse = mouse;
                            pressPosition = position;
                        }
                    }
                }

                if (quickDragSource >= 0)
                    UpdateQuickSlotDrag(mouse, position);
                else if (dragSource >= 0)
                    UpdateDrag(mouse, position, pointerSlot);
            }

            UpdateHover(pointerSlot);
        }

        private bool ContainsBackpack(Vector2 position)
        {
            return panel.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(
                (RectTransform)panel.transform, position, EventCamera);
        }

        private void UpdateQuickSlotDrag(Mouse mouse, Vector2 position)
        {
            GameObject preview = dragPreview.Rect.gameObject;
            if (!preview.activeSelf && (position - pressPosition).sqrMagnitude >= DragThreshold * DragThreshold)
            {
                dragHud.SetQuickSlotDragging(quickDragSource, true);
                ShowDragPreview(new PlayerInventory.ItemStack(quickDragItem, dragHud.QuickSlots.GetCount(quickDragSource)));
            }

            if (preview.activeSelf)
                PositionDragPreview(position);
            if (mouse.leftButton.isPressed)
                return;

            // 바로가기만 해제하므로 가방의 빈 공간이나 대상 칸의 아이템과 관계없습니다.
            int source = quickDragSource;
            bool unregister = mouse.leftButton.wasReleasedThisFrame && preview.activeSelf && ContainsBackpack(position);
            CancelDrag();
            if (unregister)
                dragHud.QuickSlots.Unregister(source);
        }

        private void UpdateDrag(Mouse mouse, Vector2 position, int pointerSlot)
        {
            GameObject preview = dragPreview.Rect.gameObject;
            if (!preview.activeSelf && (position - pressPosition).sqrMagnitude >= DragThreshold * DragThreshold)
            {
                slots[dragSource].Group.alpha = 0.45f;
                ShowDragPreview(inventory.GetSlot(dragSource));
            }

            if (preview.activeSelf)
                PositionDragPreview(position);

            bool overChest = preview.activeSelf && HasOpenChest && chestPanel.ContainsDropPoint(position, EventCamera);
            chestPanel?.HighlightDrop(overChest);
            if (dragHud == null)
                dragHud = inventory.GetComponentInChildren<PlayerHud>();
            int quickSlot = preview.activeSelf && dragHud != null ? dragHud.FindQuickSlot(position) : -1;
            dragHud?.HighlightQuickSlot(quickSlot);
            if (mouse.leftButton.isPressed)
                return;

            int source = dragSource;
            bool released = mouse.leftButton.wasReleasedThisFrame;
            bool dragged = preview.activeSelf;
            bool shift = shiftAtPress;
            bool click = released && !dragged && source == pointerSlot;
            CancelDrag(resetClick: !click);
            if (!released)
                return;

            if (click && HasOpenChest)
                StoreFromClick(source, mouse, shift);
            else if (dragged && overChest)
                chestPanel.Deposit(source);
            else if (dragged && pointerSlot >= 0)
                inventory.MoveStack(source, pointerSlot);
            else if (dragged && quickSlot >= 0)
                dragHud.QuickSlots.Register(source, quickSlot);
        }

        private void StoreFromClick(int slot, Mouse mouse, bool shift)
        {
            float now = Time.unscaledTime;
            bool doubleClick = previousStoreClickSlot == slot && previousStoreClickMouse == mouse &&
                now - previousStoreClickTime <= StoreDoubleClickInterval;
            if (shift || doubleClick)
            {
                previousStoreClickSlot = -1;
                chestPanel.Deposit(slot, shift ? int.MaxValue : 1);
                return;
            }

            previousStoreClickSlot = slot;
            previousStoreClickTime = now;
            previousStoreClickMouse = mouse;
        }

        internal void BeginLootDrag(int source, PlayerInventory.ItemStack stack, Vector2 position)
        {
            if (!HasOpenChest)
                return;

            CancelDrag();
            lootDragSource = source;
            lootDragItem = stack.Item;
            ShowDragPreview(stack);
            MoveLootDrag(position);
        }

        internal void MoveLootDrag(Vector2 position)
        {
            if (lootDragSource < 0)
                return;

            PositionDragPreview(position);
            int target = FindPointerSlot(position);
            UpdateHover(target >= 0 && inventory.SpaceInSlot(target, lootDragItem) > 0 ? target : -1);
        }

        internal void EndLootDrag(Vector2 position)
        {
            if (lootDragSource < 0)
                return;

            int source = lootDragSource;
            int target = FindPointerSlot(position);
            CancelDrag();
            if (target >= 0 && HasOpenChest)
                chestPanel.TakeToSlot(source, target);
            UpdateHover(target);
        }

        internal void CancelLootDrag()
        {
            if (lootDragSource >= 0)
                CancelDrag();
        }

        private void ShowDragPreview(PlayerInventory.ItemStack stack)
        {
            dragPreview.Bind(stack, false);
            dragPreview.Rect.sizeDelta = slots[0].Rect.rect.size;
            if (HasOpenChest)
            {
                dragPreview.Rect.SetParent(panel.transform.parent, false);
                dragPreview.Rect.SetAsLastSibling();
            }
            dragPreview.Rect.gameObject.SetActive(true);
        }

        private void PositionDragPreview(Vector2 position)
        {
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)panel.transform, position, EventCamera, out Vector3 worldPoint))
                dragPreview.Rect.position = worldPoint;
        }

        private void CancelDrag(bool resetClick = true)
        {
            if (resetClick)
                previousStoreClickSlot = -1;
            if (dragSource < 0 && lootDragSource < 0 && quickDragSource < 0)
                return;

            if (dragSource >= 0)
                slots[dragSource].Group.alpha = 1f;
            if (quickDragSource >= 0 && dragHud != null)
                dragHud.SetQuickSlotDragging(quickDragSource, false);
            dragPreview.Rect.gameObject.SetActive(false);
            if (dragPreview.Rect.parent != panel.transform)
                dragPreview.Rect.SetParent(panel.transform, false);
            chestPanel?.HighlightDrop(false);
            dragHud?.HighlightQuickSlot(-1);
            dragSource = -1;
            quickDragSource = -1;
            quickDragItem = null;
            lootDragSource = -1;
            lootDragItem = null;
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
            return mouse == null ? -1 : FindPointerSlot(mouse.position.ReadValue());
        }

        private int FindPointerSlot(Vector2 position)
        {
            Camera eventCamera = EventCamera;
            for (int index = 0; index < slots.Length; index++)
            {
                if (slots[index].Rect.gameObject.activeSelf &&
                    RectTransformUtility.RectangleContainsScreenPoint(slots[index].Rect, position, eventCamera))
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
            private readonly UnityEngine.UI.Text tierBadge;
            private bool occupied;
            private ItemTier tier;

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

                // 왼쪽 위 등급 글자입니다. 테두리도 등급 색으로 바뀝니다.
                tierBadge = Instantiate(count, slot.transform);
                tierBadge.gameObject.name = "Tier";
                tierBadge.alignment = TextAnchor.UpperLeft;
                tierBadge.fontSize = 12;
                tierBadge.fontStyle = FontStyle.Bold;
                tierBadge.rectTransform.anchorMin = Vector2.zero;
                tierBadge.rectTransform.anchorMax = Vector2.one;
                tierBadge.rectTransform.offsetMin = new Vector2(5, 5);
                tierBadge.rectTransform.offsetMax = new Vector2(-5, -3);

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
                count.text = occupied && stack.Item.MaxStack > 1 && stack.Count > 1 ? $"×{stack.Count}" : string.Empty;
                tier = occupied ? stack.Item.Tier : ItemTier.None;
                tierBadge.text = TierStyle.Label(tier);
                tierBadge.color = TierStyle.Color(tier);
                SetHighlighted(occupied && highlighted);
            }

            public void SetHighlighted(bool highlighted)
            {
                Color borderColor = !occupied ? EmptyBorder : tier != ItemTier.None ? TierStyle.Color(tier) : OccupiedBorder;
                border.effectColor = highlighted ? HoverBorder : borderColor;
            }
        }
    }
}
