using Nomad.Terrain;
using UnityEngine;
using UnityEngine.Rendering.Universal;

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

        [Header("필드에 있는 동안의 카메라 (TerrainTest 기준)")]
        [SerializeField] private bool postProcessing = true;
        [SerializeField] private AntialiasingMode antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        [SerializeField] private CameraClearFlags clearFlags = CameraClearFlags.SolidColor;
        [SerializeField] private Color background = new Color(0.16f, 0.2f, 0.14f);

        [Tooltip("진입마다 새 seed를 쓰므로, 경로 준비에 실패한 seed는 이 횟수까지 새 seed로 다시 생성합니다.")]
        [SerializeField, Min(0)] private int maxRetries = 5;

        private TerrainIntegrationControls controls;
        private FieldNavigation navigation;
        private CameraState previousCamera;
        private int retries;
        private bool retryPending;

        private void Start()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null || manager.Player == null)
            {
                Debug.LogError("Field 씬에 연결할 GameManager Player가 없습니다. GameConfig의 Player 모듈을 확인해 주세요.", this);
                return;
            }

            Camera camera = manager.GetComponentInChildren<Camera>(true);
            ApplyCamera(camera);

            controls = GetComponent<TerrainIntegrationControls>();
            controls.Bind(manager.Player, camera);
            controls.ExitReached += OnExitReached;
            navigation = GetComponent<FieldNavigation>();
            navigation.GenerationCompleted += OnGenerationCompleted;

            // 이전 지역(마을)이 아직 언로드되지 않았으면 그 충돌체가 지형 바닥·경로 판정에 섞입니다.
            // 꺼 둔 컴포넌트는 Start가 미뤄지므로, 지역 전환이 끝난 뒤 켜서 생성을 시작합니다.
            if (manager.IsLoading)
            {
                controls.enabled = false;
                manager.RegionLoaded += OnRegionLoaded;
            }
        }

        private void OnRegionLoaded(UnityEngine.SceneManagement.Scene region)
        {
            if (region != gameObject.scene)
                return;

            GameManager.Instance.RegionLoaded -= OnRegionLoaded;
            controls.enabled = true;
        }

        // 실패한 생성은 플레이어를 배치하지 않아 멈추므로 새 seed로 다시 만듭니다.
        // 생성 도중 호출되는 이벤트 안에서 재생성하면 바깥 생성 흐름과 겹치므로 다음 프레임에 처리합니다.
        private void OnGenerationCompleted(FieldGenerationResult result)
        {
            if (!result.Success && retries < maxRetries)
                retryPending = true;
        }

        private void Update()
        {
            if (!retryPending)
                return;

            retryPending = false;
            retries++;
            Debug.LogWarning($"필드 경로 준비 실패({navigation.LastResult?.FailureReason}). 새 seed로 다시 생성합니다 ({retries}/{maxRetries}).", this);
            controls.Regenerate(true);
        }

        // 영속 카메라를 다른 지역(마을)에서도 쓰므로 필드를 떠날 때 원래 설정으로 되돌립니다.
        private void OnDestroy()
        {
            if (controls != null)
                controls.ExitReached -= OnExitReached;
            if (navigation != null)
                navigation.GenerationCompleted -= OnGenerationCompleted;
            if (GameManager.Instance != null)
                GameManager.Instance.RegionLoaded -= OnRegionLoaded;

            previousCamera?.Restore();
        }

        private void ApplyCamera(Camera camera)
        {
            if (camera == null)
                return;

            previousCamera = new CameraState(camera);
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = postProcessing;
            data.antialiasing = antialiasing;
            camera.clearFlags = clearFlags;
            camera.backgroundColor = background;
        }

        private void OnExitReached(FieldExit exit)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.LoadRegion(returnRegion);
        }

        private sealed class CameraState
        {
            private readonly Camera camera;
            private readonly bool postProcessing;
            private readonly AntialiasingMode antialiasing;
            private readonly CameraClearFlags clearFlags;
            private readonly Color background;

            public CameraState(Camera target)
            {
                camera = target;
                UniversalAdditionalCameraData data = target.GetUniversalAdditionalCameraData();
                postProcessing = data.renderPostProcessing;
                antialiasing = data.antialiasing;
                clearFlags = target.clearFlags;
                background = target.backgroundColor;
            }

            public void Restore()
            {
                if (camera == null)
                    return;

                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = postProcessing;
                data.antialiasing = antialiasing;
                camera.clearFlags = clearFlags;
                camera.backgroundColor = background;
            }
        }
    }
}
