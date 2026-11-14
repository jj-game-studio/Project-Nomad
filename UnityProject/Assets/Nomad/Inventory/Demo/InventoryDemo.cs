using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Inventory
{
    public sealed class InventoryDemo : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private UnityEngine.UI.Text status;

        private void Start()
        {
            inventory.Add("scrap", 96);
            inventory.Add("field-knife", 1);
            inventory.Add("field-vest", 1);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame)
                status.text = $"Scrap added   {inventory.Add("scrap", 10)} / 10";
            if (keyboard.digit2Key.wasPressedThisFrame)
                status.text = $"Scrap removed   {inventory.Remove("scrap", 10)} / 10";
            if (keyboard.digit3Key.wasPressedThisFrame)
                status.text = $"Knives added   {inventory.Add("field-knife", 20)} / 20";
        }
    }
}
