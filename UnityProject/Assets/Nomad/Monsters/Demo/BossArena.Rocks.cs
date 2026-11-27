using System.Linq;
using Nomad.Combat;
using Nomad.Terrain;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters.Demo
{
    public sealed partial class BossArena
    {
        public const string RockResource = "BossArenaRock";
        public const int InitialRockCount = 8;
        public int RockCount => GetComponentsInChildren<BossBreakableRock>().Count(rock => !rock.Broken);
        private Transform rockRoot;
        private GameObject rockPrefab;
        private int rocksCreated;

        public void PrepareCombat(Health boss, Health player)
        {
            ClearForeignDressing();
            var rocks = new GameObject("Boss arena rocks");
            rocks.transform.SetParent(transform, false);
            rockRoot = rocks.transform;
            BossEncounterContent content = FindFirstObjectByType<BossEncounterContent>();
            rockPrefab = content != null ? content.Rock : Resources.Load<GameObject>(RockResource);
            if (player.GetComponent<Nomad.WorldHud.CharacterHealthHud>() == null) player.gameObject.AddComponent<Nomad.WorldHud.CharacterHealthHud>();
            foreach (Vector2 offset in new[] { new Vector2(-14, -14), new Vector2(14, -14), new Vector2(-18, 4),
                new Vector2(18, 4), new Vector2(-12, 16), new Vector2(12, 16), new Vector2(-7, -1), new Vector2(7, -1) })
                CreateRock(GroundPoint(Center + Quaternion.LookRotation(Forward) * new Vector3(offset.x, 0, offset.y)));
            boss.gameObject.AddComponent<BossArmor>().Configure(content != null ? content.BossHealth : BossArmor.ReferenceHealth);
            boss.gameObject.AddComponent<DragonBossCombat>().Configure(this, player);
            boss.gameObject.AddComponent<BossStrikeFeedback>().Configure(player.transform);
        }

        private GameObject CreateRock(Vector3 point)
        {
            GameObject rock = rockPrefab != null ? Instantiate(rockPrefab, rockRoot) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Breakable arena boulders";
            rock.transform.SetParent(rockRoot, false);
            rock.transform.SetPositionAndRotation(point, Quaternion.Euler(0, rocksCreated * 67, 0));
            foreach (Collider collider in rock.GetComponentsInChildren<Collider>()) { collider.enabled = false; Destroy(collider); }
            Renderer[] renderers = rock.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float scale = 3.4f / Mathf.Max(bounds.size.x, bounds.size.z);
            rock.transform.localScale *= scale;
            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            rock.transform.position += point - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            var collision = new GameObject("Rock collision");
            collision.transform.SetParent(rock.transform, true);
            collision.transform.SetPositionAndRotation(point + Vector3.up * 1.1f, Quaternion.identity);
            // 장식 프리팹의 피벗과 무관한 월드 크기로 통행/돌진 충돌을 맞춥니다.
            collision.AddComponent<BoxCollider>().size = new Vector3(3.2f, 2.2f, 3.2f);
            NavMeshObstacle obstacle = collision.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(3.2f, 2.2f, 3.2f);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            rock.AddComponent<BossBreakableRock>();
            rocksCreated++;
            return rock;
        }

        public int ScatterParryRocks(Vector3 player, Vector3 boss, int count)
        {
            // 유도용 돌이 계속 누적되어 방을 막지 않게 먼 오래된 돌부터 교체합니다.
            int replace = Mathf.Max(0, RockCount + count - 16);
            foreach (BossBreakableRock old in GetComponentsInChildren<BossBreakableRock>()
                .Where(rock => Vector3.Distance(rock.transform.position, player) > 6 && Vector3.Distance(rock.transform.position, boss) > 6)
                .Take(replace)) old.Shatter();

            int spawned = 0;
            for (int attempt = 0; attempt < 160 && spawned < count; attempt++)
            {
                float angle = (rocksCreated * 137.5f + attempt * 47) * Mathf.Deg2Rad;
                float radius = 7 + (attempt % 4) * 3.4f;
                Vector3 point = GroundPoint(Center + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius);
                Vector3 local = Quaternion.Inverse(Quaternion.LookRotation(Forward)) * (point - Center);
                if (Mathf.Abs(local.x) > HalfSize - 3 || Mathf.Abs(local.z) > HalfSize - 3 ||
                    (local.z < -HalfSize + 7 && Mathf.Abs(local.x) < 5) || Vector3.Distance(point, player) < 5 || Vector3.Distance(point, boss) < 6) continue;
                if (GetComponentsInChildren<BossBreakableRock>().Any(rock => Vector3.Distance(rock.transform.position, point) < 6)) continue;
                GameObject rock = CreateRock(point);
                rock.AddComponent<BossRockEruption>().Configure();
                spawned++;
            }
            return spawned;
        }

        private void ClearForeignDressing()
        {
            foreach (FieldDressing dressing in FindObjectsByType<FieldDressing>(FindObjectsSortMode.None))
            {
                if (dressing.Root == null) continue;
                Transform[] roots = dressing.Root.Cast<Transform>().SelectMany(child => child.name == "Outer Forest"
                    ? child.Cast<Transform>() : new[] { child }).ToArray();
                foreach (Transform decoration in roots)
                {
                    // 지면 메시 자체는 유지하고 방 안을 침범하는 개별 돌/나무만 치웁니다.
                    if (!decoration.name.StartsWith("SM_")) continue;
                    Vector3 local = Quaternion.Inverse(Quaternion.LookRotation(Forward)) * (decoration.position - Center);
                    Renderer renderer = decoration.GetComponentInChildren<Renderer>();
                    float margin = renderer != null ? Mathf.Min(4, renderer.bounds.extents.magnitude) : 1;
                    if (Mathf.Abs(local.x) > HalfSize + margin || Mathf.Abs(local.z) > HalfSize + margin) continue;
                    decoration.gameObject.SetActive(false);
                    Destroy(decoration.gameObject);
                }
            }
        }
    }
}
