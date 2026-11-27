using Nomad.Combat;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    [DefaultExecutionOrder(220)]
    public sealed class BossFallingRock : MonoBehaviour
    {
        public const float FallSeconds = 0.65f;
        private BossArena arena;
        private Health player;
        private GameObject visual;
        private Vector3 landing, offset;
        private float warning, age;

        public static BossFallingRock Create(Transform parent, BossArena room, Health player, Vector3 point, float warning)
        {
            var root = new GameObject("Single falling boss boulder");
            root.transform.SetParent(parent, false);
            var falling = root.AddComponent<BossFallingRock>();
            falling.arena = room; falling.player = player; falling.landing = point; falling.warning = warning;
            var prefab = Resources.Load<GameObject>(BossArena.RockResource);
            falling.visual = prefab != null ? Instantiate(prefab, root.transform) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            falling.visual.transform.SetParent(root.transform, false);
            foreach (Collider collider in falling.visual.GetComponentsInChildren<Collider>()) { collider.enabled = false; Destroy(collider); }
            Renderer[] renderers = falling.visual.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            falling.visual.transform.localScale *= 3.4f / Mathf.Max(bounds.size.x, bounds.size.z);
            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            falling.offset = falling.visual.transform.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            falling.visual.AddComponent<CartoonSurfaceStyle>();
            falling.visual.SetActive(false);
            return falling;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age < warning) return;
            visual.SetActive(true);
            float t = Mathf.Clamp01((age - warning) / FallSeconds);
            visual.transform.position = landing + offset + Vector3.up * (12 * (1 - t * t));
            if (t < 1) return;
            GameObject stone = arena.LandRock(landing);
            if (stone != null)
            {
                BossStoneDebris.Create(arena.transform, landing + Vector3.up * 0.3f);
                if (player != null && !player.IsDead && Vector3.ProjectOnPlane(player.transform.position - landing, Vector3.up).magnitude < 2.1f)
                    PlayerBossReaction.Play(player, landing, 3, 0.8f, 0, 0.25f);
            }
            Destroy(gameObject);
        }
    }
}
