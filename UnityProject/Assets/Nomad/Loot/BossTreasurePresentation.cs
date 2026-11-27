using Nomad.Equipment;
using Nomad.Monsters.Demo;
using UnityEngine;

namespace Nomad.Loot
{
    public sealed partial class BossTreasurePresentation : MonoBehaviour
    {
        private LootChest chest;
        private ParticleSystem gleam;
        private Light lamp;

        public void Build()
        {
            chest = GetComponent<LootChest>();
            GameObject prefab = Resources.Load<GameObject>("BossTreasureChest");
            if (prefab == null) return;
            foreach (string state in new[] { "Closed", "Open", "Empty" })
            {
                Transform holder = transform.Find(state);
                foreach (Renderer old in holder.GetComponentsInChildren<Renderer>(true)) old.enabled = false;
                GameObject model = Instantiate(prefab, holder);
                model.name = "Gilded royal treasure chest";
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                model.transform.localScale *= 2.8f / bounds.size.x;
                bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                model.transform.position += transform.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                foreach (Collider collider in model.GetComponentsInChildren<Collider>()) { collider.enabled = false; Destroy(collider); }
                Transform lid = System.Array.Find(model.GetComponentsInChildren<Transform>(), bone => bone.name.EndsWith("_Lid"));
                if (state != "Closed" && lid != null) lid.localRotation *= Quaternion.Euler(-105, 0, 0);
                Decorate(holder, model, state == "Empty");
                model.AddComponent<CartoonSurfaceStyle>();
            }
            BoxCollider collision = GetComponent<BoxCollider>();
            collision.center = Vector3.up * 0.45f;
            collision.size = new Vector3(1.3f, 0.9f, 0.95f);
            gleam = WeaponVfx.CreateParticles(transform, "Treasure golden motes", WeaponVfx.Glow);
            var main = gleam.main;
            main.startLifetime = 1.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
            main.startColor = new Color(2, 1.3f, 0.15f);
            var shape = gleam.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.2f, 0.1f, 0.75f);
            var emission = gleam.emission;
            emission.enabled = true;
            emission.rateOverTime = 14;
            var velocity = gleam.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = 0.7f;
            gleam.transform.localPosition = Vector3.up * 0.5f;
            WeaponVfx.Fade(gleam, 0);
            lamp = new GameObject("Treasure amber light").AddComponent<Light>();
            lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = Vector3.up;
            lamp.color = new Color(1, 0.6f, 0.12f);
            lamp.range = 5;
            lamp.intensity = 2;
        }

        private void Update()
        {
            if (lamp == null) return;
            bool remains = chest.Status != LootStatus.Empty;
            lamp.enabled = remains;
            var emission = gleam.emission;
            emission.enabled = remains;
        }
    }
}
