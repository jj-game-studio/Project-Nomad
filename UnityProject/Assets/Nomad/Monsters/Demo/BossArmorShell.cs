using System.Collections.Generic;
using Nomad.Combat;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    [DefaultExecutionOrder(395)]
    public sealed class BossArmorShell : MonoBehaviour
    {
        private static Material material;
        private BossArmor armor;
        private DragonBossCombat boss;
        private Health health;
        private readonly List<SkinnedMeshRenderer>[] layers = { new(), new() };
        public int LayersVisible => 2 - armor.BreakCount;

        public void Configure(DragonBossCombat owner)
        {
            boss = owner; armor = GetComponent<BossArmor>(); health = GetComponent<Health>();
            armor.Broken += Shatter;
        }

        private void Start()
        {
            if (material == null) material = new Material(Resources.Load<Shader>("BossArmorShell"));
            var sources = transform.Find("Body").GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var source in sources)
            {
                if (source.name.Contains("outline")) continue;
                for (int layer = armor.BreakCount; layer < 2; layer++)
                {
                    var root = new GameObject(layer == 0 ? "Outer fitted carapace" : "Inner fitted carapace");
                    root.transform.SetParent(source.transform, false);
                    var shell = root.AddComponent<SkinnedMeshRenderer>();
                    shell.sharedMesh = source.sharedMesh; shell.bones = source.bones;
                    shell.rootBone = source.rootBone; shell.localBounds = source.localBounds;
                    var slots = new Material[source.sharedMaterials.Length]; System.Array.Fill(slots, material);
                    shell.sharedMaterials = slots;
                    shell.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    layers[layer].Add(shell);
                }
            }
        }

        private void LateUpdate()
        {
            if (boss == null || armor == null) return;
            for (int layer = 0; layer < 2; layer++)
            foreach (var shell in layers[layer])
            {
                if (shell == null) continue;
                shell.enabled = boss.Introduced && !health.IsDead;
                var tint = new MaterialPropertyBlock();
                tint.SetColor("_LayerColor", layer == 0 ? new Color(0.25f,0.22f,0.2f) : new Color(0.38f,0.24f,0.13f));
                tint.SetFloat("_Offset", shell.sharedMesh.bounds.extents.magnitude * (layer == 0 ? 0.004f : 0.002f));
                tint.SetFloat("_Stress", layer == armor.BreakCount ? 1 - armor.RemainingRatio : 0);
                var source = shell.transform.parent.GetComponent<SkinnedMeshRenderer>();
                if (source != null && source.sharedMaterial.mainTexture != null)
                    tint.SetTexture("_BaseMap",source.sharedMaterial.mainTexture);
                shell.SetPropertyBlock(tint);
            }
        }

        private void Shatter(int count)
        {
            int layer = count - 1;
            if (layer < 0 || layer >= 2) return;
            foreach (var shell in layers[layer])
            {
                if (shell == null) continue;
                var baked = new Mesh(); shell.BakeMesh(baked);
                Vector3[] vertices = baked.vertices;
                int pieces = Mathf.Min(24, vertices.Length);
                for (int i = 0; i < pieces; i++)
                {
                    Transform shard = BossFlameVfx.Rock(null, "Crumbled fitted shell");
                    shard.position = shell.transform.TransformPoint(vertices[i * vertices.Length / pieces]);
                    shard.localScale = Vector3.one * Random.Range(0.12f, 0.3f);
                    shard.GetComponent<Renderer>().sharedMaterial = material;
                    var body = shard.gameObject.AddComponent<Rigidbody>();
                    body.linearVelocity = (shard.position - transform.position).normalized * 3 + Vector3.up * 2;
                    body.angularVelocity = Random.insideUnitSphere * 4;
                    Destroy(shard.gameObject, 1.2f);
                }
                Destroy(baked); Destroy(shell.gameObject);
            }
            layers[layer].Clear();
            BossStoneDebris.Create(transform.parent, transform.position + Vector3.up);
        }

        private void OnDestroy() { if (armor != null) armor.Broken -= Shatter; }
    }
}
