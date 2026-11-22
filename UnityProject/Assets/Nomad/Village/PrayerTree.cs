using Nomad.Combat;
using Nomad.Interaction;
using Nomad.Player;
using UnityEngine;

namespace Nomad.Village
{
    // 마을 광장의 큰 나무입니다. E로 기도하면 체력과 마나를 최대치로 채웁니다.
    [DisallowMultipleComponent]
    public sealed class PrayerTree : MonoBehaviour, IInteractable, IInteractionAction
    {
        [SerializeField] private string interactionId = "village-prayer-tree";
        [SerializeField, Min(0)] private float trunkRadius = 1f;
        [SerializeField, Min(0)] private float pointHeight = 1f;

        private Transform player;

        public string InteractionId => interactionId;
        public string InteractionAction => "기도";

        // 줄기가 굵어 중심은 2m 판정 밖이므로, 플레이어 쪽 줄기 표면을 상호작용 지점으로 씁니다.
        public Vector3 InteractionPoint
        {
            get
            {
                Vector3 center = transform.position;
                Vector3 toward = player != null ? player.position - center : -transform.forward;
                toward.y = 0;
                if (toward.sqrMagnitude < 0.0001f)
                    toward = -Vector3.forward;

                return center + toward.normalized * trunkRadius + Vector3.up * pointHeight;
            }
        }

        private void Update()
        {
            // 판정 전에도 플레이어 쪽 지점을 쓰도록 마을에 들어온 플레이어를 찾아 둡니다.
            if (player != null)
                return;

            PlayerInteraction found = FindAnyObjectByType<PlayerInteraction>();
            if (found != null)
                player = found.transform;
        }

        public bool CanInteract(InteractionContext context)
        {
            player = context.Player;
            return context.Player != null && context.Player.GetComponent<Health>() is { IsDead: false };
        }

        public void Interact(InteractionContext context)
        {
            if (!CanInteract(context))
                return;

            context.Player.GetComponent<Health>().RestoreFull();
            if (context.Player.TryGetComponent(out PlayerMana mana))
                mana.RestoreFull();
        }
    }
}
