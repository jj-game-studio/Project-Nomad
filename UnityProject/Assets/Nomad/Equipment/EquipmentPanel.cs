using Nomad.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Equipment
{
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(PlayerEquipment))]
    public sealed class EquipmentPanel : MonoBehaviour
    {
        private static readonly string[] Labels = { "무기 1", "무기 2", "머리", "신체", "가방", "얼굴", "장신구", "반지", "토템 1", "토템 2" };
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
        private int dragEquipment = -1;
        private ItemDefinition draggedEquipmentItem;
        private Vector2 equipmentPressPosition;
        private bool equipmentDragging;
        private Mouse dragMouse;
        private bool wasOpen;

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
        public int BagPreviewSlot { get; private set; } = -1;
        public bool CanDropToBag { get; private set; }

        private void Start()
        {
            equipment = GetComponent<PlayerEquipment>();
            inventory = GetComponent<PlayerInventory>();
            combat = GetComponent<PlayerEquipmentCombat>();
            visuals = GetComponent<EquipmentCombatVisuals>();
            var view = InventoryPanel.GetOrCreate(inventory);
            panel = (RectTransform)view.transform.Find("InventoryPanel");
            bag = (RectTransform)panel.Find("Slots");
            inventoryDragPreview = (RectTransform)panel.Find("DragPreview");
            capacity = panel.Find("Capacity").GetComponent<UnityEngine.UI.Text>();
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 16);

            panel.sizeDelta = new Vector2(484, 752);
            panel.anchoredPosition = Vector2.zero;
            Place((RectTransform)panel.Find("Title"), new Vector2(432, 32), new Vector2(0, -26));
            var title = panel.Find("Title").GetComponent<UnityEngine.UI.Text>();
            title.font = font;
            title.text = "장비";
            title.fontSize = 23;
            Place(capacity.rectTransform, new Vector2(432, 30), new Vector2(0, -264));
            capacity.font = font;
            Place(bag, new Vector2(432, 344), new Vector2(0, -286), new Vector2(0.5f, 1));
            var details = panel.Find("ItemDetails").GetComponent<UnityEngine.UI.Text>();
            Place(details.rectTransform, new Vector2(432, 30), new Vector2(0, -652));
            details.font = font;

            equipmentGrid = Rect("EquipmentSlots", panel, new Vector2(432, 184), new Vector2(0, -52), new Vector2(0.5f, 1));
            var layout = equipmentGrid.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            layout.cellSize = new Vector2(80, 88);
            layout.spacing = new Vector2(8, 8);
            layout.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 5;
            for (int index = 0; index < 10; index++) CreateSlot(index);

            notice = Text("EquipmentHelp", panel, "가방 → 장비 드래그 / 선택 후 장비 클릭\n장비 우클릭: 해제 · 무기 클릭: 선택", new Vector2(440, 44), new Vector2(0, -704), 13);
            toolbar = Rect("EquipmentDebugToolbar", view.transform, new Vector2(330, 42), Vector2.zero);
            toolbar.anchorMin = toolbar.anchorMax = toolbar.pivot = new Vector2(0, 1);
            toolbar.anchoredPosition = new Vector2(18, -18);
            var background = toolbar.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color32(16, 29, 43, 240);
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
            if (panel == null)
                return;

            FitToCanvas();

            Mouse mouse = Mouse.current;
            if (combat != null) combat.EquipmentUiOpen = IsOpen ||
                (mouse != null && Contains(toolbar, mouse.position.ReadValue()));
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
                return;

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
        }

        public void FitToCanvas()
        {
            if (panel == null) return;
            Canvas canvas = panel.GetComponentInParent<Canvas>();
            float height = canvas.pixelRect.height / canvas.scaleFactor;
            float width = canvas.pixelRect.width / canvas.scaleFactor;
            float scale = Mathf.Min(1f, (height - 48f) / 752f, (width - 32f) / 484f);
            panel.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        }

        private void ProcessPointer(Mouse mouse)
        {
            Vector2 point = mouse.position.ReadValue();
            int bagSlot = Find(bag, point);
            int gearSlot = Find(equipmentGrid, point);
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
            if (gearSlot >= 0 && selected >= 0 && selectedItem != null)
            {
                notice.text = equipment.TryEquip(selected, (EquipmentSlot)gearSlot) ? "장착했습니다." : "이 칸에 장착할 수 없는 아이템입니다.";
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
                if (!equipmentDragging && gearSlot <= 1) equipment.SelectWeapon((EquipmentSlot)gearSlot);
                CancelSelection();
            }
            else if (dragEquipment >= 0 || (bagSlot < 0 && gearSlot < 0))
                CancelSelection();
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
            PreviewSlot = -1;
            CanDrop = false;
            capacity.text = $"가방 ({inventory.UsedSlots} / {PlayerInventory.Capacity})";
            for (int index = 0; index < 10; index++)
            {
                ItemDefinition item = equipment.GetItem((EquipmentSlot)index);
                names[index].text = item != null ? item.DisplayName : "—";
                icons[index].sprite = item?.Icon;
                icons[index].enabled = item?.Icon != null;
                icons[index].color = Color.white;
                names[index].color = new Color32(238, 235, 222, 255);
                backgrounds[index].color = new Color32(37, 55, 74, 255);
                borders[index].effectColor = (int)equipment.ActiveWeaponSlot == index
                    ? new Color32(238, 186, 88, 255) : new Color32(108, 128, 148, 255);
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

            names[target].text = selectedItem.DisplayName;
            names[target].color = new Color32(163, 242, 210, 210);
            icons[target].sprite = selectedItem.Icon;
            icons[target].enabled = selectedItem.Icon != null;
            icons[target].color = new Color(1, 1, 1, 0.6f);
        }

        private void CreateSlot(int index)
        {
            RectTransform slot = Rect("EquipmentSlot" + index, equipmentGrid, new Vector2(80, 88), Vector2.zero);
            var image = slot.gameObject.AddComponent<UnityEngine.UI.Image>();
            backgrounds[index] = image;
            image.color = new Color32(37, 55, 74, 255);
            borders[index] = slot.gameObject.AddComponent<UnityEngine.UI.Outline>();
            borders[index].effectDistance = new Vector2(1, -1);
            borders[index].useGraphicAlpha = false;
            var iconRect = Rect("Icon", slot, new Vector2(42, 42), new Vector2(0, -32));
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
            if (combat != null) combat.EquipmentUiOpen = false;
            CancelSelection();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) CancelSelection();
        }

        private void OnDestroy()
        {
            if (toolbar != null) Destroy(toolbar.gameObject);
            if (font != null) Destroy(font);
        }
    }
}
