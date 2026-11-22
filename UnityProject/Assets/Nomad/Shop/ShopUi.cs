using UnityEngine;
using UnityEngine.UI;

namespace Nomad.Shop
{
    internal static class ShopUi
    {
        internal static readonly Color Canvas = new Color(0.975f, 0.947f, 0.88f);
        internal static readonly Color Surface = new Color(1f, 0.98f, 0.93f);
        internal static readonly Color Slot = new Color(0.91f, 0.87f, 0.77f);
        internal static readonly Color Ink = new Color(0.23f, 0.22f, 0.15f);
        internal static readonly Color Muted = new Color(0.49f, 0.46f, 0.36f);
        internal static readonly Color Accent = new Color(0.95f, 0.69f, 0.22f);
        internal static readonly Color Selected = new Color(1f, 0.94f, 0.79f);
        internal static readonly Color GoldInk = new Color(0.50f, 0.35f, 0.13f);
        internal static readonly Color Forest = new Color(0.14f, 0.25f, 0.17f);
        internal static readonly Color Border = new Color(0.75f, 0.68f, 0.52f);
        internal const float CellSize = 96;
        internal const float CellHeight = 76;
        private static Font bodyFont;
        private static Font headingFont;

        internal static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        internal static Image Background(RectTransform rect, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        internal static ShopRoundedRect Rounded(RectTransform rect, Color fill, float radius = 12,
            float borderWidth = 1.5f, Color? border = null)
        {
            var shape = rect.gameObject.AddComponent<ShopRoundedRect>();
            shape.color = fill;
            shape.Style(radius, borderWidth, border ?? Border);
            shape.SetShading(0.055f);
            return shape;
        }

        internal static void Shadow(Graphic graphic, float depth, float alpha = 0.16f)
        {
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.08f, 0.15f, 0.11f, alpha);
            shadow.effectDistance = new Vector2(0, -depth);
        }

        internal static Text Label(Transform parent, string name, string value, float x, float y,
            float width, float height, int size = 18, Color? color = null)
        {
            Text text = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Text>();
            bodyFont = bodyFont != null ? bodyFont : Resources.Load<Font>("ShopFonts/Pretendard-Regular");
            text.font = bodyFont;
            text.fontSize = size;
            text.text = value;
            text.color = color ?? Ink;
            text.raycastTarget = false;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        internal static Text Heading(Transform parent, string name, string value, float x, float y,
            float width, float height, int size, Color? color = null)
        {
            Text text = Label(parent, name, value, x, y, width, height, size, color);
            headingFont = headingFont != null ? headingFont : Resources.Load<Font>("ShopFonts/Pretendard-SemiBold");
            text.font = headingFont;
            return text;
        }

        internal static Button Button(Transform parent, string name, string label, float x, float y,
            float width, float height, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = Rect(parent, name, x, y, width, height);
            ShopRoundedRect frame = Rounded(rect, Surface, 14, 1);
            Shadow(frame, 2, 0.12f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.94f, 0.92f, 0.87f, 1);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);
            Text text = Heading(rect, "Label", label, 4, 0, width - 8, height, 16);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        internal static InputField QuantityInput(Transform parent, float x, float y)
        {
            RectTransform rect = Rect(parent, "Quantity", x, y, 112, 40);
            Rounded(rect, Surface, 12, 1, Border);
            Text text = Label(rect, "Value", "1", 8, 0, 96, 40, 20);
            text.alignment = TextAnchor.MiddleCenter;

            InputField input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = rect.GetComponent<ShopRoundedRect>();
            input.textComponent = text;
            input.contentType = InputField.ContentType.IntegerNumber;
            input.characterValidation = InputField.CharacterValidation.None;
            input.onValidateInput = (_, _, character) => character >= '0' && character <= '9' ? character : '\0';
            input.characterLimit = 10;
            input.navigation = new Navigation { mode = Navigation.Mode.None };
            input.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
            input.customCaretColor = true;
            input.caretColor = Ink;
            return input;
        }

        internal static Slider QuantitySlider(Transform parent, float x, float y, float width)
        {
            RectTransform rect = Rect(parent, "Quantity Slider", x, y, width, 32);
            Rounded(rect, Color.clear, 0, 0);
            Rounded(Rect(rect, "Track", 12, 12, width - 24, 8), Slot, 4, 0).raycastTarget = false;
            RectTransform fillArea = Rect(rect, "Fill area", 12, 12, width - 24, 8);
            RectTransform fill = Rect(fillArea, "Fill", 0, 0, width - 24, 8);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            Rounded(fill, Forest, 4, 0).raycastTarget = false;
            RectTransform handleArea = Rect(rect, "Handle area", 12, 0, width - 24, 32);
            RectTransform handle = Rect(handleArea, "Handle", 0, 4, 24, 24);
            handle.pivot = Vector2.one * 0.5f;
            handle.sizeDelta = new Vector2(24, -8);
            handle.anchoredPosition = Vector2.zero;
            var grip = Rounded(handle, Surface, 12, 2, GoldInk);
            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = grip;
            slider.wholeNumbers = true;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            return slider;
        }

        internal static void Coin(Transform parent, float x, float y, float size)
        {
            RectTransform coin = Rect(parent, "Coin", x, y, size, size);
            Rounded(coin, Accent, size / 2, 1, GoldInk).raycastTarget = false;
            Rounded(Rect(coin, "Inset", 3, 3, size - 6, size - 6), Color.clear, size / 2, 1, Canvas).raycastTarget = false;
            Text mark = Heading(coin, "Mark", "G", 0, 0, size, size, (int)(size * 0.6f), GoldInk);
            mark.alignment = TextAnchor.MiddleCenter;
        }

        internal static void ShopSign(Transform parent, float x, float y)
        {
            RectTransform sign = Rect(parent, "Merchant emblem", x, y, 58, 54);
            Rounded(Rect(sign, "Stall", 5, 17, 48, 36), Border, 5, 1, Accent).raycastTarget = false;
            Rounded(Rect(sign, "Window", 13, 25, 32, 20), Forest, 3, 0).raycastTarget = false;
            for (int index = 0; index < 5; index++)
                Rounded(Rect(sign, "Awning " + index, index * 11, 0, 12, 24),
                    index % 2 == 0 ? Canvas : Accent, 5, 0).raycastTarget = false;
            Rounded(Rect(parent, "Title divider", x + 75, y + 3, 1, 48), Border, 0, 0).raycastTarget = false;
        }

        internal static RectTransform Grid(Transform parent, string name, float x, bool merchant = false)
        {
            RectTransform viewport = Rect(parent, name, x, 206, 530, 530);
            Rounded(viewport, merchant ? Canvas : Slot, 18, merchant ? 0 : 1, Border);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect(viewport, "Content", 8, 8, 514, 334);
            var layout = content.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = merchant ? new Vector2(164, 196) : new Vector2(CellSize, CellHeight);
            layout.spacing = new Vector2(8, 8);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = merchant ? 3 : 5;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 25;
            return content;
        }
    }
}
