using UnityEngine;

namespace Nomad.Game
{
    // 플레이어가 트리거에 들어오면 다른 지역으로 전환합니다. 같은 오브젝트에 트리거 Collider가 필요합니다.
    [DisallowMultipleComponent]
    public sealed class RegionExit : MonoBehaviour
    {
        [Tooltip("전환할 지역 씬 이름")]
        [SerializeField] private string targetRegion = "Field";

        public string TargetRegion => targetRegion;

        private void OnTriggerEnter(Collider other)
        {
            GameManager manager = GameManager.Instance;
            if (manager == null || manager.Player == null)
                return;

            if (other.transform.IsChildOf(manager.Player.transform))
                manager.LoadRegion(targetRegion);
        }
    }
}
