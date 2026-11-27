using System.Collections.Generic;
using Nomad.Combat;
using Nomad.Hud;
using Nomad.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Nomad.Monsters.Demo
{
    [DefaultExecutionOrder(410)]
    public sealed class BossBattleHud : MonoBehaviour
    {
        private DragonBossCombat boss;
        private Health health;
        private BossArmor armor;
        private Canvas canvas;
        private GameObject frame, lower;
        private RectTransform fill;
        private readonly Image[] shellFills = new Image[2];
        private readonly Text[] shellNames = new Text[2];
        private Text value, state, title;
        private RectTransform delayedFill;
        private BossHealthFlameGraphic healthFx;
        private float delayedRatio = 1, hitFlash;
        private int previousHealth;
        private PlayerHud hud;
        private PlayerMana mana;
        private PlayerStamina stamina;
        private BossBattleFrameGraphic manaRing, staminaRing;
        private Text manaValue, staminaValue;
        private Transform manaGroup;
        private bool manaWasActive;
        private readonly Dictionary<RectTransform, Vector2> positions = new();
        public bool Visible => frame != null && frame.activeSelf;

        public void Configure(DragonBossCombat owner, Health player)
        {
            boss = owner; health = GetComponent<Health>(); armor = GetComponent<BossArmor>();
            previousHealth = health.CurrentHealth;
            hud = player.GetComponent<PlayerHud>();
            mana = player.GetComponent<PlayerMana>(); stamina = player.GetComponent<PlayerStamina>();
            var root = new GameObject("Boss battle HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 65;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform plate = Rect("Obsidian boss frame", canvas.transform, new Vector2(1160, 126), new Vector2(0, -90), true);
            plate.gameObject.AddComponent<BossBattleFrameGraphic>().raycastTarget = false;
            frame = plate.gameObject;
            title = Label("잿불의 폭군  ·  최후의 수호자", plate, new Vector2(1000, 36), new Vector2(0, 34), 25);
            Image track = Image("Health track", plate, new Vector2(1060, 30), new Vector2(0, -4), new Color(0.2f, 0.015f, 0.02f));
            delayedFill = Image("Health damage trail", track.transform, new Vector2(1052, 20), Vector2.zero, new Color(1, 0.56f, 0.08f)).rectTransform;
            delayedFill.anchorMin = delayedFill.anchorMax = delayedFill.pivot = new Vector2(0, 0.5f);
            delayedFill.anchoredPosition = new Vector2(4, 0);
            fill = Image("Ruby health", track.transform, new Vector2(1052, 20), Vector2.zero, new Color(0.83f, 0.035f, 0.045f)).rectTransform;
            fill.anchorMin = fill.anchorMax = fill.pivot = new Vector2(0, 0.5f);
            fill.anchoredPosition = new Vector2(4, 0);
            healthFx = Rect("Boss health flames", track.transform, new Vector2(1060, 30), Vector2.zero).gameObject.AddComponent<BossHealthFlameGraphic>();
            healthFx.raycastTarget = false;
            value = Label("", plate, new Vector2(500, 25), new Vector2(0, -36), 18);
            Image armorTrack = Image("Two carapace layers", plate, new Vector2(520, 14), new Vector2(0, -73), new Color(0.08f, 0.06f, 0.04f));
            for (int layer = 0; layer < 2; layer++)
            {
                float x = layer == 0 ? -132 : 132;
                shellFills[layer] = Image(layer == 0 ? "Outer shell durability" : "Inner shell durability", armorTrack.transform,
                    new Vector2(250, 10), new Vector2(x - 125, 0), new Color(0.7f,0.7f,0.7f));
                shellFills[layer].rectTransform.pivot = new Vector2(0, 0.5f);
                shellNames[layer] = Label(layer == 0 ? "외피 1단계" : "내피 2단계", armorTrack.transform,
                    new Vector2(250, 20), new Vector2(x, 18), 14);
            }
            state = Label("", plate, new Vector2(900, 24), new Vector2(0, -96), 16);
            frame.SetActive(false);
        }

        private void LateUpdate()
        {
            bool visible = boss != null && boss.Introduced && !health.IsDead;
            frame.SetActive(visible);
            GetComponent<Nomad.Monsters.SegmentedHealthBar>().enabled = false;
            if (visible)
            {
                fill.sizeDelta = new Vector2(1052 * health.CurrentHealth / health.MaxHealth, 20);
                value.text = health.CurrentHealth.ToString("N0") + " / " + health.MaxHealth.ToString("N0");
                for (int layer = 0; layer < 2; layer++)
                {
                    bool broken = layer < armor.BreakCount;
                    float shellRatio = broken ? 0 : layer == armor.BreakCount ? armor.RemainingRatio : 1;
                    shellFills[layer].rectTransform.sizeDelta = new Vector2(250 * shellRatio, 10);
                    shellFills[layer].color = layer == 0 ? new Color(0.5f,0.72f,0.85f) : new Color(1,0.6f,0.2f);
                    shellNames[layer].text = (layer == 0 ? "외피 1단계" : "내피 2단계") + (broken ? "  파괴 완료" : "  남음");
                    shellNames[layer].color = broken ? new Color(1,0.55f,0.15f) : Color.white;
                }
                state.text = "갑옷 파괴 " + armor.BreakCount + "/2 · 받는 피해 " + Mathf.RoundToInt(armor.DamageMultiplier * 100) + "%" + (boss.EnrageTransitioning ? "   ·   광폭 전환" : boss.Enraged ? "   ·   2페이즈 · 광폭" : "");
                state.color = boss.EnrageTransitioning || boss.Enraged ? new Color(1, 0.3f, 0.12f) : new Color(0.95f, 0.8f, 0.5f);
                title.text = boss.EnrageTransitioning ? "잿불의 폭군  ·  광폭화" : boss.Enraged ? "잿불의 폭군  ·  2페이즈 광폭" : "잿불의 폭군  ·  최후의 수호자";
                if (health.CurrentHealth < previousHealth) hitFlash = 0.4f;
                previousHealth = health.CurrentHealth;
                hitFlash = Mathf.Max(0, hitFlash - Time.deltaTime);
                float ratio = health.CurrentHealth / (float)health.MaxHealth;
                delayedRatio = ratio > delayedRatio ? ratio : Mathf.MoveTowards(delayedRatio, ratio, Time.deltaTime * 0.28f);
                delayedFill.sizeDelta = new Vector2(1052 * delayedRatio, 20);
                healthFx.Intensity = Mathf.Max(boss.EnrageAmount * (0.8f + Mathf.Sin(Time.time * 5) * 0.2f), hitFlash);
                healthFx.SetVerticesDirty();
            }
            if (lower == null && hud != null && hud.Canvas != null) BuildLower();
            Gauge(manaRing, manaValue, mana != null ? mana.Current / mana.Maximum : 0);
            Gauge(staminaRing, staminaValue, stamina != null ? stamina.Current / stamina.Maximum : 0);
        }

        private void BuildLower()
        {
            Transform bar = hud.Canvas.transform.Find("Bottom Bar");
            if (bar == null) return;
            RectTransform plate = Rect("Boss battle lower frame", bar, new Vector2(1120, 138), new Vector2(0, 75));
            lower = plate.gameObject;
            plate.SetAsFirstSibling();
            plate.gameObject.AddComponent<BossBattleFrameGraphic>().raycastTarget = false;
            foreach (RectTransform child in bar)
            {
                if (child.name == "Health") { positions.Add(child, child.anchoredPosition); child.anchoredPosition = new Vector2(-510, 80); }
                if (child.name.StartsWith("Slot ")) { positions.Add(child, child.anchoredPosition); child.anchoredPosition += Vector2.right * 100; }
            }
            manaGroup = bar.Find("Mana");
            if (manaGroup != null) { manaWasActive = manaGroup.gameObject.activeSelf; manaGroup.gameObject.SetActive(false); }
            manaRing = Ring(bar, "MP", -215, new Color(0.2f, 0.6f, 1), out manaValue);
            staminaRing = Ring(bar, "STM", -125, new Color(1, 0.66f, 0.15f), out staminaValue);
        }

        private BossBattleFrameGraphic Ring(Transform parent, string title, float x, Color color, out Text number)
        {
            RectTransform rect = Rect(title + " battle gauge", parent, new Vector2(72, 72), new Vector2(x, 75));
            var graphic = rect.gameObject.AddComponent<BossBattleFrameGraphic>();
            graphic.Ring = true; graphic.GaugeColor = color; graphic.raycastTarget = false;
            Label(title, rect, new Vector2(80, 20), new Vector2(0, 48), 15);
            number = Label("100%", rect, new Vector2(72, 30), Vector2.zero, 18);
            return graphic;
        }
        private static void Gauge(BossBattleFrameGraphic ring, Text value, float fraction)
        {
            if (ring == null) return;
            ring.Fraction = Mathf.Clamp01(fraction); ring.SetVerticesDirty();
            value.text = Mathf.RoundToInt(fraction * 100) + "%";
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 point, bool top = false)
        {
            var root = new GameObject(name, typeof(RectTransform)); root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, top ? 1 : 0);
            rect.sizeDelta = size; rect.anchoredPosition = point;
            return rect;
        }
        private static Image Image(string name, Transform parent, Vector2 size, Vector2 point, Color color)
        {
            var image = Rect(name, parent, size, point).gameObject.AddComponent<Image>();
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            image.color = color; image.raycastTarget = false;
            return image;
        }
        private static Text Label(string text, Transform parent, Vector2 size, Vector2 point, int fontSize)
        {
            var label = Rect(text, parent, size, point).gameObject.AddComponent<Text>();
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.font = Resources.Load<Font>("ShopFonts/Jua-Regular"); label.fontSize = fontSize;
            label.color = new Color(0.92f, 0.77f, 0.5f); label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false; label.text = text;
            return label;
        }
        private void OnDestroy()
        {
            foreach (var pair in positions) if (pair.Key != null) pair.Key.anchoredPosition = pair.Value;
            if (manaGroup != null) manaGroup.gameObject.SetActive(manaWasActive);
            if (manaRing != null) Destroy(manaRing.gameObject);
            if (staminaRing != null) Destroy(staminaRing.gameObject);
            if (lower != null) Destroy(lower);
        }
    }
}
