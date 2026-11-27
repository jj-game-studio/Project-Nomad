using System.Collections.Generic;
using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Inventory;
using Nomad.Player;
using TMPro;
using UnityEngine;

namespace Nomad.Hud
{
    [DisallowMultipleComponent]
    public sealed partial class PlayerHud : MonoBehaviour
    {
        public enum HudStyle { Original, Medieval }

        [SerializeField] private HudStyle style = HudStyle.Medieval;
        [SerializeField] private Health health;
        [SerializeField] private TMP_FontAsset font;

        private Canvas canvas;
        private RectTransform healthFill;
        private TMP_Text healthText;
        private RectTransform manaFill;
        private TMP_Text manaText;
        private TMP_Text weaponText;
        private TMP_Text attackStatusText;
        private TMP_FontAsset weaponFont;
        private GameObject root;
        private PlayerMana mana;
        private PlayerStamina stamina;
        private float manaFraction;
        private float staminaFraction;
        private PlayerEquipmentCombat combat;
        private ItemDefinition shownWeapon;
        private int shownArrows = -1;
        private float shownMana = -1f;
        private float shownMaxMana = -1f;
        private int shownAvailableMana = -1;
        private int shownRequiredMana = -1;
        private int shownSelected = -2;
        private readonly List<HudArtwork> surfaces = new List<HudArtwork>();
        private readonly List<GameObject> trims = new List<GameObject>();
        private readonly RectTransform[] quickRects = new RectTransform[PlayerQuickSlots.Capacity];
        private readonly HudArtwork[] quickBackgrounds = new HudArtwork[PlayerQuickSlots.Capacity];
        private readonly HudArtwork[] selectionGlows = new HudArtwork[PlayerQuickSlots.Capacity];
        private readonly UnityEngine.UI.Image[] quickIcons = new UnityEngine.UI.Image[PlayerQuickSlots.Capacity];
        private readonly TMP_Text[] quickNames = new TMP_Text[PlayerQuickSlots.Capacity];
        private readonly TMP_Text[] quickCaptions = new TMP_Text[PlayerQuickSlots.Capacity];
        private readonly TMP_Text[] quickCounts = new TMP_Text[PlayerQuickSlots.Capacity];
        private readonly HudArtwork[] dropHighlights = new HudArtwork[PlayerQuickSlots.Capacity];
        private readonly GameObject[] emptyContents = new GameObject[PlayerQuickSlots.Capacity];
        private readonly CanvasGroup[] quickGroups = new CanvasGroup[PlayerQuickSlots.Capacity];

        public PlayerQuickSlots QuickSlots { get; private set; }

        public HudStyle Style => style;
        private int shownHealth = -1;
        private int shownMaxHealth = -1;

        public Canvas Canvas => canvas;
        public float ManaFraction => manaFraction;
        public float StaminaFraction => staminaFraction;
        public string HealthLabel => healthText == null ? string.Empty : healthText.text;
        public string AttackStatus => attackStatusText == null ? string.Empty : attackStatusText.text;
        // 현재 선택(장착 사용 중)된 무기가 등록된 퀵바 칸 번호, 없으면 -1입니다.
        public int SelectedQuickSlot => shownSelected < 0 ? -1 : shownSelected;

        private static readonly Color Panel = new Color32(12, 17, 29, 225);
        private static readonly Color Red = new Color32(255, 70, 70, 255);
        private static readonly Color Blue = new Color32(122, 102, 255, 255);
        private static readonly Color Amber = new Color32(255, 193, 104, 255);
        private static readonly Color BarTrack = new Color32(64, 58, 56, 235);
        private static readonly Color SlotGray = new Color32(78, 72, 70, 205);
        private static readonly Color SlotRed = new Color32(150, 74, 66, 220);
        private static readonly Color SlotSelected = new Color32(226, 72, 64, 245);
        private static readonly Color SelectedGlow = new Color32(255, 54, 54, 150);

