using Nomad.Inventory;
using Nomad.Hud;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Equipment
{
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(PlayerEquipment))]
    public sealed class EquipmentPanel : MonoBehaviour
    {
        private static readonly string[] Labels = { "무기 1", "무기 2", "투구", "갑옷", "바지", "신발", "가방", "장신구 1", "장신구 2", "장신구 3" };
        private readonly UnityEngine.UI.Text[] names = new UnityEngine.UI.Text[10];
        private readonly UnityEngine.UI.Image[] icons = new UnityEngine.UI.Image[10];
        private readonly UnityEngine.UI.Outline[] borders = new UnityEngine.UI.Outline[10];
        private readonly UnityEngine.UI.Image[] backgrounds = new UnityEngine.UI.Image[10];
        private PlayerEquipment equipment;
        private PlayerInventory inventory;
        private PlayerEquipmentCombat combat;
        private EquipmentCombatVisuals visuals;
        private RectTransform panel;
        private RectTransform bag;
        private RectTransform equipmentGrid;
        private RectTransform toolbar;
        private RectTransform inventoryDragPreview;
        private RectTransform equipmentDragPreview;
        private RectTransform bagDropPreview;
        private UnityEngine.UI.Text equipmentDragName;
        private UnityEngine.UI.Text bagDropName;
        private UnityEngine.UI.Image equipmentDragIcon;
        private UnityEngine.UI.Image bagDropIcon;
        private UnityEngine.UI.Image bagDropBackground;
        private UnityEngine.UI.Text capacity;
        private UnityEngine.UI.Text notice;
        private UnityEngine.UI.Text rangeLabel;
        private Font font;
        private int selected = -1;
        private ItemDefinition selectedItem;
        private EquipmentSlot enhancementSlot;
        private UnityEngine.UI.Button enhanceButton;
        private UnityEngine.UI.Text enhanceLabel;
        private int dragEquipment = -1;
        private ItemDefinition draggedEquipmentItem;
        private Vector2 equipmentPressPosition;
        private bool equipmentDragging;
        private Mouse dragMouse;
        private Vector2 bagPressPosition;
        // 더블클릭 판정용 직전 클릭입니다. 가방 칸은 0 이상, 장비칸은 -1-번호로 저장합니다.
        private const float DoubleClickInterval = 0.3f;
        private int lastClickTarget = int.MinValue;
        private ItemDefinition lastClickItem;
        private float lastClickTime;
        private bool wasOpen;
        private bool pointerFocused = true;
        private InventoryPanel inventoryView;
        private RectTransform bagTooltip;
        private RectTransform tooltipCanvas;
        private InventoryTooltip hoverTooltip;
        private readonly ItemDefinition[] comparisonItems = new ItemDefinition[3];
        private const int StatCount = (int)StatType.InventorySlots + 1;
        private const float PanelWidth = 484f;
        private const float StatsWidth = 260f;
        private const float StatsGap = 12f;
        private readonly UnityEngine.UI.Text[] statValues = new UnityEngine.UI.Text[StatCount];
        private PlayerStats stats;
        private PlayerHud hud;
        private RectTransform statsPanel;
        private UnityEngine.UI.Text details;
        private int laidOutSlots = -1;

        public bool IsOpen => panel != null && panel.gameObject.activeInHierarchy;
        public RectTransform EquipmentGrid => equipmentGrid;
        public RectTransform BagGrid => bag;
        public RectTransform Toolbar => toolbar;
        public string Notice => notice != null ? notice.text : string.Empty;
        public int PreviewSlot { get; private set; } = -1;
        public bool CanDrop { get; private set; }
        public RectTransform DragPreview => inventoryDragPreview;
        public RectTransform EquipmentDragPreview => equipmentDragPreview;
        public RectTransform BagDropPreview => bagDropPreview;
        public RectTransform StatsPanel => statsPanel;
        public string StatText(StatType stat) => statValues[(int)stat] != null ? statValues[(int)stat].text : string.Empty;
        public int BagPreviewSlot { get; private set; } = -1;
        public bool CanDropToBag { get; private set; }

        private void Start()
        {
            equipment = GetComponent<PlayerEquipment>();
            inventory = GetComponent<PlayerInventory>();
            combat = GetComponent<PlayerEquipmentCombat>();
            visuals = GetComponent<EquipmentCombatVisuals>();
            stats = GetComponent<PlayerStats>();
            hud = GetComponentInChildren<PlayerHud>();
            var view = InventoryPanel.GetOrCreate(inventory);
            inventoryView = view;
            tooltipCanvas = (RectTransform)view.GetComponent<Canvas>().transform;
            bagTooltip = (RectTransform)tooltipCanvas.Find("InventoryTooltip");
            panel = (RectTransform)view.transform.Find("InventoryPanel");
            bag = (RectTransform)panel.Find("Slots");
            inventoryDragPreview = (RectTransform)panel.Find("DragPreview");
            capacity = panel.Find("Capacity").GetComponent<UnityEngine.UI.Text>();
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 16);

            hoverTooltip = new InventoryTooltip(tooltipCanvas, font, "EquipmentTooltip");

            panel.sizeDelta = new Vector2(PanelWidth, 752);
            panel.anchoredPosition = Vector2.zero;
            Place((RectTransform)panel.Find("Title"), new Vector2(432, 32), new Vector2(0, -26));
            var title = panel.Find("Title").GetComponent<UnityEngine.UI.Text>();
            title.font = font;
            title.text = "장비";
            title.fontSize = 23;
            title.color = new Color32(244, 237, 215, 255);
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleLeft;
            Place(capacity.rectTransform, new Vector2(432, 30), new Vector2(0, -264));
            capacity.font = font;
            details = panel.Find("ItemDetails").GetComponent<UnityEngine.UI.Text>();
            details.font = font;

            equipmentGrid = Rect("EquipmentSlots", panel, new Vector2(432, 184), new Vector2(0, -52), new Vector2(0.5f, 1));
            var layout = equipmentGrid.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            layout.cellSize = new Vector2(80, 88);
            layout.spacing = new Vector2(8, 8);
            layout.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 5;
            for (int index = 0; index < 10; index++) CreateSlot(index);

            // 결과 문구는 검증용으로 유지하되, 화면 하단 HUD와 겹치므로 표시하지 않습니다.
            notice = Text("EquipmentHelp", panel, "", new Vector2(440, 44), Vector2.zero, 13);
            notice.gameObject.SetActive(false);
            CreateStatsPanel();
            LayoutBag();
            toolbar = Rect("EquipmentDebugToolbar", view.transform, new Vector2(330, 42), Vector2.zero);
            toolbar.anchorMin = toolbar.anchorMax = toolbar.pivot = new Vector2(0, 1);
            toolbar.anchoredPosition = new Vector2(18, -18);
            var background = toolbar.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color32(16, 29, 43, 240);
            // 클릭은 위치 검사로 처리하므로 카메라가 열린 창으로 인식하지 않게 합니다.
            background.raycastTarget = false;
            rangeLabel = Text("RangeToggle", toolbar, "", new Vector2(310, 32), new Vector2(165, -21), 16);
            rangeLabel.rectTransform.anchorMin = rangeLabel.rectTransform.anchorMax = new Vector2(0, 1);
            equipmentDragPreview = CreatePreview("EquipmentDragPreview", out equipmentDragName, out equipmentDragIcon);
            bagDropPreview = CreatePreview("BagDropPreview", out bagDropName, out bagDropIcon);
            bagDropBackground = bagDropPreview.GetComponent<UnityEngine.UI.Image>();
            Refresh();
            FitToCanvas();
        }

        private void LateUpdate()
        {
            HideTooltips();
            if (panel == null)
                return;

            FitToCanvas();
            toolbar.gameObject.SetActive(!IsOpen);
            // 장비 창과 오른쪽 스탯 창을 합친 영역이 화면 가운데 오도록 옮깁니다. 상자 창이 열리면 가방 UI 배치를 따릅니다.
            bool looting = inventoryView != null && inventoryView.HasOpenChest;
            if (statsPanel != null) statsPanel.gameObject.SetActive(!looting);
            float extraWidth = looting ? inventoryView.ChestLayoutWidth : StatsGap + StatsWidth;
            panel.anchoredPosition = new Vector2(-extraWidth * panel.localScale.x / 2f, 0);
            inventoryView?.UpdateChestLayout();

            Mouse mouse = Mouse.current;
            if (combat != null) combat.EquipmentUiOpen = IsOpen ||
                (mouse != null && Contains(toolbar, mouse.position.ReadValue()));
            if (inventory?.BlocksTradeInput == true || inventoryView?.BlocksSplitInput == true)
            {
                CancelSelection();
                ResetClick();
                return;
            }

            if (visuals != null)
            {
                rangeLabel.text = $"공격 범위 [F2] : {(visuals.ShowRange ? "ON" : "OFF")}  (클릭)";
                if (mouse?.leftButton.wasReleasedThisFrame == true && Contains(toolbar, mouse.position.ReadValue()))
                    visuals.ShowRange = !visuals.ShowRange;
            }

            if (!IsOpen || wasOpen != IsOpen || mouse != dragMouse || Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                CancelSelection();
            wasOpen = IsOpen;
            if (!IsOpen)
            {
                ResetClick();
                return;
            }

            if (selected >= 0 && inventory.GetSlot(selected).Item != selectedItem)
                CancelSelection();
            if (dragEquipment >= 0 && equipment.GetItem((EquipmentSlot)dragEquipment) != draggedEquipmentItem)
                CancelSelection();
            if (mouse != null) ProcessPointer(mouse);
            Refresh();
            UpdateDropPreview(mouse);
            UpdateEquipmentDrag(mouse);
            // 장비 UI가 나중에 생성되어도 가방의 기존 드래그 아이템을 가리지 않습니다.
            if (inventoryDragPreview != null && inventoryDragPreview.gameObject.activeInHierarchy)
                inventoryDragPreview.SetAsLastSibling();
            UpdateTooltips();
        }

        private void UpdateTooltips()
        {
            Mouse mouse = Mouse.current;
            if (!IsOpen || !pointerFocused || inventoryView == null || !inventoryView.isActiveAndEnabled || mouse == null ||
                mouse.leftButton.isPressed || mouse.rightButton.isPressed ||
                inventoryDragPreview.gameObject.activeInHierarchy || equipmentDragging)
                return;

            Vector2 point = mouse.position.ReadValue();
            int gearSlot = Find(equipmentGrid, point);
            Canvas canvas = tooltipCanvas.GetComponent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (gearSlot >= 0)
            {
                ItemDefinition item = equipment.GetItem((EquipmentSlot)gearSlot);
                hoverTooltip.ShowCard(item, 1, point, camera, 320f, "장착 중 · " + Labels[gearSlot]);
                return;
            }

            int index = Find(bag, point);
            ItemDefinition candidate = index >= 0 ? inventory.GetSlot(index).Item : null;
            // 기본 가방 표시 상태를 따라 드래그·포커스·상자 입력 차단을 유지합니다.
            if (candidate == null || candidate.EquipmentType == EquipmentType.None || candidate.EquipmentType == EquipmentType.Weapon ||
                bagTooltip == null || !bagTooltip.gameObject.activeInHierarchy)
                return;

            int count = 0;
            for (int slot = 2; slot < PlayerEquipment.Capacity && count < comparisonItems.Length; slot++)
            {
                ItemDefinition equipped = equipment.GetItem((EquipmentSlot)slot);
                if (equipped != null && equipped.EquipmentType == candidate.EquipmentType)
                    comparisonItems[count++] = equipped;
            }
            if (count == 0) return;

            // 별도 장착품 창 대신 한 툴팁의 능력치 옆에 증감만 표시합니다.
            bagTooltip.gameObject.SetActive(false);
            hoverTooltip.ShowCompared(candidate, point, camera, comparisonItems, count);
        }

        private void HideTooltips() => hoverTooltip?.Hide();

        public void FitToCanvas()
        {
            if (panel == null) return;
            Canvas canvas = panel.GetComponentInParent<Canvas>();
            float height = canvas.pixelRect.height / canvas.scaleFactor;
            float width = canvas.pixelRect.width / canvas.scaleFactor;
            // 오른쪽 스탯 창까지 화면 안에 들어오도록 전체 너비로 배율을 정합니다.
            float total = PanelWidth + (inventoryView != null && inventoryView.HasOpenChest
                ? inventoryView.ChestLayoutWidth : StatsGap + StatsWidth);
            float scale = Mathf.Min(1f, (height - 48f) / panel.sizeDelta.y, (width - 32f) / total);
            panel.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        }

        private void ProcessPointer(Mouse mouse)
        {
            Vector2 point = mouse.position.ReadValue();
            int bagSlot = Find(bag, point);
            int gearSlot = Find(equipmentGrid, point);
            if (mouse.rightButton.wasPressedThisFrame && bagSlot >= 0)
            {
                notice.text = equipment.TryEquipAuto(bagSlot) ? "장착했습니다." : "장착할 수 없는 아이템입니다.";
                CancelSelection();
                ResetClick();
                return;
            }

            if (mouse.rightButton.wasPressedThisFrame && gearSlot >= 0)
            {
                bool removed = equipment.TryUnequip((EquipmentSlot)gearSlot);
                notice.text = removed ? "장비를 가방에 넣었습니다." : "빈 가방 칸이 없거나 장비가 비어 있습니다.";
                CancelSelection();
            }
            if (mouse.leftButton.wasPressedThisFrame)
            {
                dragMouse = mouse;
                if (bagSlot >= 0)
                {
                    bagPressPosition = point;
                    selected = bagSlot;
                    selectedItem = inventory.GetSlot(bagSlot).Item;
                    dragEquipment = -1;
                    if (selectedItem != null) notice.text = $"선택: {selectedItem.DisplayName}\n장비칸에 드래그하거나 장비칸을 클릭하세요.";
                }
                else if (gearSlot >= 0)
                {
                    // 클릭 장착 선택은 유지하되, 실제 이동이 시작되면 장비 해제 드래그로 전환합니다.
                    dragMouse = mouse;
                    dragEquipment = gearSlot;
                    draggedEquipmentItem = equipment.GetItem((EquipmentSlot)gearSlot);
                    equipmentPressPosition = point;
                    equipmentDragging = false;
                }
            }

            if (dragEquipment >= 0 && draggedEquipmentItem != null &&
                (point - equipmentPressPosition).sqrMagnitude >= 25f)
            {
                if (!equipmentDragging) notice.text = $"해제: {draggedEquipmentItem.DisplayName}\n가방으로 드래그하세요. 호환 장비 칸은 교체합니다.";
                equipmentDragging = true;
                selected = -1;
                selectedItem = null;
            }

            if (!mouse.leftButton.wasReleasedThisFrame)
                return;

            // 드래그 없이 같은 칸을 두 번 누르면 더블클릭입니다. 그 밖의 놓기는 직전 클릭 기록을 지웁니다.
            bool bagClick = bagSlot >= 0 && selected == bagSlot && selectedItem != null &&
                (point - bagPressPosition).sqrMagnitude < 25f;
            bool gearClick = gearSlot >= 0 && dragEquipment == gearSlot && !equipmentDragging;
            if (!bagClick && !gearClick)
                ResetClick();

            if (bagClick && !inventoryView.HasOpenChest && selectedItem.EquipmentType != EquipmentType.None)
            {
                // 상자가 열려 있으면 가방 더블클릭은 기존처럼 상자 넣기에 양보합니다.
                if (IsDoubleClick(bagSlot, selectedItem))
                {
                    notice.text = equipment.TryEquipAuto(bagSlot) ? "장착했습니다." : "장착할 수 없는 아이템입니다.";
                    CancelSelection();
                }
            }
            else if (bagClick)
                ResetClick();
            else if (gearSlot >= 0 && selected >= 0 && selectedItem != null)
            {
                notice.text = equipment.TryEquip(selected, (EquipmentSlot)gearSlot) ? "장착했습니다." : "이 칸에 장착할 수 없는 아이템입니다.";
                CancelSelection();
            }
            else if (dragEquipment >= 0 && equipmentDragging && hud != null && hud.FindQuickSlot(point) >= 0)
            {
                hud.QuickSlots.RegisterEquipped((EquipmentSlot)dragEquipment, hud.FindQuickSlot(point));
                CancelSelection();
            }
            else if (bagSlot >= 0 && dragEquipment >= 0 && equipmentDragging)
            {
                bool changed = inventory.GetSlot(bagSlot).Item == null
                    ? equipment.TryUnequip((EquipmentSlot)dragEquipment, bagSlot)
                    : equipment.TryEquip(bagSlot, (EquipmentSlot)dragEquipment);
                notice.text = changed ? "장비를 가방으로 옮겼습니다." : "옮길 수 없는 가방 칸입니다.";
                CancelSelection();
            }
            else if (gearSlot >= 0 && dragEquipment == gearSlot)
            {
                enhancementSlot = (EquipmentSlot)gearSlot;
                ItemDefinition item = equipment.GetItem((EquipmentSlot)gearSlot);
                if (gearClick && item != null && IsDoubleClick(-1 - gearSlot, item))
                {
                    // 빈 가방 칸이 없거나 가방 용량 규칙에 막히면 TryUnequip이 거절해 그대로 둡니다.
                    bool removed = equipment.TryUnequip((EquipmentSlot)gearSlot);
                    notice.text = removed ? "장비를 가방에 넣었습니다." : "빈 가방 칸이 없어 해제할 수 없습니다.";
                }
                else if (gearClick && gearSlot <= 1)
                    equipment.SelectWeapon((EquipmentSlot)gearSlot);
                CancelSelection();
            }
            else if (dragEquipment >= 0 || (bagSlot < 0 && gearSlot < 0))
                CancelSelection();
        }

        private bool IsDoubleClick(int target, ItemDefinition item)
        {
            float now = Time.unscaledTime;
            bool twice = lastClickTarget == target && lastClickItem == item && now - lastClickTime <= DoubleClickInterval;

            // 세 번째 클릭이 다시 더블클릭으로 잡히지 않도록 성공하면 기록을 비웁니다.
            lastClickTarget = twice ? int.MinValue : target;
            lastClickItem = twice ? null : item;
            lastClickTime = now;
            return twice;
        }

        private void ResetClick()
        {
            lastClickTarget = int.MinValue;
            lastClickItem = null;
        }

        private void UpdateEquipmentDrag(Mouse mouse)
        {
            BagPreviewSlot = -1;
            CanDropToBag = false;
            bagDropPreview.gameObject.SetActive(false);
            if (mouse == null || !mouse.leftButton.isPressed || dragEquipment < 0 || draggedEquipmentItem == null)
                return;

            Vector2 point = mouse.position.ReadValue();
            if (!equipmentDragging) return;

            hud?.HighlightQuickSlot(hud.FindQuickSlot(point), equipment.GetItem((EquipmentSlot)dragEquipment));
            equipmentDragPreview.gameObject.SetActive(true);
            BindPreview(equipmentDragName, equipmentDragIcon, draggedEquipmentItem);
            PositionPreview(equipmentDragPreview, point);
            names[dragEquipment].color = new Color(1, 1, 1, 0.4f);
            icons[dragEquipment].color = new Color(1, 1, 1, 0.4f);

            int target = Find(bag, point);
            if (target >= 0)
            {
                BagPreviewSlot = target;
                PlayerInventory.ItemStack destination = inventory.GetSlot(target);
                CanDropToBag = equipment.isActiveAndEnabled && (destination.Item == null ||
                    (destination.Count == 1 && destination.Item.MaxStack == 1 &&
                     PlayerEquipment.Fits(destination.Item.EquipmentType, (EquipmentSlot)dragEquipment)));
                var targetRect = (RectTransform)bag.GetChild(target);
                bagDropPreview.sizeDelta = targetRect.rect.size - Vector2.one * 4;
                bagDropPreview.position = targetRect.TransformPoint(targetRect.rect.center);
                bagDropBackground.color = CanDropToBag ? new Color(0.18f, 0.49f, 0.38f, 0.82f) : new Color(0.59f, 0.2f, 0.23f, 0.82f);
                BindPreview(bagDropName, bagDropIcon, CanDropToBag ? draggedEquipmentItem : null);
                bagDropPreview.gameObject.SetActive(true);
                bagDropPreview.SetAsLastSibling();
            }
            equipmentDragPreview.SetAsLastSibling();
        }

        private RectTransform CreatePreview(string name, out UnityEngine.UI.Text label, out UnityEngine.UI.Image icon)
        {
            RectTransform rect = Rect(name, panel, new Vector2(80, 80), Vector2.zero);
            rect.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(56, 65, 71, 230);
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0.8f;
            group.blocksRaycasts = false;
            var iconRect = Rect("Icon", rect, new Vector2(54, 54), new Vector2(0, -40));
            icon = iconRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            label = Text("ItemName", rect, "", new Vector2(76, 44), new Vector2(0, -40), 13);
            rect.gameObject.SetActive(false);
            return rect;
        }

        private static void BindPreview(UnityEngine.UI.Text label, UnityEngine.UI.Image icon, ItemDefinition item)
        {
            icon.sprite = item?.Icon;
            icon.enabled = item?.Icon != null;
            label.text = item != null && item.Icon == null ? item.DisplayName : string.Empty;
        }

        private void PositionPreview(RectTransform preview, Vector2 point)
        {
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(panel, point, null, out Vector3 world))
                preview.position = world;
        }

        private void Refresh()
        {
            if (enhanceButton != null)
            {
                ItemDefinition item = equipment.GetItem(enhancementSlot);
                enhanceLabel.text = item == null ? "장비 선택 후 강화" : $"{item.DisplayName} 강화";
                enhanceButton.interactable = equipment.EnhancementMaterial(enhancementSlot) >= 0;
            }
            PreviewSlot = -1;
            CanDrop = false;
            capacity.text = $"가방 ({inventory.UsedSlots} / {inventory.SlotCount})";
            LayoutBag();
            RefreshStats();
            for (int index = 0; index < 10; index++)
            {
                ItemDefinition item = equipment.GetItem((EquipmentSlot)index);
                names[index].text = item == null ? string.Empty : item.Icon == null ? item.DisplayName :
                    item.EnhancementLevel > 0 ? $"+{item.EnhancementLevel}" : string.Empty;
                EquipmentType type = index < 2 ? EquipmentType.Weapon : index == 2 ? EquipmentType.Head :
                    index == 3 ? EquipmentType.Body : index == 4 ? EquipmentType.Legs : index == 5 ? EquipmentType.Feet :
                    index == 6 ? EquipmentType.Bag : EquipmentType.Accessory;
                icons[index].sprite = item != null ? item.Icon : ItemUiStyle.Symbol(type);
                icons[index].enabled = icons[index].sprite != null;
                icons[index].color = item != null ? Color.white : new Color(1, 1, 1, 0.45f);
                names[index].color = new Color32(238, 235, 222, 255);
                backgrounds[index].color = ItemUiStyle.SlotFill;
                // 선택한 무기칸은 금색, 나머지는 장착 장비의 등급 색으로 테두리를 그립니다.
                borders[index].effectColor = (int)equipment.ActiveWeaponSlot == index ? new Color32(238, 186, 88, 255)
                    : item != null && item.Tier != ItemTier.None ? TierStyle.Color(item.Tier) : new Color32(108, 128, 148, 255);
            }
        }

        // 가방 장비로 칸 수가 바뀌면 가방 영역과 창 높이를 행 수에 맞춥니다.
        private void LayoutBag()
        {
            if (details == null || laidOutSlots == inventory.SlotCount)
                return;

            laidOutSlots = inventory.SlotCount;
            int rows = Mathf.CeilToInt(laidOutSlots / 5f);
            float bagHeight = rows * 88f - 8f;
            Place(bag, new Vector2(432, bagHeight), new Vector2(0, -286), new Vector2(0.5f, 1));
            Place(details.rectTransform, new Vector2(432, 30), new Vector2(0, -286 - bagHeight - 22));
            panel.sizeDelta = new Vector2(PanelWidth, 286 + bagHeight + 52);

            if (statsPanel != null)
                statsPanel.sizeDelta = new Vector2(StatsWidth, panel.sizeDelta.y);
            FitToCanvas();
        }

        // 장비 창 오른쪽에 붙은 스탯 창입니다. 장비 창과 함께 열리고 배율도 함께 바뀝니다.
        private void CreateStatsPanel()
        {
            statsPanel = Rect("StatsPanel", panel, new Vector2(StatsWidth, panel.sizeDelta.y), Vector2.zero, new Vector2(0, 1));
            statsPanel.anchorMin = statsPanel.anchorMax = new Vector2(1, 1);
            statsPanel.anchoredPosition = new Vector2(StatsGap, 0);
            var background = statsPanel.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = panel.TryGetComponent(out UnityEngine.UI.Image source) ? source.color : new Color32(24, 26, 28, 245);
            background.raycastTarget = false;

            var title = Text("Title", statsPanel, "스탯", new Vector2(220, 32), new Vector2(0, -26), 23);
            ItemUiStyle.Frame(statsPanel);
            title.alignment = TextAnchor.MiddleLeft;
            var forge = Rect("EnhanceButton", statsPanel, new Vector2(220, 40), new Vector2(0, 35), new Vector2(0.5f, 0.5f));
            forge.anchorMin = forge.anchorMax = new Vector2(0.5f, 0);
            forge.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(93, 71, 38, 255);
            enhanceButton = forge.gameObject.AddComponent<UnityEngine.UI.Button>();
            enhanceButton.onClick.AddListener(() =>
            {
                notice.text = equipment.TryEnhance(enhancementSlot) ? "동일 장비 1개를 소비해 강화했습니다." : "강화할 동일 장비가 필요합니다. 최대 +3입니다.";
            });
            enhanceLabel = Text("Label", forge, "장비 선택 후 강화", new Vector2(214, 36), Vector2.zero, 14);
            enhanceLabel.rectTransform.anchorMin = enhanceLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            enhanceLabel.raycastTarget = false;
            var help = Text("EnhanceHelp", statsPanel, "동일 장비 1개 · 확정 성공 · 최대 +3", new Vector2(230, 38), new Vector2(0, 78), 12);
            help.rectTransform.anchorMin = help.rectTransform.anchorMax = new Vector2(0.5f, 0);
            for (int index = 0; index < StatCount; index++)
            {
                var label = Text("Stat" + (StatType)index, statsPanel, StatFormat.Name((StatType)index),
                    new Vector2(220, 26), new Vector2(0, -66 - index * 30), 15);
                label.alignment = TextAnchor.MiddleLeft;
                statValues[index] = Text("Value", label.transform, "", new Vector2(220, 26), Vector2.zero, 15);
                statValues[index].rectTransform.anchorMin = statValues[index].rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                statValues[index].alignment = TextAnchor.MiddleRight;
                statValues[index].supportRichText = true;
            }
        }

        private void RefreshStats()
        {
            // 상자 창이 오른쪽에 열리면 겹치지 않게 스탯 창을 숨깁니다.
            if (statsPanel == null)
                return;

            statsPanel.gameObject.SetActive(stats != null && !inventoryView.HasOpenChest);
            if (stats == null)
                return;

            for (int index = 0; index < StatCount; index++)
            {
                var stat = (StatType)index;
                float bonus = stat == StatType.Attack || stat == StatType.InventorySlots ? 0 : equipment.Sum(stat);
                string text = StatFormat.Value(stat, stats.Get(stat));

                // 장비로 오른 수치는 괄호 안에 초록색으로 함께 보여 줍니다. 방어력은 전부 장비 수치입니다.
                if (Mathf.Abs(bonus) >= 0.05f && stat != StatType.Defense)
                    text += $" <color=#80D99A>({StatFormat.Bonus(stat, bonus)})</color>";
                statValues[index].text = text;
            }
        }

        private void UpdateDropPreview(Mouse mouse)
        {
            if (mouse == null || !mouse.leftButton.isPressed || selected < 0 || selectedItem == null ||
                inventoryDragPreview == null || !inventoryDragPreview.gameObject.activeInHierarchy)
                return;

            int target = Find(equipmentGrid, mouse.position.ReadValue());
            if (target < 0) return;

            PreviewSlot = target;
            CanDrop = inventory.GetSlot(selected).Count == 1 && selectedItem.MaxStack == 1 &&
                PlayerEquipment.Fits(selectedItem.EquipmentType, (EquipmentSlot)target);
            borders[target].effectColor = CanDrop ? new Color32(100, 235, 180, 255) : new Color32(237, 106, 103, 255);
            backgrounds[target].color = CanDrop ? new Color32(42, 79, 76, 255) : new Color32(91, 45, 52, 255);
            if (!CanDrop) return;

            names[target].text = selectedItem.Icon == null ? selectedItem.DisplayName : string.Empty;
            names[target].color = new Color32(163, 242, 210, 210);
            icons[target].sprite = selectedItem.Icon;
            icons[target].enabled = selectedItem.Icon != null;
            icons[target].color = new Color(1, 1, 1, 0.6f);
        }

        private void CreateSlot(int index)
        {
            RectTransform slot = Rect("EquipmentSlot" + index, equipmentGrid, new Vector2(80, 88), Vector2.zero);
            var image = slot.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = ItemUiStyle.Rounded;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            backgrounds[index] = image;
            image.color = new Color32(37, 55, 74, 255);
            borders[index] = slot.gameObject.AddComponent<UnityEngine.UI.Outline>();
            borders[index].effectDistance = new Vector2(1, -1);
            borders[index].useGraphicAlpha = false;
            var iconRect = Rect("Icon", slot, new Vector2(60, 58), new Vector2(0, -32));
            icons[index] = iconRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            icons[index].preserveAspect = true;
            icons[index].raycastTarget = false;
            names[index] = Text("ItemName", slot, "", new Vector2(76, 42), new Vector2(0, -32), 13);
            Text("SlotLabel", slot, Labels[index], new Vector2(78, 22), new Vector2(0, -73), 13);
        }

        private UnityEngine.UI.Text Text(string name, Transform parent, string content, Vector2 size, Vector2 position, int fontSize)
        {
            var text = Rect(name, parent, size, position).gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color32(238, 235, 222, 255);
            text.text = content;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position, Vector2? pivot = null)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Place(rect, size, position, pivot);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 size, Vector2 position, Vector2? pivot = null)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static bool Contains(RectTransform rect, Vector2 point) => RectTransformUtility.RectangleContainsScreenPoint(rect, point);

        private static int Find(RectTransform grid, Vector2 point)
        {
            for (int index = 0; index < grid.childCount; index++)
                if (grid.GetChild(index).gameObject.activeInHierarchy && Contains((RectTransform)grid.GetChild(index), point))
                    return index;
            return -1;
        }

        private void CancelSelection()
        {
            bool restore = PreviewSlot >= 0 || dragEquipment >= 0;
            if (dragEquipment >= 0) hud?.HighlightQuickSlot(-1);
            selected = -1;
            selectedItem = null;
            dragEquipment = -1;
            draggedEquipmentItem = null;
            equipmentDragging = false;
            BagPreviewSlot = -1;
            CanDropToBag = false;
            if (equipmentDragPreview != null) equipmentDragPreview.gameObject.SetActive(false);
            if (bagDropPreview != null) bagDropPreview.gameObject.SetActive(false);
            PreviewSlot = -1;
            CanDrop = false;
            if (restore && equipmentGrid != null) Refresh();
        }

        private void OnDisable()
        {
            HideTooltips();
            if (combat != null) combat.EquipmentUiOpen = false;
            CancelSelection();
        }

        private void OnApplicationFocus(bool focused)
        {
            pointerFocused = focused;
            if (!focused)
            {
                CancelSelection();
                HideTooltips();
            }
        }

        private void OnDestroy()
        {
            hoverTooltip?.Dispose();
            if (toolbar != null) Destroy(toolbar.gameObject);
            if (font != null) Destroy(font);
        }
    }
}
