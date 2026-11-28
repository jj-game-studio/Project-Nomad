using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Monsters.Demo
{
    // 원본 에셋은 유지하고 불투명 표면에만 명암 단계와 얇은 외곽선을 적용합니다.
    public sealed class CartoonSurfaceStyle : MonoBehaviour
    {
        private readonly Dictionary<Renderer, Material[]> originals = new();
        private readonly List<Material> owned = new();
        private readonly List<GameObject> edges = new();
        private readonly Dictionary<Renderer, Renderer> outlineSources = new();
        private float nextScan;

        private void Start() => Refresh();

        private void Update()
        {
            if (Time.time < nextScan) return;
            nextScan = Time.time + 0.5f;
            Refresh();
        }

        private void LateUpdate()
        {
            foreach (var pair in outlineSources)
                if (pair.Key != null && pair.Value != null) pair.Value.enabled = pair.Key.enabled;
        }

        public void Refresh()
        {
            Shader cel = Resources.Load<Shader>("BossCel");
            Shader ink = Resources.Load<Shader>("BossOutline");
            foreach (Renderer source in GetComponentsInChildren<Renderer>(true))
            {
                if (originals.ContainsKey(source) || source.name == "Cartoon ink outline" ||
                    (source is not MeshRenderer && source is not SkinnedMeshRenderer) ||
                    source.GetComponent<TextMesh>() != null) continue;
                Material[] old = source.sharedMaterials;
                if (old.Length == 0 || System.Array.Exists(old, mat => mat == null || mat.renderQueue > 2500)) continue;
                originals.Add(source, old);
                var replacement = new Material[old.Length];
                for (int i = 0; i < old.Length; i++)
                {
                    var mat = new Material(cel) { mainTexture = old[i].mainTexture,
                        color = old[i].HasProperty("_BaseColor") ? old[i].GetColor("_BaseColor") : old[i].color };
                    replacement[i] = mat;
                    foreach (string property in new[] { "_Albedo_Map", "_BaseMap", "_MainTex" })
                    {
                        if (!old[i].HasProperty(property) || old[i].GetTexture(property) == null) continue;
                        mat.mainTexture = old[i].GetTexture(property);
                        mat.mainTextureScale = old[i].GetTextureScale(property);
                        mat.mainTextureOffset = old[i].GetTextureOffset(property);
                        break;
                    }
                    owned.Add(mat);
                }
                source.sharedMaterials = replacement;

                var root = new GameObject("Cartoon ink outline");
                root.transform.SetParent(source.transform, false);
                edges.Add(root);
                Renderer outline;
                if (source is SkinnedMeshRenderer skin)
                {
                    var clone = root.AddComponent<SkinnedMeshRenderer>();
                    clone.sharedMesh = skin.sharedMesh;
                    clone.bones = skin.bones;
                    clone.rootBone = skin.rootBone;
                    clone.localBounds = skin.localBounds;
                    outline = clone;
                }
                else
                {
                    MeshFilter filter = source.GetComponent<MeshFilter>();
                    if (filter == null) { Destroy(root); continue; }
                    root.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    outline = root.AddComponent<MeshRenderer>();
                }
                var edge = new Material(ink);
                edge.SetFloat("_Width", 0.018f);
                edge.SetColor("_OutlineColor", new Color(0.045f, 0.035f, 0.05f));
                owned.Add(edge);
                var slots = new Material[old.Length];
                System.Array.Fill(slots, edge);
                outline.sharedMaterials = slots;
                outline.enabled = source.enabled;
                outlineSources.Add(source, outline);
                outline.shadowCastingMode = ShadowCastingMode.Off;
                outline.receiveShadows = false;
            }
        }

        private void OnDestroy()
        {
            foreach (var entry in originals) if (entry.Key != null) entry.Key.sharedMaterials = entry.Value;
            foreach (GameObject edge in edges) if (edge != null) Destroy(edge);
            foreach (Material mat in owned) if (mat != null) Destroy(mat);
        }
    }
}
