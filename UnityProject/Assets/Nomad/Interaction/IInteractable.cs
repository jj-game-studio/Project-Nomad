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

    // 기본 2m보다 넓은 E 판정 거리가 필요한 큰 대상(예: 마을 기도 나무)이 구현합니다.
    public interface IInteractionRange
    {
        float InteractionRange { get; }
    }

    public static class InteractionRange
    {
        public const float Default = 2f;

        public static float Of(IInteractable target) =>
            target is IInteractionRange ranged ? Mathf.Max(Default, ranged.InteractionRange) : Default;
    }
}
