using System.Collections.Generic;
using Nomad.Combat;
using Nomad.Monsters;
using Nomad.Monsters.Demo;
using Nomad.Terrain;
using UnityEngine;

namespace Nomad.Game.Modules
{
    // Field 지역 모듈: 필드 생성이 끝나면 적 생성 지점에 몬스터를 배치하고, 다시 생성하면 이전 몬스터를 지웁니다.
    // 수량·안전 거리는 #59 통합 테스트(GameplayIntegration)와 같은 임시값이며 최종 난이도가 아닙니다.
    [RequireComponent(typeof(MonsterActorFactory))]
    public sealed class FieldMonsters : MonoBehaviour
    {
        [SerializeField, Min(0)] private int normalCount = 6;
        [SerializeField, Min(0)] private int eliteCount = 1;
        [SerializeField, Min(0)] private int bossCount = 1;

        [Tooltip("플레이어 시작점에서 이 거리 안의 생성 지점에는 배치하지 않습니다.")]
        [SerializeField, Min(0f)] private float safeRadius = 12f;

        private readonly List<Health> enemies = new List<Health>();
        private MonsterActorFactory factory;
        private FieldNavigation navigation;
        private GameObject enemyRoot;
        private string generationId;

        public IReadOnlyList<Health> Enemies => enemies;

        private void Start()
        {
            factory = GetComponent<MonsterActorFactory>();
            navigation = FindInScene<FieldNavigation>();
            if (navigation == null)
            {
                Debug.LogError("몬스터를 배치할 FieldNavigation이 이 지역 씬에 없습니다.", this);
                return;
            }

            navigation.GenerationStarted += OnGenerationStarted;
            navigation.GenerationCompleted += OnGenerationCompleted;

            // 단독 Play에서는 이 모듈보다 필드 생성이 먼저 끝날 수 있어 마지막 결과도 소비합니다.
            if (navigation.LastResult != null)
                OnGenerationCompleted(navigation.LastResult);
        }

        private void OnDestroy()
        {
            if (navigation == null)
                return;

            navigation.GenerationStarted -= OnGenerationStarted;
            navigation.GenerationCompleted -= OnGenerationCompleted;
        }

        private void OnGenerationStarted(string id) => Clear();

        private void OnGenerationCompleted(FieldGenerationResult field)
        {
            if (!field.Success || field.GenerationId == generationId)
                return;

            Health target = GameManager.Instance != null && GameManager.Instance.Player != null
                ? GameManager.Instance.Player.GetComponent<Health>() : null;
            if (target == null)
            {
                Debug.LogError("몬스터가 노릴 Player Health가 없습니다.", this);
                return;
            }

            Clear();
            generationId = field.GenerationId;
            enemyRoot = new GameObject("Field Enemies " + generationId);
            enemyRoot.transform.SetParent(transform, false);

            int normals = 0;
            int elites = 0;
            int bosses = 0;
            foreach (FieldSpawnPoint point in Shuffle(field.EnemyPoints, field.Seed))
            {
                if (normals >= normalCount && elites >= eliteCount && bosses >= bossCount)
                    break;
                if (!CanSpawn(point, field.PlayerPosition))
                    continue;

                // 일반을 먼저 채우고 엘리트·보스 순으로 배치합니다. 일반은 검·석궁·지팡이를 번갈아 씁니다.
                MonsterRank rank = normals < normalCount ? MonsterRank.Normal
                    : elites < eliteCount ? MonsterRank.Elite : MonsterRank.Boss;
                MonsterTestSettings settings = MonsterTestSettings.Defaults(rank);
                if (rank == MonsterRank.Normal)
                    settings.normalStyle = normals % 3;

                enemies.Add(factory.CreateRanked(target, settings, rank, point.Position, point.Rotation,
                    rank + " " + point.Id, enemyRoot.transform));

                if (rank == MonsterRank.Boss) bosses++;
                else if (rank == MonsterRank.Elite) elites++;
                else normals++;
            }
        }

        // 시작점과 너무 가깝거나 다른 충돌체와 겹치는 지점은 건너뜁니다.
        private bool CanSpawn(FieldSpawnPoint point, Vector3 playerPosition)
        {
            if (Vector3.Distance(point.Position, playerPosition) < safeRadius)
                return false;

            return !Physics.CheckCapsule(point.Position + Vector3.up * 0.4f, point.Position + Vector3.up * 1.5f,
                0.35f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        // 같은 seed는 같은 배치가 되도록 seed로 섞습니다.
        private static List<FieldSpawnPoint> Shuffle(IReadOnlyList<FieldSpawnPoint> points, int seed)
        {
            var list = new List<FieldSpawnPoint>(points);
            var random = new System.Random(seed);
            for (int index = list.Count - 1; index > 0; index--)
            {
                int other = random.Next(index + 1);
                (list[index], list[other]) = (list[other], list[index]);
            }

            return list;
        }

        // 지형이 교체되는 프레임에 이전 몬스터가 움직이지 않도록 바로 끄고 지웁니다.
        private void Clear()
        {
            if (enemyRoot != null)
            {
                enemyRoot.SetActive(false);
                Destroy(enemyRoot);
                enemyRoot = null;
            }

            enemies.Clear();
            generationId = null;
        }

        private T FindInScene<T>() where T : Component
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
