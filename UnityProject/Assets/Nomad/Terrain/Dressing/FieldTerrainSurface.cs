using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Terrain
{
    // 3m 타일 필드의 높이(평지 0/1/2m, 계단 칸은 연속 경사)를 하나의 Unity Terrain으로 옮깁니다.
    // 타일의 외형과 BoxCollider를 끄고 TerrainCollider가 보행 면이 됩니다. 계단 칸은 걸을 수 있는 경사로가 되고,
    // 높이 차 경계는 샘플 하나(0.25m 이하) 폭의 급경사라 NavMesh 최대 경사(45°)를 넘어 그대로 절벽입니다.
    public sealed class FieldTerrainSurface
    {
        private const float MaxSampleSpacing = 0.25f;
        private const float HeightRange = 4f;
        private const float RockSteepness = 50f;

        private static readonly Color Grass = new Color(0.34f, 0.43f, 0.2f);
        private static readonly Color Dirt = new Color(0.45f, 0.36f, 0.24f);
        private static readonly Color Rock = new Color(0.42f, 0.4f, 0.37f);

        private readonly List<Object> owned = new List<Object>();

        public UnityEngine.Terrain Terrain { get; private set; }

        public UnityEngine.Terrain Build(TerrainGenerationResult result, int seed)
        {
            Release();
            Transform root = result.Pieces[0].transform.parent;
            var cells = new Dictionary<Vector2Int, TerrainPiece>();
            Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);
            Vector2Int max = new Vector2Int(int.MinValue, int.MinValue);
            foreach (TerrainPiece piece in result.Pieces)
            {
                Vector2Int cell = Cell(piece.transform.position);
                cells[cell] = piece;
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
            data.SetHeights(0, 0, SampleHeights(cells, min, max, origin, extent, resolution));
            owned.Add(data);

            PaintLayers(data, cells, min, origin, extent, seed);
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

        private static float[,] SampleHeights(Dictionary<Vector2Int, TerrainPiece> cells, Vector2Int min, Vector2Int max, Vector3 origin,
            Vector2 extent, int resolution)
        {
            var heights = new float[resolution, resolution];
            for (int row = 0; row < resolution; row++)
            for (int column = 0; column < resolution; column++)
            {
                var world = new Vector3(origin.x + extent.x * column / (resolution - 1), 0f,
                    origin.z + extent.y * row / (resolution - 1));
                heights[row, column] = Mathf.Clamp01((HeightAt(cells, min, max, world) - origin.y) / HeightRange);
            }
            return heights;
        }

        // 해당 칸 조각의 윗면 높이입니다. 계단 칸은 계단 대신 연속 경사(ContinuousHeight)를 씁니다.
        private static float HeightAt(Dictionary<Vector2Int, TerrainPiece> cells, Vector2Int min, Vector2Int max, Vector3 world)
        {
            // 격자 바깥 끝 샘플은 가장 가까운 안쪽 칸을 씁니다.
            Vector2Int cell = Vector2Int.Min(Vector2Int.Max(Cell(world), min), max);
            if (!cells.TryGetValue(cell, out TerrainPiece piece)) return 0f;

            MicroTerrainTile tile = piece.GetComponent<MicroTerrainTile>();
            Vector3 local = piece.transform.InverseTransformPoint(world);
            float half = MicroTerrainTile.Size * 0.5f;
            return piece.transform.position.y
                + tile.ContinuousHeight(Mathf.Clamp(local.x, -half, half), Mathf.Clamp(local.z, -half, half));
        }

        // 급경사는 암벽색, 경사로와 저주파 노이즈 일부는 흙, 나머지는 잔디로 칠합니다.
        private void PaintLayers(TerrainData data, Dictionary<Vector2Int, TerrainPiece> cells, Vector2Int min,
            Vector3 origin, Vector2 extent, int seed)
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
                bool ramp = cells.TryGetValue(Cell(world), out TerrainPiece piece)
                    && piece.GetComponent<MicroTerrainTile>().IsStair;
                float patch = Mathf.InverseLerp(0.62f, 0.72f, Mathf.PerlinNoise(noise.x + world.x * 0.09f, noise.y + world.z * 0.09f));
                float dirt = (1f - rock) * Mathf.Max(ramp ? 0.85f : 0f, patch * 0.8f);

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
                texture.SetPixel(x, y, color * Mathf.Lerp(0.975f, 1.025f, value));
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
