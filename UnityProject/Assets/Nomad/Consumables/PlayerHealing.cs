using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Consumables
{
    [RequireComponent(typeof(Health), typeof(PlayerInventory))]
    public sealed class PlayerHealing : MonoBehaviour
    {
        public const string PotionId = "health-potion";
        [SerializeField, Min(1)] private int healAmount = 40;
        private Health health;
        private PlayerInventory inventory;
        private PlayerEquipmentCombat combat;
        private float nextUse;
        public int Count => inventory != null ? inventory.GetCount(PotionId) : 0;
        public int HealAmount => healAmount;

        private void Awake()
        {
            health = GetComponent<Health>();
            inventory = GetComponent<PlayerInventory>();
            combat = GetComponent<PlayerEquipmentCombat>();
        }

        private void Update()
        {
            if (Keyboard.current?.qKey.wasPressedThisFrame == true) TryUse();
        }

        public bool TryUse()
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0 || Time.time < nextUse ||
                inventory.BlocksTradeInput || inventory.Panel?.HasOpenChest == true ||
                inventory.Panel?.BlocksSplitInput == true || combat?.EquipmentUiOpen == true || combat?.InputBlocked == true || Count <= 0)
                return false;
            if (!health.Heal(healAmount)) return false;
            inventory.Remove(PotionId, 1);
            nextUse = Time.time + 0.6f;
            return true;
        }
    }
}