        private void Awake()
        {
            if (health == null)
                health = GetComponentInParent<Health>();

            mana = GetComponentInParent<PlayerMana>();
            stamina = GetComponentInParent<PlayerStamina>();
            combat = GetComponentInParent<PlayerEquipmentCombat>();
            PlayerInventory inventory = GetComponentInParent<PlayerInventory>();
            if (inventory != null)
            {
                QuickSlots = inventory.GetComponent<PlayerQuickSlots>();
                if (QuickSlots == null)
                    QuickSlots = inventory.gameObject.AddComponent<PlayerQuickSlots>();
            }

            // 칸 이름(무기1·물약 등)이 한글이라 한글 글꼴을 먼저 준비합니다.
            if (Resources.Load<Font>("ShopFonts/NanumGothic-Regular") is Font koreanFont)
                weaponFont = TMP_FontAsset.CreateFontAsset(koreanFont);

            Build();
            BuildChargeGauge();
            if (weaponFont != null)
            {
                weaponText.font = weaponFont;
                attackStatusText.font = weaponFont;
                chargeLabel.font = weaponFont;
            }

            SetResources(0, 0, 0, 0);
            SetWeapon(null);
            SetStyle(style);
        }

        private void OnEnable()
        {
            if (root != null)
                root.SetActive(true);
            if (QuickSlots != null)
                QuickSlots.Changed += RefreshQuickSlots;
            RefreshQuickSlots();
        }

        private void OnDisable()
        {
            if (root != null)
                root.SetActive(false);
            if (QuickSlots != null)
                QuickSlots.Changed -= RefreshQuickSlots;
            HighlightQuickSlot(-1);
        }

        private void LateUpdate()
        {
            UpdateChargeGauge();

            if (mana != null && (shownMana != mana.Current || shownMaxMana != mana.Maximum))
            {
                shownMana = mana.Current;
                shownMaxMana = mana.Maximum;
                manaFraction = SetBar(manaFill, manaText, shownMana, shownMaxMana);
            }

            if (stamina != null)
                staminaFraction = Fraction(stamina.Current, stamina.Maximum);

            if (combat != null)
                UpdateWeaponStatus();

            UpdateSelection();

            int current = health == null ? 0 : health.CurrentHealth;
            int maximum = health == null ? 0 : health.MaxHealth;
            if (current == shownHealth && maximum == shownMaxHealth)
                return;

            shownHealth = current;
            shownMaxHealth = maximum;
            SetBar(healthFill, healthText, current, maximum);
        }

        private void UpdateWeaponStatus()
        {
            ItemDefinition weapon = combat.CurrentWeapon;
            int requiredMana = weapon?.AttackMode == WeaponAttackMode.Staff ? weapon.ManaCost : 0;
            int availableMana = mana != null ? Mathf.FloorToInt(mana.Current) : 0;
            if (requiredMana != shownRequiredMana || availableMana != shownAvailableMana || attackStatusText.text.Length == 0)
            {
                shownRequiredMana = requiredMana;
                shownAvailableMana = availableMana;
                bool insufficient = requiredMana > 0 && availableMana < requiredMana;
                attackStatusText.text = insufficient ? $"마나 부족 {availableMana}/{requiredMana}" : "CURRENT";

                // 시안에는 CURRENT 표기가 없어 마나 부족 경고만 화면에 띄웁니다.
                attackStatusText.gameObject.SetActive(insufficient);
            }

            if (combat.IsCharging)
                attackStatusText.text = "";

            // 활은 남은 화살 수를 선택 칸 위에 표시합니다.
            int arrows = weapon?.AttackMode == WeaponAttackMode.Bow ? combat.ArrowCount : -1;
            if (weapon != shownWeapon || arrows != shownArrows)
            {
                shownWeapon = weapon;
                shownArrows = arrows;
                SetWeapon(arrows >= 0 ? $"화살 {arrows}" : null);
            }
        }

