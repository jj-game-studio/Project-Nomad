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
        public int RockCount { get; private set; }

        public void PrepareCombat(Health boss, Health player)
        {
            ClearForeignDressing();
            var rocks = new GameObject("Boss arena rocks");
            rocks.transform.SetParent(transform, false);
            BossEncounterContent content = FindFirstObjectByType<BossEncounterContent>();
            GameObject prefab = content != null ? content.Rock : Resources.Load<GameObject>(RockResource);
            foreach (Vector2 offset in new[] { new Vector2(-7, -5), new Vector2(7, -5), new Vector2(-8, 2),
                new Vector2(8, 2), new Vector2(-5, 8), new Vector2(5, 8) })
            {
                Vector3 point = Center + Quaternion.LookRotation(Forward) * new Vector3(offset.x, 0, offset.y);
                GameObject rock = prefab != null ? Instantiate(prefab, rocks.transform) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = "Breakable arena boulders";
                rock.transform.SetParent(rocks.transform, false);
                rock.transform.SetPositionAndRotation(point, Quaternion.Euler(0, RockCount * 67, 0));
                foreach (Collider collider in rock.GetComponentsInChildren<Collider>()) { collider.enabled = false; Destroy(collider); }
                Renderer[] renderers = rock.GetComponentsInChildren<Renderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float scale = 2f / Mathf.Max(bounds.size.x, bounds.size.z);
                rock.transform.localScale *= scale;
                bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                rock.transform.position += point - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

                var collision = new GameObject("Rock collision");
                collision.transform.SetParent(rock.transform, true);
                collision.transform.SetPositionAndRotation(point + Vector3.up * 0.7f, Quaternion.identity);
                // 장식 프리팹의 피벗과 무관한 월드 크기로 통행/돌진 충돌을 맞춥니다.
                collision.AddComponent<BoxCollider>().size = new Vector3(1.9f, 1.4f, 1.9f);
                NavMeshObstacle obstacle = collision.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = new Vector3(1.9f, 1.4f, 1.9f);
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
                rock.AddComponent<BossBreakableRock>();
                RockCount++;
            }
            boss.gameObject.AddComponent<DragonBossCombat>().Configure(this, player);
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
