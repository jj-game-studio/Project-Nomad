using System.Collections.Generic;
using Nomad.Props;
using UnityEngine;

namespace Nomad.Terrain
{
    // 3m WFC 필드가 생성되면 식생·엄폐물·절벽 바위를 seed 기반으로 배치합니다.
    // 이동을 막는 물체는 BoxCollider만 가지며, 소품 소유자 하위에 두어 이후 경로 검사에 포함됩니다.
    public sealed class FieldDressing : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private static readonly Vector2Int[] Sides =
            { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };

        [SerializeField] private TerrainGenerator generator;
        [SerializeField] private PropScatter obstacleOwner;
        [Tooltip("비어 있으면 Resources의 비공개 꾸밈 목록을 사용합니다. 둘 다 없으면 꾸밈 없이 동작합니다.")]
        [SerializeField] private FieldDressingSet set;
        [SerializeField, Range(0, 32)] private int repairLimit = 24;

        private readonly List<GameObject> blockers = new List<GameObject>();
        private GameObject root;
        private Vector2 noiseOffset;
        private int reservedCorner = -1;

        public int Placed { get; private set; }
        public int Blockers => blockers.Count;
        public int RemovedBlockers { get; private set; }
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
            FieldDressingSet source = set != null ? set : Resources.Load<FieldDressingSet>(FieldDressingSet.ResourcePath);
            if (source == null || result == null || !result.Success || result.Pieces.Count == 0) return;

            // 이전 10m 비교 모드(L)는 꾸밈 대상이 아닙니다.
            if (result.Pieces[0].GetComponent<MicroTerrainTile>() == null) return;

            root = new GameObject("FieldDressing");
            root.transform.SetParent(obstacleOwner != null ? obstacleOwner.transform : transform, false);
            var random = new System.Random(unchecked(result.Seed * 7919 + 17));
            Dictionary<Vector2Int, float> tops = BuildTopMap(result.Pieces);

            // 군락 밀도와 테마는 seed별로 이동한 저주파 노이즈로 정해 이웃 타일끼리 비슷해지게 합니다.
            noiseOffset = new Vector2(Range(random, 0f, 1000f), Range(random, 0f, 1000f));

