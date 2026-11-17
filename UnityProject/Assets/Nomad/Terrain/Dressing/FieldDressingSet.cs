using UnityEngine;

namespace Nomad.Terrain
{
    // 필드 꾸밈에 사용할 구매 애셋 목록입니다. 실제 에셋은 비공개 저장소의 Resources에 둡니다.
    [CreateAssetMenu(menuName = "Nomad/Terrain/Field Dressing Set")]
    public sealed class FieldDressingSet : ScriptableObject
    {
        public const string ResourcePath = "Nomad/FieldDressingSet";

        [Header("지면")]
        [Tooltip("생성 타일의 외형 재질을 교체합니다. 비어 있으면 기존 재질을 유지합니다.")]
        public Material groundMaterial;

        [Header("충돌 없음")]
        public GameObject[] groundCover;
        public GameObject[] cliffRocks;

        [Header("이동을 막음 (BoxCollider로 경로 검사에 포함)")]
        public GameObject[] trees;
        public GameObject[] cover;

        [Header("밀도 (타일당 확률 또는 개수)")]
        [Range(0, 8)] public int groundCoverPerTile = 3;
        [Range(0f, 1f)] public float treeChance = 0.12f;
        [Range(0f, 1f)] public float coverChance = 0.06f;
    }
}
