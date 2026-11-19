using System.Globalization;
using Nomad.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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
            internal ShopRoundedRect Frame;
            internal ShopRoundedRect CountBadge;
        }

        private Shopkeeper shop;
        private PlayerInventory inventory;
        private PlayerWallet wallet;
        private SlotView[] playerSlots;
        private SlotView[] merchantSlots;
        private Text gold;
        private Text detail;
        private Text description;
        private Text readiness;
        private Text status;
        private Text capacity;
        private InputField quantityInput;
        private Button less;
        private Button more;
        private Button maximum;
        private Button affordable;
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
            ShopUi.Background(shade, new Color(0.06f, 0.12f, 0.09f, 0.76f));

            RectTransform panel = ShopUi.Rect(transform, "Shop Window", 0, 0, 1160, 800);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * 0.5f;
            ShopUi.Shadow(ShopUi.Rounded(panel, ShopUi.Cream, 26, 3, ShopUi.Forest), 8, 0.3f);
            ShopUi.Rounded(ShopUi.Rect(panel, "Header", 12, 12, 1136, 100), ShopUi.Forest, 20, 1, ShopUi.Border);
            ShopUi.Purse(panel, 32, 32);
            ShopUi.Heading(panel, "Title", shop.Catalog.MerchantName, 106, 24, 570, 44, 36, ShopUi.Cream);
            ShopUi.Label(panel, "Hint", "Good supplies. Better adventures.", 108, 72, 570, 22, 16, ShopUi.Cream);
            ShopUi.Rounded(ShopUi.Rect(panel, "Wallet badge", 812, 32, 190, 52), ShopUi.Cream, 18, 2, ShopUi.Gold);
            ShopUi.Heading(panel, "Wallet icon", "G", 826, 42, 30, 30, 24, ShopUi.GoldInk);
            gold = ShopUi.Label(panel, "Gold", "", 862, 40, 126, 36, 23, ShopUi.Ink);
            gold.alignment = TextAnchor.MiddleRight;
            ShopUi.Button(panel, "Close", "Close", 1030, 35, 90, 46, Close);
            ShopUi.Heading(panel, "Player Heading", "YOUR SATCHEL", 28, 122, 340, 32, 23);
            ShopUi.Heading(panel, "Shop Heading", "MERCHANT GOODS", 602, 122, 370, 32, 23);
            capacity = ShopUi.Label(panel, "Capacity", "", 378, 128, 180, 24, 14, ShopUi.Muted);
            capacity.alignment = TextAnchor.MiddleRight;
            Text stockHint = ShopUi.Label(panel, "Stock hint", "Price per item", 958, 128, 174, 24, 14, ShopUi.Muted);
            stockHint.alignment = TextAnchor.MiddleRight;

            ShopUi.Label(panel, "Backpack hint", "Select an item to see its selling price.", 28, 166, 530, 30, 15, ShopUi.Muted);
            string[] groups = { "All", "Materials", "Equipment", "Supplies" };
            filters = new Button[groups.Length];
            for (int index = 0; index < groups.Length; index++)
            {
                int captured = index - 1;
                filters[index] = ShopUi.Button(panel, "Filter " + groups[index], groups[index],
                    602 + index * 134, 166, 128, 30, () => Filter(captured));
            }

            playerSlots = BuildSlots(ShopUi.Grid(panel, "Backpack", 28), PlayerInventory.Capacity, false);
            merchantSlots = BuildSlots(ShopUi.Grid(panel, "Stock", 602), shop.Catalog.Count, true);
            ShopUi.Rounded(ShopUi.Rect(panel, "Trade tray", 20, 570, 1120, 188), new Color(0.92f, 0.90f, 0.82f), 18, 1, ShopUi.Border);
            detail = ShopUi.Label(panel, "Selection", "", 48, 582, 1064, 32, 19, ShopUi.Ink);
            description = ShopUi.Label(panel, "Description", "", 48, 616, 1064, 24, 15, ShopUi.Muted);
            readiness = ShopUi.Label(panel, "Readiness", "", 48, 642, 1064, 22, 14, ShopUi.GoldInk);
            ShopUi.Label(panel, "Quantity Label", "Quantity", 48, 666, 80, 40, 16, ShopUi.Muted);
            less = ShopUi.Button(panel, "Less", "-", 145, 666, 40, 40, () => ChangeQuantity(-1));
            quantityInput = ShopUi.QuantityInput(panel, 193, 666);
            quantityInput.onValueChanged.AddListener(EditQuantity);
            quantityInput.onEndEdit.AddListener(_ => CommitQuantity());
            more = ShopUi.Button(panel, "More", "+", 313, 666, 40, 40, () => ChangeQuantity(1));
            maximum = ShopUi.Button(panel, "Maximum", "Max", 365, 666, 80, 40, SelectAll);
            affordable = ShopUi.Button(panel, "Affordable", "Buy limit", 457, 666, 172, 40, () =>
            {
                quantity = shop.BuyLimit(inventory, merchantIndex);
                EventSystem.current?.SetSelectedGameObject(null);
                Refresh();
            });
            buy = ShopUi.Button(panel, "Buy", "Buy [B]", 654, 664, 210, 48, () => Trade(true));
            buy.targetGraphic.color = ShopUi.Forest;
            sell = ShopUi.Button(panel, "Sell", "Sell [V]", 886, 664, 226, 48, () => Trade(false));
            sell.targetGraphic.color = ShopUi.Gold;
            status = ShopUi.Label(panel, "Status", "", 48, 723, 1064, 24, 14, ShopUi.GoldInk);
            ShopUi.Label(panel, "Keys", "Arrows: select   |   Shift+click: all   |   Tab: switch   |   +/-: quantity   |   Enter: trade   |   E / Esc: close",
                28, 772, 1104, 20, 13, ShopUi.Muted);
        }

        private SlotView[] BuildSlots(RectTransform parent, int count, bool merchant)
        {
            var result = new SlotView[count];
            for (int index = 0; index < count; index++)
            {
                int captured = index;
                Button button = ShopUi.Button(parent, $"Slot {index}", "", 0, 0, ShopUi.CellSize, ShopUi.CellHeight,
                    () => Select(merchant, captured, Keyboard.current?.leftShiftKey.isPressed == true ||
                        Keyboard.current?.rightShiftKey.isPressed == true));
                Destroy(button.transform.Find("Label").gameObject);
                Image icon = ShopUi.Rect(button.transform, "Icon", 30, 4, 36, 32).gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                ShopRoundedRect badge = ShopUi.Rounded(ShopUi.Rect(button.transform, "Count badge", 54, 5, 36, 20),
                    ShopUi.Forest, 7, 0);
                badge.raycastTarget = false;
                result[index] = new SlotView
                {
                    Button = button, Icon = icon,
                    Name = ShopUi.Label(button.transform, "Name", "", 4, 38, 88, 18, 13),
                    Count = ShopUi.Label(button.transform, "Count", "", 54, 5, 36, 20, 11, ShopUi.Cream),
                    Price = ShopUi.Label(button.transform, "Price", "", 4, 56, 88, 18, 13, ShopUi.GoldInk),
                    Frame = (ShopRoundedRect)button.targetGraphic,
                    CountBadge = badge
                };
                result[index].Name.alignment = TextAnchor.MiddleCenter;
                result[index].Price.alignment = TextAnchor.MiddleCenter;
                result[index].Count.alignment = TextAnchor.MiddleCenter;
                result[index].Count.resizeTextForBestFit = true;
                result[index].Count.resizeTextMinSize = 7;
                result[index].Count.resizeTextMaxSize = 11;
            }

            return result;
        }

        private void Refresh()
        {
            if (closed || inventory == null || shop == null)
                return;

            int available = AvailableQuantity;
            quantity = Mathf.Clamp(quantity, 0, available);
            if (!quantityInput.isFocused || quantity > 0)
                quantityInput.SetTextWithoutNotify(quantity.ToString(CultureInfo.InvariantCulture));
            quantityInput.interactable = available > 0;
            less.interactable = quantity > 1;
            more.interactable = quantity < available;
            maximum.interactable = available > 0 && quantity != available;
            affordable.interactable = merchantSelected && shop.BuyLimit(inventory, merchantIndex) > 0;
            for (int index = 0; index < filters.Length; index++)
                filters[index].interactable = categoryFilter != index - 1;
            gold.text = $"{wallet.Gold:N0} G";
            capacity.text = $"{inventory.UsedSlots} / {PlayerInventory.Capacity} slots";
            for (int index = 0; index < playerSlots.Length; index++)
            {
                PlayerInventory.ItemStack stack = inventory.GetSlot(index);
                int offer = shop.FindOffer(stack.Item);
                int price = offer >= 0 ? shop.Catalog.GetOffer(offer).SellPrice : 0;
                SetSlot(playerSlots[index], stack.Item, stack.Count, price, false, !merchantSelected && playerIndex == index);
            }

            for (int index = 0; index < merchantSlots.Length; index++)
            {
                ShopCatalog.Offer offer = shop.Catalog.GetOffer(index);
                merchantSlots[index].Button.gameObject.SetActive(categoryFilter < 0 || (int)offer.Group == categoryFilter);
                SetSlot(merchantSlots[index], offer.Item, shop.StockAt(index), offer.BuyPrice, true,
                    merchantSelected && merchantIndex == index);
            }

            ShopCatalog.Offer selected = shop.Catalog.GetOffer(merchantIndex);
            PlayerInventory.ItemStack owned = inventory.GetSlot(playerIndex);
            int sellOffer = shop.FindOffer(owned.Item);
            int sellPrice = sellOffer >= 0 ? shop.Catalog.GetOffer(sellOffer).SellPrice : 0;
            long buyTotal = (long)selected.BuyPrice * quantity;
            bool visible = categoryFilter < 0 || (int)selected.Group == categoryFilter;
            bool canBuy = shop.CanBuy(inventory, merchantIndex, quantity, out string buyReason);
            bool canSell = shop.CanSell(inventory, playerIndex, quantity, out string sellReason);
            buy.interactable = merchantSelected && visible && canBuy;
            sell.interactable = !merchantSelected && canSell;
            buy.GetComponentInChildren<Text>().color = buy.interactable ? ShopUi.Cream : ShopUi.Muted;
            sell.GetComponentInChildren<Text>().color = sell.interactable ? ShopUi.Ink : ShopUi.Muted;
            detail.text = merchantSelected
                ? $"BUY  {selected.Item.DisplayName}  /  {selected.BuyPrice:N0} G each  /  Total {buyTotal:N0} G"
                : owned.Item == null ? "Select an item from your backpack."
                : sellPrice == 0 ? $"{owned.Item.DisplayName}  /  This shop does not buy this item."
                : $"SELL  {owned.Item.DisplayName}  /  {sellPrice:N0} G each  /  Total {(long)sellPrice * quantity:N0} G";
            ShopCatalog.Offer shown = merchantSelected ? selected : sellOffer >= 0 ? shop.Catalog.GetOffer(sellOffer) : null;
            description.text = shown == null ? "" : $"{shown.Group}  /  {shown.Description}";
            readiness.text = merchantSelected
                ? !visible ? "No goods in this category."
                : shop.StockAt(merchantIndex) == 0 ? "Sold out."
                : quantity == 0 ? "Enter a quantity to buy."
                : $"{buyReason}  Buy limit: {shop.BuyLimit(inventory, merchantIndex):N0} (gold / space / stock)"
                : owned.Item == null ? "Select an item to sell." : sellPrice == 0 ? "This merchant does not buy this item." : sellReason;
            if (merchantSelected && !visible)
                detail.text = description.text = "";
        }

        private static void SetSlot(SlotView view, ItemDefinition item, int count, int price, bool buying, bool selected)
        {
            view.Icon.sprite = item != null ? item.Icon : null;
            view.Icon.enabled = view.Icon.sprite != null;
            view.Name.text = item != null ? item.DisplayName : "";
            view.Name.color = item != null ? ShopUi.Ink : ShopUi.Muted;
            view.Count.text = item != null ? $"x{count}" : "";
            view.CountBadge.gameObject.SetActive(item != null);
            view.Price.text = item == null ? "" : buying && count == 0 ? "Sold out" : price > 0 ? $"{(buying ? "Buy" : "Sell")} {price:N0} G" : "Not bought";
            view.Icon.color = buying && count == 0 ? new Color(1, 1, 1, 0.35f) : Color.white;
            view.Frame.color = selected ? new Color(0.91f, 0.94f, 0.82f) : item != null ? ShopUi.Surface : new Color(0.92f, 0.90f, 0.83f);
            view.Frame.Style(12, selected ? 2.5f : 1.25f, selected ? ShopUi.Gold : ShopUi.Border);
        }

        private int AvailableQuantity => merchantSelected
            ? categoryFilter < 0 || (int)shop.Catalog.GetOffer(merchantIndex).Group == categoryFilter ? shop.StockAt(merchantIndex) : 0
            : inventory.GetSlot(playerIndex).Count;

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

        private void Select(bool merchant, int index, bool all = false)
        {
            merchantSelected = merchant;
            if (merchant)
                merchantIndex = index;
            else
                playerIndex = index;

            quantity = all ? AvailableQuantity : Mathf.Min(1, AvailableQuantity);
            status.text = "";

            // Enter는 아래 단일 거래 경로에서 처리합니다. uGUI Submit과 중복 실행하지 않습니다.
            EventSystem.current?.SetSelectedGameObject(null);
            Refresh();
            RectTransform rect = (RectTransform)(merchant ? merchantSlots[index].Button.transform : playerSlots[index].Button.transform);
            ScrollRect scroll = rect.GetComponentInParent<ScrollRect>();
            Canvas.ForceUpdateCanvases();
            float top = -rect.anchoredPosition.y;
            float offset = scroll.content.anchoredPosition.y;
            float height = scroll.viewport.rect.height;
            if (top < offset || top + rect.rect.height > offset + height)
                scroll.content.anchoredPosition = new Vector2(0, Mathf.Max(0, top < offset ? top : top + rect.rect.height - height));
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
            if (closed || shop == null || inventory == null || quantity <= 0 ||
                (buying && categoryFilter >= 0 && (int)shop.Catalog.GetOffer(merchantIndex).Group != categoryFilter) ||
                lastTradeFrame == Time.frameCount || merchantSelected != buying)
                return;

            lastTradeFrame = Time.frameCount;
            string message;
            if (buying)
                shop.TryBuy(inventory, merchantIndex, quantity, out message);
            else
                shop.TrySell(inventory, playerIndex, quantity, out message);

            status.text = message;
            EventSystem.current?.SetSelectedGameObject(null);
            Refresh();
        }

        private void Update()
        {
            if (closed)
                return;
            if (shop == null || inventory == null || !shop.CanAccess(inventory))
            {
                Close();
                return;
            }

            Keyboard keys = Keyboard.current;
            if (keys == null || Time.frameCount == openedFrame)
                return;

            // 입력 확정 Enter가 같은 프레임에 구매/판매까지 실행되지 않게 합니다.
            if (quantityInput.isFocused || quantityEditedFrame == Time.frameCount)
            {
                if (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame ||
                    keys.escapeKey.wasPressedThisFrame || keys.tabKey.wasPressedThisFrame)
                {
                    quantityInput.DeactivateInputField();
                    EventSystem.current?.SetSelectedGameObject(null);
                    CommitQuantity();
                }
                return;
            }
            if (keys.escapeKey.wasPressedThisFrame || keys.eKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (keys.tabKey.wasPressedThisFrame)
                Select(!merchantSelected, merchantSelected ? playerIndex : merchantIndex);
            int delta = (keys.rightArrowKey.wasPressedThisFrame ? 1 : 0) - (keys.leftArrowKey.wasPressedThisFrame ? 1 : 0)
                + (keys.downArrowKey.wasPressedThisFrame ? 5 : 0) - (keys.upArrowKey.wasPressedThisFrame ? 5 : 0);
            if (delta != 0)
            {
                if (merchantSelected)
                {
                    var visible = new System.Collections.Generic.List<int>();
                    for (int index = 0; index < merchantSlots.Length; index++)
                        if (merchantSlots[index].Button.gameObject.activeSelf)
                            visible.Add(index);
                    if (visible.Count > 0)
                        Select(true, visible[Mathf.Clamp(visible.IndexOf(merchantIndex) + delta, 0, visible.Count - 1)]);
                }
                else
                    Select(false, Mathf.Clamp(playerIndex + delta, 0, playerSlots.Length - 1));
            }
            if (keys.equalsKey.wasPressedThisFrame || keys.numpadPlusKey.wasPressedThisFrame)
                ChangeQuantity(1);
            if (keys.minusKey.wasPressedThisFrame || keys.numpadMinusKey.wasPressedThisFrame)
                ChangeQuantity(-1);
            if (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame)
                Trade(merchantSelected);
            if (keys.bKey.wasPressedThisFrame)
                Trade(true);
            if (keys.vKey.wasPressedThisFrame)
                Trade(false);
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
