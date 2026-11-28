using UnityEngine;
using UnityEngine.UI;

namespace Nomad.Monsters.Demo
{
    public sealed class ParrySuccessBanner : MonoBehaviour
    {
        private Transform player;
        private Text label;
        private float age;

        public static void Show(Transform player)
        {
            var root = new GameObject("Parry success banner", typeof(Canvas), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var effect = root.AddComponent<ParrySuccessBanner>();
            effect.player = player;
            effect.label = new GameObject("패링!", typeof(RectTransform)).AddComponent<Text>();
            effect.label.transform.SetParent(root.transform, false);
            effect.label.font = Resources.Load<Font>("ShopFonts/Jua-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            effect.label.text = "패링!";
            effect.label.fontSize = 38;
            effect.label.color = new Color(1, 0.88f, 0.12f);
            effect.label.alignment = TextAnchor.MiddleCenter;
            effect.label.raycastTarget = false;
            effect.label.rectTransform.sizeDelta = new Vector2(230, 90);
            var outline = effect.label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.18f, 0.065f, 0.01f);
            outline.effectDistance = new Vector2(2, -2);
            Destroy(root, 0.95f);
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (player == null || Camera.main == null) return;
            label.transform.position = Camera.main.WorldToScreenPoint(player.position + Vector3.up * 2.8f) + Vector3.up * age * 34;
            label.transform.localScale = Vector3.one * (1 + 0.28f * Mathf.Exp(-age * 12));
            GetComponent<CanvasGroup>().alpha = Mathf.Clamp01((0.95f - age) / 0.3f);
        }
    }
}
