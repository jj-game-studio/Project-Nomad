using Nomad.Player;
using UnityEngine;

namespace Nomad.Village
{
    // 마을 씬에 있는 동안만 적용하는 규칙입니다. 마을을 떠나 씬이 내려가면 원래 값으로 되돌립니다.
    [DisallowMultipleComponent]
    public sealed class VillageRules : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeedMultiplier = 1.5f;

        private PlayerMovement player;

        private void Update()
        {
            // 영속 Player는 지역 로드 뒤 입구로 옮겨지므로 매 프레임 확인해 연결합니다.
            if (player != null)
                return;

            player = FindAnyObjectByType<PlayerMovement>();
            if (player != null)
                player.SpeedMultiplier = moveSpeedMultiplier;
        }

        private void OnDisable()
        {
            if (player != null)
                player.SpeedMultiplier = 1f;
            player = null;
        }
    }
}
