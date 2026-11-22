using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed class MonsterTestRanges : MonoBehaviour
    {
        public bool ShowDetection { get; set; }
        public bool ShowTravel { get; set; }
        private MonsterTracker tracker;
        private LineRenderer[] rings;
        private Material material;
        private MaterialPropertyBlock tint;
        private GameObject disc;
        private Mesh discMesh;
        private Renderer discRenderer;

        private void Awake()
        {
            tracker = GetComponent<MonsterTracker>();
            tint = new MaterialPropertyBlock();
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            rings = new LineRenderer[3];
            for (int i = 0; i < rings.Length; i++)
            {
                var child = new GameObject("Test range " + i);
                child.transform.SetParent(transform, false);
                LineRenderer line = child.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.widthMultiplier = 0.04f;
                line.useWorldSpace = true;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rings[i] = line;
            }
            disc = new GameObject("Faint detection area");
            disc.transform.SetParent(transform, false);
            disc.transform.localPosition = Vector3.up * 0.035f;
            discMesh = new Mesh();
            var vertices = new Vector3[66];
            var triangles = new int[64 * 3];
            for (int i = 0; i <= 64; i++) vertices[i + 1] = Quaternion.AngleAxis(i * 360f / 64f, Vector3.up) * Vector3.forward;
            for (int i = 0; i < 64; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            discMesh.vertices = vertices;
            discMesh.triangles = triangles;
            disc.AddComponent<MeshFilter>().sharedMesh = discMesh;
            discRenderer = disc.AddComponent<MeshRenderer>();
            discRenderer.sharedMaterial = material;
            discRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void LateUpdate()
        {
            MonsterRank rank = GetComponent<MonsterIdentity>()?.Rank ?? MonsterRank.Normal;
            Color color = rank == MonsterRank.Boss ? new Color(1f, 0.65f, 0.25f, 0.16f) :
                rank == MonsterRank.Elite ? new Color(0.8f, 0.45f, 1f, 0.16f) : new Color(0.2f, 0.85f, 1f, 0.16f);
            Draw(rings[0], ShowDetection, transform.position, tracker.SightRange, color);
            disc.SetActive(ShowDetection);
            disc.transform.localScale = Vector3.one * tracker.SightRange;
            color.a = 0.025f;
            tint.SetColor("_BaseColor", color);
            discRenderer.SetPropertyBlock(tint);
            Draw(rings[1], ShowTravel, transform.position, tracker.AbandonRange,
                new Color(1f, 0.7f, 0.2f, 0.22f));
            Draw(rings[2], ShowTravel && tracker.HomeRange > 0f, tracker.HomePosition,
                tracker.HomeRange, new Color(0.8f, 0.45f, 1f, 0.25f));
        }

        private void Draw(LineRenderer line, bool visible, Vector3 center, float radius, Color color)
        {
            line.enabled = visible;
            if (!visible)
                return;

            line.positionCount = 65;
            center.y += 0.07f;
            for (int i = 0; i <= 64; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(360f * i / 64f, Vector3.up) * Vector3.forward;
                line.SetPosition(i, center + direction * radius);
            }

            tint.SetColor("_BaseColor", color);
            line.SetPropertyBlock(tint);
        }

        private void OnDestroy()
        {
            if (material != null)
                Destroy(material);
            if (discMesh != null) Destroy(discMesh);
        }
    }
}
