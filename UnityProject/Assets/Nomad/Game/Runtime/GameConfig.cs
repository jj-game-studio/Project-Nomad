using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Game
{
    // 메인 게임 구성입니다. 기능 추가는 Game 씬이 아니라 이 에셋(Resources/GameConfig)만 수정합니다.
    [CreateAssetMenu(menuName = "Nomad/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "GameConfig";

        // 특정 지역에 들어갈 때마다 그 지역 씬 안에 생성되고, 지역과 함께 사라지는 모듈입니다(예: 필드 몬스터 배치).
        [Serializable]
        public sealed class RegionModules
        {
            public string region;
            public GameObject[] prefabs = Array.Empty<GameObject>();
        }

        [Tooltip("Game 씬에서 시작할 때 처음 불러올 지역 씬 이름")]
        [SerializeField] private string startRegion = "Village";

        [Tooltip("지역 씬 이름. 이 씬을 단독 Play하면 GameManager가 자동으로 생성됩니다.")]
        [SerializeField] private string[] regionScenes = { "Village" };

        [Tooltip("GameManager 아래에 한 번 생성되어 지역 전환 후에도 유지되는 모듈 프리팹")]
        [SerializeField] private GameObject[] modules = Array.Empty<GameObject>();

        [Tooltip("지역에 들어갈 때 그 지역 씬에 생성되고 지역과 함께 사라지는 모듈 프리팹")]
        [SerializeField] private RegionModules[] regionModules = Array.Empty<RegionModules>();

        public string StartRegion => startRegion;
        public IReadOnlyList<string> RegionScenes => regionScenes;
        public IReadOnlyList<GameObject> Modules => modules;

        public bool IsRegion(string sceneName) => Array.IndexOf(regionScenes, sceneName) >= 0;

        public IEnumerable<GameObject> GetRegionModules(string region)
        {
            foreach (RegionModules entry in regionModules)
            {
                if (entry == null || entry.region != region || entry.prefabs == null)
                    continue;

                foreach (GameObject prefab in entry.prefabs)
                {
                    if (prefab != null)
                        yield return prefab;
                }
            }
        }

        public static GameConfig Load() => Resources.Load<GameConfig>(ResourcePath);
    }
}
