using Nomad.Inventory;
using UnityEngine;

namespace Nomad.Shop
{
    public sealed class ShopDemo : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;

        private void Start()
        {
            inventory.Add("scrap", 12);
            inventory.Add("field-knife", 1);
            inventory.Add("field-vest", 1);
            InventoryPanel.GetOrCreate(inventory);
        }

        private void OnGUI()
        {
            if (inventory == null || inventory.HasOpenTrade)
                return;

            GUI.Box(new Rect(16, 16, 450, 82), "SHOP DEBUG");
            GUI.Label(new Rect(30, 42, 420, 24), "WASD: move   E: trade near SHOP   Tab: backpack");
            PlayerWallet wallet = inventory.GetComponent<PlayerWallet>();
            GUI.Label(new Rect(30, 66, 420, 24), $"Gold: {wallet.Gold:N0} G  |  Fixed test prices / no save");
        }
    }
}
