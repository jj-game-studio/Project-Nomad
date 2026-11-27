using System.Collections.Generic;
using Nomad.Combat;
using Nomad.Consumables;
using Nomad.Equipment;
using Nomad.Game;
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
        private TMP_Text weaponText;
        private TMP_Text attackStatusText;
        private TMP_Text potionText;
        private PlayerHealing healing;
        private int shownPotions = -1;
        private TMP_FontAsset weaponFont;
        private HudArtwork manaRing;
        private HudArtwork staminaRing;
        private TMP_Text manaText;
        private TMP_Text staminaText;
        private GameObject root;
        private PlayerMana mana;
        private PlayerStamina stamina;
        private float shownStamina = -1f;
        private PlayerEquipmentCombat combat;
        private ItemDefinition shownWeapon;
        private int shownArrows = -1;
        private float shownMana = -1f;
        private int shownAvailableMana = -1;
        private int shownRequiredMana = -1;
        private readonly List<HudArtwork> surfaces = new List<HudArtwork>();
        private readonly List<GameObject> trims = new List<GameObject>();
        private HudArtwork selection;
        private readonly RectTransform[] quickRects = new RectTransform[PlayerQuickSlots.Capacity];
        private readonly UnityEngine.UI.Image[] quickIcons = new UnityEngine.UI.Image[PlayerQuickSlots.Capacity];
        private readonly TMP_Text[] quickNames = new TMP_Text[PlayerQuickSlots.Capacity];
        private readonly TMP_Text[] quickCounts = new TMP_Text[PlayerQuickSlots.Capacity];
        private readonly HudArtwork[] dropHighlights = new HudArtwork[PlayerQuickSlots.Capacity];
        private readonly GameObject[] emptyContents = new GameObject[PlayerQuickSlots.Capacity];

        private readonly CanvasGroup[] quickGroups = new CanvasGroup[PlayerQuickSlots.Capacity];

        public PlayerQuickSlots QuickSlots { get; private set; }

        public HudStyle Style => style;
        private int shownHealth = -1;
        private int shownMaxHealth = -1;

        public Canvas Canvas => canvas;
        public float ManaFraction => manaRing == null ? 0f : manaRing.Fraction;
        public float StaminaFraction => staminaRing == null ? 0f : staminaRing.Fraction;
        public string HealthLabel => healthText == null ? string.Empty : healthText.text;
        public string AttackStatus => attackStatusText == null ? string.Empty : attackStatusText.text;

        private static readonly Color Panel = new Color32(12, 17, 29, 225);
        private static readonly Color Red = new Color32(255, 104, 112, 255);
        private static readonly Color Blue = new Color32(102, 204, 255, 255);
        private static readonly Color Amber = new Color32(255, 193, 104, 255);

        private void Awake()
        {
            if (health == null)
                health = GetComponentInParent<Health>();

            mana = GetComponentInParent<PlayerMana>();
            stamina = GetComponentInParent<PlayerStamina>();
            combat = GetComponentInParent<PlayerEquipmentCombat>();
            healing = GetComponentInParent<PlayerHealing>();
            PlayerInventory inventory = GetComponentInParent<PlayerInventory>();
            if (inventory != null)
            {
                QuickSlots = inventory.GetComponent<PlayerQuickSlots>();
                if (QuickSlots == null)
                    QuickSlots = inventory.gameObject.AddComponent<PlayerQuickSlots>();
            }

            Build();
            BuildChargeGauge();
            if (combat != null && Resources.Load<Font>("ShopFonts/NanumGothic-Regular") is Font koreanFont)
            {
                weaponFont = TMP_FontAsset.CreateFontAsset(koreanFont);
                weaponText.font = weaponFont;
                attackStatusText.font = weaponFont;
                potionText.font = weaponFont;
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
            // 설정의 UI 표시가 꺼지면 하단 HUD 캔버스만 그리지 않습니다.
            canvas.enabled = HudVisibilitySettings.Visible;

            UpdateChargeGauge();
            potionText.gameObject.SetActive(healing != null);
            if (healing != null && healing.Count != shownPotions)
            {
                shownPotions = healing.Count;
                potionText.text = $"Q  회복약 ×{shownPotions}";
                potionText.color = shownPotions > 0 ? Color.white : Red;
            }
            if (mana != null && shownMana != mana.Current)
            {
                shownMana = mana.Current;
                SetResource(manaRing, manaText, shownMana, mana.Maximum);
            }

            if (stamina != null && shownStamina != stamina.Current)
            {
                shownStamina = stamina.Current;
                SetResource(staminaRing, staminaText, shownStamina, stamina.Maximum);
            }

            if (combat != null)
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
                    attackStatusText.color = insufficient ? Red : Color.white;
                }
                if (combat.IsCharging)
                {
                    attackStatusText.text = "";
                    attackStatusText.color = Amber;
                }
                int arrows = weapon?.AttackMode == WeaponAttackMode.Bow ? combat.ArrowCount : -1;
                if (weapon != shownWeapon || arrows != shownArrows)
                {
                    shownWeapon = weapon;
                    shownArrows = arrows;
                    SetWeapon(arrows >= 0 ? $"{weapon.DisplayName}  ·  {arrows}" : weapon?.DisplayName);
                }
            }

            int current = health == null ? 0 : health.CurrentHealth;
            int maximum = health == null ? 0 : health.MaxHealth;
            if (current == shownHealth && maximum == shownMaxHealth)
                return;

            shownHealth = current;
            shownMaxHealth = maximum;
            healthText.text = maximum > 0 ? $"{current} / {maximum}" : "-- / --";
            healthFill.anchorMax = new Vector2(maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f, 1f);
        }

        // 표시만 갱신합니다. 자원 소모/회복과 장착 규칙은 해당 시스템에서 소유합니다.
        public void SetResources(float mana, float maxMana, float stamina, float maxStamina)
        {
            SetResource(manaRing, manaText, mana, maxMana);
            SetResource(staminaRing, staminaText, stamina, maxStamina);
        }

        public void SetWeapon(string displayName)
        {
            weaponText.text = string.IsNullOrWhiteSpace(displayName) ? "UNASSIGNED" : displayName;
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

        public void HighlightQuickSlot(int index)
        {
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
                // 공용 규격상 무기·방어구는 최대 중첩 1이며, 바로가기에는 보유 수량을 표시하지 않습니다.
                bool showCount = item != null && item.MaxStack > 1;
                quickCounts[index].text = showCount ? $"×{QuickSlots.GetCount(index)}" : string.Empty;
                quickCounts[index].gameObject.SetActive(showCount);
            }
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

            // 선택 강조와 자원 게이지는 두 스타일에서 동일하게 유지합니다.
            selection.Configure(HudArtwork.Shape.Panel, Amber);
        }

        private static void SetResource(HudArtwork ring, TMP_Text label, float current, float maximum)
        {
            bool available = maximum > 0 && !float.IsInfinity(maximum) && !float.IsNaN(maximum);
            ring.Fraction = available ? current / maximum : 0;
            label.text = available ? $"{Mathf.RoundToInt(ring.Fraction * 100)}%" : "--";
        }

        private void Build()
        {
            if (font == null)
            {
                font = Resources.Load<TMP_FontAsset>("NomadHudFont");
            }

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

            // 상태 표시는 입력을 받지 않아 기존 조준/상자 UI의 클릭 판정을 가리지 않습니다.
            RectTransform bar = Rect("Bottom Bar", root.transform, new Vector2(1020, 154));
            bar.anchorMin = bar.anchorMax = new Vector2(.5f, 0f);
            bar.pivot = new Vector2(.5f, 0f);
            bar.anchoredPosition = new Vector2(0, 22);

            RectTransform hp = Rect("Health", bar, new Vector2(270, 54), new Vector2(-358, -12));
            Surface("Track", hp, new Vector2(270, 54));
            Art("Heart", hp, new Vector2(35, 36), new Vector2(-108, 0), HudArtwork.Shape.Heart, Red);
            RectTransform track = Rect("Health Track", hp, new Vector2(211, 30), new Vector2(20, 0));
            Art("Empty", track, new Vector2(211, 30), Vector2.zero, HudArtwork.Shape.Panel, new Color32(80, 36, 48, 255));
            healthFill = Art("Fill", track, new Vector2(211, 30), Vector2.zero, HudArtwork.Shape.Panel, Red).rectTransform;
            healthFill.anchorMin = Vector2.zero;
            healthFill.anchorMax = Vector2.one;
            healthFill.offsetMin = healthFill.offsetMax = Vector2.zero;
            healthText = Label("Health Value", track, "", new Vector2(210, 30), Vector2.zero, 21);
            Label("Health Caption", hp, "HEALTH", new Vector2(240, 20), new Vector2(0, -44), 12);
            potionText = Label("Health Potion", hp, "", new Vector2(270, 22), new Vector2(0, 45), 14);

            manaRing = Resource(bar, "Mana", -157, Blue, HudArtwork.Shape.Crystal, out manaText);
            staminaRing = Resource(bar, "Stamina", -49, Amber, HudArtwork.Shape.Bolt, out staminaText);
            for (int index = 0; index < PlayerQuickSlots.Capacity; index++)
            {
                float x = 99 + index * 112;
                RectTransform slot = Rect($"Slot {index + 1}", bar, new Vector2(92, 92), new Vector2(x, 0));
                if (index == 0)
                    selection = Art("Selection", slot, new Vector2(100, 100), Vector2.zero, HudArtwork.Shape.Panel, Amber);
                Surface("Background", slot, new Vector2(92, 92));
                quickRects[index] = slot;
                quickGroups[index] = slot.gameObject.AddComponent<CanvasGroup>();
                dropHighlights[index] = Art("Drop Highlight", slot, new Vector2(96, 96), Vector2.zero,
                    HudArtwork.Shape.Fittings, Amber);
                dropHighlights[index].gameObject.SetActive(false);
                RectTransform empty = Rect("Empty Content", slot, new Vector2(92, 92));
                emptyContents[index] = empty.gameObject;
                if (index == 0)
                {
                    Art("Weapon", empty, new Vector2(75, 65), new Vector2(0, 3), HudArtwork.Shape.Weapon, new Color32(217, 210, 190, 255));
                    Transform captionParent = combat != null ? slot : empty;
                    weaponText = Label("Weapon Name", captionParent, "", new Vector2(190, 26), new Vector2(0, 76), 16);
                    attackStatusText = Label("Current", captionParent, "CURRENT", new Vector2(190, 20), new Vector2(0, 55), 11);
                }
                else
                    Label("Empty", empty, "+", new Vector2(32, 32), Vector2.zero, 23).color = new Color32(94, 104, 121, 255);

                var icon = Rect("Item Icon", slot, new Vector2(64, 64), new Vector2(0, 6))
                    .gameObject.AddComponent<UnityEngine.UI.Image>();
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                quickIcons[index] = icon;
                quickNames[index] = Label("Item Name", slot, "", new Vector2(80, 54), new Vector2(0, 6), 14);
                quickNames[index].enableAutoSizing = true;
                quickNames[index].fontSizeMin = 8;
                quickCounts[index] = Label("Item Count", slot, "", new Vector2(80, 20), new Vector2(0, -32), 15);

                Label("Key", slot, (index + 1).ToString(), new Vector2(24, 24), new Vector2(0, -64), 16);
            }
        }

        private HudArtwork Resource(Transform parent, string name, float x, Color tint, HudArtwork.Shape icon, out TMP_Text text)
        {
            RectTransform group = Rect(name, parent, new Vector2(86, 86), new Vector2(x, -3));
            Surface("Background", group, new Vector2(86, 86));
            Art("Track", group, new Vector2(84, 84), Vector2.zero, HudArtwork.Shape.Ring, new Color32(50, 65, 83, 255), 5);
            HudArtwork ring = Art("Fill", group, new Vector2(84, 84), Vector2.zero, HudArtwork.Shape.Ring, tint, 5);
            Art("Icon", group, new Vector2(31, 36), new Vector2(0, 8), icon, tint);
            text = Label("Value", group, "--", new Vector2(70, 20), new Vector2(0, -21), 15);
            Label("Caption", group, name.ToUpperInvariant(), new Vector2(105, 20), new Vector2(0, -61), 12);
            return ring;
        }

        private void Surface(string name, Transform parent, Vector2 size)
        {
            HudArtwork border = Art(name + " Brass", parent, size + new Vector2(4, 4), Vector2.zero,
                HudArtwork.Shape.Panel, new Color32(103, 89, 70, 255));
            trims.Add(border.gameObject);
            surfaces.Add(Art(name, parent, size, Vector2.zero, HudArtwork.Shape.Panel, Panel));
            HudArtwork fittings = Art(name + " Fittings", parent, size, Vector2.zero,
                HudArtwork.Shape.Fittings, new Color32(172, 147, 104, 255));
            trims.Add(fittings.gameObject);
        }

        private HudArtwork Art(string name, Transform parent, Vector2 size, Vector2 position, HudArtwork.Shape shape, Color color, float line = 4)
        {
            HudArtwork artwork = Rect(name, parent, size, position).gameObject.AddComponent<HudArtwork>();
            artwork.Configure(shape, color, line);
            return artwork;
        }

        private TMP_Text Label(string name, Transform parent, string value, Vector2 size, Vector2 position, int fontSize)
        {
            var text = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
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
