using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nomad.Inventory
{
    // 첨부 시트의 둥근 짙은 슬롯과 밝은 수량 배지를 공용 UI에 적용합니다.
    public static class ItemUiStyle
    {
        public static readonly Color SlotFill = new Color32(39, 48, 49, 248);
        private static Sprite rounded;
        private static Dictionary<string, Sprite> symbols;

        public static void Frame(RectTransform panel, bool header = true)
        {
            Image fill = panel.GetComponent<Image>();
            if (fill == null) fill = panel.gameObject.AddComponent<Image>();
            fill.sprite = Rounded;
            fill.type = Image.Type.Sliced;
            fill.color = new Color32(22, 29, 28, 250);
            var outline = panel.GetComponent<Outline>() ?? panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(151, 137, 105, 230);
            outline.effectDistance = new Vector2(1, -1);
            var shadow = panel.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.65f);
            shadow.effectDistance = new Vector2(4, -5);
            if (!header || panel.Find("Header Rule") != null) return;

            var rule = new GameObject("Header Rule", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            rule.transform.SetParent(panel, false);
            rule.raycastTarget = false;
            rule.color = new Color32(166, 151, 119, 145);
            rule.rectTransform.anchorMin = new Vector2(0, 1);
            rule.rectTransform.anchorMax = Vector2.one;
            rule.rectTransform.sizeDelta = new Vector2(-32, 1);
            rule.rectTransform.anchoredPosition = new Vector2(0, -53);
        }

        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int size = 32;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var nearest = new Vector2(Mathf.Clamp(x, 7, 24), Mathf.Clamp(y, 7, 24));
                    float alpha = Mathf.Clamp01(7.5f - Vector2.Distance(new Vector2(x, y), nearest));
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                texture.Apply();
                rounded = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100, 0,
                    SpriteMeshType.FullRect, Vector4.one * 8);
                return rounded;
            }
        }

        public static Sprite Symbol(EquipmentType type)
        {
            if (symbols == null)
            {
                symbols = new Dictionary<string, Sprite>();
                foreach (Sprite sprite in Resources.LoadAll<Sprite>("ItemUi/StyleSheet")) symbols[sprite.name] = sprite;
            }
            string key = "slot-" + (type == EquipmentType.Weapon ? "weapon" : type.ToString().ToLowerInvariant());
            return symbols.TryGetValue(key, out Sprite result) ? result : null;
        }

        public static Image AddCountBadge(Text count)
        {
            var badge = new GameObject("Count Badge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            badge.transform.SetParent(count.transform.parent, false);
            badge.sprite = Rounded;
            badge.type = Image.Type.Sliced;
            badge.color = new Color32(55, 66, 46, 240);
            badge.raycastTarget = false;
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = Vector2.right;
            badge.rectTransform.pivot = Vector2.right;
            badge.rectTransform.anchoredPosition = new Vector2(-4, 4);
            badge.rectTransform.sizeDelta = new Vector2(44, 22);
            var outline = badge.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(156, 162, 127, 230);
            outline.effectDistance = new Vector2(1, -1);
            count.rectTransform.anchorMin = count.rectTransform.anchorMax = Vector2.right;
            count.rectTransform.pivot = Vector2.right;
            count.rectTransform.anchoredPosition = badge.rectTransform.anchoredPosition;
            count.rectTransform.sizeDelta = badge.rectTransform.sizeDelta;
            count.alignment = TextAnchor.MiddleCenter;
            count.fontSize = 15;
            count.color = new Color32(246, 241, 222, 255);
            count.transform.SetAsLastSibling();
            return badge;
        }
    }
}
