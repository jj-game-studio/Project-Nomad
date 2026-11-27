using UnityEngine;
using UnityEngine.UI;
using Nomad.Monsters;

namespace Nomad.CombatFeedback
{
    public sealed class CombatFeedbackOverlay : MonoBehaviour
    {
        private sealed class Number
        {
            public Text text;
            public Vector3 point;
            public float started;
            public Color color;
        }
        private readonly Number[] numbers = new Number[16];
        private RectTransform canvasRect;
        private Image vignette;
        private Texture2D edgeTexture;
        private Sprite edgeSprite;
        private Text status, banner;
        private Image armorFill;
        private float hurtAt = -10, hurtStrength, hurtFrom, bannerUntil;
        private float hurtAttack, hurtHold, hurtRelease;
        public int LastDamage { get; private set; }
        public float VignetteAlpha => vignette != null ? vignette.color.a : 0;
        public int ActiveNumbers { get; private set; }
        public bool LastCritical { get; private set; }

        private void Awake()
        {
            var root = new GameObject("Combat feedback", typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 70;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = root.GetComponent<RectTransform>();
            status = Label("Armor status", 22, new Vector2(640, 36), new Vector2(0, -92), true);
            banner = Label("Armor break", 34, new Vector2(860, 55), new Vector2(0, -132), true);
            status.text = banner.text = "";
            for (int index = 0; index < numbers.Length; index++)
            {
                Text label = Label("Damage number", 34, new Vector2(220, 64), Vector2.zero, false);
                label.gameObject.SetActive(false);
                numbers[index] = new Number { text = label, started = -10 };
            }
        }

        private Text Label(string name, int size, Vector2 dimensions, Vector2 position, bool top)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(canvasRect, false);
            Text label = go.GetComponent<Text>();
            label.font = Resources.Load<Font>("ShopFonts/Jua-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1, 0.85f, 0.45f);
            label.raycastTarget = false;
            go.GetComponent<Outline>().effectColor = new Color(0.04f, 0.02f, 0, 0.95f);
            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, top ? 1 : 0.5f);
            rect.pivot = new Vector2(0.5f, top ? 1 : 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            return label;
        }

        public void ShowDamage(int amount, Vector3 point, Color color, bool critical = false)
        {
            LastDamage = amount;
            LastCritical = critical;
            Number slot = numbers[0];
            foreach (Number candidate in numbers) if (candidate.started < slot.started) slot = candidate;
            slot.started = Time.time;
            slot.point = point;
            slot.color = critical ? new Color(1, 0.4f, 0.05f) : color;
            slot.text.fontSize = critical ? 46 : 34;
            slot.text.text = amount + (critical ? "!" : "");
            slot.text.gameObject.SetActive(true);
        }

        public void ShowArmor(int stage, float multiplier) => status.text = $"갑옷 파괴 {stage}/2  ·  받는 피해 {multiplier * 100:0}%";
        public void ShowDurability(float ratio, int stage)
        {
            if (armorFill == null)
            {
                var root = new GameObject("Armor durability", typeof(RectTransform), typeof(Image));
                root.transform.SetParent(canvasRect, false);
                var background = root.GetComponent<Image>();
                background.color = new Color(0.04f, 0.035f, 0.025f, 0.95f);
                background.raycastTarget = false;
                var rect = (RectTransform)root.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
                rect.anchoredPosition = new Vector2(0, -132);
                rect.sizeDelta = new Vector2(480, 14);
                var child = new GameObject("Remaining armor", typeof(RectTransform), typeof(Image));
                child.transform.SetParent(rect, false);
                armorFill = child.GetComponent<Image>();
                armorFill.color = new Color(0.7f, 0.8f, 0.9f);
                armorFill.raycastTarget = false;
                var bar = armorFill.rectTransform;
                bar.anchorMin = Vector2.zero;
                bar.anchorMax = Vector2.one;
                bar.offsetMin = new Vector2(2, 2);
                bar.offsetMax = new Vector2(-2, -2);
                bar.pivot = new Vector2(0, 0.5f);
            }
            armorFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(ratio), 1, 1);
            armorFill.transform.parent.gameObject.SetActive(true);
            status.text = $"내구도 {ratio * 100:0}% · " + status.text;
            banner.rectTransform.anchoredPosition = new Vector2(0, -152);
            if (stage >= 2) status.text = "갑옷 완전 파괴 · 받는 피해 155%";
        }
        public void HideArmor()
        {
            status.text = "";
            bannerUntil = 0;
            if (armorFill != null) armorFill.transform.parent.gameObject.SetActive(false);
        }
        public void ArmorBreak(int stage)
        {
            banner.text = $"갑옷 파괴 {stage}/2!";
            bannerUntil = Time.time + 1.8f;
        }

