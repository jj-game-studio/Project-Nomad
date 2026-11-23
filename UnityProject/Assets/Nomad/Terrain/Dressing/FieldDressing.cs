using System.Collections.Generic;
using Nomad.Props;
using UnityEngine;

namespace Nomad.Terrain
{
    // 3m WFC 필드가 생성되면 식생·엄폐물·절벽 바위를 seed 기반으로 배치합니다.
    // 이동을 막는 물체는 BoxCollider만 가지며, 소품 소유자 하위에 두어 이후 경로 검사에 포함됩니다.
    public sealed partial class FieldDressing : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private static readonly Vector2Int[] Sides =
            { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };

        [SerializeField] private TerrainGenerator generator;
        [SerializeField] private PropScatter obstacleOwner;
        [Tooltip("비어 있으면 Resources의 비공개 꾸밈 목록을 사용합니다. 둘 다 없으면 꾸밈 없이 동작합니다.")]
        [SerializeField] private FieldDressingSet set;
        [SerializeField, Range(0, 64)] private int repairLimit = 40;
        [Tooltip("켜면 타일 블록 대신 Unity Terrain을 지면·충돌·경로로 사용합니다(계단 칸은 경사로).")]
        [SerializeField] private bool terrainSurface = true;

        private readonly List<GameObject> blockers = new List<GameObject>();
        private readonly FieldTerrainSurface surface = new FieldTerrainSurface();
        private UnityEngine.Terrain ground;
        private GameObject root;
        private Vector2 noiseOffset;

        public int Placed { get; private set; }
        public int Blockers => blockers.Count;
        public int RemovedBlockers { get; private set; }
        public int RemovedStructureWalls { get; private set; }
        public int RidgeSegments { get; private set; }
        public int RidgeCandidates { get; private set; }
        public Transform Root => root != null ? root.transform : null;

        // 편집 모드 검증처럼 OnEnable이 호출되지 않는 경우에도 생성 이벤트를 연결합니다.
        public void Configure(TerrainGenerator source, PropScatter owner)
        {
            Unsubscribe();
            Clear();
            generator = source;
            obstacleOwner = owner;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (generator == null) return;

            // 중복 구독을 막기 위해 먼저 해제합니다.
            Unsubscribe();
            generator.GenerationStarted += OnStarted;
            generator.GenerationCompleted += Dress;
        }

        private void Unsubscribe()
        {
            if (generator == null) return;
            generator.GenerationStarted -= OnStarted;
            generator.GenerationCompleted -= Dress;
        }

        private void OnDestroy() => Clear();

        private void OnStarted(string id) => Clear();

        public void Dress(TerrainGenerationResult result)
        {
            Clear();
            if (result == null || !result.Success || result.Pieces.Count == 0) return;

            // 이전 10m 비교 모드(L)는 꾸밈 대상이 아닙니다.
            if (result.Pieces[0].GetComponent<MicroTerrainTile>() == null) return;

            // 지면은 구매 애셋 없이 만들 수 있으므로 꾸밈 목록이 없어도 먼저 Terrain으로 바꿉니다.
            // 이후 소품 배치·경로 검사는 모두 이 Terrain을 바닥으로 사용합니다.
            ground = terrainSurface ? surface.Build(result, result.Seed) : null;

            FieldDressingSet source = set != null ? set : Resources.Load<FieldDressingSet>(FieldDressingSet.ResourcePath);
            if (source == null) return;

            root = new GameObject("FieldDressing");
            root.transform.SetParent(obstacleOwner != null ? obstacleOwner.transform : transform, false);
            var random = new System.Random(unchecked(result.Seed * 7919 + 17));
            Dictionary<Vector2Int, float> tops = ground != null ? BuildTerrainTopMap(result.Pieces) : BuildTopMap(result.Pieces);

            // 군락 밀도와 테마는 seed별로 이동한 저주파 노이즈로 정해 이웃 타일끼리 비슷해지게 합니다.
            noiseOffset = new Vector2(Range(random, 0f, 1000f), Range(random, 0f, 1000f));

            // 폐허 구조물을 먼저 짓고, 그 자리와 주변에는 식생·울타리를 두지 않습니다.
            // 별도 난수를 써서 구조물이 없을 때의 기존 꾸밈 배치가 그대로 유지되게 합니다.
            PlaceStructures(source, result.Pieces, new System.Random(unchecked(result.Seed * 4513 + 101)));

            foreach (TerrainPiece piece in result.Pieces)
            {
                MicroTerrainTile tile = piece.GetComponent<MicroTerrainTile>();
                if (tile == null) continue;
                if (!terrainSurface) ApplyGround(piece, source.groundMaterial, tile.Level + (tile.IsStair ? 0.5f : 0f));

                // Terrain 지면에서는 칸 중심의 실제 표면 높이를, 타일 지면에서는 타일 높이를 기준으로 삼습니다.
                Vector3 center = piece.transform.position + Vector3.up * tile.Level;
                center.y = GroundY(center, center.y);
                PlaceBorderWalls(source, center, tops);
                if (ground == null && tile.IsStair) continue;
                if (InStructure(center, 1.5f)) continue;

                // Terrain 언덕에는 수직 절벽이 없으므로 절벽 바위는 타일 지면에서만 둡니다.
                if (ground == null) PlaceCliffRocks(source, center, tops, random);
                else PlaceBoulder(source, center, tops, random);

                // 식생 노이즈가 낮은 빈터는 상자·전투 공간으로 비워 두고, 군락에만 식생과 장애물을 둡니다.
                float density = Density(center);
                if (density < source.coverThreshold) continue;

                PlaceGroundCover(source, center, density, random);
                PlaceBlockers(source, center, tops, random);
                PlaceFence(source, center, tops, random);
            }

            if (ground != null) PlaceRidges(source, result.Pieces);

            Physics.SyncTransforms();
            RepairPaths(result);
        }

