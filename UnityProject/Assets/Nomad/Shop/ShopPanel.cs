using System.Globalization;
using Nomad.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nomad.Shop
{
    [DefaultExecutionOrder(-50)]
    public sealed class ShopPanel : MonoBehaviour
    {
        private sealed class SlotView
        {
            internal Button Button;
            internal Image Icon;
            internal Text Name;
            internal Text Count;
            internal Text Price;
            internal Text Keep;
            internal ShopRoundedRect Frame;
            internal ShopRoundedRect CountBadge;
        }

        private Shopkeeper shop;
        private PlayerInventory inventory;
        private PlayerWallet wallet;
        private SlotView[] playerSlots;
        private SlotView[] merchantSlots;
        private Image selectedIcon;
        private RectTransform tradePopup;
        private Slider quantitySlider;
        private bool syncingQuantity;
        private Text itemTitle;
        private Text tradeHeading;
        private Text unitPrice;
        private Text totalCaption;
        private RectTransform saleNotification;
        private Text saleNotificationText;
        private float saleNotificationUntil;
        private Selectable[] popupControls;
        private int popupFocus = -1;
        private ItemDefinition popupItem;
        private string popupName;
        private Text gold;
        private Text detail;
        private Text description;
        private Text readiness;
        private Text status;
        private Text capacity;
        private Text balance;
        private Button protection;
        private InputField quantityInput;
        private Button less;
        private Button more;
        private Button maximum;
        private Button[] filters;
        private int categoryFilter = -1;
        private Button buy;
        private Button sell;
        private bool merchantSelected = true;
        private int merchantIndex;
        private int playerIndex;
        private int quantity = 1;
        private int openedFrame;
        private int lastTradeFrame = -1;
        private int quantityEditedFrame = -1;
        private bool closed;

        public static ShopPanel Open(Shopkeeper target, PlayerInventory player)
        {
            if (target == null || player == null || !target.CanAccess(player) || player.HasOpenTrade || player.BlocksTradeInput)
                return null;

            var root = new GameObject("Shop UI", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(root, player.gameObject.scene);
            ShopPanel view = root.AddComponent<ShopPanel>();
            view.shop = target;
            view.inventory = player;
            view.wallet = player.GetComponent<PlayerWallet>();
            view.openedFrame = Time.frameCount;
            player.SetTrading(true);
            player.Panel?.CloseForTrade();
            view.Build();
            player.Changed += view.Refresh;
            view.wallet.Changed += view.Refresh;
            target.Changed += view.Refresh;
            view.Refresh();
            return view;
        }

        private void Build()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            GetComponent<Canvas>().pixelPerfect = true;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                var events = new GameObject("Shop Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }

            RectTransform shade = ShopUi.Rect(transform, "Modal Shade", 0, 0, 0, 0);
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = Vector2.one;
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            ShopUi.Background(shade, new Color(0.06f, 0.09f, 0.05f, 0.58f));

            RectTransform panel = ShopUi.Rect(transform, "Shop Window", 0, 0, 1160, 630);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * 0.5f;
            ShopUi.Shadow(ShopUi.Rounded(panel, ShopUi.Forest, 30, 2, ShopUi.Border), 6, 0.25f);
            ShopUi.Rounded(ShopUi.Rect(panel, "Paper", 3, 96, 1154, 531), ShopUi.Canvas, 28, 2, ShopUi.Border);
            ShopUi.Rounded(ShopUi.Rect(panel, "Paper inset", 8, 101, 1144, 521), Color.clear, 24, 1, ShopUi.Surface).raycastTarget = false;
            ShopUi.ShopSign(panel, 30, 24);
            ShopUi.Heading(panel, "Title", shop.Catalog.MerchantName, 112, 23, 570, 50, 36, ShopUi.Canvas);
            ShopUi.Rounded(ShopUi.Rect(panel, "Wallet badge", 792, 25, 210, 48), ShopUi.Forest, 24, 2, ShopUi.Accent);
            ShopUi.Coin(panel, 806, 36, 26);
            ShopUi.Heading(panel, "Wallet icon", "골드", 840, 32, 48, 34, 18, ShopUi.Canvas);
            gold = ShopUi.Heading(panel, "Gold", "", 889, 31, 96, 36, 25, ShopUi.Canvas);
            gold.alignment = TextAnchor.MiddleRight;
            ShopUi.Button(panel, "Close", "닫기", 1082, 25, 52, 48, Close);
            ShopUi.Heading(panel, "Player Heading", "내 가방", 28, 122, 340, 32, 23);
            ShopUi.Heading(panel, "Shop Heading", "상점 상품", 602, 122, 370, 32, 23);
            capacity = ShopUi.Label(panel, "Capacity", "", 378, 128, 180, 24, 15, ShopUi.Ink);
            capacity.alignment = TextAnchor.MiddleRight;

            string[] groups = { "All", "Materials", "Equipment", "Supplies" };
            string[] groupLabels = { "전체", "재료", "장비", "소모품" };
            filters = new Button[groups.Length];
            for (int index = 0; index < groups.Length; index++)
            {
                int captured = index - 1;
                filters[index] = ShopUi.Button(panel, "Filter " + groups[index], groupLabels[index],
                    602 + index * 134, 166, 128, 30, () => Filter(captured));
            }

            playerSlots = BuildSlots(ShopUi.Grid(panel, "Backpack", 28), PlayerInventory.MaxCapacity, false);
            merchantSlots = BuildSlots(ShopUi.Grid(panel, "Stock", 602, true, shop.Catalog.Count), shop.Catalog.Count, true);
            BuildTradePopup(panel);
            saleNotification = ShopUi.Rect(panel, "Sale notification", 280, 596, 600, 32);
            ShopUi.Rounded(saleNotification, ShopUi.Forest, 16, 1, ShopUi.Accent).raycastTarget = false;
            saleNotificationText = ShopUi.Label(saleNotification, "Message", "", 12, 0, 576, 32, 16, Color.white);
            saleNotificationText.alignment = TextAnchor.MiddleCenter;
            saleNotificationText.resizeTextForBestFit = true;
            saleNotificationText.resizeTextMinSize = 12;
            saleNotificationText.resizeTextMaxSize = 16;
            saleNotification.gameObject.SetActive(false);
        }

        private void BuildTradePopup(RectTransform panel)
        {
            tradePopup = ShopUi.Rect(panel, "Trade Popup", 0, 0, 1160, 630);
            ShopUi.Background(tradePopup, new Color(0.08f, 0.10f, 0.07f, 0.68f));
            RectTransform card = ShopUi.Rect(tradePopup, "Card", 160, 45, 840, 540);
            tradePopup.gameObject.AddComponent<ShopPopupBackdrop>().Configure(card, ClosePopup);
            ShopUi.Shadow(ShopUi.Rounded(card, ShopUi.Canvas, 24, 2, ShopUi.Border), 6, 0.25f);
            tradeHeading = ShopUi.Heading(card, "Trade heading", "", 30, 20, 560, 36, 26, ShopUi.GoldInk);
            ShopUi.Button(card, "Popup Close", "×", 770, 18, 40, 34, ClosePopup);
            ShopUi.Rounded(ShopUi.Rect(card, "Selected icon frame", 30, 76, 140, 140), ShopUi.Slot, 20, 1);
            selectedIcon = ShopUi.Rect(card, "Selected icon", 42, 88, 116, 116).gameObject.AddComponent<Image>();
            selectedIcon.preserveAspect = true;
            selectedIcon.raycastTarget = false;
            itemTitle = ShopUi.Heading(card, "Item title", "", 198, 76, 612, 40, 30);
            description = ShopUi.Label(card, "Description", "", 198, 122, 612, 46, 17, ShopUi.Muted);
            unitPrice = ShopUi.Label(card, "Unit price", "", 198, 173, 612, 28, 18);
            readiness = ShopUi.Label(card, "Readiness", "", 198, 206, 442, 34, 14, ShopUi.Muted);
            ShopUi.Rounded(ShopUi.Rect(card, "Divider", 30, 250, 780, 1), ShopUi.Border, 0, 0).raycastTarget = false;
            ShopUi.Heading(card, "Quantity caption", "수량", 30, 274, 150, 40, 22);
            less = ShopUi.Button(card, "Less", "−", 430, 274, 44, 40, () => ChangeQuantity(-1));
            quantityInput = ShopUi.QuantityInput(card, 484, 274);
            quantityInput.onValueChanged.AddListener(EditQuantity);
            quantityInput.onEndEdit.AddListener(_ => CommitQuantity());
            more = ShopUi.Button(card, "More", "+", 606, 274, 44, 40, () => ChangeQuantity(1));
            maximum = ShopUi.Button(card, "Maximum", "최대", 668, 274, 142, 40, SelectAll);
            quantitySlider = ShopUi.QuantitySlider(card, 198, 322, 612);
            quantitySlider.onValueChanged.AddListener(value =>
            {
                if (syncingQuantity)
                    return;
                quantity = Mathf.RoundToInt(value);
                Refresh();
            });
            totalCaption = ShopUi.Heading(card, "Total caption", "", 30, 360, 320, 36, 24);
            detail = ShopUi.Heading(card, "Selection", "", 400, 360, 410, 36, 28);
            detail.alignment = TextAnchor.MiddleRight;
            ShopUi.Label(card, "Balance caption", "거래 후 잔액", 30, 404, 320, 28, 16, ShopUi.Muted);
            balance = ShopUi.Label(card, "Balance", "", 400, 404, 410, 28, 17);
            balance.alignment = TextAnchor.MiddleRight;
            Button cancel = ShopUi.Button(card, "Cancel", "취소", 30, 454, 250, 54, ClosePopup);
            protection = ShopUi.Button(card, "Protection", "판매 잠금", 652, 210, 158, 28, ToggleProtection);
            buy = ShopUi.Button(card, "Buy", "구매", 296, 454, 514, 54, () => Trade(true));
            buy.targetGraphic.color = ShopUi.Forest;
            sell = ShopUi.Button(card, "Sell", "판매", 296, 454, 514, 54, () => Trade(false));
            sell.targetGraphic.color = ShopUi.Forest;
            status = ShopUi.Label(card, "Status", "", 30, 514, 780, 20, 13, ShopUi.GoldInk);
            popupControls = new Selectable[] { buy, sell, cancel, less, quantityInput, more, maximum, quantitySlider, protection };
            tradePopup.gameObject.SetActive(false);
        }

        private void MovePopupFocus(bool backwards)
        {
            quantityInput.DeactivateInputField();
            EventSystem.current?.SetSelectedGameObject(null);
            if (popupFocus < 0 && backwards)
                popupFocus = 0;
            for (int step = 0; step < popupControls.Length; step++)
            {
                popupFocus = (popupFocus + (backwards ? popupControls.Length - 1 : 1)) % popupControls.Length;
                Selectable control = popupControls[popupFocus];
                if (control.gameObject.activeInHierarchy && control.interactable)
                    break;
            }
            RefreshPopupFocus();
        }

        private void RefreshPopupFocus()
        {
            for (int index = 0; index < popupControls.Length; index++)
            {
                if (popupControls[index].targetGraphic is ShopRoundedRect frame)
                    frame.Style(popupControls[index] == quantitySlider ? 12 : 14,
                        index == popupFocus ? 3 : 1, index == popupFocus ? ShopUi.Accent : ShopUi.Border);
            }
        }

        private SlotView[] BuildSlots(RectTransform parent, int count, bool merchant)
        {
            var result = new SlotView[count];
            for (int index = 0; index < count; index++)
            {
                int captured = index;
                Button button = ShopUi.Button(parent, $"Slot {index}", "", 0, 0, ShopUi.CellSize, ShopUi.CellHeight,
                    () => Select(merchant, captured, Keyboard.current?.leftShiftKey.isPressed == true ||
                        Keyboard.current?.rightShiftKey.isPressed == true, true));
                Destroy(button.transform.Find("Label").gameObject);
                if (merchant)
                    ((RectTransform)button.transform).sizeDelta = new Vector2(164, 196);
                Image icon = ShopUi.Rect(button.transform, "Icon", merchant ? 36 : 26, merchant ? 30 : 8,
                    merchant ? 92 : 44, merchant ? 84 : 44).gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                ShopRoundedRect badge = ShopUi.Rounded(ShopUi.Rect(button.transform, "Count badge",
                    merchant ? 100 : 54, merchant ? 10 : 5, merchant ? 52 : 36, merchant ? 24 : 20),
                    ShopUi.Forest, 12, 0);
                badge.raycastTarget = false;
                if (merchant)
                    ShopUi.Rounded(ShopUi.Rect(button.transform, "Price pill", 12, 155, 140, 30),
                        ShopUi.Slot, 15, 1).raycastTarget = false;
                result[index] = new SlotView
                {
                    Button = button, Icon = icon,
                    Name = ShopUi.Heading(button.transform, "Name", "", 4, merchant ? 120 : 62,
                        merchant ? 156 : 88, merchant ? 28 : 18, merchant ? 19 : 13),
                    Count = ShopUi.Label(button.transform, "Count", "", merchant ? 100 : 54,
                        merchant ? 10 : 5, merchant ? 52 : 36, merchant ? 24 : 20,
                        merchant ? 16 : 12, ShopUi.Canvas),
                    Price = ShopUi.Label(button.transform, "Price", "", 4, merchant ? 155 : 56,
                        merchant ? 156 : 88, merchant ? 30 : 18, merchant ? 17 : 13, ShopUi.GoldInk),
                    Keep = ShopUi.Label(button.transform, "Keep", "", 4, 5, 28, 20, 9, ShopUi.GoldInk),
                    Frame = (ShopRoundedRect)button.targetGraphic,
                    CountBadge = badge
                };
                result[index].Name.alignment = TextAnchor.MiddleCenter;
                result[index].Price.alignment = TextAnchor.MiddleCenter;
                result[index].Count.alignment = TextAnchor.MiddleCenter;
                result[index].Count.resizeTextForBestFit = true;
                result[index].Count.resizeTextMinSize = 7;
                result[index].Count.resizeTextMaxSize = merchant ? 16 : 12;
            }

            return result;
        }

        private void Refresh()
        {
            if (closed || inventory == null || shop == null)
                return;

            int available = AvailableQuantity;
            quantity = Mathf.Clamp(quantity, 0, available);
            if (available > 0 && quantity == 0 && !quantityInput.isFocused)
                quantity = 1;
            if (!quantityInput.isFocused || quantity > 0)
                quantityInput.SetTextWithoutNotify(quantity.ToString(CultureInfo.InvariantCulture));
            quantityInput.interactable = available > 0;
            less.interactable = quantity > 1;
            more.interactable = quantity < available;
            maximum.interactable = available > 0 && quantity != available;
            syncingQuantity = true;
            quantitySlider.maxValue = available;
            quantitySlider.minValue = available > 0 ? 1 : 0;
            quantitySlider.SetValueWithoutNotify(quantity);
            syncingQuantity = false;
            quantitySlider.interactable = available > 1;
            for (int index = 0; index < filters.Length; index++)
            {
                bool active = categoryFilter == index - 1;
                filters[index].interactable = !active;
                filters[index].targetGraphic.color = active ? ShopUi.Forest : ShopUi.Surface;
                filters[index].GetComponentInChildren<Text>().color = active ? ShopUi.Canvas : ShopUi.Ink;
                ColorBlock colors = filters[index].colors;
                colors.disabledColor = Color.white;
                filters[index].colors = colors;
            }
            gold.text = $"{wallet.Gold:N0}";
            capacity.text = $"{inventory.UsedSlots} / {inventory.SlotCount}칸";
            for (int index = 0; index < playerSlots.Length; index++)
            {
                // 가방 장비로 열리지 않은 칸은 표시하지 않습니다.
                playerSlots[index].Button.gameObject.SetActive(index < inventory.SlotCount);
                PlayerInventory.ItemStack stack = inventory.GetSlot(index);
                int offer = shop.FindOffer(stack.Item);
                int price = offer >= 0 ? shop.Catalog.GetOffer(offer).SellPrice : 0;
                string displayName = stack.Item?.EnhancementLevel > 0 ? stack.Item.DisplayName : offer >= 0 ? shop.Catalog.GetOffer(offer).DisplayName : stack.Item?.DisplayName;
                SetSlot(playerSlots[index], stack.Item, displayName, stack.Count, price, false, !merchantSelected && playerIndex == index,
                    wallet.IsSaleProtected(stack.Item));
            }

            for (int index = 0; index < merchantSlots.Length; index++)
            {
                ShopCatalog.Offer offer = shop.Catalog.GetOffer(index);
                merchantSlots[index].Button.gameObject.SetActive(categoryFilter < 0 || (int)offer.Group == categoryFilter);
                SetSlot(merchantSlots[index], offer.Item, offer.DisplayName, shop.StockAt(index), offer.BuyPrice, true,
                    merchantSelected && merchantIndex == index);
            }

            ShopCatalog.Offer selected = shop.Catalog.GetOffer(merchantIndex);
            PlayerInventory.ItemStack owned = inventory.GetSlot(playerIndex);
            int sellOffer = shop.FindOffer(owned.Item);
            int sellPrice = sellOffer >= 0 ? shop.Catalog.GetOffer(sellOffer).SellPrice : 0;
            bool kept = wallet.IsSaleProtected(owned.Item);
            buy.gameObject.SetActive(merchantSelected);
            sell.gameObject.SetActive(!merchantSelected);
            protection.gameObject.SetActive(!merchantSelected && owned.Item == popupItem && owned.Count > 0);
            protection.interactable = !merchantSelected && owned.Item != null;
            protection.GetComponentInChildren<Text>().text = kept ? "잠금 해제" : "판매 잠금";
            long buyTotal = (long)selected.BuyPrice * quantity;
            bool visible = categoryFilter < 0 || (int)selected.Group == categoryFilter;
            bool canBuy = shop.CanBuy(inventory, merchantIndex, quantity, out string buyReason);
            bool canSell = shop.CanSell(inventory, playerIndex, quantity, out string sellReason);
            buy.interactable = merchantSelected && visible && canBuy;
            sell.interactable = !merchantSelected && owned.Item == popupItem && canSell;
            buy.targetGraphic.color = buy.interactable ? ShopUi.Forest : ShopUi.Slot;
            sell.targetGraphic.color = sell.interactable ? ShopUi.Forest : ShopUi.Slot;
            buy.GetComponentInChildren<Text>().color = buy.interactable ? ShopUi.Canvas : ShopUi.Muted;
            sell.GetComponentInChildren<Text>().color = sell.interactable ? ShopUi.Canvas : ShopUi.Muted;
            string ownedName = owned.Item?.EnhancementLevel > 0 ? owned.Item.DisplayName : sellOffer >= 0 ? shop.Catalog.GetOffer(sellOffer).DisplayName : owned.Item?.DisplayName;
            tradeHeading.text = merchantSelected ? "상품 구매" : "아이템 판매";
            itemTitle.text = popupName ?? (merchantSelected ? selected.DisplayName : ownedName);
            int shownOffer = shop.FindOffer(popupItem);
            ShopCatalog.Offer shown = merchantSelected ? selected : shownOffer >= 0 ? shop.Catalog.GetOffer(shownOffer) : null;
            int unitPrice = merchantSelected ? selected.BuyPrice : shown?.SellPrice ?? 0;
            this.unitPrice.text = $"개당 {unitPrice:N0} G";
            totalCaption.text = merchantSelected ? "총 구매 금액" : "총 판매 금액";
            detail.text = $"{(long)unitPrice * quantity:N0} G";
            selectedIcon.sprite = popupItem?.Icon;
            selectedIcon.enabled = selectedIcon.sprite != null;
            description.text = shown == null ? "" : $"{CategoryName(shown.Group)}  /  {shown.Description}";
            if (!merchantSelected && popupItem?.EnhancementLevel > 0)
                description.text += "\n강화 장비는 기본 매입가로 판매되며 강화는 소멸합니다.";
            readiness.text = merchantSelected
                ? !visible ? "이 분류에는 상품이 없습니다."
                : shop.StockAt(merchantIndex) == 0 ? "품절입니다."
                : available == 0 ? "골드 또는 가방 공간이 부족해 구매할 수 없습니다."
                : quantity == 0 ? "구매 수량을 입력하세요."
                : canBuy ? $"구매 가능 {available:N0}개" : buyReason
                : owned.Item == null || owned.Item != popupItem ? "선택한 아이템을 모두 판매했거나 가방에서 이동했습니다."
                : sellPrice == 0 ? "상점에서 매입하지 않는 아이템입니다."
                : canSell ? $"판매 가능 {available:N0}개" : sellReason;
            if (merchantSelected && !visible)
                detail.text = description.text = "";
            balance.text = buy.interactable ? $"{wallet.Gold - buyTotal:N0} G"
                : sell.interactable ? $"{wallet.Gold + (long)sellPrice * quantity:N0} G" : $"{wallet.Gold:N0} G";
            RefreshPopupFocus();
        }

        private static string CategoryName(ShopCatalog.Category category) => category switch
        {
            ShopCatalog.Category.Materials => "재료",
            ShopCatalog.Category.Equipment => "장비",
            ShopCatalog.Category.Supplies => "소모품",
            _ => ""
        };

        private static void SetSlot(SlotView view, ItemDefinition item, string displayName, int count, int price, bool buying, bool selected,
            bool kept = false)
        {
            view.Icon.sprite = item != null ? item.Icon : null;
            view.Icon.enabled = view.Icon.sprite != null;
            view.Name.text = displayName ?? "";
            view.Name.color = item != null ? ShopUi.Ink : ShopUi.Muted;
            view.Count.text = item != null ? $"x{count}" : "";
            view.Keep.text = kept ? "잠금" : "";
            view.CountBadge.gameObject.SetActive(item != null);
            view.Price.gameObject.SetActive(buying);
            view.Price.text = item == null ? "" : buying && count == 0 ? "품절" : $"{price:N0} G";
            view.Icon.color = buying && count == 0 ? new Color(1, 1, 1, 0.35f) : Color.white;
            view.Frame.color = selected ? ShopUi.Selected : item != null ? ShopUi.Surface : new Color(0.94f, 0.91f, 0.84f);
            view.Frame.Style(buying ? 18 : 12, selected ? 3 : 1, selected ? ShopUi.Accent : ShopUi.Border);
        }

        private void ToggleProtection()
        {
            if (closed || merchantSelected || inventory == null || wallet == null)
                return;
            ItemDefinition item = inventory.GetSlot(playerIndex).Item;
            if (wallet.SetSaleProtected(item, !wallet.IsSaleProtected(item)))
                status.text = wallet.IsSaleProtected(item) ? "같은 아이템을 모두 판매 잠금했습니다." : "판매 잠금을 해제했습니다.";
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private bool PopupOpen => tradePopup.gameObject.activeSelf;

        private int AvailableQuantity => merchantSelected
            ? categoryFilter < 0 || (int)shop.Catalog.GetOffer(merchantIndex).Group == categoryFilter ? shop.BuyLimit(inventory, merchantIndex) : 0
            : PopupOpen && inventory.GetSlot(playerIndex).Item != popupItem ? 0 : inventory.GetSlot(playerIndex).Count;

        private void Filter(int category)
        {
            categoryFilter = category;
            int first = 0;
            for (int index = 0; index < shop.Catalog.Count; index++)
                if (category < 0 || (int)shop.Catalog.GetOffer(index).Group == category)
                {
                    first = index;
                    break;
                }
            Select(true, first);
        }

        private void Select(bool merchant, int index, bool all = false, bool showPopup = false, bool reveal = false)
        {
            merchantSelected = merchant;
            if (merchant)
                merchantIndex = index;
            else
                playerIndex = index;

            if (showPopup)
            {
                saleNotification.gameObject.SetActive(false);
                popupItem = merchant ? shop.Catalog.GetOffer(index).Item : inventory.GetSlot(index).Item;
                if (popupItem == null)
                {
                    Refresh();
                    return;
                }
                int offer = shop.FindOffer(popupItem);
                popupName = popupItem.EnhancementLevel > 0 ? popupItem.DisplayName : offer >= 0 ? shop.Catalog.GetOffer(offer).DisplayName : popupItem.DisplayName;
                tradePopup.gameObject.SetActive(true);
                popupFocus = -1;
                foreach (ScrollRect list in GetComponentsInChildren<ScrollRect>())
                    list.StopMovement();
            }
            quantity = all ? AvailableQuantity : Mathf.Min(1, AvailableQuantity);
            status.text = "";

            // Enter는 아래 단일 거래 경로에서 처리합니다. uGUI Submit과 중복 실행하지 않습니다.
            EventSystem.current?.SetSelectedGameObject(null);
            Refresh();
            // 마우스 선택은 목록 위치를 건드리지 않습니다. 키보드로 화면 밖에 이동할 때만 스크롤합니다.
            if (!reveal)
                return;
            RectTransform rect = (RectTransform)(merchant ? merchantSlots[index].Button.transform : playerSlots[index].Button.transform);
            ScrollRect scroll = rect.GetComponentInParent<ScrollRect>();
            Canvas.ForceUpdateCanvases();
            float top = -rect.anchoredPosition.y;
            float offset = scroll.content.anchoredPosition.y;
            float height = scroll.viewport.rect.height;
            if (top < offset || top + rect.rect.height > offset + height)
                scroll.content.anchoredPosition = new Vector2(0, Mathf.Max(0, top < offset ? top : top + rect.rect.height - height));
        }

        private void ClosePopup()
        {
            quantityInput.DeactivateInputField();
            EventSystem.current?.SetSelectedGameObject(null);
            tradePopup.gameObject.SetActive(false);
            popupFocus = -1;
        }

        private void ChangeQuantity(int delta)
        {
            quantity = (int)System.Math.Clamp((long)quantity + delta, AvailableQuantity > 0 ? 1 : 0, AvailableQuantity);
            EventSystem.current?.SetSelectedGameObject(null);
            Refresh();
        }

        private void SelectAll()
        {
            quantity = AvailableQuantity;
            EventSystem.current?.SetSelectedGameObject(null);
            Refresh();
        }

        private void EditQuantity(string value)
        {
            if (closed || shop == null || inventory == null)
                return;

            quantityEditedFrame = Time.frameCount;
            quantity = long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed)
                ? (int)System.Math.Min(parsed, AvailableQuantity) : 0;
            Refresh();
        }

        private void CommitQuantity()
        {
            if (closed || shop == null || inventory == null)
                return;

            quantityEditedFrame = Time.frameCount;
            quantity = Mathf.Clamp(quantity, AvailableQuantity > 0 ? 1 : 0, AvailableQuantity);
            quantityInput.SetTextWithoutNotify(quantity.ToString(CultureInfo.InvariantCulture));
            Refresh();
        }

        private void Trade(bool buying)
        {
            if (closed || !PopupOpen || shop == null || inventory == null || quantity <= 0 ||
                (!buying && inventory.GetSlot(playerIndex).Item != popupItem) ||
                (buying && categoryFilter >= 0 && (int)shop.Catalog.GetOffer(merchantIndex).Group != categoryFilter) ||
                lastTradeFrame == Time.frameCount || merchantSelected != buying)
                return;

            lastTradeFrame = Time.frameCount;
            int soldCount = quantity;
            int previousGold = wallet.Gold;
            string message;
            bool success = buying
                ? shop.TryBuy(inventory, merchantIndex, quantity, out message)
                : shop.TrySell(inventory, playerIndex, quantity, out message);

            status.text = success ? "" : message;
            EventSystem.current?.SetSelectedGameObject(null);
            Refresh();

            if (success)
            {
                // Refresh는 잔량에 맞춰 수량을 바꾸므로 완료 금액은 실제 재화 증분으로 표시합니다.
                string count = soldCount > 1 ? $" {soldCount:N0}개" : "";
                string trade = buying ? "구매" : "판매";
                int change = wallet.Gold - previousGold;
                saleNotificationText.text = $"{popupName}{count} {trade} {(change >= 0 ? "+" : "−")}{Mathf.Abs(change):N0} G";
                saleNotificationUntil = Time.unscaledTime + 3;
                saleNotification.gameObject.SetActive(true);
                ClosePopup();
            }
        }

        private void Update()
        {
            if (closed)
                return;
            if (saleNotification != null && saleNotification.gameObject.activeSelf && Time.unscaledTime >= saleNotificationUntil)
                saleNotification.gameObject.SetActive(false);
            if (tradePopup == null || saleNotification == null || shop == null || inventory == null || !shop.CanAccess(inventory))
            {
                Close();
                return;
            }

            if (Mouse.current?.rightButton.wasPressedThisFrame == true && PopupOpen)
            {
                ClosePopup();
                return;
            }

            Keyboard keys = Keyboard.current;
            if (keys == null || Time.frameCount == openedFrame)
                return;

            if (keys.escapeKey.wasPressedThisFrame || keys.eKey.wasPressedThisFrame)
            {
                if (PopupOpen)
                    ClosePopup();
                else
                    Close();
                return;
            }

            // 팝업이 열린 동안에는 뒤쪽 목록의 선택·스크롤을 바꾸지 않습니다.
            if (PopupOpen)
            {
                if (keys.tabKey.wasPressedThisFrame)
                {
                    CommitQuantity();
                    MovePopupFocus(keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed);
                    return;
                }
                if (quantityInput.isFocused || quantityEditedFrame == Time.frameCount)
                {
                    if (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame || keys.tabKey.wasPressedThisFrame)
                    {
                        quantityInput.DeactivateInputField();
                        EventSystem.current?.SetSelectedGameObject(null);
                        CommitQuantity();
                    }
                    return;
                }

                if (keys.qKey.wasPressedThisFrame && quantityInput.interactable)
                {
                    quantityInput.Select();
                    quantityInput.ActivateInputField();
                    return;
                }
                if (keys.equalsKey.wasPressedThisFrame || keys.numpadPlusKey.wasPressedThisFrame)
                    ChangeQuantity(1);
                if (keys.minusKey.wasPressedThisFrame || keys.numpadMinusKey.wasPressedThisFrame)
                    ChangeQuantity(-1);
                if (popupFocus >= 0 && popupControls[popupFocus] == quantitySlider)
                {
                    if (keys.leftArrowKey.wasPressedThisFrame) ChangeQuantity(-1);
                    if (keys.rightArrowKey.wasPressedThisFrame) ChangeQuantity(1);
                }
                if (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame)
                {
                    if (popupFocus < 0)
                        Trade(merchantSelected);
                    else if (popupControls[popupFocus] is Button button && button.interactable)
                        button.onClick.Invoke();
                    else if (popupControls[popupFocus] == quantityInput && quantityInput.interactable)
                    {
                        quantityInput.Select();
                        quantityInput.ActivateInputField();
                    }
                }
                if (keys.bKey.wasPressedThisFrame)
                    Trade(true);
                if (keys.vKey.wasPressedThisFrame)
                    Trade(false);
                if (keys.lKey.wasPressedThisFrame)
                    ToggleProtection();
                return;
            }

            if (keys.tabKey.wasPressedThisFrame)
                Select(!merchantSelected, merchantSelected ? playerIndex : merchantIndex);
            int columns = merchantSelected ? 3 : 5;
            int delta = (keys.rightArrowKey.wasPressedThisFrame ? 1 : 0) - (keys.leftArrowKey.wasPressedThisFrame ? 1 : 0)
                + (keys.downArrowKey.wasPressedThisFrame ? columns : 0) - (keys.upArrowKey.wasPressedThisFrame ? columns : 0);
            if (delta != 0)
            {
                if (merchantSelected)
                {
                    var visible = new System.Collections.Generic.List<int>();
                    for (int index = 0; index < merchantSlots.Length; index++)
                        if (merchantSlots[index].Button.gameObject.activeSelf)
                            visible.Add(index);
                    if (visible.Count > 0)
                        Select(true, visible[Mathf.Clamp(visible.IndexOf(merchantIndex) + delta, 0, visible.Count - 1)], reveal: true);
                }
                else
                    Select(false, Mathf.Clamp(playerIndex + delta, 0, inventory.SlotCount - 1), reveal: true);
            }
            if (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame ||
                (merchantSelected ? keys.bKey.wasPressedThisFrame : keys.vKey.wasPressedThisFrame))
                Select(merchantSelected, merchantSelected ? merchantIndex : playerIndex,
                    keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed, true);
            if (keys.lKey.wasPressedThisFrame)
                ToggleProtection();
        }

        public void Close()
        {
            if (closed)
                return;

            Release();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnDisable() => Release();

        private void Release()
        {
            if (closed)
                return;

            closed = true;
            if (inventory != null)
            {
                inventory.Changed -= Refresh;
                inventory.SetTrading(false);
            }
            if (wallet != null)
                wallet.Changed -= Refresh;
            if (shop != null)
                shop.Changed -= Refresh;
        }
    }
}
