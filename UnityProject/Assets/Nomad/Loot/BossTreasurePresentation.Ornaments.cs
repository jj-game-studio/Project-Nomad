using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Loot
{
    public sealed partial class BossTreasurePresentation
    {
        private readonly List<Material> ornaments = new();

        private void Decorate(Transform holder, GameObject model, bool empty)
        {
            var gold = new Material(Resources.Load<Shader>("BossCel")) { color = empty ? new Color(0.3f, 0.27f, 0.2f) : new Color(1, 0.58f, 0.11f) };
            var ruby = new Material(Resources.Load<Shader>("BossCel")) { color = empty ? new Color(0.2f, 0.15f, 0.15f) : new Color(0.85f, 0.025f, 0.06f) };
            ornaments.Add(gold);
            ornaments.Add(ruby);
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                var tint = new MaterialPropertyBlock();
                tint.SetColor("_BaseColor", empty ? new Color(0.25f, 0.23f, 0.22f) : new Color(0.63f, 0.3f, 0.1f));
                renderer.SetPropertyBlock(tint);
            }
            Vector3 low = holder.InverseTransformPoint(bounds.min), high = holder.InverseTransformPoint(bounds.max);
            Vector3 size = high - low, center = (low + high) * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                AddOrnament(holder, "Heavy golden chest band", PrimitiveType.Cube,
                    center + Vector3.right * side * size.x * 0.33f - Vector3.up * size.y * 0.1f,
                    new Vector3(size.x * 0.065f, size.y * 0.72f, size.z * 1.025f), gold);
                foreach (float depth in new[] { -1f, 1f })
                    AddOrnament(holder, "Gold corner rivet", PrimitiveType.Sphere,
                        center + new Vector3(side * size.x * 0.33f, -size.y * 0.27f, depth * size.z * 0.52f),
                        Vector3.one * size.x * 0.055f, gold);
            }
            Vector3 lockPoint = center + new Vector3(0, -size.y * 0.08f, -size.z * 0.53f);
            AddOrnament(holder, "Ornate gold lock", PrimitiveType.Cube, lockPoint, new Vector3(size.x * 0.18f, size.y * 0.26f, size.z * 0.09f), gold);
            AddOrnament(holder, "Boss ruby seal", PrimitiveType.Sphere, lockPoint - Vector3.forward * size.z * 0.07f,
                new Vector3(size.x * 0.12f, size.y * 0.19f, size.z * 0.08f), ruby);
        }

        private static void AddOrnament(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 size, Material material)
        {
            GameObject root = GameObject.CreatePrimitive(type);
            root.name = name;
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localScale = size;
            root.GetComponent<Collider>().enabled = false;
            Destroy(root.GetComponent<Collider>());
            root.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void OnDestroy() { foreach (Material material in ornaments) if (material != null) Destroy(material); }
    }
}
