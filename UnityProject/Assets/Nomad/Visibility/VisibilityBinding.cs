using System;
using Nomad.Game;
using Nomad.Player;
using UnityEngine;

namespace Nomad.Visibility
{
    // GameConfig의 영속 모듈로 등록할 때 씬 참조 없이 현재 Player에 연결합니다.
    [DefaultExecutionOrder(400)]
    [RequireComponent(typeof(PlayerVisibility), typeof(VisibilityField))]
    public sealed class VisibilityBinding : MonoBehaviour
    {
        [Tooltip("이 지역에서는 시야를 끄고 모두 보이게 합니다.")]
        [SerializeField] private string[] disabledRegions = { "Village" };
        private PlayerVisibility sight;
        private float nextSearch;

        private void Awake() => sight = GetComponent<PlayerVisibility>();

        private void Update()
        {
            GameManager game = GameManager.Instance;
            sight.Suppressed = game != null && game.CurrentRegion.IsValid() &&
                Array.IndexOf(disabledRegions, game.CurrentRegion.name) >= 0;

            if (sight.Observer != null && sight.Observer.gameObject.activeInHierarchy) return;
            if (Time.unscaledTime < nextSearch) return;
            nextSearch = Time.unscaledTime + 0.1f;

            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            if (player != null) sight.Configure(player.transform, Camera.main, sight.Distance, sight.Angle);
        }
    }
}
