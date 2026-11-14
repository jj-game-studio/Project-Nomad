using Nomad.Inventory;
using UnityEngine;

namespace Nomad.Interaction
{
    public readonly struct InteractionContext
    {
        public Transform Player { get; }
        public PlayerInventory Inventory { get; }

        public InteractionContext(Transform player, PlayerInventory inventory)
        {
            Player = player;
            Inventory = inventory;
        }
    }

    public interface IInteractable
    {
        string InteractionId { get; }
        Vector3 InteractionPoint { get; }

        bool CanInteract(InteractionContext context);
        void Interact(InteractionContext context);
    }
}
