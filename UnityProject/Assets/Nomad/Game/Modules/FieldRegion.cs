using Nomad.Terrain;
using UnityEngine;

namespace Nomad.Game.Modules
{
    // Field 지역 씬의 생성 필드를 메인 게임의 영속 Player에 연결하고, 출구에 닿으면 돌아갈 지역을 불러옵니다.
    // 필드 생성(TerrainIntegrationControls.Start)보다 먼저 Player를 넘겨야 하므로 앞선 실행 순서를 씁니다.
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(TerrainIntegrationControls))]
    public sealed class FieldRegion : MonoBehaviour
    {
        [Tooltip("필드 출구에 닿으면 돌아갈 지역 씬 이름")]
        [SerializeField] private string returnRegion = "Village";

        private TerrainIntegrationControls controls;

        private void Start()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null || manager.Player == null)
            {
                Debug.LogError("Field 씬에 연결할 GameManager Player가 없습니다. GameConfig의 Player 모듈을 확인해 주세요.", this);
                return;
            }

            controls = GetComponent<TerrainIntegrationControls>();
            controls.Bind(manager.Player, manager.GetComponentInChildren<Camera>(true));
            controls.ExitReached += OnExitReached;
        }

        private void OnDestroy()
        {
            if (controls != null)
                controls.ExitReached -= OnExitReached;
        }

        private void OnExitReached(FieldExit exit)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.LoadRegion(returnRegion);
        }
    }
}