        // 손에 든 무기와 같은 아이템이 등록된 무기 칸을 빨갛게 강조합니다.
        private void UpdateSelection()
        {
            ItemDefinition weapon = combat != null ? combat.CurrentWeapon : null;
            int selected = -1;
            for (int index = 0; weapon != null && QuickSlots != null && index < PlayerQuickSlots.WeaponSlotCount; index++)
            {
                if (QuickSlots.GetItem(index) == weapon)
                    selected = index;
            }

            if (selected == shownSelected)
                return;

            shownSelected = selected;
            for (int index = 0; index < PlayerQuickSlots.Capacity; index++)
            {
                bool weaponSlot = PlayerQuickSlots.IsWeaponSlot(index);
                quickBackgrounds[index].color = index == selected ? SlotSelected : weaponSlot ? SlotRed : SlotGray;
                selectionGlows[index].gameObject.SetActive(index == selected);
            }

            // 화살 수·마나 경고는 선택된 무기 칸 위에 붙습니다.
            RectTransform anchor = quickRects[selected >= 0 ? selected : 0];
            weaponText.rectTransform.SetParent(anchor, false);
            attackStatusText.rectTransform.SetParent(anchor, false);
        }

        // 표시만 갱신합니다. 자원 소모/회복과 장착 규칙은 해당 시스템에서 소유합니다.
        public void SetResources(float mana, float maxMana, float stamina, float maxStamina)
        {
            manaFraction = SetBar(manaFill, manaText, mana, maxMana);
            staminaFraction = Fraction(stamina, maxStamina);
        }

        public void SetWeapon(string displayName)
        {
            weaponText.text = displayName ?? string.Empty;
            weaponText.gameObject.SetActive(!string.IsNullOrEmpty(displayName));
        }

        public int FindQuickSlot(Vector2 screenPoint)
        {
            if (!isActiveAndEnabled || QuickSlots == null || !QuickSlots.isActiveAndEnabled || !root.activeInHierarchy)
                return -1;

            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            for (int index = 0; index < quickRects.Length; index++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(quickRects[index], screenPoint, eventCamera))
                    return index;
            }

            return -1;
        }

        public void SetQuickSlotDragging(int index, bool dragging)
        {
            quickGroups[index].alpha = dragging ? .45f : 1f;
        }

        // 끌고 있는 아이템을 받을 수 없는 칸(무기 칸의 포션 등)은 강조하지 않습니다.
        public void HighlightQuickSlot(int index, ItemDefinition item = null)
        {
            if (item != null && !PlayerQuickSlots.Accepts(index, item))
                index = -1;

            for (int slot = 0; slot < dropHighlights.Length; slot++)
            {
                if (dropHighlights[slot] != null)
                    dropHighlights[slot].gameObject.SetActive(slot == index);
            }
        }

        private void RefreshQuickSlots()
        {
            if (root == null)
                return;

            for (int index = 0; index < quickRects.Length; index++)
            {
                ItemDefinition item = QuickSlots == null ? null : QuickSlots.GetItem(index);
                emptyContents[index].SetActive(item == null);
                quickIcons[index].sprite = item == null ? null : item.Icon;
                quickIcons[index].enabled = item != null && item.Icon != null;
                quickNames[index].text = item != null && item.Icon == null ? item.DisplayName : string.Empty;
                quickNames[index].gameObject.SetActive(item != null && item.Icon == null);

                // 무기 칸은 고정 이름, 소모품 칸은 등록한 아이템 이름을 칸 아래에 씁니다.
                quickCaptions[index].text = PlayerQuickSlots.IsWeaponSlot(index) ? $"무기{index + 1}" :
                    item != null ? item.DisplayName : string.Empty;

                // 공용 규격상 무기·방어구는 최대 중첩 1이며, 바로가기에는 보유 수량을 표시하지 않습니다.
                bool showCount = item != null && item.MaxStack > 1;
                quickCounts[index].text = showCount ? $"×{QuickSlots.GetCount(index)}" : string.Empty;
                quickCounts[index].gameObject.SetActive(showCount);
            }

            shownSelected = -2;
        }

