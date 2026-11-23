using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Visibility
{
    // 시야에 들어온 식생·소품만 반투명 재질로 바꿉니다. 원본 재질/프리팹은 수정하지 않습니다.
    public sealed class VisibilityFoliage : MonoBehaviour
    {
        internal static readonly HashSet<VisibilityFoliage> Active = new();
        private static readonly string[] TextureNames = { "_BaseMap", "_MainTex", "_Albedo_Map" };
        private static readonly string[] ColorNames = { "_BaseColor", "_Color" };

        [SerializeField] private Shader fadeShader;
        [Tooltip("시야 안에서의 불투명도입니다. 1이면 반투명하지 않습니다.")]
        [SerializeField, Range(0.05f, 1f)] private float fadeAlpha = 0.35f;
        [Tooltip("켜면 이 물체가 시선을 막습니다. 나무·덤불은 기본으로 막지 않습니다.")]
        [SerializeField] private bool blocksSight;
        [SerializeField, Min(0.1f)] private float fadeSpeed = 4f;

        private readonly Dictionary<Renderer, Material[]> originals = new();
        private readonly List<Material> copies = new();
        private Renderer[] renderers;
        private float targetAlpha = 1f;

        public bool BlocksSight => blocksSight;
        public bool InSight { get; private set; }
        public float Alpha { get; private set; } = 1f;
        public bool Faded => originals.Count > 0;

        public void Configure(Shader shader, float alpha = 0.35f, bool solid = false)
        {
            Restore();
            fadeShader = shader;
            fadeAlpha = Mathf.Clamp(alpha, 0.05f, 1f);
            blocksSight = solid;
        }

        private void OnEnable()
        {
            if (Application.isPlaying) Active.Add(this);
        }

        // 중심과 지면 가까운 테두리 4곳 중 하나라도 보이면 시야 안으로 봅니다.
        // 큰 덤불의 일부만 시야에 걸쳐도 안쪽 적이 보이게 하며, 자기 충돌체는 무시합니다.
        public void Evaluate(PlayerVisibility sight)
        {
            InSight = false;
            if (sight != null && sight.Available && TryBounds(out Bounds bounds))
            {
                Vector3 center = bounds.center;
                Vector3 low = new Vector3(center.x, Mathf.Min(bounds.max.y, bounds.min.y + 0.5f), center.z);
                Vector3 x = Vector3.right * bounds.extents.x;
                Vector3 z = Vector3.forward * bounds.extents.z;
                InSight = sight.CanSee(center, transform) || sight.CanSee(low, transform) ||
                    sight.CanSee(low + x, transform) || sight.CanSee(low - x, transform) ||
                    sight.CanSee(low + z, transform) || sight.CanSee(low - z, transform);
            }
            targetAlpha = InSight ? fadeAlpha : 1f;
        }

        public void ResetSight()
        {
            InSight = false;
            targetAlpha = 1f;
        }

        private void Update()
        {
            if (Mathf.Approximately(Alpha, targetAlpha) && (Alpha < 1f) == Faded) return;
            Alpha = Mathf.MoveTowards(Alpha, targetAlpha, Time.unscaledDeltaTime * fadeSpeed);

            // 완전히 불투명해지면 원래 재질로 돌려 원본 외형을 그대로 유지합니다.
            if (Alpha >= 1f) { Restore(); return; }
            Apply();
            foreach (Material copy in copies) copy.SetFloat("_Alpha", Alpha);
        }

        private bool TryBounds(out Bounds bounds)
        {
            // 판정 주기마다 할당하지 않도록 Renderer 목록을 한 번만 모읍니다.
            renderers ??= GetComponentsInChildren<Renderer>(true);
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled) continue;
                if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer) continue;
                if (found) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; found = true; }
            }
            return found;
        }

        private void Apply()
        {
            if (originals.Count > 0 || fadeShader == null) return;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer) continue;
                Material[] materials = renderer.sharedMaterials;
                originals.Add(renderer, materials);

                var replacements = new Material[materials.Length];
                for (int index = 0; index < materials.Length; index++)
                {
                    replacements[index] = Copy(materials[index]);
                    copies.Add(replacements[index]);
                }
                renderer.sharedMaterials = replacements;
            }
        }

        // 구매 애셋 셰이더마다 텍스처 이름이 달라 알려진 이름을 차례로 복사합니다(Synty는 _Albedo_Map).
        private Material Copy(Material original)
        {
            var copy = new Material(fadeShader) { name = "Visibility " + (original != null ? original.name : "Foliage") };
            if (original == null) return copy;

            foreach (string name in TextureNames)
            {
                if (!original.HasProperty(name) || original.GetTexture(name) == null) continue;
                copy.SetTexture("_BaseMap", original.GetTexture(name));
                copy.SetTextureScale("_BaseMap", original.GetTextureScale(name));
                copy.SetTextureOffset("_BaseMap", original.GetTextureOffset(name));
                break;
            }
            foreach (string name in ColorNames)
            {
                if (!original.HasProperty(name)) continue;
                copy.SetColor("_BaseColor", original.GetColor(name));
                break;
            }
            copy.SetFloat("_Cutoff", original.HasProperty("_Cutoff") ? original.GetFloat("_Cutoff") : 0.1f);
            return copy;
        }

        private void Restore()
        {
            foreach (var item in originals)
                if (item.Key != null) item.Key.sharedMaterials = item.Value;
            originals.Clear();
            foreach (Material copy in copies)
                if (copy != null) Destroy(copy);
            copies.Clear();
        }

        private void OnDisable()
        {
            Active.Remove(this);
            Restore();
            Alpha = 1f;
            ResetSight();
        }
    }
}
