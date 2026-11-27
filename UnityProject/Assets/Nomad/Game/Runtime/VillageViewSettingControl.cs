using TMPro;
using UnityEngine;

namespace Nomad.Game
{
    // 두 설정 화면이 동일한 표시와 저장 동작을 사용합니다.
    public sealed class VillageViewSettingControl : MonoBehaviour
    {
        private TMP_Text label;

        public static UnityEngine.UI.Button Create(Transform parent, TMP_FontAsset font, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject("VillageView", typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = Color.white;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = new Color32(51, 59, 51, 255);
            colors.highlightedColor = colors.selectedColor = new Color32(136, 102, 51, 255);
            colors.pressedColor = new Color32(111, 83, 43, 255);
            button.colors = colors;

            var textRect = (RectTransform)new GameObject("Label", typeof(RectTransform)).transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 0);
            textRect.offsetMax = new Vector2(-8, 0);
            var text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 24;
            text.color = new Color32(248, 239, 218, 255);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            var control = rect.gameObject.AddComponent<VillageViewSettingControl>();
            control.label = text;
            button.onClick.AddListener(control.Toggle);
            control.Refresh();
            return button;
        }

        private void OnEnable() => Refresh();
        private void Toggle()
        {
            VillageViewSettings.SetFirstPerson(!VillageViewSettings.FirstPerson);
            Refresh();
        }

        private void Refresh()
        {
            if (label != null)
                label.text = "마을 시점: " + (VillageViewSettings.FirstPerson ? "1인칭" : "탑다운") + "  ›";
        }
    }
}
