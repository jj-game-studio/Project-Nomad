using System.Collections.Generic;
using Nomad.Equipment;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class BossRuinsDressing : MonoBehaviour
    {
        private readonly List<Material> materials = new();
        private BossArena arena;
        private Material stone, edge, red, gold, lava;
        private Transform player;
        private Camera skyCamera;
        private CameraClearFlags previousFlags;
        private Color previousBackground;

        public void Build(BossArena room)
        {
            arena = room;
            stone = Material(new Color(0.18f, 0.15f, 0.19f));
            edge = Material(new Color(0.32f, 0.27f, 0.3f));
            red = Material(new Color(0.38f, 0.035f, 0.045f));
            gold = Material(new Color(0.62f, 0.37f, 0.16f));
            lava = new Material(Resources.Load<Shader>("BossLava"));
            materials.Add(lava);
            Primitive("Lava sea", PrimitiveType.Cylinder, arena.Center - Vector3.up * 4,
                new Vector3(105, 0.25f, 105), Quaternion.identity, lava);
            Primitive("Basalt arena foundation", PrimitiveType.Cylinder, arena.Center - Vector3.up * 2.2f,
                new Vector3(44, 2, 44), Quaternion.identity, stone);

            Quaternion facing = Quaternion.LookRotation(arena.Forward);
            for (int index = 0; index < 32; index++)
            {
                Quaternion turn = facing * Quaternion.Euler(0, index * 360f / 32, 0);
                Vector3 point = arena.Center + turn * Vector3.forward * (27 + index % 3 * 2.5f);
                float height = 6 + index * 7 % 8;
                Cliff(point + Vector3.up * (height * 0.5f - 6), new Vector3(5, height, 5), turn * Quaternion.Euler(index % 3 * 4, 0, 8), index % 2 == 0 ? stone : edge);
                if (index % 4 == 0)
                    Primitive("Lava fall", PrimitiveType.Cube, point - turn * Vector3.forward * 2.35f - Vector3.up,
                        new Vector3(0.7f, 8, 0.08f), turn, lava);
            }

            for (int index = 0; index < 8; index++)
            {
                Quaternion turn = facing * Quaternion.Euler(0, index * 45 + 22.5f, 0);
                Vector3 point = arena.GroundPoint(arena.Center + turn * Vector3.forward * 21.6f);
                Primitive("Ruined basalt pillar", PrimitiveType.Cube, point + Vector3.up * 2,
                    new Vector3(1.3f, 4 + index % 2, 1.3f), turn, stone);
                Primitive("Pillar crown", PrimitiveType.Cube, point + Vector3.up * 4.1f,
                    new Vector3(1.7f, 0.3f, 1.7f), turn, edge);
                Primitive("Crimson banner", PrimitiveType.Cube, point - turn * Vector3.forward * 0.73f + Vector3.up * 2.5f,
                    new Vector3(0.8f, 1.9f, 0.05f), turn, red);
                Primitive("Banner gold sigil", PrimitiveType.Cube, point - turn * Vector3.forward * 0.77f + Vector3.up * 2.6f,
                    new Vector3(0.08f, 0.75f, 0.035f), turn * Quaternion.Euler(0, 0, 20), gold);
                Flame(point - turn * Vector3.forward * 1.2f + Vector3.up * 0.8f);
            }

            Vector3 arch = arena.Center + arena.Forward * 23;
            for (int index = 0; index < 12; index++)
            {
                if (index is 5 or 6) continue;
                float angle = (15 + index * 150f / 11) * Mathf.Deg2Rad;
                Vector3 point = arch + facing * new Vector3(Mathf.Cos(angle) * 5, Mathf.Sin(angle) * 5, 0);
                Primitive("Broken ceremonial arch", PrimitiveType.Cube, point, new Vector3(1.5f, 1.1f, 1.4f),
                    facing * Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg - 90), edge);
            }
            BuildMasonry(facing);
            foreach (float radius in new[] { 7f, 13f, 19f }) Ring(radius);
            for (int index = 0; index < 24; index++) Crack(index, facing);
        }

        private void Ring(float radius)
        {
            var line = new GameObject("Ancient stone engraving").AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = WeaponVfx.Plain;
            line.startColor = line.endColor = new Color(0.4f, 0.29f, 0.18f, 0.6f);
            line.widthMultiplier = 0.04f;
            line.loop = true;
            line.positionCount = 128;
            for (int index = 0; index < 128; index++)
            {
                float angle = index * Mathf.PI * 2 / 128;
                line.SetPosition(index, arena.GroundPoint(arena.Center + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius) + Vector3.up * 0.035f);
            }
        }

        private void Cliff(Vector3 point, Vector3 size, Quaternion facing, Material material)
        {
            GameObject prefab = Resources.Load<GameObject>(BossArena.RockResource);
            if (prefab == null) { Primitive("Volcanic cliff", PrimitiveType.Cube, point, size, facing, material); return; }
            GameObject root = Instantiate(prefab, transform);
            root.name = "Chiseled volcanic cliff";
            root.transform.SetPositionAndRotation(point, facing);
            foreach (Collider collider in root.GetComponentsInChildren<Collider>()) { collider.enabled = false; Destroy(collider); }
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            root.transform.localScale = Vector3.Scale(root.transform.localScale,
                new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z));
            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) { bounds.Encapsulate(renderer.bounds); renderer.sharedMaterial = material; }
            root.transform.position += point - bounds.center;
        }

        private void Crack(int index, Quaternion facing)
        {
            Quaternion turn = facing * Quaternion.Euler(0, index * 137.5f, 0);
            Vector3 point = arena.GroundPoint(arena.Center + turn * Vector3.forward * (9 + index % 4 * 3));
            Primitive("Fractured stone slab", PrimitiveType.Cube, point - Vector3.up * 0.06f,
                new Vector3(2 + index % 3, 0.1f, 0.06f), turn, index % 4 == 0 ? gold : stone);
        }

        private void Flame(Vector3 point)
        {
            Primitive("Brazier", PrimitiveType.Cylinder, point - Vector3.up * 0.3f, new Vector3(0.85f, 0.3f, 0.85f), Quaternion.identity, edge);
            ParticleSystem particles = WeaponVfx.CreateParticles(transform, "Brazier fire", WeaponVfx.Glow);
            var main = particles.main;
            main.startLifetime = 0.6f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startColor = new Color(2, 0.45f, 0.03f);
            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 14;
            particles.transform.position = point;
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = 1.4f;
            WeaponVfx.Shrink(particles);
        }

        private GameObject Primitive(string name, PrimitiveType type, Vector3 point, Vector3 scale, Quaternion rotation, Material material)
        {
            GameObject root = GameObject.CreatePrimitive(type);
            root.name = name;
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(point, rotation);
            root.transform.localScale = scale;
            root.GetComponent<Collider>().enabled = false;
            Destroy(root.GetComponent<Collider>());
            root.GetComponent<Renderer>().sharedMaterial = material;
            return root;
        }

        private Material Material(Color color)
        {
            var material = new Material(Resources.Load<Shader>("BossStone")) { color = color };

            materials.Add(material);
            return material;
        }
        private void Update()
        {
            if (player == null) player = FindAnyObjectByType<PlayerEquipmentCombat>()?.transform;
            bool inside = player != null && Vector3.ProjectOnPlane(player.position - arena.Center, Vector3.up).magnitude < BossArena.HalfSize + 5;
            Camera view = Camera.main;
            if (!inside || view == null) { RestoreSky(); return; }
            if (skyCamera != view)
            {
                RestoreSky();
                skyCamera = view;
                previousFlags = view.clearFlags;
                previousBackground = view.backgroundColor;
            }
            // 경기장 안에서만 화산 하늘색을 쓰고 필드/마을로 나가면 원래 값을 복구합니다.
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.095f, 0.025f, 0.035f);
        }
        private void RestoreSky()
        {
            if (skyCamera == null) return;
            skyCamera.clearFlags = previousFlags;
            skyCamera.backgroundColor = previousBackground;
            skyCamera = null;
        }
        private void OnDestroy() { RestoreSky(); foreach (Material material in materials) Destroy(material); foreach (Mesh mesh in masonryMeshes) Destroy(mesh); }
    }
}
