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
        private Transform rear, front;
        private readonly List<Transform>[] layers = { new(), new() };
        public int LayersVisible => 2 - armor.BreakCount;

        public void Configure(DragonBossCombat owner)
        {
            boss = owner; armor = GetComponent<BossArmor>();
            Transform[] bones = GetComponentsInChildren<Transform>();
            rear = System.Array.Find(bones, bone => bone.name == "base");
            front = System.Array.Find(bones, bone => bone.name == "neck1");
            if (rear == null || front == null) { rear = transform; front = transform; }
            if (material == null) material = new Material(Resources.Load<Shader>("BossArmorShell"));

            for (int layer = 1; layer >= 0; layer--)
            for (int index = 0; index < 12; index++)
            {
                Transform plate = BossFlameVfx.Rock(transform, layer == 0 ? "Outer carapace plate" : "Inner carapace plate");
                plate.GetComponent<Renderer>().sharedMaterial = material;
                plate.localScale = layer == 0 ? new Vector3(1.65f, 0.8f, 1.8f) : new Vector3(1.45f, 0.6f, 1.55f);
                layers[layer].Add(plate);
            }
            armor.Broken += Shatter;
        }

        private void LateUpdate()
        {
            if (boss == null || armor == null) return;
            Vector3 along = front.position - rear.position;
            Quaternion facing = along.sqrMagnitude > 0.1f ? Quaternion.LookRotation(along, transform.up) : transform.rotation;
            for (int layer = 0; layer < 2; layer++)
            for (int index = 0; index < layers[layer].Count; index++)
            {
                Transform plate = layers[layer][index];
                if (plate == null) continue;
                plate.gameObject.SetActive(boss.Introduced && !GetComponent<Health>().IsDead);
                int side = index % 3 - 1; float row = index / 3 / 3f;
                Vector3 center = Vector3.Lerp(rear.position, front.position, row);
                plate.position = center + transform.right * (side * 0.8f) + transform.up *
                    ((side == 0 ? 0.85f : 0.46f) + (layer == 0 ? 0.17f : 0));
                plate.rotation = facing * Quaternion.Euler(0, 0, -side * 25);
                var tint = new MaterialPropertyBlock();
                tint.SetColor("_LayerColor", layer == 0 ? new Color(0.17f,0.23f,0.27f) : new Color(0.4f,0.24f,0.12f));
                tint.SetFloat("_Stress", layer == armor.BreakCount ? 1 - armor.RemainingRatio : 0);
                plate.GetComponent<Renderer>().SetPropertyBlock(tint);
            }
        }

        private void Shatter(int count)
        {
            int layer = count - 1;
            if (layer < 0 || layer >= 2) return;
            foreach (Transform plate in layers[layer])
            {
                if (plate == null) continue;
                plate.SetParent(null, true);
                var shard = plate.gameObject.AddComponent<Rigidbody>();
                shard.linearVelocity = (plate.position - transform.position).normalized * 4 + Vector3.up * 3;
                shard.angularVelocity = Random.insideUnitSphere * 6;
                Destroy(plate.gameObject, 1.4f);
            }
            layers[layer].Clear();
            BossStoneDebris.Create(transform.parent, transform.position + Vector3.up * 1.3f);
        }

        private void OnDestroy() { if (armor != null) armor.Broken -= Shatter; }
    }
}
