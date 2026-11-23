using System;
using UnityEngine;

namespace Nomad.Terrain
{
    // 필드 꾸밈에 사용할 구매 애셋 목록입니다. 실제 에셋은 비공개 저장소의 Resources에 둡니다.
    [CreateAssetMenu(menuName = "Nomad/Terrain/Field Dressing Set")]
    public sealed class FieldDressingSet : ScriptableObject
    {
        public const string ResourcePath = "Nomad/FieldDressingSet";

        // 한 군락은 하나의 테마에서 식생을 골라, 같은 소품이 맵 전체에 고르게 반복되지 않게 합니다.
        [Serializable]
        public sealed class CoverTheme
        {
            public string name;
            public GameObject[] prefabs;
        }

        [Header("지면")]
        [Tooltip("생성 타일의 외형 재질을 교체합니다. 비어 있으면 기존 재질을 유지합니다.")]
        public Material groundMaterial;

        [Header("충돌 없음")]
        public CoverTheme[] coverThemes;
        public GameObject[] cliffRocks;

        [Header("이동을 막음 (BoxCollider로 경로 검사에 포함)")]
        public GameObject[] trees;
        public GameObject[] cover;
        public GameObject[] fences;
        public GameObject[] borderWalls;

        [Header("폐허 구조물 (Kingdom 집 조각을 조립, 지붕 없음)")]
        [Tooltip("돌 기단 벽 (길이 2.5m)")]
        public GameObject[] structureBases;
        [Tooltip("기단 위에 올리는 목재 벽 (길이 2.5m)")]
        public GameObject[] structureWalls;
        public GameObject[] structureCorners;
        [Tooltip("충돌 없는 바닥 판 (2.5×2.5m)")]
        public GameObject[] structureFloors;
        [Range(0, 6)] public int structureMin = 3;
        [Range(0, 6)] public int structureMax = 4;

        [Header("밀도 (나무·엄폐물·울타리 확률은 군락 타일 기준)")]
        [Tooltip("군락 노이즈가 이 값보다 높은 타일에만 식생을 둡니다. 높을수록 빈터가 많아집니다.")]
        [Range(0f, 1f)] public float coverThreshold = 0.48f;
        [Range(1, 4)] public int coverMaxPerTile = 3;
        [Range(0f, 1f)] public float treeChance = 0.2f;
        [Range(0f, 1f)] public float coverChance = 0.08f;
        [Range(0f, 1f)] public float fenceChance = 0.06f;
        [Range(0f, 1f)] public float cliffRockChance = 0.4f;
    }
}
