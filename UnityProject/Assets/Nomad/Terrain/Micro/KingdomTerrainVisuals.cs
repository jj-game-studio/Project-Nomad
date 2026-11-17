using System;
using UnityEngine;

namespace Nomad.Terrain
{
    // 구매 애셋에서 만든 메시와 재질은 비공개 Resources 라이브러리에만 보관합니다.
    public sealed class KingdomTerrainVisuals : ScriptableObject
    {
        public const string ResourcePath = "Nomad/KingdomTerrainVisuals";

        [Serializable]
        public sealed class Entry
        {
            public MicroTerrainShape shape;
            public int level;
            public Mesh mesh;
        }

        public Material material;
        public Material sides;
        public Entry[] entries = Array.Empty<Entry>();

        public static bool Apply(MicroTerrainTile tile)
        {
            KingdomTerrainVisuals library = Resources.Load<KingdomTerrainVisuals>(ResourcePath);
            if (library == null) return false;

            foreach (Entry entry in library.entries)
            {
                if (entry.shape != tile.Shape || entry.level != tile.Level) continue;

                Transform visual = tile.transform.Find("Visual");
                if (visual == null || entry.mesh == null || library.material == null || library.sides == null)
                    throw new InvalidOperationException("Kingdom 지형 외형 데이터가 유효하지 않습니다.");

                visual.GetComponent<MeshFilter>().sharedMesh = entry.mesh;
                visual.GetComponent<MeshRenderer>().sharedMaterials = new[] { library.material, library.sides };
                return true;
            }

            throw new InvalidOperationException($"Kingdom 지형 외형 누락: {tile.Shape}/{tile.Level}");
        }
    }
}
