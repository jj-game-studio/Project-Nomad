using UnityEngine;

namespace Nomad.Game
{
    // 플레이어가 트리거에 들어오면 다른 지역으로 전환합니다. 같은 오브젝트에 트리거 Collider가 필요합니다.
    [DisallowMultipleComponent]
    public sealed class RegionExit : MonoBehaviour
    {
        [Tooltip("전환할 지역 씬 이름")]
        [SerializeField] private string targetRegion = "Field";

        [Tooltip("켜면 바로 이동하지 않고 필드 선택창을 띄웁니다(#194).")]
        [SerializeField] private bool chooseField = true;

        public string TargetRegion => targetRegion;

        private void OnTriggerEnter(Collider other)
        {
            GameManager manager = GameManager.Instance;
            if (manager == null || manager.Player == null)
                return;

            if (other.transform.IsChildOf(manager.Player.transform))
            {
                if (chooseField && targetRegion == "Field")
                    manager.OpenFieldSelect(targetRegion, () => PushBack(manager));
                else
                    manager.LoadRegion(targetRegion);
            }
        }

        // 플레이어를 트리거 영역에서 가장 가까운 수평 경계 밖(들어온 쪽)으로 1m 밀어냅니다.
        private void PushBack(GameManager manager)
        {
            if (manager.Player == null || !TryGetComponent(out Collider trigger))
                return;

            Bounds bounds = trigger.bounds;
            Vector3 position = manager.Player.transform.position;

            float toMinX = position.x - bounds.min.x;
            float toMaxX = bounds.max.x - position.x;
            float toMinZ = position.z - bounds.min.z;
            float toMaxZ = bounds.max.z - position.z;
            float nearest = Mathf.Min(Mathf.Min(toMinX, toMaxX), Mathf.Min(toMinZ, toMaxZ));

            const float margin = 1f;
            if (nearest == toMinX) position.x = bounds.min.x - margin;
            else if (nearest == toMaxX) position.x = bounds.max.x + margin;
            else if (nearest == toMinZ) position.z = bounds.min.z - margin;
            else position.z = bounds.max.z + margin;

            manager.PlacePlayer(position, manager.Player.transform.rotation);
        }
    }
}
