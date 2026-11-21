using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Nomad.Inventory
{
    internal sealed class InventorySplitDialog
    {
        private static readonly Color Ink = new Color32(239, 240, 234, 255);
        private static readonly Color Accent = new Color32(224, 182, 104, 255);
        private readonly RectTransform canvas;
        private readonly RectTransform root;
        private readonly RectTransform card;
        private readonly Font font;
        private readonly UnityEngine.UI.Text itemName;
        private readonly UnityEngine.UI.Text remaining;
        private readonly UnityEngine.UI.Text feedback;
        private readonly UnityEngine.UI.Image icon;
        private readonly UnityEngine.UI.InputField amountInput;
        private readonly UnityEngine.UI.Slider slider;
        private readonly UnityEngine.UI.Button confirm;
        private PlayerInventory inventory;
        private ItemDefinition item;
        private int source;
        private int count;
        private int closedFrame = -1;
        private GameObject ownedEventSystem;
        private bool committing;

        public bool IsOpen => root != null && root.gameObject.activeSelf;
        public bool BlocksInput => IsOpen || Time.frameCount == closedFrame;

        public InventorySplitDialog(RectTransform canvas)
        {
            this.canvas = canvas;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 20);
            root = Rect("InventorySplitDialog", canvas, Vector2.zero, Vector2.zero);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, .65f);
            card = Rect("Card", root, new Vector2(620, 412), Vector2.zero);
            card.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(19, 20, 22, 255);
            Text("Title", "아이템 분할", new Vector2(540, 42), new Vector2(0, 158), 28);

            icon = Rect("Icon", card, new Vector2(42, 42), new Vector2(-225, 99))
                .gameObject.AddComponent<UnityEngine.UI.Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            itemName = Text("Item", "", new Vector2(420, 42), new Vector2(20, 99), 19);
            amountInput = CreateInput();
            slider = CreateSlider();
            remaining = Text("Remaining", "", new Vector2(540, 30), new Vector2(0, -45), 17);
            feedback = Text("Feedback", "", new Vector2(540, 46), new Vector2(0, -88), 16);
            feedback.color = new Color32(244, 167, 139, 255);
            confirm = Button("Confirm", "분할 [Enter]", new Vector2(310, 50), new Vector2(95, -150), Accent, Confirm);
            Button("Cancel", "취소 [Esc]", new Vector2(170, 50), new Vector2(-155, -150),
                new Color32(64, 69, 70, 255), Close);
            root.gameObject.SetActive(false);
        }

        public void Open(PlayerInventory owner, int slot)
        {
            Close();
            inventory = owner;
            source = slot;
            PlayerInventory.ItemStack stack = owner.GetSlot(slot);
            item = stack.Item;
            count = stack.Count;
            if (item == null || count < 2)
                return;

            EnsureEventSystem();
            itemName.text = $"{item.DisplayName}  ×{count}";
            icon.sprite = item.Icon;
            icon.enabled = item.Icon != null;
            slider.minValue = 1;
            slider.maxValue = count - 1;
            slider.SetValueWithoutNotify(count / 2);
            amountInput.SetTextWithoutNotify((count / 2).ToString());
            root.SetAsLastSibling();
            root.gameObject.SetActive(true);
            inventory.Changed += InventoryChanged;
            Fit();
            Validate();
            EventSystem.current.SetSelectedGameObject(amountInput.gameObject);
            amountInput.ActivateInputField();
        }

        public void Tick()
        {
            if (!IsOpen)
                return;

            Fit();
            Keyboard keyboard = Keyboard.current;
            if (keyboard?.escapeKey.wasPressedThisFrame == true)
                Close();
            else if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                Confirm();
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            inventory.Changed -= InventoryChanged;
            closedFrame = Time.frameCount;
            root.gameObject.SetActive(false);
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(root))
                EventSystem.current.SetSelectedGameObject(null);
            if (ownedEventSystem != null)
                ownedEventSystem.SetActive(false);
        }

        public void Dispose()
        {
            Close();
            if (root != null) Object.Destroy(root.gameObject);
            if (ownedEventSystem != null) Object.Destroy(ownedEventSystem);
            if (font != null) Object.Destroy(font);
        }

        private void InventoryChanged()
        {
            if (!committing) Validate();
        }

        private void Confirm()
        {
            if (!IsOpen || committing || !Validate())
                return;

            committing = true;
            bool split = inventory.TrySplit(source, int.Parse(amountInput.text), item, count, out _, out string error);
            committing = false;
            if (split) Close();
            else feedback.text = error;
        }

        private bool Validate()
        {
            string error = string.Empty;
            PlayerInventory.ItemStack current = inventory.GetSlot(source);
            bool validAmount = int.TryParse(amountInput.text, out int amount) && amount >= 1 && amount < count;
            if (current.Item != item || current.Count != count)
                error = "아이템이 변경되었습니다. 분할 창을 다시 열어 주세요.";
            else if (!validAmount)
                error = $"1 ~ {count - 1}개를 입력하세요.";
            else if (inventory.UsedSlots >= PlayerInventory.Capacity)
                error = "빈 가방칸이 필요합니다.";

            if (validAmount) slider.SetValueWithoutNotify(amount);
            remaining.text = validAmount ? $"분할 {amount}개  ·  원래 칸에 {count - amount}개 남음" : "분할할 수량을 선택하세요.";
            feedback.text = error;
            confirm.interactable = error.Length == 0;
            return confirm.interactable;
        }

        private void Fit()
        {
            float scale = Mathf.Min(1f, (canvas.rect.width - 32) / 620f, (canvas.rect.height - 32) / 412f);
            card.localScale = Vector3.one * Mathf.Max(.1f, scale);
            root.SetAsLastSibling();
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            if (ownedEventSystem == null)
            {
                ownedEventSystem = new GameObject("Split Event System");
                ownedEventSystem.SetActive(false);
                ownedEventSystem.transform.SetParent(canvas, false);
                ownedEventSystem.AddComponent<EventSystem>();
                ownedEventSystem.AddComponent<InputSystemUIInputModule>();
            }
            ownedEventSystem.SetActive(true);
        }

        private UnityEngine.UI.InputField CreateInput()
        {
            RectTransform rect = Rect("Amount", card, new Vector2(160, 52), new Vector2(0, 34));
            var background = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color32(35, 39, 40, 255);
            var text = Text("Value", "", new Vector2(144, 44), Vector2.zero, 29, rect);
            var input = rect.gameObject.AddComponent<UnityEngine.UI.InputField>();
            input.targetGraphic = background;
            input.textComponent = text;
            input.contentType = UnityEngine.UI.InputField.ContentType.IntegerNumber;
            input.characterLimit = 10;
            input.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            input.onValueChanged.AddListener(_ => { if (IsOpen) Validate(); });
            return input;
        }

        private UnityEngine.UI.Slider CreateSlider()
        {
            RectTransform track = Rect("Slider", card, new Vector2(500, 28), new Vector2(0, -13));
            track.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(51, 56, 57, 255);
            RectTransform fill = Rect("Fill", track, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.gameObject.AddComponent<UnityEngine.UI.Image>().color = Accent;
            RectTransform area = Rect("HandleArea", track, new Vector2(-24, 0), Vector2.zero);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            RectTransform handle = Rect("Handle", area, new Vector2(28, 38), Vector2.zero);
            var grip = handle.gameObject.AddComponent<UnityEngine.UI.Image>();
            grip.color = Ink;
            var control = track.gameObject.AddComponent<UnityEngine.UI.Slider>();
            control.fillRect = fill;
            control.handleRect = handle;
            control.targetGraphic = grip;
            control.wholeNumbers = true;
            control.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            control.onValueChanged.AddListener(value => amountInput.text = Mathf.RoundToInt(value).ToString());
            return control;
        }

        private UnityEngine.UI.Button Button(string name, string label, Vector2 size, Vector2 position,
            Color color, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = Rect(name, card, size, position);
            var background = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = color;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = background;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            button.onClick.AddListener(action);
            var text = Text("Label", label, size, Vector2.zero, 19, rect);
            text.color = name == "Confirm" ? new Color32(25, 25, 23, 255) : Ink;
            return button;
        }

        private UnityEngine.UI.Text Text(string name, string value, Vector2 size, Vector2 position, int fontSize,
            RectTransform parent = null)
        {
            var text = Rect(name, parent != null ? parent : card, size, position)
                .gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Ink;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform Rect(string name, RectTransform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }
    }
}
