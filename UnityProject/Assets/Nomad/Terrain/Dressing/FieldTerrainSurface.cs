using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Terrain
{
    // 생성 필드 범위를 seed 기반 Perlin(fBm) 언덕 지형으로 덮습니다. 높이는 0/1/2m 세 단(넓은 평지)으로 계단화하고,
    // 단 사이는 걸을 수 있는 완만한 경사(약 30° 이하)로 잇습니다. 수직 절벽은 만들지 않습니다.
    // WFC 타일은 기존 계약(결과·후보 순회·연결 정보)을 위한 논리 칸으로 남기고 외형과 BoxCollider만 끕니다.
    public sealed class FieldTerrainSurface
    {
        private const float MaxSampleSpacing = 0.25f;
        private const float HeightRange = 4f;
        private const float RockSteepness = 50f;

        // 언덕 크기(약 28m 주기)와 단 사이 경사 구간 비율입니다. 구간이 넓을수록 경사가 완만합니다.
        private const float HillFrequency = 1f / 28f;
        private const float SlopeStart = 0.5f;

        private static readonly Color Grass = new Color(0.34f, 0.43f, 0.2f);
        private static readonly Color Dirt = new Color(0.45f, 0.36f, 0.24f);
        private static readonly Color Rock = new Color(0.42f, 0.4f, 0.37f);

        private readonly List<Object> owned = new List<Object>();

        public UnityEngine.Terrain Terrain { get; private set; }

        public UnityEngine.Terrain Build(TerrainGenerationResult result, int seed)
        {
            Release();
            Transform root = result.Pieces[0].transform.parent;
            Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);
            Vector2Int max = new Vector2Int(int.MinValue, int.MinValue);
            foreach (TerrainPiece piece in result.Pieces)
            {
                Vector2Int cell = Cell(piece.transform.position);
                min = Vector2Int.Min(min, cell);
                max = Vector2Int.Max(max, cell);
            }

            // 필드 격자를 정확히 덮는 크기와, 샘플 간격 0.25m 이하가 되는 2^n+1 해상도를 정합니다.
            float size = MicroTerrainTile.Size;
            var extent = new Vector2((max.x - min.x + 1) * size, (max.y - min.y + 1) * size);
            int resolution = 33;
            while (Mathf.Max(extent.x, extent.y) / (resolution - 1) > MaxSampleSpacing) resolution = (resolution - 1) * 2 + 1;
            Vector3 origin = new Vector3(min.x * size - size * 0.5f, root.position.y, min.y * size - size * 0.5f);

            var data = new TerrainData { heightmapResolution = resolution };
            data.size = new Vector3(extent.x, HeightRange, extent.y);
            data.SetHeights(0, 0, SampleHeights(origin, extent, resolution, seed));
            owned.Add(data);

            PaintLayers(data, origin, extent, seed);
            DisableTiles(result.Pieces);

            GameObject surface = UnityEngine.Terrain.CreateTerrainGameObject(data);
            surface.name = "FieldTerrain";
            surface.transform.SetParent(root, false);
            surface.transform.position = origin;
            Terrain = surface.GetComponent<UnityEngine.Terrain>();
            Terrain.drawInstanced = true;
            Physics.SyncTransforms();
            return Terrain;
        }

        private static float[,] SampleHeights(Vector3 origin, Vector2 extent, int resolution, int seed)
        {
            var offset = new Vector2(seed % 7919 * 0.731f, seed % 7907 * 0.519f);
            var heights = new float[resolution, resolution];
            for (int row = 0; row < resolution; row++)
            for (int column = 0; column < resolution; column++)
            {
                float x = origin.x + extent.x * column / (resolution - 1);
                float z = origin.z + extent.y * row / (resolution - 1);
                heights[row, column] = Mathf.Clamp01(TerracedHeight(x, z, offset) / HeightRange);
            }
            return heights;
        }

        // 두 옥타브 Perlin 값을 0~2 범위로 펴고, 각 단의 앞부분은 평지로 두고 뒷부분(SlopeStart~1)만
        // smoothstep으로 다음 단까지 올립니다. 결과는 0/1/2m 평지와 그 사이 경사입니다.
        private static float TerracedHeight(float x, float z, Vector2 offset)
        {
            float noise = Mathf.PerlinNoise(offset.x + x * HillFrequency, offset.y + z * HillFrequency) * 0.7f
                + Mathf.PerlinNoise(offset.y + x * HillFrequency * 2f, offset.x + z * HillFrequency * 2f) * 0.3f;
            float t = Mathf.Clamp01((noise - 0.25f) / 0.5f) * 2f;

            int level = Mathf.Min(Mathf.FloorToInt(t), 1);
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SlopeStart, 1f, t - level));
            return level + rise;
        }

        // 단 사이 경사는 흙길처럼, 저주파 노이즈 일부는 흙 패치, 드문 급경사는 암벽색, 나머지는 잔디로 칠합니다.
        private void PaintLayers(TerrainData data, Vector3 origin, Vector2 extent, int seed)
        {
            data.terrainLayers = new[]
            {
                Layer(Grass, seed, 6f), Layer(Dirt, seed + 1, 4f), Layer(Rock, seed + 2, 3f),
            };

            int resolution = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Max(extent.x, extent.y) * 2f)), 16, 512);
            data.alphamapResolution = resolution;
            var alphas = new float[resolution, resolution, 3];
            var noise = new Vector2(seed % 997 * 0.37f, seed % 991 * 0.53f);

            for (int row = 0; row < resolution; row++)
            for (int column = 0; column < resolution; column++)
            {
                float u = (column + 0.5f) / resolution;
                float v = (row + 0.5f) / resolution;
                var world = new Vector3(origin.x + extent.x * u, 0f, origin.z + extent.y * v);

                float rock = Mathf.InverseLerp(RockSteepness - 10f, RockSteepness, data.GetSteepness(u, v));
                float slope = Mathf.InverseLerp(8f, 20f, data.GetSteepness(u, v));
                float patch = Mathf.InverseLerp(0.62f, 0.72f, Mathf.PerlinNoise(noise.x + world.x * 0.09f, noise.y + world.z * 0.09f));
                float dirt = (1f - rock) * Mathf.Max(slope * 0.85f, patch * 0.8f);

                alphas[row, column, 0] = 1f - rock - dirt;
                alphas[row, column, 1] = dirt;
                alphas[row, column, 2] = rock;
            }
            data.SetAlphamaps(0, 0, alphas);
        }

        // 구매 애셋 없이 쓰도록 단색에 아주 약한 얼룩(±2.5%)만 넣은 작은 텍스처를 만듭니다. 강하면 물결무늬로 보입니다.
        private TerrainLayer Layer(Color color, int seed, float tile)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            var offset = new Vector2(seed % 89 * 1.7f, seed % 83 * 2.3f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 가장자리가 이어지도록 원형 좌표로 노이즈를 읽습니다.
                float angleX = x * Mathf.PI * 2f / size;
                float angleY = y * Mathf.PI * 2f / size;
                float value = Mathf.PerlinNoise(offset.x + Mathf.Cos(angleX) * 1.5f + Mathf.Sin(angleY) * 0.7f,
                    offset.y + Mathf.Sin(angleX) * 1.5f + Mathf.Cos(angleY) * 0.7f);
                // Terrain Lit은 마스크 맵이 없으면 알파를 매끄러움으로 읽습니다. 알파 0으로 반들거림을 없앱니다.
                Color pixel = color * Mathf.Lerp(0.975f, 1.025f, value);
                pixel.a = 0f;
                texture.SetPixel(x, y, pixel);
            }
            texture.Apply(true);

            var layer = new TerrainLayer { diffuseTexture = texture, tileSize = new Vector2(tile, tile), smoothness = 0f, metallic = 0f };
            owned.Add(texture);
            owned.Add(layer);
            return layer;
        }

        // 타일은 생성 결과와 연결 정보로 남겨 두고, 보이는 면과 충돌만 Terrain에 넘깁니다.
        private static void DisableTiles(IReadOnlyList<TerrainPiece> pieces)
        {
            foreach (TerrainPiece piece in pieces)
            {
                foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                foreach (BoxCollider box in piece.GetComponentsInChildren<BoxCollider>(true)) box.enabled = false;
            }
        }

        private static Vector2Int Cell(Vector3 position) => new Vector2Int(
            Mathf.FloorToInt(position.x / MicroTerrainTile.Size + 0.5f), Mathf.FloorToInt(position.z / MicroTerrainTile.Size + 0.5f));

        // 실행 중 만든 TerrainData·레이어·텍스처를 정리합니다. Terrain 오브젝트는 생성 루트와 함께 정리됩니다.
        public void Release()
        {
            foreach (Object item in owned)
            {
                if (item == null) continue;
                if (Application.isPlaying) Object.Destroy(item);
                else Object.DestroyImmediate(item);
            }
            owned.Clear();
            Terrain = null;
        }
    }
}
