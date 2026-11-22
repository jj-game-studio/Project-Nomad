using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nomad.Game
{
    // 지역에 들어올 때 플레이어가 서는 위치입니다. 이전 지역 이름이 맞는 입구를 먼저 사용합니다.
    [DisallowMultipleComponent]
    public sealed class RegionEntrance : MonoBehaviour
    {
        [Tooltip("이 지역에서 들어올 때 사용합니다. 비우면 기본 입구(게임 시작·단독 Play)입니다.")]
        [SerializeField] private string fromRegion;

        public string FromRegion => fromRegion;

        public static RegionEntrance Find(Scene region, string previousRegion)
        {
            RegionEntrance standard = null;
            RegionEntrance any = null;
            foreach (GameObject root in region.GetRootGameObjects())
            {
                foreach (RegionEntrance entrance in root.GetComponentsInChildren<RegionEntrance>())
                {
                    if (!string.IsNullOrEmpty(previousRegion) && entrance.fromRegion == previousRegion)
                        return entrance;

                    if (standard == null && string.IsNullOrEmpty(entrance.fromRegion))
                        standard = entrance;
                    if (any == null)
                        any = entrance;
                }
            }

            // 맞는 입구가 없으면 기본 입구, 그것도 없으면 아무 입구나 사용합니다.
            return standard != null ? standard : any;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.5f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.9f, transform.forward);
        }
    }
}
