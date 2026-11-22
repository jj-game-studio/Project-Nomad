using Nomad.Monsters;
using Nomad.Terrain;
using UnityEngine;

namespace Nomad.Visibility
{
    // 기존 생성기/몬스터 코드를 수정하지 않는 선택적 연결 모듈입니다.
    [DefaultExecutionOrder(600)]
    public sealed class VisibilityField : MonoBehaviour
    {
        private const int Samples = 256;
        [SerializeField] private PlayerVisibility sight;
        [SerializeField] private Shader foliageShader;
        [SerializeField] private bool connectGeneratedFoliage = true;
        private readonly float[] pixels = new float[Samples];
        private Texture2D distances;
        private float nextScan;
        private float nextMask;

        public void Configure(PlayerVisibility source, Shader shader)
        {
            sight = source;
            foliageShader = shader;
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
                Shader.SetGlobalFloat("_NomadVisibilityActive", 0f);
                return;
            }

            // 생성된 적은 같은 프레임의 렌더링 전에 등록해 첫 프레임 노출을 막습니다.
            foreach (MonsterIdentity monster in FindObjectsByType<MonsterIdentity>(FindObjectsSortMode.None))
                if (monster.GetComponent<VisibilityTarget>() == null)
                    monster.gameObject.AddComponent<VisibilityTarget>();

            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.1f;
                if (connectGeneratedFoliage && foliageShader != null) ConnectFoliage();
            }
            if (Time.unscaledTime < nextMask) return;
            nextMask = Time.unscaledTime + 0.08f;

            // 식생은 눈높이 방사선으로 부분 노출합니다. 적은 별도의 3D 표본으로 판정합니다.
            for (int index = 0; index < Samples; index++)
            {
                float degrees = -sight.Angle * 0.5f + sight.Angle * index / (Samples - 1f);
                Vector3 direction = Quaternion.AngleAxis(degrees, Vector3.up) * sight.Forward;
                float clear = sight.ClearDistance(sight.Eye, direction, sight.Distance);
                pixels[index] = clear;
            }
            distances.SetPixelData(pixels, 0);
            distances.Apply(false, false);
            Shader.SetGlobalTexture("_NomadVisibilityDistances", distances);
            UpdateGlobals();
        }

        private void UpdateGlobals()
        {
            Shader.SetGlobalVector("_NomadVisibilityEye", sight.Eye);
            Shader.SetGlobalVector("_NomadVisibilityForward", sight.Forward);
            Shader.SetGlobalVector("_NomadVisibilityParams", new Vector4(sight.Distance, sight.Angle * Mathf.Deg2Rad, Samples, 0f));
            Shader.SetGlobalFloat("_NomadVisibilityActive", 1f);
        }

        private void ConnectFoliage()
        {
            foreach (FieldDressing dressing in FindObjectsByType<FieldDressing>(FindObjectsSortMode.None))
            {
                if (dressing.Root == null) continue;
                foreach (Transform plant in dressing.Root)
                {
                    // 돌/담장/줄기 등 단단한 장애물에는 적용하지 않습니다.
                    string label = plant.name.ToLowerInvariant();
                    bool tree = label.Contains("tree");
                    if (!tree && !label.Contains("bush") && !label.Contains("fern") && !label.Contains("leaves")) continue;
                    if (plant.GetComponent<VisibilityFoliage>() != null) continue;
                    plant.gameObject.AddComponent<VisibilityFoliage>().Configure(foliageShader, tree ? 1.5f : 0.4f, tree);
                }
            }
        }

        private void OnDisable()
        {
            Shader.SetGlobalFloat("_NomadVisibilityActive", 0f);
            if (distances != null) Destroy(distances);
            foreach (VisibilityTarget target in VisibilityTarget.Active) target.Release();
        }
    }
}
