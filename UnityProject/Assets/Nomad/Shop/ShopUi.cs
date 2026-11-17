using UnityEngine;
using UnityEngine.UI;

namespace Nomad.Shop
{
    internal static class ShopUi
    {
        internal static readonly Color Cream = new Color(0.96f, 0.93f, 0.85f);
        internal static readonly Color Surface = new Color(0.99f, 0.97f, 0.91f);
        internal static readonly Color Slot = new Color(0.88f, 0.86f, 0.79f);
        internal static readonly Color Ink = new Color(0.20f, 0.27f, 0.23f);
        internal static readonly Color Muted = new Color(0.46f, 0.51f, 0.44f);
        internal static readonly Color Gold = new Color(0.85f, 0.69f, 0.36f);
        internal static readonly Color GoldInk = new Color(0.51f, 0.37f, 0.14f);
        internal static readonly Color Forest = new Color(0.19f, 0.32f, 0.27f);
        internal static readonly Color Border = new Color(0.69f, 0.72f, 0.62f);
        internal const float CellSize = 96;
        private static Font bodyFont;
        private static Font titleFont;

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
            bodyFont = bodyFont != null ? bodyFont : Resources.Load<Font>("ShopFonts/VarelaRound-Regular");
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
            titleFont = titleFont != null ? titleFont : Resources.Load<Font>("ShopFonts/LilitaOne-Regular");
            text.font = titleFont;
            return text;
        }

        internal static Button Button(Transform parent, string name, string label, float x, float y,
            float width, float height, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = Rect(parent, name, x, y, width, height);
            ShopRoundedRect frame = Rounded(rect, Surface, 10);
            Shadow(frame, 3);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);
            Text text = Label(rect, "Label", label, 4, 0, width - 8, height);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        internal static InputField QuantityInput(Transform parent, float x, float y)
        {
            RectTransform rect = Rect(parent, "Quantity", x, y, 112, 40);
            Rounded(rect, Surface, 10, 1.5f, Forest);
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
            input.selectionColor = new Color(Gold.r, Gold.g, Gold.b, 0.35f);
            input.customCaretColor = true;
            input.caretColor = Ink;
            return input;
        }

        internal static void Purse(Transform parent, float x, float y)
        {
            RectTransform emblem = Rect(parent, "Merchant emblem", x, y, 56, 56);
            Rounded(emblem, Cream, 16, 2, Gold);
            Rounded(Rect(emblem, "Handle", 19, 10, 18, 22), Color.clear, 9, 3, Forest).raycastTarget = false;
            Rounded(Rect(emblem, "Bag", 12, 23, 32, 25), Gold, 9, 2, GoldInk).raycastTarget = false;
            Text coin = Heading(emblem, "Coin", "G", 12, 24, 32, 22, 17, GoldInk);
            coin.alignment = TextAnchor.MiddleCenter;
        }

        internal static RectTransform Grid(Transform parent, string name, float x)
        {
            RectTransform viewport = Rect(parent, name, x, 166, 530, 430);
            Rounded(viewport, Slot, 16, 1, Border);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect(viewport, "Content", 8, 8, 514, 414);
            var layout = content.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = Vector2.one * CellSize;
            layout.spacing = new Vector2(8, 8);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 5;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25;
            return content;
        }
    }
}
