using System.Collections;
using TMPro;
using UnityEngine;

namespace Nomad.Game
{
    internal sealed class RegionTransitionView
    {
        private readonly CanvasGroup group;
        private readonly TextMeshProUGUI message;
        private readonly TMP_FontAsset koreanFont;

        public RegionTransitionView(Transform parent)
        {
            var root = new GameObject("RegionTransition", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(UnityEngine.UI.Image));
            root.transform.SetParent(parent, false);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;

            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;

            group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            root.GetComponent<UnityEngine.UI.Image>().color = new Color32(19, 23, 21, 255);

            var label = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)label.transform;
            rect.SetParent(root.transform, false);
            rect.sizeDelta = new Vector2(860f, 144f);

            koreanFont = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular");
            message = label.GetComponent<TextMeshProUGUI>();
            message.font = koreanFont != null ? koreanFont : Resources.Load<TMP_FontAsset>("NomadHudFont");
            message.fontSize = 38f;
            message.color = new Color32(248, 239, 218, 255);
            message.alignment = TextAlignmentOptions.Center;
            message.raycastTarget = false;
        }

        public void Show()
        {
            group.gameObject.SetActive(true);
            HideMessage();
        }

        public void ShowDestination(string sceneName)
        {
            string text = sceneName switch
            {
                "Village" => koreanFont != null ? "마을로 이동 중..." : "Loading village...",
                "Field" => koreanFont != null ? "필드로 이동 중..." : "Loading field...",
                _ => koreanFont != null ? "지역 이동 중..." : "Loading..."
            };

            ShowMessage(text);
        }

        public void ShowFieldFailure() => ShowMessage(koreanFont != null
            ? "필드를 준비하지 못했습니다.\n이전 지역으로 돌아갑니다."
            : "The field could not be loaded.\nReturning to the previous region.");

        private void ShowMessage(string text)
        {
            message.text = text;
            message.gameObject.SetActive(true);
        }

        public void HideMessage() => message.gameObject.SetActive(false);

        public void Hide() => group.gameObject.SetActive(false);

        public IEnumerator FadeTo(float target, float duration)
        {
            float from = group.alpha;
            float start = Time.realtimeSinceStartup;
            float elapsed = 0f;

            // 씬 로드와 지형 생성에 소비한 시간이나 게임 시간 배율과 독립적으로 페이드합니다.
            while (elapsed < duration)
            {
                group.alpha = Mathf.SmoothStep(from, target, elapsed / duration);
                yield return null;
                elapsed = Time.realtimeSinceStartup - start;
            }

            group.alpha = target;
        }

        public void ReleaseFont()
        {
            if (koreanFont == null)
                return;

            foreach (Texture2D atlas in koreanFont.atlasTextures)
                Object.Destroy(atlas);

            Object.Destroy(koreanFont.material);
            Object.Destroy(koreanFont);
        }
    }
}