        // 각 3m 칸의 윗면 높이입니다. 계단은 측면 판정에서 제외하도록 NaN으로 둡니다.
        private static Dictionary<Vector2Int, float> BuildTopMap(IReadOnlyList<TerrainPiece> pieces)
        {
            var tops = new Dictionary<Vector2Int, float>();
            foreach (TerrainPiece piece in pieces)
            {
                MicroTerrainTile tile = piece.GetComponent<MicroTerrainTile>();
                if (tile == null) continue;
                tops[Cell(piece.transform.position)] = tile.IsStair ? float.NaN : piece.transform.position.y + tile.Level;
            }
            return tops;
        }

        // Terrain 지면에서는 칸 안 다섯 지점이 모두 같은 높이일 때만 평지로 보고, 경사가 걸친 칸은 NaN입니다.
        // 나무·엄폐물·울타리는 같은 높이 평지끼리만 배치되므로 경사로를 막지 않습니다.
        private Dictionary<Vector2Int, float> BuildTerrainTopMap(IReadOnlyList<TerrainPiece> pieces)
        {
            var tops = new Dictionary<Vector2Int, float>();
            foreach (TerrainPiece piece in pieces)
            {
                Vector3 center = piece.transform.position;
                float height = GroundY(center, center.y);
                bool flat = true;
                foreach (Vector2Int side in Sides)
                {
                    Vector3 probe = center + new Vector3(side.x, 0f, side.y) * 1.3f;
                    flat &= Mathf.Abs(GroundY(probe, height) - height) < 0.03f;
                }
                tops[Cell(center)] = flat ? height : float.NaN;
            }
            return tops;
        }

        private float GroundY(Vector3 point, float fallback) => FieldGround.SurfaceHeight(ground, point, fallback);

        private static Vector2Int Cell(Vector3 position) => new Vector2Int(
            Mathf.RoundToInt(position.x / MicroTerrainTile.Size), Mathf.RoundToInt(position.z / MicroTerrainTile.Size));