        // 표시를 재생성하지 않고 같은 체력·자원·슬롯 상태로 스타일만 비교합니다.
        public void SetStyle(HudStyle next)
        {
            style = next;
            bool medieval = next == HudStyle.Medieval;
            if (root == null)
                return;

            foreach (HudArtwork surface in surfaces)
                surface.Configure(medieval ? HudArtwork.Shape.Leather : HudArtwork.Shape.Panel,
                    medieval ? new Color32(34, 29, 26, 240) : Panel);
            foreach (GameObject trim in trims)
                trim.SetActive(medieval);
        }

        private static float Fraction(float current, float maximum)
        {
            bool available = maximum > 0 && !float.IsInfinity(maximum) && !float.IsNaN(maximum);
            return available ? Mathf.Clamp01(current / maximum) : 0f;
        }

        private static float SetBar(RectTransform fill, TMP_Text label, float current, float maximum)
        {
            float fraction = Fraction(current, maximum);
            fill.anchorMax = new Vector2(fraction, 1f);

            bool available = maximum > 0 && !float.IsInfinity(maximum) && !float.IsNaN(maximum);
            label.text = available ? $"{Mathf.RoundToInt(Mathf.Max(0, current))} / {Mathf.RoundToInt(maximum)}" : "-- / --";
            return fraction;
        }