        public void Hurt(float fraction)
        {
            var settings = CombatFeedbackSettings.Current;
            if (!isActiveAndEnabled || !settings.PlayerVignette || fraction <= 0 || Time.timeScale <= 0) return;
            if (vignette == null) BuildVignette();

            hurtFrom = EvaluateVignette();
            // 기존 효과가 남아 있으면 작은 후속 피해도 현재 피크를 약하게 만들지 않습니다.
            float requested = 0.18f + Mathf.Clamp01(fraction) * 1.1f;
            float existing = Time.time - hurtAt < hurtAttack + hurtHold + hurtRelease ? hurtStrength : 0;
            hurtStrength = Mathf.Clamp(Mathf.Max(existing, requested), 0, settings.VignetteMaximum);
            hurtAttack = Mathf.Max(0.001f, settings.VignetteAttack);
            hurtHold = Mathf.Max(0, settings.VignetteHold);
            hurtRelease = Mathf.Max(0.001f, settings.VignetteRelease);
            hurtAt = Time.time;
            RefreshVignette();
        }

        private float EvaluateVignette()
        {
            if (!CombatFeedbackSettings.Current.PlayerVignette) return 0;
            float age = Time.time - hurtAt;
            if (age < hurtAttack)
                return Mathf.Lerp(hurtFrom, hurtStrength, Mathf.SmoothStep(0, 1, age / Mathf.Max(0.001f, hurtAttack)));
            if (age < hurtAttack + hurtHold) return hurtStrength;

            float remaining = 1 - Mathf.Clamp01((age - hurtAttack - hurtHold) / Mathf.Max(0.001f, hurtRelease));
            return hurtStrength * remaining * remaining;
        }

        public void RefreshVignette()
        {
            if (!CombatFeedbackSettings.Current.PlayerVignette)
                hurtStrength = hurtFrom = 0;
            if (vignette == null) return;

            Color color = CombatFeedbackSettings.Current.VignetteColor;
            color.a = Mathf.Min(EvaluateVignette(), CombatFeedbackSettings.Current.VignetteMaximum);
            vignette.color = color;
        }

        public void ClearFeedback()
        {
            hurtAt = -10;
            hurtStrength = hurtFrom = 0;
            RefreshVignette();
            ClearDamageNumbers();
        }

        public void ClearDamageNumbers()
        {
            foreach (Number number in numbers)
            {
                number.started = -10;
                if (number.text != null) number.text.gameObject.SetActive(false);
            }
            ActiveNumbers = 0;
        }

        private void BuildVignette()
        {
            edgeTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            edgeTexture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float dx = (x / 127f - 0.5f) * 2;
                float dy = (y / 127f - 0.5f) * 2;
                float radius = Mathf.Pow(dx * dx * dx * dx + dy * dy * dy * dy, 0.25f);
                float edge = Mathf.Clamp01((radius - 0.58f) / 0.42f);
                edgeTexture.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0, 1, edge)));
            }
            edgeTexture.Apply();
            edgeSprite = Sprite.Create(edgeTexture, new Rect(0, 0, 128, 128), Vector2.one * 0.5f);
            var go = new GameObject("Damage vignette", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvasRect, false);
            go.transform.SetAsFirstSibling();
            vignette = go.GetComponent<Image>();
            vignette.sprite = edgeSprite;
            vignette.color = Color.clear;
            vignette.raycastTarget = false;
            RectTransform rect = vignette.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void LateUpdate() => RefreshPositions(Camera.main);
        public void RefreshPositions(Camera camera)
        {
            RefreshVignette();
            if (camera == null) return;
            ActiveNumbers = 0;
            foreach (Number number in numbers)
            {
                float age = Time.time - number.started;
                bool live = age < 0.8f;
                Vector3 viewport = camera.WorldToViewportPoint(number.point + Vector3.up * age * 0.9f);
                number.text.gameObject.SetActive(live && viewport.z > 0);
                if (!live) continue;
                ActiveNumbers++;
                number.text.rectTransform.anchoredPosition = new Vector2((viewport.x - 0.5f) * canvasRect.rect.width, (viewport.y - 0.5f) * canvasRect.rect.height);
                number.text.rectTransform.localScale = Vector3.one * (1 + Mathf.Max(0, 1 - age / 0.12f) * 0.3f);
                number.text.color = new Color(number.color.r, number.color.g, number.color.b, Mathf.Clamp01((0.8f - age) / 0.25f));
            }
            banner.color = new Color(1, 0.75f, 0.2f, Mathf.Clamp01((bannerUntil - Time.time) / 0.4f));
        }

        private void OnDisable() => ClearFeedback();

        private void OnDestroy()
        {
            if (edgeSprite != null) Destroy(edgeSprite);
            if (edgeTexture != null) Destroy(edgeTexture);
        }
    }
}