        // 단색 지면에서도 높이 차가 읽히도록 높은 층일수록 조금 밝게 칠합니다.
        private static void ApplyGround(TerrainPiece piece, Material material, float level)
        {
            if (material == null) return;

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, material.GetColor(BaseColor) * (1f + level * 0.08f));
            foreach (MeshRenderer renderer in piece.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled) continue;
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int index = 0; index < materials.Length; index++) materials[index] = material;
                renderer.sharedMaterials = materials;
                renderer.SetPropertyBlock(block);
            }
        }

        private float Density(Vector3 center)
        {
            Vector2 cell = Cell(center);
            return Mathf.PerlinNoise(noiseOffset.x + cell.x * 0.17f, noiseOffset.y + cell.y * 0.17f);
        }

        // 노이즈가 높을수록 한 테마의 식생을 더 많이 둡니다.
        private void PlaceGroundCover(FieldDressingSet source, Vector3 center, float density, System.Random random)
        {
            if (source.coverThemes == null || source.coverThemes.Length == 0) return;

            Vector2 cell = Cell(center);
            float spread = (density - source.coverThreshold) / Mathf.Max(0.01f, 1f - source.coverThreshold);
            int count = 1 + Mathf.FloorToInt(spread * source.coverMaxPerTile);
            float themeNoise = Mathf.PerlinNoise(noiseOffset.y + cell.x * 0.08f, noiseOffset.x + cell.y * 0.08f);
            int theme = Mathf.Clamp(Mathf.FloorToInt(themeNoise * source.coverThemes.Length), 0, source.coverThemes.Length - 1);

            for (int index = 0; index < count; index++)
            {
                GameObject prefab = Pick(source.coverThemes[theme].prefabs, random);
                if (prefab == null) return;

                Vector3 offset = new Vector3(Range(random, -1.3f, 1.3f), 0f, Range(random, -1.3f, 1.3f));
                GameObject plant = Spawn(prefab, center + offset, Range(random, 0f, 360f), Range(random, 0.85f, 1.15f));

                // 폭 1m를 넘는 큰 덤불은 통과하면 어색하고 막아도 의미 있는 공간이 되지 않아 두지 않습니다.
                // 목록에서도 뺐지만, 목록을 바꿔도 다시 섞이지 않도록 배치 단계에서 한 번 더 거릅니다.
                if (TryLocalBounds(plant, out Bounds bounds)
                    && Mathf.Max(bounds.size.x, bounds.size.z) * plant.transform.localScale.x > 1f)
                {
                    plant.SetActive(false);
                    Release(plant);
                    Placed--;
                }
            }
        }

        // 내부 높이 차 경계 일부에만 원래 비율의 큰 바위를 절벽 면에 박아 넣습니다.
        // 작게 축소하면 디테일이 뭉개져 보이므로 폭 3.4m 기준으로 키우고, 남는 높이는 땅속에 묻습니다.
        // 충돌은 기존 타일이 담당합니다.
        private void PlaceCliffRocks(FieldDressingSet source, Vector3 center, Dictionary<Vector2Int, float> tops,
            System.Random random)
        {
            Vector2Int cell = Cell(center);
            foreach (Vector2Int side in Sides)
            {
                if (!tops.TryGetValue(cell + side, out float neighborTop) || float.IsNaN(neighborTop)) continue;
                if (neighborTop >= center.y - 0.01f || random.NextDouble() >= source.cliffRockChance) continue;

                GameObject prefab = Pick(source.cliffRocks, random);
                if (prefab == null || !TryLocalBounds(prefab, out Bounds bounds) || bounds.size.y < 0.01f) return;

                float scale = 3.4f / Mathf.Max(bounds.size.x, bounds.size.z);
                Vector3 outward = new Vector3(side.x, 0f, side.y);
                Vector3 edge = new Vector3(center.x, 0f, center.z) + outward * (MicroTerrainTile.Size * 0.5f);

                GameObject rock = Spawn(prefab, edge, 0f, 1f);
                rock.transform.rotation = Quaternion.LookRotation(outward) * Quaternion.Euler(0f, Range(random, -15f, 15f), 0f);
                rock.transform.localScale = Vector3.one * scale;

                // 윗면 위로는 0.1m만 드러나고, 두께의 15%만 낮은 쪽으로 튀어나오게 절벽 안에 묻습니다.
                float top = center.y + 0.1f;
                float front = bounds.size.z * scale * 0.15f - bounds.max.z * scale;
                rock.transform.position = new Vector3(edge.x, top - bounds.max.y * scale, edge.z) + outward * front;
            }
        }

        // 맵 외곽 변에 돌벽을 세웁니다. 타일 바깥에 두므로 경로 표본을 가리지 않고 맵 밖 이탈만 막습니다.
        private void PlaceBorderWalls(FieldDressingSet source, Vector3 center, Dictionary<Vector2Int, float> tops)
        {
            Vector2Int cell = Cell(center);
            for (int index = 0; index < Sides.Length; index++)
            {
                Vector2Int side = Sides[index];
                if (tops.ContainsKey(cell + side)) continue;

                // 같은 변에는 위치로 정한 같은 종류를 써서 벽 줄이 들쭉날쭉하지 않게 합니다.
                GameObject prefab = source.borderWalls == null || source.borderWalls.Length == 0 ? null
                    : source.borderWalls[Mathf.Abs(cell.x * 31 + cell.y * 17 + index) % source.borderWalls.Length];
                if (prefab == null) return;

                float bottom = float.IsNaN(tops[cell]) ? center.y : tops[cell];
                Vector3 outward = new Vector3(side.x, 0f, side.y);
                PlaceEdgeSegment(prefab, new Vector3(center.x, bottom, center.z), outward, 0.5f, removable: false);
            }
        }

        // 같은 높이 평지끼리 맞닿은 변을 따라 2~3칸짜리 울타리 줄을 드물게 둡니다.
        // 한 칸짜리 조각은 어색하므로 두 칸 미만이면 놓지 않습니다. 경로를 막으면 복구 단계에서 제거됩니다.
        private void PlaceFence(FieldDressingSet source, Vector3 center, Dictionary<Vector2Int, float> tops,
            System.Random random)
        {
            if (random.NextDouble() >= source.fenceChance) return;

            Vector2Int side = Sides[random.Next(2)];
            var along = new Vector2Int(side.y, side.x);
            int length = 2 + random.Next(2);
            Vector2Int start = Cell(center);

            for (int step = 0; step < length; step++)
            {
                Vector2Int cell = start + along * step;
                if (!SameLevel(tops, cell, center.y) || !SameLevel(tops, cell + side, center.y))
                {
                    length = step;
                    break;
                }
            }
            if (length < 2) return;

            GameObject prefab = Pick(source.fences, random);
            if (prefab == null) return;

            var outward = new Vector3(side.x, 0f, side.y);
            for (int step = 0; step < length; step++)
            {
                Vector3 cellCenter = center + new Vector3(along.x, 0f, along.y) * (MicroTerrainTile.Size * step);
                PlaceEdgeSegment(prefab, cellCenter, outward, 0f, removable: true);
            }
        }

        private static bool SameLevel(Dictionary<Vector2Int, float> tops, Vector2Int cell, float height) =>
            tops.TryGetValue(cell, out float top) && !float.IsNaN(top) && Mathf.Abs(top - height) < 0.01f;

        // 프리팹의 긴 축을 타일 변 방향(3m)에 맞추고 높이와 두께는 원래 비율을 유지합니다.
        private void PlaceEdgeSegment(GameObject prefab, Vector3 center, Vector3 outward, float outside, bool removable)
        {
            if (!TryLocalBounds(prefab, out Bounds bounds)) return;

            float depth = bounds.size.x >= bounds.size.z ? bounds.size.z : bounds.size.x;
            Vector3 position = center + outward * (MicroTerrainTile.Size * 0.5f + depth * outside);
            PlaceSegment(prefab, position, outward, removable, MicroTerrainTile.Size);
        }

        // 언덕 단 경계의 위쪽 등고선(0.85m, 1.85m)을 따라 돌담·울타리 줄을 세웁니다.
        // 등고선을 실제로 따라가며 프리팹 원래 길이만큼 끝과 끝을 이어 붙이므로 조각 사이가 벌어지거나 겹치지 않습니다.
        // 한 줄은 한 종류로 통일하고, 몇 조각마다 한 조각 길이를 통로로 비웁니다. 경로를 막으면 복구 단계에서 제거됩니다.
        private void PlaceRidges(FieldDressingSet source, IReadOnlyList<TerrainPiece> pieces)
        {
            var cells = new HashSet<Vector2Int>();
            foreach (TerrainPiece piece in pieces) cells.Add(Cell(piece.transform.position));

            Bounds area = ground.terrainData.bounds;
            area.center += ground.transform.position;
            area.Expand(new Vector3(-2f, 100f, -2f));

            var traced = new List<Vector3>();
            float size = MicroTerrainTile.Size;
            foreach (float contour in new[] { 0.85f, 1.85f })
            foreach (TerrainPiece piece in pieces)
            foreach (Vector2Int step in new[] { new Vector2Int(1, 0), new Vector2Int(0, 1) })
            {
                Vector2Int cell = Cell(piece.transform.position);
                if (!cells.Contains(cell + step)) continue;

                // 이웃 칸 중심 사이에서 등고선이 지나는 점을 줄의 시작점 후보로 씁니다.
                var from = new Vector3(cell.x * size, 0f, cell.y * size);
                Vector3 to = from + new Vector3(step.x, 0f, step.y) * size;
                float a = Relief(from);
                float b = Relief(to);
                if ((a - contour) * (b - contour) >= 0f) continue;

                RidgeCandidates++;
                Vector3 seed = Vector3.Lerp(from, to, (contour - a) / (b - a));
                if (Near(traced, seed, 3f, traced.Count)) continue;

                TraceRidge(source, cells, area, seed, contour, traced);
            }
        }

        private void TraceRidge(FieldDressingSet source, HashSet<Vector2Int> cells, Bounds area, Vector3 seed, float contour,
            List<Vector3> traced)
        {
            // 줄 전체에 같은 종류·같은 프리팹을 씁니다. 돌담 쪽 비중을 조금 더 둡니다.
            bool stone = Mathf.PerlinNoise(noiseOffset.y + seed.x * 0.05f, noiseOffset.x + seed.z * 0.05f) > 0.42f;
            GameObject[] options = stone ? source.borderWalls : source.fences;
            if (options == null || options.Length == 0) return;

            int hash = Mathf.Abs(Mathf.RoundToInt(seed.x * 7f) * 31 + Mathf.RoundToInt(seed.z * 7f) * 17);
            GameObject prefab = options[hash % options.Length];
            if (!TryLocalBounds(prefab, out Bounds shape)) return;

            // 조각 길이는 프리팹의 원래 긴 변 길이입니다(늘리지 않음). 너무 짧은 조각은 2m 이상이 되도록 묶어 늘립니다.
            float length = Mathf.Max(shape.size.x, shape.size.z);
            float depth = Mathf.Min(shape.size.x, shape.size.z);
            if (length < 2f) length = 2f;
            int openingEvery = 3 + hash % 3;
            int start = traced.Count;

            // 시작점에서 양쪽으로 등고선을 따라갑니다.
            foreach (float direction in new[] { 1f, -1f })
            {
                Vector3 current = seed;
                for (int index = 1; index <= 40; index++)
                {
                    Vector3 tangent = Tangent(current) * direction;
                    if (tangent == Vector3.zero) break;

                    Vector3 next = ProjectToContour(current + tangent * length, contour);
                    var chord = new Vector3(next.x - current.x, 0f, next.z - current.z);
                    if (!area.Contains(next) || chord.magnitude < length * 0.6f) break;

                    // 다른 줄과 만나면 겹치지 않게 멈춥니다.
                    Vector3 middle = (current + next) * 0.5f;
                    if (Near(traced, middle, length * 0.6f, start) || InStructure(middle, length * 0.5f + 0.5f)) break;
                    traced.Add(middle);

                    // 몇 조각마다 한 조각 길이를 통로로 비웁니다.
                    bool opening = index % (openingEvery + 1) == 0;
                    Vector3 outward = Vector3.Cross(chord.normalized, Vector3.up);
                    if (!opening && !CrossesSample(cells, middle, outward, depth, chord.magnitude * 0.5f))
                    {
                        PlaceSegment(prefab, middle, outward, removable: true, chord.magnitude);
                        RidgeSegments++;
                    }
                    current = next;
                }
            }
        }

        // 지면 기준 상대 높이와 그 기울기입니다.
        private float Relief(Vector3 point) => GroundY(point, 0f) - ground.transform.position.y;

        private Vector3 Gradient(Vector3 point)
        {
            const float delta = 0.25f;
            return new Vector3(
                Relief(point + Vector3.right * delta) - Relief(point - Vector3.right * delta), 0f,
                Relief(point + Vector3.forward * delta) - Relief(point - Vector3.forward * delta)) / (2f * delta);
        }

        // 등고선 방향(기울기에 수직)입니다. 같은 방향 규칙을 써서 한쪽으로 계속 따라갈 수 있습니다.
        private Vector3 Tangent(Vector3 point)
        {
            Vector3 gradient = Gradient(point);
            return gradient.sqrMagnitude < 0.0001f ? Vector3.zero : Vector3.Cross(Vector3.up, gradient).normalized;
        }

        // 기울기 방향으로 몇 번 보정해 점을 등고선 위로 되돌립니다(뉴턴 방식, 한 번에 최대 1m).
        private Vector3 ProjectToContour(Vector3 point, float contour)
        {
            for (int iteration = 0; iteration < 6; iteration++)
            {
                Vector3 gradient = Gradient(point);
                if (gradient.sqrMagnitude < 0.0001f) break;

                Vector3 move = gradient * ((contour - Relief(point)) / gradient.sqrMagnitude);
                point += Vector3.ClampMagnitude(move, 1f);
            }
            return point;
        }

        private static bool Near(List<Vector3> points, Vector3 point, float distance, int count)
        {
            for (int index = 0; index < count; index++)
                if (FlatDistance(points[index], point) < distance) return true;
            return false;
        }

        // 기존 경로 검사는 칸마다 중심 ±1.1m 격자 표본이 0.4m 안에서 보행 면을 찾아야 합니다.
        // NavMesh는 담 두께의 절반에 캐릭터 반지름(0.35m)을 더한 폭만큼 깎이므로, 표본이 그 안으로 0.38m보다
        // 깊이 들어가면 막힙니다. 얇은 울타리는 거의 항상 놓이고, 두꺼운 돌담은 표본 근처에서 틈이 됩니다.
        private static bool CrossesSample(HashSet<Vector2Int> cells, Vector3 point, Vector3 outward, float depth, float halfLength)
        {
            Vector3 along = Vector3.Cross(Vector3.up, outward);
            Vector2Int near = Cell(point);
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                Vector2Int cell = near + new Vector2Int(dx, dz);
                if (!cells.Contains(cell)) continue;

                foreach (float x in new[] { -1.1f, 0.03f, 1.1f })
                foreach (float z in new[] { -1.1f, 0.07f, 1.1f })
                {
                    Vector3 offset = new Vector3(cell.x * MicroTerrainTile.Size + x, 0f, cell.y * MicroTerrainTile.Size + z) - point;
                    offset.y = 0f;
                    if (Mathf.Abs(Vector3.Dot(offset, along)) <= halfLength + 0.3f
                        && Mathf.Abs(Vector3.Dot(offset, outward)) < depth * 0.5f + 0.35f - 0.38f) return true;
                }

                // 칸 연결점(변 중앙 안쪽 1.05m)은 위에서 내려 쏘는 바닥 확인 광선이 지나므로 담이 덮지 않게 합니다.
                foreach (Vector3 port in new[] { new Vector3(1.05f, 0f, 0f), new Vector3(-1.05f, 0f, 0f), new Vector3(0f, 0f, 1.05f), new Vector3(0f, 0f, -1.05f) })
                {
                    Vector3 offset = new Vector3(cell.x * MicroTerrainTile.Size, 0f, cell.y * MicroTerrainTile.Size) + port - point;
                    offset.y = 0f;
                    if (Mathf.Abs(Vector3.Dot(offset, along)) <= halfLength + 0.1f
                        && Mathf.Abs(Vector3.Dot(offset, outward)) < depth * 0.5f + 0.1f) return true;
                }
            }
            return false;
        }

        // 경사가 걸친 칸 일부에 큰 바위를 반쯤 묻어 둡니다. 축소한 작은 바위는 디테일이 뭉개져 보여 쓰지 않습니다.
        private void PlaceBoulder(FieldDressingSet source, Vector3 center, Dictionary<Vector2Int, float> tops,
            System.Random random)
        {
            if (random.NextDouble() >= 0.12) return;
            if (tops.TryGetValue(Cell(center), out float top) && !float.IsNaN(top)) return;

            GameObject prefab = Pick(source.cliffRocks, random);
            if (prefab == null || !TryLocalBounds(prefab, out Bounds bounds)) return;

            float scale = Range(random, 2.4f, 3.2f) / Mathf.Max(bounds.size.x, bounds.size.z);
            // 칸 모서리(네 칸이 만나는 점)는 경로 표본에서 가장 먼 자리입니다.
            var corner = new Vector3(random.Next(2) == 0 ? -1.5f : 1.5f, 0f, random.Next(2) == 0 ? -1.5f : 1.5f);
            GameObject rock = Spawn(prefab, center + corner, Range(random, 0f, 360f), 1f);
            rock.transform.localScale = Vector3.one * scale;

            // 지면 위로 약 1.2m만 드러나게 묻습니다.
            float visible = Mathf.Min(1.2f, bounds.size.y * scale);
            rock.transform.position += Vector3.up * (visible - bounds.max.y * scale);
            AddBlocker(rock, trunkOnly: false, removable: true, limitFootprint: false);

            // 충돌은 바위 중심부(약 0.8m)만 잡아 모서리 주변 표본과 통로를 남깁니다.
            BoxCollider core = rock.GetComponent<BoxCollider>();
            if (core != null) core.size = new Vector3(0.8f / scale, core.size.y, 0.8f / scale);
        }

        // position을 중심으로, outward에 수직인 방향으로 span 길이의 벽·울타리 한 칸을 세웁니다.
        private void PlaceSegment(GameObject prefab, Vector3 position, Vector3 outward, bool removable, float span)
        {
            if (!TryLocalBounds(prefab, out Bounds bounds)) return;

            bool alongX = bounds.size.x >= bounds.size.z;
            float length = alongX ? bounds.size.x : bounds.size.z;
            float stretch = span / Mathf.Max(0.01f, length);

            Vector3 along = Vector3.Cross(Vector3.up, outward);
            Quaternion facing = Quaternion.LookRotation(alongX ? outward : along);
            position.y = GroundY(position, position.y);

            GameObject segment = Spawn(prefab, position, 0f, 1f);
            segment.transform.rotation = facing;
            segment.transform.localScale = alongX ? new Vector3(stretch, 1f, 1f) : new Vector3(1f, 1f, stretch);

            // 프리팹 피벗이 중심이 아니어도 변의 중앙에 오도록 맞춥니다.
            Vector3 pivotOffset = segment.transform.TransformVector(new Vector3(bounds.center.x, 0f, bounds.center.z));
            segment.transform.position = position - pivotOffset + Vector3.up * -bounds.min.y;
            AddBlocker(segment, trunkOnly: false, removable, limitFootprint: false);
        }

        // 나무와 엄폐물은 사분면 안쪽(중심에서 ±0.75m)에 둡니다. 기존 경로 검사는 타일마다 ±1.1m 격자 표본을
        // 확인하므로, 표본이 장애물에 덮여도 0.4m 안에서 보행 면을 찾을 수 있는 위치입니다.
        // 해당 모서리 쪽 이웃 세 칸이 같은 높이의 평지일 때만 배치해 벽·낙하 경계 옆 통로를 막지 않습니다.
        private void PlaceBlockers(FieldDressingSet source, Vector3 center, Dictionary<Vector2Int, float> tops,
            System.Random random)
        {
            int treeCorner = -1;
            if (random.NextDouble() < source.treeChance)
            {
                int corner = random.Next(4);
                GameObject prefab = Pick(source.trees, random);
                if (prefab != null && OpenCorner(center, corner, tops))
                {
                    treeCorner = corner;
                    GameObject tree = Spawn(prefab, center + Corner(corner), Range(random, 0f, 360f), Range(random, 0.85f, 1.15f));
                    AddBlocker(tree, trunkOnly: true);
                }
            }

            if (random.NextDouble() >= source.coverChance) return;
            int coverCorner = (treeCorner + 1 + random.Next(3)) % 4;
            GameObject coverPrefab = Pick(source.cover, random);
            if (coverPrefab != null && OpenCorner(center, coverCorner, tops))
                AddBlocker(Spawn(coverPrefab, center + Corner(coverCorner), random.Next(4) * 90f, 1f), trunkOnly: false);
        }

        private static Vector3 Corner(int index)
        {
            const float inset = 0.75f;
            return new Vector3(index % 2 == 0 ? -inset : inset, 0f, index < 2 ? -inset : inset);
        }

        private bool OpenCorner(Vector3 center, int index, Dictionary<Vector2Int, float> tops)
        {
            // 언덕 지형에서는 단 가장자리(경사가 걸친 칸)의 완만한 자리(15° 미만)에만 둡니다.
            // 평평한 단 한가운데는 상자·전투 공간으로 비워 두고, 경로를 막는지는 이후 복구 단계가 확인합니다.
            if (ground != null)
            {
                if (tops.TryGetValue(Cell(center), out float top) && !float.IsNaN(top)) return false;
                Vector3 local = center + Corner(index) - ground.transform.position;
                Vector3 size = ground.terrainData.size;
                return ground.terrainData.GetSteepness(local.x / size.x, local.z / size.z) < 15f;
            }

            Vector2Int cell = Cell(center);
            var x = new Vector2Int(index % 2 == 0 ? -1 : 1, 0);
            var z = new Vector2Int(0, index < 2 ? -1 : 1);
            foreach (Vector2Int offset in new[] { x, z, x + z })
            {
                if (!tops.TryGetValue(cell + offset, out float top) || float.IsNaN(top)) return false;
                if (Mathf.Abs(top - center.y) > 0.01f) return false;
            }
            return true;
        }

        private GameObject Spawn(GameObject prefab, Vector3 position, float yaw, float scale)
        {
            // Terrain 지면에서는 놓을 자리의 실제 표면 높이에 맞춥니다.
            position.y = GroundY(position, position.y);
            GameObject instance = Instantiate(prefab, position, Quaternion.Euler(0f, yaw, 0f), root.transform);
            instance.transform.localScale = prefab.transform.localScale * scale;

            // 원본 MeshCollider는 지면 판정과 경로 검사를 흐리므로 모두 제거하고 필요한 박스만 다시 붙입니다.
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) DestroyImmediate(collider);
            Placed++;
            return instance;
        }

        private void AddBlocker(GameObject instance, bool trunkOnly, bool removable = true, bool limitFootprint = true)
        {
            if (!TryLocalBounds(instance, out Bounds bounds)) return;

            // 경로 표본 사이에 들어가지 않는 큰 엄폐물은 배치하지 않습니다.
            Vector3 scale = instance.transform.localScale;
            if (limitFootprint && !trunkOnly && Mathf.Max(bounds.size.x * scale.x, bounds.size.z * scale.z) > 0.9f)
            {
                instance.SetActive(false);
                Release(instance);
                Placed--;
                return;
            }

            BoxCollider box = instance.AddComponent<BoxCollider>();
            box.center = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.5f, bounds.center.z);
            box.size = trunkOnly
                ? new Vector3(0.4f / scale.x, bounds.size.y, 0.4f / scale.z)
                : bounds.size;
            if (trunkOnly) box.center = new Vector3(0f, box.center.y, 0f);

            // 외곽 벽처럼 경로 복구로 지우면 안 되는 물체는 목록에 넣지 않습니다.
            if (removable) blockers.Add(instance);
        }

        private static bool TryLocalBounds(GameObject instance, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;

                Bounds mesh = filter.sharedMesh.bounds;
                Matrix4x4 toRoot = instance.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Vector3 min = toRoot.MultiplyPoint3x4(mesh.min);
                Vector3 max = toRoot.MultiplyPoint3x4(mesh.max);
                var local = new Bounds((min + max) * 0.5f, Vector3.Max(min, max) - Vector3.Min(min, max));
                if (found) bounds.Encapsulate(local);
                else bounds = local;
                found = true;
            }
            return found;
        }

        // 꾸밈 때문에 끊긴 경로가 있으면 막힌 지점 주변의 나무·엄폐물부터 제거합니다.
        private void RepairPaths(TerrainGenerationResult result)
        {
            GameObject terrainRoot = result.Pieces[0].transform.parent.gameObject;

            // 기준 크기(18×18=324칸)보다 큰 필드는 장애물도 많으므로 복구 횟수를 면적에 비례해 늘립니다.
            int limit = Mathf.CeilToInt(repairLimit * Mathf.Max(1f, result.Pieces.Count / 324f));
            for (int attempt = 0; attempt < limit && blockers.Count > 0; attempt++)
            {
                if (MicroTerrainWalkability.Validate(terrainRoot, result.Pieces, out _, out _, root)) return;
                // 막힌 지점에서 가장 가까운 장애물 하나만 지웁니다. 한꺼번에 지우면 길을 막지 않은 담까지 사라집니다.
                float nearest = float.PositiveInfinity;
                foreach (GameObject blocker in blockers)
                    nearest = Mathf.Min(nearest, FlatDistance(blocker.transform.position, MicroTerrainWalkability.BlockedPoint));
                RemoveBlockersNear(MicroTerrainWalkability.BlockedPoint, nearest + 0.01f);
            }

            if (blockers.Count > 0 && !MicroTerrainWalkability.Validate(terrainRoot, result.Pieces, out _, out string reason, root))
            {
                Debug.LogWarning($"꾸밈 경로 복구 {limit}회 후에도 막혀 장애물을 모두 제거합니다: {reason}", this);
                RemoveBlockersNear(Vector3.zero, float.PositiveInfinity);
            }
        }

        private int RemoveBlockersNear(Vector3 point, float distance)
        {
            int removed = 0;
            for (int index = blockers.Count - 1; index >= 0; index--)
            {
                if (FlatDistance(blockers[index].transform.position, point) > distance) continue;

                // Destroy는 지연되므로 먼저 비활성화해 다음 검사에서 제외합니다.
                if (blockers[index].name == "StructureWall") RemovedStructureWalls++;
                blockers[index].SetActive(false);
                Release(blockers[index]);
                blockers.RemoveAt(index);
                removed++;
            }
            RemovedBlockers += removed;
            return removed;
        }

        public void Clear()
        {
            blockers.Clear();
            structureAreas.Clear();
            Placed = 0;
            RemovedBlockers = 0;
            RemovedStructureWalls = 0;
            RidgeSegments = 0;
            RidgeCandidates = 0;
            surface.Release();
            ground = null;
            if (root == null) return;

            root.SetActive(false);
            Release(root);
            root = null;
        }

        private static void Release(GameObject target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        private static GameObject Pick(GameObject[] options, System.Random random) =>
            options == null || options.Length == 0 ? null : options[random.Next(options.Length)];

        private static float Range(System.Random random, float min, float max) =>
            min + (float)random.NextDouble() * (max - min);
    }
}