        private void Build()
        {
            if (font == null)
                font = Resources.Load<TMP_FontAsset>("NomadHudFont");

            root = new GameObject("Player HUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            // 지역 씬이 바뀌어도 HUD가 플레이어와 함께 남도록 플레이어와 같은 씬에 둡니다.
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            // 화면 아래 띠 하나에 왼쪽 상태 바와 오른쪽으로 치우친 퀵바를 둡니다. 입력은 받지 않습니다.
            RectTransform bar = Rect("Bottom Bar", root.transform, new Vector2(0, 260));
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(.5f, 0f);
            bar.anchoredPosition = Vector2.zero;

            healthFill = StatusBar(bar, "Health", 150, HudArtwork.Shape.Heart, Red, out healthText);
            manaFill = StatusBar(bar, "Mana", 30, HudArtwork.Shape.Star, Blue, out manaText);

            // 시안 비율대로 퀵바 중심을 화면 가운데에서 오른쪽으로 옮깁니다.
            const float size = 138, gap = 18;
            float first = 360 - (PlayerQuickSlots.Capacity - 1) * (size + gap) * .5f;
            for (int index = 0; index < PlayerQuickSlots.Capacity; index++)
                BuildQuickSlot(bar, index, new Vector2(first + index * (size + gap), 0), size);

            weaponText = Label("Weapon Name", quickRects[0], "", new Vector2(190, 26), new Vector2(0, size * .5f + 18), 18);
            attackStatusText = Label("Current", quickRects[0], "CURRENT", new Vector2(190, 22), new Vector2(0, size * .5f + 40), 16);
            attackStatusText.color = Red;
            attackStatusText.gameObject.SetActive(false);
        }

        private RectTransform StatusBar(RectTransform parent, string name, float y, HudArtwork.Shape icon, Color tint, out TMP_Text value)
        {
            RectTransform group = Rect(name, parent, new Vector2(560, 100));
            group.anchorMin = group.anchorMax = group.pivot = Vector2.zero;
            group.anchoredPosition = new Vector2(70, y);

            // 아이콘은 막대 왼쪽에 크게, 수치는 막대 위에 흰색으로 둡니다.
            Art(name == "Health" ? "Heart" : "Star", group, new Vector2(92, 88), new Vector2(-216, -12), icon, tint);
            RectTransform track = Rect(name + " Track", group, new Vector2(490, 32), new Vector2(46, -28));
            Art("Outline", track, new Vector2(496, 38), Vector2.zero, HudArtwork.Shape.Panel, new Color32(24, 20, 20, 230));
            Art("Empty", track, new Vector2(490, 32), Vector2.zero, HudArtwork.Shape.Panel, BarTrack);

            RectTransform fill = Art("Fill", track, new Vector2(490, 32), Vector2.zero, HudArtwork.Shape.Panel, tint).rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;

            value = Label(name + " Value", track, "", new Vector2(490, 40), new Vector2(0, 42), 38);
            value.alignment = TextAlignmentOptions.Left;
            value.color = Color.white;
            return fill;
        }

        private void BuildQuickSlot(RectTransform bar, int index, Vector2 position, float size)
        {
            Vector2 square = new Vector2(size, size);
            RectTransform slot = Rect($"Slot {index + 1}", bar, square);
            slot.anchorMin = slot.anchorMax = new Vector2(.5f, 0f);
            slot.anchoredPosition = position + new Vector2(0, 120);
            quickRects[index] = slot;
            quickGroups[index] = slot.gameObject.AddComponent<CanvasGroup>();

            // 선택 무기 칸은 바깥 붉은 빛과 진한 빨강 배경으로 두 겹 강조합니다.
            selectionGlows[index] = Art("Selection", slot, square + new Vector2(16, 16), Vector2.zero, HudArtwork.Shape.Panel, SelectedGlow);
            selectionGlows[index].gameObject.SetActive(false);
            bool weaponSlot = PlayerQuickSlots.IsWeaponSlot(index);
            quickBackgrounds[index] = Art("Background", slot, square, Vector2.zero, HudArtwork.Shape.Panel, weaponSlot ? SlotRed : SlotGray);
            dropHighlights[index] = Art("Drop Highlight", slot, square + new Vector2(6, 6), Vector2.zero, HudArtwork.Shape.Fittings, Amber);
            dropHighlights[index].gameObject.SetActive(false);

            RectTransform empty = Rect("Empty Content", slot, square);
            emptyContents[index] = empty.gameObject;

            var icon = Rect("Item Icon", slot, square * .72f, new Vector2(4, -2)).gameObject.AddComponent<UnityEngine.UI.Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            quickIcons[index] = icon;
            quickNames[index] = Label("Item Name", slot, "", square * .85f, Vector2.zero, 18, true);
            quickNames[index].enableAutoSizing = true;
            quickNames[index].fontSizeMin = 10;

            quickCounts[index] = Label("Item Count", slot, "", new Vector2(60, 28), new Vector2(size * .5f - 32, -size * .5f + 18), 22);
            quickCounts[index].alignment = TextAlignmentOptions.Right;
            TMP_Text key = Label("Key", slot, (index + 1).ToString(), new Vector2(30, 30), new Vector2(-size * .5f + 18, size * .5f - 18), 22);
            key.alignment = TextAlignmentOptions.Left;
            quickCaptions[index] = Label("Caption", slot, "", new Vector2(size + 10, 30), new Vector2(0, -size * .5f - 20), 22, true);
        }

        private HudArtwork Art(string name, Transform parent, Vector2 size, Vector2 position, HudArtwork.Shape shape, Color color, float line = 4)
        {
            HudArtwork artwork = Rect(name, parent, size, position).gameObject.AddComponent<HudArtwork>();
            artwork.Configure(shape, color, line);
            artwork.raycastTarget = false;
            return artwork;
        }

        // 차지 게이지 패널은 기존 가죽/패널 스타일 전환을 그대로 따릅니다.
        private void Surface(string name, Transform parent, Vector2 size)
        {
            HudArtwork border = Art(name + " Brass", parent, size + new Vector2(4, 4), Vector2.zero,
                HudArtwork.Shape.Panel, new Color32(103, 89, 70, 255));
            trims.Add(border.gameObject);
            surfaces.Add(Art(name, parent, size, Vector2.zero, HudArtwork.Shape.Panel, Panel));
        }

        private TMP_Text Label(string name, Transform parent, string value, Vector2 size, Vector2 position, int fontSize, bool korean = false)
        {
            var text = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = korean && weaponFont != null ? weaponFont : font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = new Color32(242, 242, 235, 255);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.fontStyle = FontStyles.Bold;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position = default)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private void OnDestroy()
        {
            if (weaponFont != null)
                Destroy(weaponFont);

            if (root != null)
                Destroy(root);
        }
    }
}
