using System.Collections.Generic;
using Nomad.Props;
using UnityEngine;

namespace Nomad.Terrain
{
    // 3m WFC 필드가 생성되면 식생·엄폐물·절벽 바위를 seed 기반으로 배치합니다.
    // 이동을 막는 물체는 BoxCollider만 가지며, 소품 소유자 하위에 두어 이후 경로 검사에 포함됩니다.
    public sealed class FieldDressing : MonoBehaviour
    {
        private static readonly Vector2Int[] Sides =
            { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };

        [SerializeField] private TerrainGenerator generator;
        [SerializeField] private PropScatter obstacleOwner;
        [Tooltip("비어 있으면 Resources의 비공개 꾸밈 목록을 사용합니다. 둘 다 없으면 꾸밈 없이 동작합니다.")]
        [SerializeField] private FieldDressingSet set;
        [SerializeField, Range(0, 16)] private int repairLimit = 12;

        private readonly List<GameObject> blockers = new List<GameObject>();
        private GameObject root;

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

            foreach (TerrainPiece piece in result.Pieces)
            {
                MicroTerrainTile tile = piece.GetComponent<MicroTerrainTile>();
                ApplyGround(piece, source.groundMaterial);
                if (tile == null || tile.IsStair) continue;

                Vector3 center = piece.transform.position + Vector3.up * tile.Level;
                PlaceGroundCover(source, center, random);
                PlaceCliffRocks(source, center, tile.Level, tops, random);
                PlaceBlockers(source, center, tops, random);
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

        private static void ApplyGround(TerrainPiece piece, Material material)
        {
            if (material == null) return;
            foreach (MeshRenderer renderer in piece.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled) continue;
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int index = 0; index < materials.Length; index++) materials[index] = material;
                renderer.sharedMaterials = materials;
            }
        }

        private void PlaceGroundCover(FieldDressingSet source, Vector3 center, System.Random random)
        {
            for (int count = 0; count < source.groundCoverPerTile; count++)
            {
                GameObject prefab = Pick(source.groundCover, random);
                if (prefab == null) return;

                Vector3 offset = new Vector3(Range(random, -1.3f, 1.3f), 0f, Range(random, -1.3f, 1.3f));
                Spawn(prefab, center + offset, Range(random, 0f, 360f), Range(random, 0.8f, 1.2f));
            }
        }

        // 낮은 이웃이나 맵 바깥 쪽 변에 바위 절벽을 세워 블록 단면을 가립니다. 충돌은 기존 타일이 담당합니다.
        private void PlaceCliffRocks(FieldDressingSet source, Vector3 center, int level, Dictionary<Vector2Int, float> tops,
            System.Random random)
        {
            Vector2Int cell = Cell(center);
            foreach (Vector2Int side in Sides)
            {
                bool hasNeighbor = tops.TryGetValue(cell + side, out float neighborTop);
                if (hasNeighbor && (float.IsNaN(neighborTop) || neighborTop >= center.y - 0.01f)) continue;

                GameObject prefab = Pick(source.cliffRocks, random);
                if (prefab == null) return;

                float bottom = hasNeighbor ? neighborTop : center.y - Mathf.Max(1f, level + 0.5f);
                Vector3 outward = new Vector3(side.x, 0f, side.y);
                Vector3 edge = new Vector3(center.x, bottom, center.z) + outward * (MicroTerrainTile.Size * 0.5f);
                GameObject rock = Spawn(prefab, edge, 0f, 1f);
                rock.transform.rotation = Quaternion.LookRotation(outward) * Quaternion.Euler(0f, Range(random, -8f, 8f), 0f);
                FitHeight(rock, center.y - bottom + 0.15f, MicroTerrainTile.Size * 1.1f);

                // 바위 앞뒤 두께의 대부분을 바깥으로 밀어 윗면 보행 영역을 가리지 않게 합니다.
                if (TryLocalBounds(rock, out Bounds bounds))
                    rock.transform.position += outward * Mathf.Max(0f, bounds.size.z * rock.transform.localScale.z * 0.5f - 0.2f);
            }
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

        private void AddBlocker(GameObject instance, bool trunkOnly)
        {
            if (!TryLocalBounds(instance, out Bounds bounds)) return;

            // 경로 표본 사이에 들어가지 않는 큰 엄폐물은 배치하지 않습니다.
            Vector3 scale = instance.transform.localScale;
            if (!trunkOnly && Mathf.Max(bounds.size.x * scale.x, bounds.size.z * scale.z) > 0.9f)
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
            blockers.Add(instance);
        }

        private static void FitHeight(GameObject instance, float height, float width)
        {
            if (!TryLocalBounds(instance, out Bounds bounds) || bounds.size.y <= 0.01f) return;

            float horizontal = Mathf.Max(bounds.size.x, bounds.size.z, 0.01f);
            float scale = width / horizontal;
            instance.transform.localScale = new Vector3(scale, height / bounds.size.y, scale);

            // 바위 바닥을 아래 지면에 맞춥니다.
            instance.transform.position -= Vector3.up * (bounds.min.y * instance.transform.localScale.y);
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
                if (RemoveBlockersNear(MicroTerrainWalkability.BlockedPoint, 2.5f) == 0) break;
            }

            if (blockers.Count > 0 && !MicroTerrainWalkability.Validate(terrainRoot, result.Pieces, out _, out _, root))
                RemoveBlockersNear(Vector3.zero, float.PositiveInfinity);
        }

        private int RemoveBlockersNear(Vector3 point, float distance)
        {
            int removed = 0;
            for (int index = blockers.Count - 1; index >= 0; index--)
            {
                Vector3 position = blockers[index].transform.position;
                if (Vector2.Distance(new Vector2(position.x, position.z), new Vector2(point.x, point.z)) > distance) continue;

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

        private static GameObject Pick(GameObject[] options, System.Random random) =>
            options == null || options.Length == 0 ? null : options[random.Next(options.Length)];

        private static float Range(System.Random random, float min, float max) =>
            min + (float)random.NextDouble() * (max - min);
    }
}
