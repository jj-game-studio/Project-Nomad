using UnityEngine;
using UnityEngine.UI;

namespace Nomad.Shop
{
    internal static class ShopUi
    {
        internal static readonly Color Surface = new Color(0.09f, 0.145f, 0.16f);
        internal static readonly Color Slot = new Color(0.075f, 0.08f, 0.085f, 0.96f);
        internal static readonly Color Ink = new Color(0.91f, 0.95f, 0.96f);
        internal static readonly Color Muted = new Color(0.66f, 0.76f, 0.77f);
        internal static readonly Color Gold = new Color(0.88f, 0.78f, 0.59f);

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

        internal static Text Label(Transform parent, string name, string value, float x, float y,
            float width, float height, int size = 18, Color? color = null)
        {
            Text text = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.text = value;
            text.color = color ?? Ink;
            text.raycastTarget = false;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        internal static Button Button(Transform parent, string name, string label, float x, float y,
            float width, float height, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = Rect(parent, name, x, y, width, height);
            Background(rect, Surface * new Color(1.5f, 1.5f, 1.5f, 1));
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);
            Text text = Label(rect, "Label", label, 4, 0, width - 8, height);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        internal static InputField QuantityInput(Transform parent, float x, float y)
        {
            RectTransform rect = Rect(parent, "Quantity", x, y, 112, 40);
            Background(rect, Surface);
            var border = rect.gameObject.AddComponent<Outline>();
            border.effectColor = Muted * new Color(0.5f, 0.5f, 0.5f, 1);
            border.effectDistance = new Vector2(1, -1);
            Text text = Label(rect, "Value", "1", 8, 0, 96, 40, 20);
            text.alignment = TextAnchor.MiddleCenter;

            InputField input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = rect.GetComponent<Image>();
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

        internal static RectTransform Grid(Transform parent, string name, float x)
        {
            RectTransform viewport = Rect(parent, name, x, 166, 530, 430);
            Background(viewport, Slot);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect(viewport, "Content", 8, 8, 514, 414);
            var layout = content.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(98, 98);
            layout.spacing = new Vector2(6, 6);
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
