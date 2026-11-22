using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Visibility
{
    // 원본 재질/프리팹을 수정하지 않고 이 인스턴스의 윗부분만 걷어냅니다.
    public sealed class VisibilityFoliage : MonoBehaviour
    {
        [SerializeField] private Shader cutawayShader;
        [SerializeField, Min(0f)] private float remainingHeight = 0.4f;
        [SerializeField] private bool blocksSight;
        public bool BlocksSight => blocksSight;
        private readonly Dictionary<Renderer, Material[]> originals = new();
        private readonly List<Material> copies = new();

        public void Configure(Shader shader, float height = 0.4f, bool solidTrunk = false)
        {
            Restore();
            cutawayShader = shader;
            remainingHeight = height;
            blocksSight = solidTrunk;
            if (Application.isPlaying && isActiveAndEnabled) Apply();
        }

        private void OnEnable()
        {
            if (Application.isPlaying) Apply();
        }

        private void Apply()
        {
            if (originals.Count > 0 || cutawayShader == null) return;
            float baseY = transform.position.y;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer) continue;
                Material[] materials = renderer.sharedMaterials;
                originals.Add(renderer, materials);
                var replacements = new Material[materials.Length];
                for (int index = 0; index < materials.Length; index++)
                {
                    Material original = materials[index];
                    var copy = new Material(cutawayShader) { name = "Visibility " + (original != null ? original.name : "Foliage") };
                    if (original != null)
                    {
                        if (original.HasProperty("_BaseMap")) copy.SetTexture("_BaseMap", original.GetTexture("_BaseMap"));
                        else if (original.HasProperty("_MainTex")) copy.SetTexture("_BaseMap", original.GetTexture("_MainTex"));
                        if (original.HasProperty("_BaseMap"))
                        {
                            copy.SetTextureScale("_BaseMap", original.GetTextureScale("_BaseMap"));
                            copy.SetTextureOffset("_BaseMap", original.GetTextureOffset("_BaseMap"));
                        }
                        if (original.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", original.GetColor("_BaseColor"));
                        else if (original.HasProperty("_Color")) copy.SetColor("_BaseColor", original.GetColor("_Color"));
                        copy.SetFloat("_Cutoff", original.HasProperty("_Cutoff") ? original.GetFloat("_Cutoff") : 0.1f);
                    }
                    copy.SetFloat("_CutHeight", baseY + remainingHeight);
                    copies.Add(copy);
                    replacements[index] = copy;
                }
                renderer.sharedMaterials = replacements;
            }
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

        private void OnDisable() => Restore();
    }
}
