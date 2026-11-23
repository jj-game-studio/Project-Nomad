using Nomad.Monsters;
using Nomad.Terrain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Nomad.Visibility
{
    // 기존 생성기/몬스터 코드를 수정하지 않는 선택적 연결 모듈입니다.
    [DefaultExecutionOrder(600)]
    public sealed class VisibilityField : MonoBehaviour
    {
        private const int Samples = 512;
        [SerializeField] private PlayerVisibility sight;
        [SerializeField] private Shader fadeShader;
        [SerializeField] private bool connectGeneratedFoliage = true;
        [Header("시야 밖 어둠")]
        [SerializeField] private Shader shadeShader;
        [Tooltip("시야 밖을 덮는 색입니다. 알파가 어둠 농도입니다.")]
        [SerializeField] private Color shadeColor = new Color(0.02f, 0.03f, 0.06f, 0.7f);
        [Tooltip("보이는 영역 경계에서 어둠이 짙어지는 거리(m)입니다.")]
        [SerializeField, Range(0f, 3f)] private float shadeSoftness = 0.8f;

        private readonly float[] pixels = new float[Samples];
        private Texture2D distances;
        private float nextScan;
        private float nextMask;
        private Material shadeMaterial;
        private Mesh shadeMesh;
        private MeshRenderer shadeRenderer;
        private Camera depthCamera;
        private CameraOverrideOption previousDepth;

        public MeshRenderer ShadeRenderer => shadeRenderer;
        public Color ShadeColor { get => shadeColor; set => shadeColor = value; }

        public void Configure(PlayerVisibility source, Shader foliage, Shader shade = null)
        {
            sight = source;
            fadeShader = foliage;
            if (shade != null) shadeShader = shade;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            distances = new Texture2D(Samples, 1, TextureFormat.RFloat, false, true)
            {
                name = "Visibility ray distances", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
        }

        private void LateUpdate()
        {
            if (sight == null || !sight.Available)
            {
                Deactivate();
                return;
            }

            // 생성된 적은 같은 프레임의 렌더링 전에 등록해 첫 프레임 노출을 막습니다.
            foreach (MonsterIdentity monster in FindObjectsByType<MonsterIdentity>(FindObjectsSortMode.None))
                if (monster.GetComponent<VisibilityTarget>() == null)
                    monster.gameObject.AddComponent<VisibilityTarget>();

            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.1f;
                if (connectGeneratedFoliage && fadeShader != null) ConnectFoliage();
            }
            UpdateShade();
            if (Time.unscaledTime < nextMask) return;
            nextMask = Time.unscaledTime + 0.08f;

            // 어둠은 눈높이 방사선의 도달 거리로 그립니다. 적은 별도의 3D 표본으로 판정합니다.
            for (int index = 0; index < Samples; index++)
            {
                float degrees = -180f + 360f * index / (Samples - 1f);
                Vector3 direction = Quaternion.AngleAxis(degrees, Vector3.up) * sight.Forward;
                float range = sight.RangeAtAngle(degrees);
                pixels[index] = range > 0f ? sight.ClearDistance(sight.Eye, direction, range) : 0f;
            }
            distances.SetPixelData(pixels, 0);
            distances.Apply(false, false);
            Shader.SetGlobalTexture("_NomadVisibilityDistances", distances);
            UpdateGlobals();

            foreach (VisibilityFoliage foliage in VisibilityFoliage.Active) foliage.Evaluate(sight);
        }

        private void UpdateGlobals()
        {
            Shader.SetGlobalVector("_NomadVisibilityEye", sight.Eye);
            Shader.SetGlobalVector("_NomadVisibilityForward", sight.Forward);
            Shader.SetGlobalVector("_NomadVisibilityParams", new Vector4(sight.Distance, sight.Angle * Mathf.Deg2Rad, Samples, sight.NearbyRadius));
            Shader.SetGlobalFloat("_NomadVisibilityActive", 1f);
        }

        // 카메라 바로 앞의 사각형을 화면 전체로 펼쳐 그립니다. 다른 카메라에는 보이지 않습니다.
        private void UpdateShade()
        {
            Camera camera = sight.ViewCamera;
            if (shadeShader == null || camera == null)
            {
                if (shadeRenderer != null) shadeRenderer.enabled = false;
                return;
            }
            if (shadeRenderer == null) CreateShade();
            RequestDepth(camera);

            shadeMaterial.SetColor("_ShadeColor", shadeColor);
            shadeMaterial.SetFloat("_Softness", shadeSoftness);
            Transform view = camera.transform;
            shadeRenderer.transform.SetPositionAndRotation(view.position + view.forward * (camera.nearClipPlane + 0.05f), view.rotation);
            shadeRenderer.gameObject.layer = view.gameObject.layer;
            shadeRenderer.enabled = true;
        }

        private void CreateShade()
        {
            shadeMaterial = new Material(shadeShader) { name = "Visibility Shade" };
            shadeMesh = new Mesh { name = "Visibility Shade Quad" };
            shadeMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f)
            };
            shadeMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            shadeMesh.RecalculateBounds();

            var shade = new GameObject("Visibility Shade");
            shade.transform.SetParent(transform, false);
            shade.AddComponent<MeshFilter>().sharedMesh = shadeMesh;
            shadeRenderer = shade.AddComponent<MeshRenderer>();
            shadeRenderer.sharedMaterial = shadeMaterial;
            shadeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shadeRenderer.receiveShadows = false;
            shadeRenderer.lightProbeUsage = LightProbeUsage.Off;
            shadeRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        // 어둠 셰이더는 깊이 텍스처가 필요합니다. 카메라 설정은 해제 시 되돌립니다.
        private void RequestDepth(Camera camera)
        {
            if (depthCamera == camera) return;
            ReleaseDepth();
            if (!camera.TryGetComponent(out UniversalAdditionalCameraData data)) return;
            depthCamera = camera;
            previousDepth = data.requiresDepthOption;
            data.requiresDepthOption = CameraOverrideOption.On;
        }

        private void ReleaseDepth()
        {
            if (depthCamera != null && depthCamera.TryGetComponent(out UniversalAdditionalCameraData data))
                data.requiresDepthOption = previousDepth;
            depthCamera = null;
        }

        private void ConnectFoliage()
        {
            foreach (FieldDressing dressing in FindObjectsByType<FieldDressing>(FindObjectsSortMode.None))
            {
                if (dressing.Root == null) continue;
                foreach (Transform plant in dressing.Root)
                {
                    if (plant.GetComponent<VisibilityFoliage>() != null) continue;

                    // 나무·덤불은 시선을 막지 않고, 소품은 기존 차단을 유지한 채 반투명만 적용합니다.
                    // 바위/돌담은 대상이 아닙니다.
                    string label = plant.name.ToLowerInvariant();
                    bool prop = label.Contains("prop_") || label.Contains("stump");
                    bool plantLike = label.Contains("tree") || label.Contains("bush") || label.Contains("fern") || label.Contains("leaves");
                    if (!prop && !plantLike) continue;
                    plant.gameObject.AddComponent<VisibilityFoliage>().Configure(fadeShader, solid: prop);
                }
            }
        }

        private void Deactivate()
        {
            Shader.SetGlobalFloat("_NomadVisibilityActive", 0f);
            if (shadeRenderer != null) shadeRenderer.enabled = false;
            foreach (VisibilityFoliage foliage in VisibilityFoliage.Active) foliage.ResetSight();
        }

        private void OnDisable()
        {
            Deactivate();
            ReleaseDepth();
            if (distances != null) Destroy(distances);
            foreach (VisibilityTarget target in VisibilityTarget.Active) target.Release();
        }

        private void OnDestroy()
        {
            if (shadeRenderer != null) Destroy(shadeRenderer.gameObject);
            if (shadeMaterial != null) Destroy(shadeMaterial);
            if (shadeMesh != null) Destroy(shadeMesh);
        }
    }
}
