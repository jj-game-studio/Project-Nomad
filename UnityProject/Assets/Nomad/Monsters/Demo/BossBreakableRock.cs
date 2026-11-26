using Nomad.Equipment;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed class BossBreakableRock : MonoBehaviour
    {
        public bool Broken { get; private set; }
        public bool Shatter()
        {
            if (Broken) return false;
            Broken = true;
            Vector3 center = transform.position + Vector3.up * 0.6f;
            Material material = GetComponentInChildren<Renderer>().sharedMaterial;
            for (int index = 0; index < 6; index++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "Rock debris";
                shard.transform.SetParent(transform.parent, true);
                shard.transform.position = center + Random.insideUnitSphere * 0.3f;
                shard.transform.localScale = Vector3.one * Random.Range(0.15f, 0.35f);
                shard.GetComponent<Renderer>().sharedMaterial = material;
                shard.GetComponent<Collider>().enabled = false;
                Destroy(shard.GetComponent<Collider>());
                Rigidbody body = shard.AddComponent<Rigidbody>();
                body.linearVelocity = Random.insideUnitSphere * 3 + Vector3.up * 3;
                body.angularVelocity = Random.insideUnitSphere * 6;
                Destroy(shard, 1.5f);
            }
            WeaponVfx.Burst(center, new WeaponVfxStyle(new Color(0.8f, 0.7f, 0.5f), new Color(0.4f, 0.35f, 0.25f), 0.9f));
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }
    }
}
