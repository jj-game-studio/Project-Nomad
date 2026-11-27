using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class PlayerBossReaction
    {
        private MeshRenderer fallbackSource, fallbackRenderer;
        private GameObject fallbackBody;
        private MaterialPropertyBlock fallbackTint;

        private void CreateFallbackVisual()
        {
            // 통합 테스트 씬의 캡슐도 물리 루트 대신 보이는 메시만 넘어뜨립니다.
            var child = System.Array.Find(GetComponentsInChildren<MeshRenderer>(), renderer => renderer.transform != transform &&
                renderer.name != "Cartoon ink outline" && renderer.GetComponent<MeshFilter>() != null);
            if (child != null) { visual = child.transform; fallHeight = -0.45f; return; }

            var filter = GetComponent<MeshFilter>();
            fallbackSource = GetComponent<MeshRenderer>();
            if (filter == null || fallbackSource == null || filter.sharedMesh == null) return;
            fallbackBody = new GameObject("Boss reaction body");
            fallbackBody.transform.SetParent(transform, false);
            fallbackBody.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            fallbackRenderer = fallbackBody.AddComponent<MeshRenderer>();
            fallbackRenderer.sharedMaterials = fallbackSource.sharedMaterials;
            fallbackRenderer.shadowCastingMode = fallbackSource.shadowCastingMode;
            fallbackRenderer.receiveShadows = fallbackSource.receiveShadows;
            fallbackTint = new MaterialPropertyBlock();
            fallbackSource.enabled = false;
            visual = fallbackBody.transform;
            RefreshFallbackTint();
        }

        private void RefreshFallbackTint()
        {
            if (fallbackSource == null || fallbackRenderer == null) return;
            fallbackSource.GetPropertyBlock(fallbackTint);
            fallbackRenderer.SetPropertyBlock(fallbackTint);
        }

        private void OnDestroy()
        {
            if (fallbackSource != null) fallbackSource.enabled = true;
            if (fallbackBody != null) Destroy(fallbackBody);
        }
    }
}
