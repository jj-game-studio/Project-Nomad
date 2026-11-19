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

        [Tooltip("Game 씬에서 시작할 때 처음 불러올 지역 씬 이름")]
        [SerializeField] private string startRegion = "Village";

        [Tooltip("지역 씬 이름. 이 씬을 단독 Play하면 GameManager가 자동으로 생성됩니다.")]
        [SerializeField] private string[] regionScenes = { "Village" };

        [Tooltip("GameManager 아래에 한 번 생성되어 지역 전환 후에도 유지되는 모듈 프리팹")]
        [SerializeField] private GameObject[] modules = Array.Empty<GameObject>();

        public string StartRegion => startRegion;
        public IReadOnlyList<string> RegionScenes => regionScenes;
        public IReadOnlyList<GameObject> Modules => modules;

        public bool IsRegion(string sceneName) => Array.IndexOf(regionScenes, sceneName) >= 0;

        public static GameConfig Load() => Resources.Load<GameConfig>(ResourcePath);
    }
}
