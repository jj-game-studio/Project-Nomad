using Nomad.Combat;
using Nomad.Interaction;
using Nomad.Player;
using UnityEngine;

namespace Nomad.Village
{
    // 마을 광장의 큰 나무입니다. E로 기도하면 체력과 마나를 최대치로 채우고 기도 VFX를 재생합니다.
    [DisallowMultipleComponent]
    public sealed class PrayerTree : MonoBehaviour, IInteractable, IInteractionAction, IInteractionRange
    {
        [SerializeField] private string interactionId = "village-prayer-tree";
        [SerializeField, Min(0)] private float pointHeight = 1f;

        // 줄기가 굵어 중심이 기본 2m 판정 밖이므로 줄기 반경만큼 판정 거리를 늘립니다.
        [SerializeField, Min(2)] private float range = 3.6f;

        public string InteractionId => interactionId;
        public string InteractionAction => "기도";
        public float InteractionRange => range;
        public Vector3 InteractionPoint => transform.position + Vector3.up * pointHeight;

        public bool CanInteract(InteractionContext context) =>
            context.Player != null && context.Player.GetComponent<Health>() is { IsDead: false };

        public void Interact(InteractionContext context)
        {
            if (!CanInteract(context))
                return;

            context.Player.GetComponent<Health>().RestoreFull();
            if (context.Player.TryGetComponent(out PlayerMana mana))
                mana.RestoreFull();

            PrayerVfx.Play(context.Player);
        }
    }
}
