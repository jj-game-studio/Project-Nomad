using System;
using System.Collections.Generic;
using Nomad.Interaction;
using Nomad.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Nomad.Loot
{
    internal sealed class ChestLootPanel
    {
        private const float Width = 358f;
        private const float Gap = 16f;
        private static readonly Color Background = new Color32(19, 20, 22, 248);
        private static readonly Color Ink = new Color32(233, 234, 228, 255);
        private static readonly Color Muted = new Color32(170, 177, 174, 255);
        private static readonly Color Accent = new Color32(224, 182, 104, 255);
        private static readonly Color ViewportFill = new Color32(24, 26, 27, 255);
        private static readonly Color DropFill = new Color32(69, 57, 39, 255);

        private readonly PlayerInventory inventory;
        private readonly Font font;
        private readonly RectTransform backdrop;
        private readonly RectTransform root;
        private readonly Image dropArea;
        private readonly Outline dropOutline;
        private readonly RectTransform content;
        private readonly ScrollRect scroll;
        private readonly Text summary;
        private readonly Text feedback;
        private readonly Button takeAll;
        private readonly List<Row> rows = new();
        private PlayerInteraction interaction;

        public LootChest Chest { get; private set; }
        public bool IsOpen => root != null && root.gameObject.activeSelf;
        public float BackpackOffset => (Width + Gap) * 0.5f;

        public ChestLootPanel(RectTransform backpack, Font font, PlayerInventory inventory, Action close)
        {
            this.inventory = inventory;
            this.font = font;
            float height = backpack.rect.height;
            RectTransform parent = (RectTransform)backpack.parent;
            backdrop = CreateRect("Loot Backdrop", parent);
            SetRect(backdrop, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backdrop.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.42f);
            backdrop.SetSiblingIndex(backpack.GetSiblingIndex());

            root = CreateRect("ChestLootPanel", parent);
            root.sizeDelta = new Vector2(Width, height);
            root.anchoredPosition = backpack.anchoredPosition + Vector2.right * (backpack.rect.width + Gap) * 0.5f;
            root.gameObject.AddComponent<Image>().color = Background;

            Text title = AddLabel(root, "Title", "Loot", 29, Ink, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            Dock(title.rectTransform, 22, 72, -56, -14, 1);
            Button closeButton = AddButton(root, "Close", "Close", Muted, close);
            Dock((RectTransform)closeButton.transform, Width - 78, 18, -50, -22, 1);

            summary = AddLabel(root, "Summary", "", 15, Muted, TextAnchor.MiddleLeft);
            Dock(summary.rectTransform, 22, 22, -80, -54, 1);

            RectTransform viewport = CreateRect("Viewport", root);
            Dock(viewport, 20, 20, 89, -86);
            viewport.gameObject.AddComponent<RectMask2D>();
            dropArea = viewport.gameObject.AddComponent<Image>();
            dropArea.color = ViewportFill;
            dropOutline = viewport.gameObject.AddComponent<Outline>();
            dropOutline.effectColor = Accent;
            dropOutline.enabled = false;

            content = CreateRect("Loot Stacks", viewport);
            SetRect(content, Vector2.up, Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = Vector2.up;
            VerticalLayoutGroup list = content.gameObject.AddComponent<VerticalLayoutGroup>();
            list.spacing = 6;
            list.childControlWidth = true;
            list.childControlHeight = true;
            list.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;

            feedback = AddLabel(root, "Feedback", "", 15, Accent, TextAnchor.MiddleLeft);
            Dock(feedback.rectTransform, 22, 22, 52, 82, 0);
            takeAll = AddButton(root, "Take All", "Take all", Accent, () => Transfer(-1));
            Dock((RectTransform)takeAll.transform, 20, 20, 14, 49, 0);
            root.gameObject.SetActive(false);
            backdrop.gameObject.SetActive(false);
        }

        public void Show(LootChest chest, PlayerInteraction owner)
        {
            Chest = chest;
            interaction = owner;
            Chest.Changed += Refresh;
            feedback.text = "Drag items between panels";
            backdrop.gameObject.SetActive(true);
            root.gameObject.SetActive(true);
            Refresh();
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1;
        }

        public void Hide()
        {
            if (Chest != null)
                Chest.Changed -= Refresh;
            HighlightDrop(false);
            Chest = null;
            interaction = null;
            root.gameObject.SetActive(false);
            backdrop.gameObject.SetActive(false);
        }

        private void Refresh()
        {
            if (Chest == null)
                return;

            inventory.Panel?.CancelLootDrag();
            int count = Chest.StackCount;
            while (rows.Count < count)
                rows.Add(CreateRow(rows.Count));

            int remainingStacks = 0;
            for (int index = 0; index < rows.Count; index++)
            {
                LootStack stack = index < count ? Chest.GetStack(index) : default;
                bool visible = stack.Count > 0;
                Row row = rows[index];
                row.Root.SetActive(visible);
                if (!visible)
                    continue;

                remainingStacks++;
                inventory.TryGetItem(stack.ItemId, out ItemDefinition item);
                row.Icon.sprite = item == null ? null : item.Icon;
                row.Icon.enabled = row.Icon.sprite != null;
                row.Label.text = item == null ? stack.ItemId : item.DisplayName;
                row.Count.text = $"×{stack.Count}";
            }

            summary.text = remainingStacks == 0 ? "Empty chest  ·  Drag here to store" : $"{remainingStacks} stacks  ·  Drag or double-click";
            takeAll.interactable = remainingStacks > 0;
        }

        public bool ContainsDropPoint(Vector2 screenPosition, Camera eventCamera)
        {
            return IsOpen && RectTransformUtility.RectangleContainsScreenPoint(dropArea.rectTransform, screenPosition, eventCamera);
        }

        public void HighlightDrop(bool highlighted)
        {
            if (dropOutline.enabled == highlighted)
                return;

            dropArea.color = highlighted ? DropFill : ViewportFill;
            dropOutline.enabled = highlighted;
        }

        public void Deposit(int fromSlot)
        {
            LootChest chest = Chest;
            if (interaction == null || !interaction.CanAccessChest(chest))
                return;

            int stored = chest.TryStoreStack(inventory, fromSlot);
            if (stored > 0 && Chest == chest)
                feedback.text = $"Stored {stored}";
        }

        private void BeginTakeDrag(int index, Vector2 position)
        {
            LootChest chest = Chest;
            if (interaction == null || !interaction.CanAccessChest(chest) || (uint)index >= (uint)chest.StackCount)
                return;

            LootStack stack = chest.GetStack(index);
            if (stack.Count <= 0)
                return;

            if (inventory.TryGetItem(stack.ItemId, out ItemDefinition item))
                inventory.Panel?.BeginLootDrag(index, new PlayerInventory.ItemStack(item, stack.Count), position);
            else
                feedback.text = "Unknown item";
        }

        public void TakeToSlot(int index, int slotIndex)
        {
            LootChest chest = Chest;
            if (interaction == null || !interaction.CanAccessChest(chest) || (uint)index >= (uint)chest.StackCount)
                return;

            int moved = chest.TryLootStackToSlot(inventory, index, slotIndex);
            if (chest == null || Chest != chest)
                return;

            if (moved == 0)
                feedback.text = "Slot is full or has another item";
            else if (chest.GetStack(index).Count > 0)
                feedback.text = $"Took {moved}  ·  Not enough space";
            else
                feedback.text = $"Took {moved}";
        }

        private void Transfer(int index)
        {
            LootChest chest = Chest;
            if (interaction == null || !interaction.CanAccessChest(chest))
                return;

            int start = index < 0 ? 0 : index;
            int end = index < 0 ? chest.StackCount : index + 1;
            if (start >= chest.StackCount)
                return;

            int moved = index < 0 ? chest.TryLoot(inventory) : chest.TryLootStack(inventory, index);
            if (chest == null || Chest != chest)
                return;

            bool knownRemainder = false;
            bool unknownRemainder = false;
            for (int i = start; i < end; i++)
            {
                LootStack stack = chest.GetStack(i);
                if (stack.Count <= 0)
                    continue;

                if (inventory.TryGetItem(stack.ItemId, out _))
                    knownRemainder = true;
                else
                    unknownRemainder = true;
            }

            if (knownRemainder)
                feedback.text = moved > 0 ? $"Took {moved}  ·  Not enough space" : "Not enough backpack space";
            else if (unknownRemainder)
                feedback.text = moved > 0 ? $"Took {moved}  ·  Unknown item remains" : "Unknown item";
            else
                feedback.text = moved > 0 ? $"Took {moved}" : "";
        }

        private Row CreateRow(int index)
        {
            RectTransform row = CreateRect($"Loot Stack {index}", content);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
            Image surface = row.gameObject.AddComponent<Image>();
            surface.color = new Color32(45, 48, 49, 255);
            LootStackButton button = row.gameObject.AddComponent<LootStackButton>();
            button.targetGraphic = surface;
            var colors = button.colors;
            colors.highlightedColor = new Color32(255, 224, 172, 255);
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            button.onClick.AddListener(() => Transfer(index));
            button.BeginDrag = position => BeginTakeDrag(index, position);
            button.MoveDrag = position => inventory.Panel?.MoveLootDrag(position);
            button.EndDrag = position => inventory.Panel?.EndLootDrag(position);

            RectTransform iconRect = CreateRect("Icon", row);
            Dock(iconRect, 8, Width - 92, 4, -4);
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            Text name = AddLabel(row, "Item", "", 17, Ink, TextAnchor.MiddleLeft);
            Dock(name.rectTransform, 54, 62, 0, 0);
            Text count = AddLabel(row, "Count", "", 17, Accent, TextAnchor.MiddleRight);
            Dock(count.rectTransform, Width - 102, 12, 0, 0);
            return new Row(row.gameObject, icon, name, count);
        }

        private Button AddButton(RectTransform parent, string name, string caption, Color color, Action click)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color32(55, 58, 57, 255);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => click());
            Text label = AddLabel(rect, "Label", caption, 16, color, TextAnchor.MiddleCenter);
            Dock(label.rectTransform, 0, 0, 0, 0);
            return button;
        }

        private Text AddLabel(RectTransform parent, string name, string text, int size, Color color, TextAnchor alignment)
        {
            Text label = CreateRect(name, parent).gameObject.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = size - 3;
            label.resizeTextMaxSize = size;
            return label;
        }

        private static RectTransform CreateRect(string name, RectTransform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Dock(RectTransform rect, float left, float right, float bottom, float top, int edge = -1)
        {
            SetRect(rect, new Vector2(0, edge < 0 ? 0 : edge), new Vector2(1, edge < 0 ? 1 : edge),
                new Vector2(left, bottom), new Vector2(-right, top));
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private readonly struct Row
        {
            public readonly GameObject Root;
            public readonly Image Icon;
            public readonly Text Label;
            public readonly Text Count;

            public Row(GameObject root, Image icon, Text label, Text count)
            {
                Root = root;
                Icon = icon;
                Label = label;
                Count = count;
            }
        }
    }
}
