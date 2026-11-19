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
        private PlayerEquipment equipment;
        private PlayerInventory inventory;
        private PlayerEquipmentCombat combat;
        private EquipmentCombatVisuals visuals;
        private RectTransform panel;
        private RectTransform bag;
        private RectTransform equipmentGrid;
        private RectTransform toolbar;
        private UnityEngine.UI.Text capacity;
        private UnityEngine.UI.Text notice;
        private UnityEngine.UI.Text rangeLabel;
        private Font font;
        private int selected = -1;
        private ItemDefinition selectedItem;
        private int dragEquipment = -1;
        private Mouse dragMouse;
        private bool wasOpen;

        public bool IsOpen => panel != null && panel.gameObject.activeInHierarchy;
        public RectTransform EquipmentGrid => equipmentGrid;
        public RectTransform BagGrid => bag;
        public RectTransform Toolbar => toolbar;
        public string Notice => notice != null ? notice.text : string.Empty;

        private void Start()
        {
            equipment = GetComponent<PlayerEquipment>();
            inventory = GetComponent<PlayerInventory>();
            combat = GetComponent<PlayerEquipmentCombat>();
            visuals = GetComponent<EquipmentCombatVisuals>();
            var view = InventoryPanel.GetOrCreate(inventory);
            panel = (RectTransform)view.transform.Find("InventoryPanel");
            bag = (RectTransform)panel.Find("Slots");
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
            if (mouse != null) ProcessPointer(mouse);
            Refresh();
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
                    dragEquipment = gearSlot;
            }

            if (!mouse.leftButton.wasReleasedThisFrame)
                return;
            if (gearSlot >= 0 && selected >= 0 && selectedItem != null)
            {
                notice.text = equipment.TryEquip(selected, (EquipmentSlot)gearSlot) ? "장착했습니다." : "이 칸에 장착할 수 없는 아이템입니다.";
                CancelSelection();
            }
            else if (bagSlot >= 0 && dragEquipment >= 0)
            {
                bool changed = inventory.GetSlot(bagSlot).Item == null
                    ? equipment.TryUnequip((EquipmentSlot)dragEquipment, bagSlot)
                    : equipment.TryEquip(bagSlot, (EquipmentSlot)dragEquipment);
                notice.text = changed ? "장비를 가방으로 옮겼습니다." : "옮길 수 없는 가방 칸입니다.";
                CancelSelection();
            }
            else if (gearSlot >= 0 && dragEquipment == gearSlot)
            {
                if (gearSlot <= 1) equipment.SelectWeapon((EquipmentSlot)gearSlot);
                dragEquipment = -1;
            }
            else if (bagSlot < 0 && gearSlot < 0)
                CancelSelection();
        }

        private void Refresh()
        {
            capacity.text = $"가방 ({inventory.UsedSlots} / {PlayerInventory.Capacity})";
            for (int index = 0; index < 10; index++)
            {
                ItemDefinition item = equipment.GetItem((EquipmentSlot)index);
                names[index].text = item != null ? item.DisplayName : "—";
                icons[index].sprite = item?.Icon;
                icons[index].enabled = item?.Icon != null;
                borders[index].effectColor = (int)equipment.ActiveWeaponSlot == index
                    ? new Color32(238, 186, 88, 255) : new Color32(108, 128, 148, 255);
            }
        }

        private void CreateSlot(int index)
        {
            RectTransform slot = Rect("EquipmentSlot" + index, equipmentGrid, new Vector2(80, 88), Vector2.zero);
            var image = slot.gameObject.AddComponent<UnityEngine.UI.Image>();
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
            selected = -1;
            selectedItem = null;
            dragEquipment = -1;
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
