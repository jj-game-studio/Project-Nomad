using UnityEngine;

namespace Nomad.Monsters
{
    // 장비/상호작용 컴포넌트와 충돌체가 없는 시각 전용 드롭입니다.
    public sealed class PlaceholderEquipmentDrop : MonoBehaviour
    {
        private Transform visual;
        private Material material;
        private Vector3 start;
        private Vector3 landing;
        private float elapsed;
        public MonsterRank SourceRank { get; private set; }
        public bool Landed => elapsed >= 0.65f;

        public void Configure(MonsterRank rank)
        {
            SourceRank = rank;
            start = transform.position + Vector3.up * 0.8f;
            Vector3 candidate = transform.position + transform.right * 0.7f;
            // 짧은 포물선의 착지점을 지면에 고정해 계단에서도 공중에 남지 않게 합니다.
            if (Physics.Raycast(candidate + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit,
                5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                candidate = hit.point;
            landing = candidate + Vector3.up * 0.14f;
            transform.position = start;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = rank == MonsterRank.Boss ? new Color(1f, 0.65f, 0.1f) :
                rank == MonsterRank.Elite ? new Color(0.75f, 0.3f, 1f) : new Color(0.5f, 0.85f, 1f);
            visual = new GameObject("Equipment crate marker").transform;
            visual.SetParent(transform, false);
            Part("Crate", Vector3.zero, new Vector3(0.48f, 0.25f, 0.32f));
            Part("Strap", new Vector3(0f, 0.15f, 0f), new Vector3(0.12f, 0.08f, 0.36f));
        }

        private void Part(string label, Vector3 position, Vector3 scale)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = label;
            part.transform.SetParent(visual, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider collision = part.GetComponent<Collider>();
            collision.enabled = false;
            Destroy(collision);
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void Update()
        {
            if (visual == null || Landed) return;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 0.65f);
            transform.position = Vector3.Lerp(start, landing, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.65f);
            visual.localRotation = Quaternion.Euler(0f, t * 180f, 0f);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
