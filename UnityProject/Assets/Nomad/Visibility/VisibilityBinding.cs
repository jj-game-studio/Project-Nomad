using Nomad.Player;
using UnityEngine;

namespace Nomad.Visibility
{
    // GameConfig의 영속 모듈로 등록할 때 씬 참조 없이 현재 Player에 연결합니다.
    [DefaultExecutionOrder(400)]
    [RequireComponent(typeof(PlayerVisibility), typeof(VisibilityField))]
    public sealed class VisibilityBinding : MonoBehaviour
    {
        private PlayerVisibility sight;
        private float nextSearch;

        private void Awake() => sight = GetComponent<PlayerVisibility>();

        private void Update()
        {
            if (sight.Observer != null && sight.Observer.gameObject.activeInHierarchy) return;
            if (Time.unscaledTime < nextSearch) return;
            nextSearch = Time.unscaledTime + 0.1f;

            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            if (player != null) sight.Configure(player.transform, Camera.main, sight.Distance, sight.Angle);
        }
    }
}
