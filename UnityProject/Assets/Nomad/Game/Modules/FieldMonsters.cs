using System.Collections.Generic;
using Nomad.Combat;
using Nomad.Monsters;
using Nomad.Monsters.Demo;
using Nomad.Terrain;
using UnityEngine;

namespace Nomad.Game.Modules
{
    // Field 지역 모듈: 필드 생성이 끝나면 적 생성 지점에 몬스터를 배치하고, 다시 생성하면 이전 몬스터를 지웁니다.
    // 맵 전체에 고르게 퍼진 일반 몬스터, 먼 엘리트, 별도 보스 구역의 조우 배치를 사용합니다(#39, #154).
    // 26×26칸 필드(검토 씬의 약 2배 면적)에 맞춰 일반·엘리트 수를 2배로 둡니다.
    [RequireComponent(typeof(MonsterActorFactory))]
    public sealed class FieldMonsters : MonoBehaviour
    {
        [SerializeField, Min(0)] private int normalCount = 24;
        [SerializeField, Min(0)] private int eliteCount = 2;
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

            enemies.AddRange(MonsterEncounterLayout.Spawn(field, enemyRoot.transform, factory,
                target, normalCount, eliteCount, bossCount, safeRadius));
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
