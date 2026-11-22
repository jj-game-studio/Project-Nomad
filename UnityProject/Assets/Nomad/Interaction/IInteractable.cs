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

    // 상호작용 UI의 E 옆에 보여줄 행동 이름입니다. 구현하지 않은 대상은 "상호작용"으로 표시합니다.
    public interface IInteractionAction
    {
        string InteractionAction { get; }
    }
}