            foreach (TerrainPiece piece in result.Pieces)
            {
                MicroTerrainTile tile = piece.GetComponent<MicroTerrainTile>();
                if (tile == null) continue;
                ApplyGround(piece, source.groundMaterial, tile.Level + (tile.IsStair ? 0.5f : 0f));

                Vector3 center = piece.transform.position + Vector3.up * tile.Level;
                PlaceBorderWalls(source, center, tops);
                if (tile.IsStair) continue;

                PlaceCliffRocks(source, center, tops, random);

                // 식생 노이즈가 낮은 빈터는 상자·전투 공간으로 비워 두고, 군락에만 식생과 장애물을 둡니다.
                float density = Density(center);
                if (density < source.coverThreshold) continue;

                reservedCorner = -1;
                PlaceGroundCover(source, center, density, tops, random);
                PlaceBlockers(source, center, tops, random);
                PlaceFence(source, center, tops, random);
            }

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
        private void PlaceGroundCover(FieldDressingSet source, Vector3 center, float density,
            Dictionary<Vector2Int, float> tops, System.Random random)
        {
            if (source.coverThemes == null || source.coverThemes.Length == 0) return;

            Vector2 cell = Cell(center);
            float spread = (density - source.coverThreshold) / Mathf.Max(0.01f, 1f - source.coverThreshold);
            int count = 1 + Mathf.FloorToInt(spread * source.coverMaxPerTile);
            float themeNoise = Mathf.PerlinNoise(noiseOffset.y + cell.x * 0.08f, noiseOffset.x + cell.y * 0.08f);
            int theme = Mathf.Clamp(Mathf.FloorToInt(themeNoise * source.coverThemes.Length), 0, source.coverThemes.Length - 1);

            bool bigPlaced = false;
            for (int index = 0; index < count; index++)
            {
                GameObject prefab = Pick(source.coverThemes[theme].prefabs, random);
                if (prefab == null) return;

                Vector3 offset = new Vector3(Range(random, -1.3f, 1.3f), 0f, Range(random, -1.3f, 1.3f));
                GameObject plant = Spawn(prefab, center + offset, Range(random, 0f, 360f), Range(random, 0.85f, 1.15f));
                if (!TryLocalBounds(plant, out Bounds bounds)
                    || Mathf.Max(bounds.size.x, bounds.size.z) * plant.transform.localScale.x <= 1f) continue;

                // 폭 1m를 넘는 큰 덤불은 통과하면 어색하므로 나무처럼 다룹니다. 타일당 하나만,
                // 경로 표본을 덮지 않는 사분면 위치에 두고 0.6m 몸통만 충돌체로 씁니다. 잎 가장자리는 스칠 수 있습니다.
                int corner = random.Next(4);
                // 군락 가장자리의 큰 덤불은 빈터의 개방 공간(상자 후보)을 잠식하므로 밀도가 높은 중심부에만 둡니다.
                if (bigPlaced || spread < 0.5f || corner == reservedCorner || !OpenCorner(center, corner, tops))
                {
                    plant.SetActive(false);
                    Release(plant);
                    Placed--;
                    continue;
                }

                plant.transform.position = center + Corner(corner);
                AddBlocker(plant, trunkOnly: true, trunk: 0.6f);
                reservedCorner = corner;
                bigPlaced = true;
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

            bool alongX = bounds.size.x >= bounds.size.z;
            float length = alongX ? bounds.size.x : bounds.size.z;
            float depth = alongX ? bounds.size.z : bounds.size.x;
            float stretch = MicroTerrainTile.Size / Mathf.Max(0.01f, length);

            Vector3 along = Vector3.Cross(Vector3.up, outward);
            Quaternion facing = Quaternion.LookRotation(alongX ? outward : along);
            Vector3 position = center + outward * (MicroTerrainTile.Size * 0.5f + depth * outside);

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
            int treeCorner = reservedCorner;
            if (random.NextDouble() < source.treeChance)
            {
                int corner = random.Next(4);
                GameObject prefab = Pick(source.trees, random);
                if (prefab != null && corner != reservedCorner && OpenCorner(center, corner, tops))
                {
                    treeCorner = corner;
                    GameObject tree = Spawn(prefab, center + Corner(corner), Range(random, 0f, 360f), Range(random, 0.85f, 1.15f));
                    AddBlocker(tree, trunkOnly: true);
                }
            }

            if (random.NextDouble() >= source.coverChance) return;
            int coverCorner = (treeCorner + 1 + random.Next(3)) % 4;
            if (coverCorner == reservedCorner) return;
            GameObject coverPrefab = Pick(source.cover, random);
            if (coverPrefab != null && OpenCorner(center, coverCorner, tops))
                AddBlocker(Spawn(coverPrefab, center + Corner(coverCorner), random.Next(4) * 90f, 1f), trunkOnly: false);
        }

        private static Vector3 Corner(int index)
        {
            const float inset = 0.75f;
            return new Vector3(index % 2 == 0 ? -inset : inset, 0f, index < 2 ? -inset : inset);
        }

        private static bool OpenCorner(Vector3 center, int index, Dictionary<Vector2Int, float> tops)
        {
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
            GameObject instance = Instantiate(prefab, position, Quaternion.Euler(0f, yaw, 0f), root.transform);
            instance.transform.localScale = prefab.transform.localScale * scale;

            // 원본 MeshCollider는 지면 판정과 경로 검사를 흐리므로 모두 제거하고 필요한 박스만 다시 붙입니다.
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) DestroyImmediate(collider);
            Placed++;
            return instance;
        }

        private void AddBlocker(GameObject instance, bool trunkOnly, bool removable = true, bool limitFootprint = true,
            float trunk = 0.4f)
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
                ? new Vector3(trunk / scale.x, bounds.size.y, trunk / scale.z)
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
            for (int attempt = 0; attempt < repairLimit && blockers.Count > 0; attempt++)
            {
                if (MicroTerrainWalkability.Validate(terrainRoot, result.Pieces, out _, out _, root)) return;
                if (RemoveBlockersNear(MicroTerrainWalkability.BlockedPoint, 2.5f) > 0) continue;

                // 주변에 없으면 막힌 지점에서 가장 가까운 하나만 지웁니다. 덤불 울타리처럼 멀리서 둘러싼 경우입니다.
                float nearest = float.PositiveInfinity;
                foreach (GameObject blocker in blockers)
                    nearest = Mathf.Min(nearest, FlatDistance(blocker.transform.position, MicroTerrainWalkability.BlockedPoint));
                RemoveBlockersNear(MicroTerrainWalkability.BlockedPoint, nearest + 0.01f);
            }

            if (blockers.Count > 0 && !MicroTerrainWalkability.Validate(terrainRoot, result.Pieces, out _, out _, root))
                RemoveBlockersNear(Vector3.zero, float.PositiveInfinity);
        }

        private int RemoveBlockersNear(Vector3 point, float distance)
        {
            int removed = 0;
            for (int index = blockers.Count - 1; index >= 0; index--)
            {
                if (FlatDistance(blockers[index].transform.position, point) > distance) continue;

                // Destroy는 지연되므로 먼저 비활성화해 다음 검사에서 제외합니다.
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
            Placed = 0;
            RemovedBlockers = 0;
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
