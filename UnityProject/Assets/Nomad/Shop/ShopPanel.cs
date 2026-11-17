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
            internal Outline Border;
        }

        private Shopkeeper shop;
        private PlayerInventory inventory;
        private PlayerWallet wallet;
        private SlotView[] playerSlots;
        private SlotView[] merchantSlots;
        private Text gold;
        private Text detail;
        private Text status;
        private InputField quantityInput;
        private Button less;
        private Button more;
        private Button maximum;
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
            ShopUi.Background(shade, new Color(0.02f, 0.035f, 0.04f, 0.86f));

            RectTransform panel = ShopUi.Rect(transform, "Shop Window", 0, 0, 1160, 760);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * 0.5f;
            ShopUi.Background(panel, new Color(ShopUi.Slot.r, ShopUi.Slot.g, ShopUi.Slot.b));
            ShopUi.Label(panel, "Title", "Temporary Shop", 28, 16, 520, 42, 29);
            gold = ShopUi.Label(panel, "Gold", "", 660, 18, 330, 42, 24, ShopUi.Gold);
            ShopUi.Button(panel, "Close", "Close [Esc]", 1010, 22, 122, 38, Close);
            ShopUi.Label(panel, "Hint", "Shift + click: select full stack. Enter a quantity below, then buy or sell.", 28, 68, 1104, 28, 18, ShopUi.Muted);
            ShopUi.Label(panel, "Player Heading", "Backpack  /  Sell", 28, 120, 530, 30, 22);
            ShopUi.Label(panel, "Shop Heading", "Shop stock  /  Buy", 602, 120, 530, 30, 22);

            playerSlots = BuildSlots(ShopUi.Grid(panel, "Backpack", 28), PlayerInventory.Capacity, false);
            merchantSlots = BuildSlots(ShopUi.Grid(panel, "Stock", 602), shop.Catalog.Count, true);
            detail = ShopUi.Label(panel, "Selection", "", 28, 610, 1104, 30, 20, ShopUi.Gold);
            ShopUi.Label(panel, "Quantity Label", "Quantity", 28, 650, 80, 40, 16, ShopUi.Muted);
            less = ShopUi.Button(panel, "Less", "-", 115, 650, 38, 40, () => ChangeQuantity(-1));
            quantityInput = ShopUi.QuantityInput(panel, 161, 650);
            quantityInput.onValueChanged.AddListener(EditQuantity);
            quantityInput.onEndEdit.AddListener(_ => CommitQuantity());
            more = ShopUi.Button(panel, "More", "+", 281, 650, 38, 40, () => ChangeQuantity(1));
            maximum = ShopUi.Button(panel, "Maximum", "Max", 331, 650, 80, 40, SelectAll);
            buy = ShopUi.Button(panel, "Buy", "Buy [B]", 650, 650, 230, 40, () => Trade(true));
            sell = ShopUi.Button(panel, "Sell", "Sell [V]", 902, 650, 230, 40, () => Trade(false));
            status = ShopUi.Label(panel, "Status", "", 28, 697, 1104, 24, 16, ShopUi.Gold);
            ShopUi.Label(panel, "Keys", "Arrows: select   Tab: switch list   +/-: quantity   Enter: trade / confirm input   E / Esc: close",
                28, 729, 1104, 20, 15, ShopUi.Muted);
        }

        private SlotView[] BuildSlots(RectTransform parent, int count, bool merchant)
        {
            var result = new SlotView[count];
            for (int index = 0; index < count; index++)
            {
                int captured = index;
                Button button = ShopUi.Button(parent, $"Slot {index}", "", 0, 0, 98, 98,
                    () => Select(merchant, captured, Keyboard.current?.leftShiftKey.isPressed == true ||
                        Keyboard.current?.rightShiftKey.isPressed == true));
                button.GetComponent<Image>().color = ShopUi.Surface;
                Destroy(button.transform.Find("Label").gameObject);
                Image icon = ShopUi.Rect(button.transform, "Icon", 29, 5, 40, 40).gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                result[index] = new SlotView
                {
                    Button = button, Icon = icon,
                    Name = ShopUi.Label(button.transform, "Name", "", 4, 45, 90, 18, 14),
                    Count = ShopUi.Label(button.transform, "Count", "", 4, 63, 90, 17, 13, ShopUi.Muted),
                    Price = ShopUi.Label(button.transform, "Price", "", 4, 80, 90, 18, 14, ShopUi.Gold),
                    Border = button.gameObject.AddComponent<Outline>()
                };
                result[index].Border.effectDistance = new Vector2(2, -2);
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
            gold.text = $"YOUR GOLD   {wallet.Gold:N0} G";
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
                SetSlot(merchantSlots[index], offer.Item, shop.StockAt(index), offer.BuyPrice, true,
                    merchantSelected && merchantIndex == index);
            }

            ShopCatalog.Offer selected = shop.Catalog.GetOffer(merchantIndex);
            PlayerInventory.ItemStack owned = inventory.GetSlot(playerIndex);
            int sellOffer = shop.FindOffer(owned.Item);
            int sellPrice = sellOffer >= 0 ? shop.Catalog.GetOffer(sellOffer).SellPrice : 0;
            long buyTotal = (long)selected.BuyPrice * quantity;
            buy.interactable = merchantSelected && quantity > 0 && shop.StockAt(merchantIndex) >= quantity && wallet.Gold >= buyTotal;
            sell.interactable = !merchantSelected && quantity > 0 && owned.Item != null && owned.Count >= quantity && sellPrice > 0;
            buy.GetComponentInChildren<Text>().color = buy.interactable ? ShopUi.Ink : ShopUi.Muted * 0.6f;
            sell.GetComponentInChildren<Text>().color = sell.interactable ? ShopUi.Ink : ShopUi.Muted * 0.6f;
            detail.text = merchantSelected
                ? $"BUY  {selected.Item.DisplayName}  /  {selected.BuyPrice:N0} G each  /  Total {buyTotal:N0} G"
                : owned.Item == null ? "Select an item from your backpack."
                : sellPrice == 0 ? $"{owned.Item.DisplayName}  /  This shop does not buy this item."
                : $"SELL  {owned.Item.DisplayName}  /  {sellPrice:N0} G each  /  Total {(long)sellPrice * quantity:N0} G";
        }

        private static void SetSlot(SlotView view, ItemDefinition item, int count, int price, bool buying, bool selected)
        {
            view.Icon.sprite = item != null ? item.Icon : null;
            view.Icon.enabled = view.Icon.sprite != null;
            view.Name.text = item != null ? item.DisplayName : "Empty";
            view.Name.color = item != null ? ShopUi.Ink : ShopUi.Muted;
            view.Count.text = item != null ? $"x{count}" : "";
            view.Price.text = item == null ? "" : price > 0 ? $"{(buying ? "Buy" : "Sell")} {price:N0} G" : "Not bought";
            view.Border.effectColor = selected ? ShopUi.Gold : new Color(0.2f, 0.29f, 0.3f, 0.7f);
        }

        private int AvailableQuantity => merchantSelected ? shop.StockAt(merchantIndex) : inventory.GetSlot(playerIndex).Count;

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
            if (top < offset || top + 98 > offset + height)
                scroll.content.anchoredPosition = new Vector2(0, Mathf.Max(0, top < offset ? top : top + 98 - height));
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
                Select(merchantSelected, Mathf.Clamp((merchantSelected ? merchantIndex : playerIndex) + delta,
                    0, (merchantSelected ? merchantSlots.Length : playerSlots.Length) - 1));
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
