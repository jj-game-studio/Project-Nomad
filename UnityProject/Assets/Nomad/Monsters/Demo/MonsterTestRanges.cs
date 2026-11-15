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
            rings = new LineRenderer[4];
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
        }

        private void LateUpdate()
        {
            Draw(rings[0], ShowDetection, transform.position, tracker.SightRange,
                tracker.SightAngle, new Color(0.2f, 0.85f, 1f, 0.3f));
            Draw(rings[1], ShowDetection, transform.position, tracker.ProximityRange,
                360f, new Color(0.3f, 1f, 0.5f, 0.3f));
            Draw(rings[2], ShowTravel, transform.position, tracker.AbandonRange,
                360f, new Color(1f, 0.7f, 0.2f, 0.22f));
            Draw(rings[3], ShowTravel && tracker.HomeRange > 0f, tracker.HomePosition,
                tracker.HomeRange, 360f, new Color(0.8f, 0.45f, 1f, 0.25f));
        }

        private void Draw(LineRenderer line, bool visible, Vector3 center, float radius, float angle, Color color)
        {
            line.enabled = visible;
            if (!visible)
                return;

            bool circle = angle >= 359.9f;
            line.positionCount = circle ? 65 : 67;
            center.y += 0.07f;
            if (!circle)
                line.SetPosition(0, center);
            for (int i = 0; i <= 64; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(-angle * 0.5f + angle * i / 64f,
                    Vector3.up) * transform.forward;
                line.SetPosition(circle ? i : i + 1, center + direction * radius);
            }
            if (!circle)
                line.SetPosition(66, center);

            tint.SetColor("_BaseColor", color);
            line.SetPropertyBlock(tint);
        }

        private void OnDestroy()
        {
            if (material != null)
                Destroy(material);
        }
    }
}
