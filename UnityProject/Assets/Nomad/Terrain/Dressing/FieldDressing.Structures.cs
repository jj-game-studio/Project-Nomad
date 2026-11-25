using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Terrain
{
    // 지붕 없는 폐허 집을 Kingdom 집 조각(돌 기단·목재 벽·나무 바닥)으로 조립해 seed별 랜덤 위치에 둡니다.
    // 벽은 경로 표본(칸 중심 ±1.1m 격자) 사이의 선 위에만 세워, 표본이 벽에 묻히지 않고 안팎 모두 경로 검사를 통과합니다.
    public sealed partial class FieldDressing
    {
        // 칸 중심에서 벽 선까지의 거리입니다. 표본(±1.1m, 중앙 ±0.07m)의 중간이라 벽 두께와 캐릭터 반경을 모두 수용합니다.
        private const float StructureLine = 0.55f;
        private const float StructureWallThickness = 0.3f;
        private const float StructureWallHeight = 2.2f;
        private const float StructureMaxRelief = 0.12f;
        private const int StructureAttempts = 400;
        [SerializeField, Min(1.5f)] private float structureWallBay = 2.5f;

        // 칸 단위 크기 (가로, 세로). 3칸은 약 7.1m, 4칸은 약 10.1m 폭입니다.
        private static readonly Vector2Int[] StructureSizes = { new Vector2Int(3, 3), new Vector2Int(4, 3), new Vector2Int(3, 4) };

        private readonly List<Rect> structureAreas = new List<Rect>();

        public int Structures => structureAreas.Count;
        public IReadOnlyList<Rect> StructureAreas => structureAreas;

        private void PlaceStructures(FieldDressingSet source, IReadOnlyList<TerrainPiece> pieces, System.Random random)
        {
            if (ground == null || source.structureBases == null || source.structureBases.Length == 0) return;

            var cells = new HashSet<Vector2Int>();
            foreach (TerrainPiece piece in pieces) cells.Add(Cell(piece.transform.position));

            int target = random.Next(source.structureMin, source.structureMax + 1);
            for (int attempt = 0; attempt < StructureAttempts && structureAreas.Count < target; attempt++)
            {
                Vector2Int size = StructureSizes[random.Next(StructureSizes.Length)];
                Vector2Int origin = PickCell(cells, random);

                // 외곽 두 줄(출구·외곽 돌벽 자리)과 다른 구조물 주변은 비워 둡니다.
                if (!Inside(cells, origin - new Vector2Int(2, 2), origin + size + new Vector2Int(1, 1))) continue;

                Rect area = StructureRect(origin, size);
                if (Overlaps(area, 4.5f) || !Flat(area, out float floor)) continue;

                BuildStructure(source, area, floor, random);
                structureAreas.Add(area);
            }
        }

        // 지점이 구조물(여유 거리 포함) 안이면 식생·울타리·바위를 두지 않습니다.
        private bool InStructure(Vector3 point, float margin)
        {
            foreach (Rect area in structureAreas)
            {
                if (point.x > area.xMin - margin && point.x < area.xMax + margin
                    && point.z > area.yMin - margin && point.z < area.yMax + margin) return true;
            }
            return false;
        }

        private static Vector2Int PickCell(HashSet<Vector2Int> cells, System.Random random)
        {
            int index = random.Next(cells.Count);
            foreach (Vector2Int cell in cells)
                if (index-- == 0) return cell;
            return default;
        }

        private static bool Inside(HashSet<Vector2Int> cells, Vector2Int min, Vector2Int max)
        {
            for (int x = min.x; x <= max.x; x++)
            for (int z = min.y; z <= max.y; z++)
                if (!cells.Contains(new Vector2Int(x, z))) return false;
            return true;
        }

        // 벽 선으로 둘러싼 바깥 경계입니다. 첫 칸 중심 -0.55m부터 끝 칸 중심 +0.55m까지입니다.
        private static Rect StructureRect(Vector2Int origin, Vector2Int size)
        {
            float tile = MicroTerrainTile.Size;
            float xMin = origin.x * tile - StructureLine;
            float zMin = origin.y * tile - StructureLine;
            float xMax = (origin.x + size.x - 1) * tile + StructureLine;
            float zMax = (origin.y + size.y - 1) * tile + StructureLine;
            return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        }

        private bool Overlaps(Rect area, float gap)
        {
            foreach (Rect other in structureAreas)
            {
                if (area.xMin < other.xMax + gap && area.xMax > other.xMin - gap
                    && area.yMin < other.yMax + gap && area.yMax > other.yMin - gap) return true;
            }
            return false;
        }

        // 바닥 높이 차가 작은 평지에만 짓습니다. 바닥은 가장 높은 지면 위에 깔아 땅이 바닥을 뚫고 보이지 않게 합니다.
        private bool Flat(Rect area, out float top)
        {
            float low = float.PositiveInfinity;
            top = float.NegativeInfinity;
            for (float x = area.xMin - 0.5f; x <= area.xMax + 0.51f; x += 0.5f)
            for (float z = area.yMin - 0.5f; z <= area.yMax + 0.51f; z += 0.5f)
            {
                float height = GroundY(new Vector3(x, 0f, z), float.NaN);
                if (float.IsNaN(height)) return false;
                low = Mathf.Min(low, height);
                top = Mathf.Max(top, height);
            }
            return top - low <= StructureMaxRelief;
        }

        private void BuildStructure(FieldDressingSet source, Rect area, float floor, System.Random random)
        {
            var corners = new[]
            {
                new Vector3(area.xMin, 0f, area.yMin), new Vector3(area.xMax, 0f, area.yMin),
                new Vector3(area.xMax, 0f, area.yMax), new Vector3(area.xMin, 0f, area.yMax),
            };

            // 서로 반대편의 열린 문 두 곳만 비우고, 나머지 면은 벽으로 마감합니다.
            int door = random.Next(4);
            int exit = (door + 2) % 4;
            for (int side = 0; side < 4; side++)
            {
                Vector3 from = corners[side];
                Vector3 to = corners[(side + 1) % 4];
                int count = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(from, to) / structureWallBay));
                int doorIndex = side == door || side == exit ? count / 2 : -1;
                for (int index = 0; index < count; index++)
                {
                    if (index == doorIndex) continue;

                    Vector3 a = Vector3.Lerp(from, to, index / (float)count);
                    Vector3 b = Vector3.Lerp(from, to, (index + 1) / (float)count);
                    // 폐허용 잘린 벽·창틀·뼈대 대신 빈틈 없는 기본 벽을 사용합니다.
                    GameObject timber = System.Array.Find(source.structureWalls,
                        prefab => prefab != null && prefab.name == "SM_Bld_House_Wall_01") ?? Pick(source.structureWalls, random);
                    PlaceStructureWall(Pick(source.structureBases, random), timber, a, b);
                }
            }

            GameObject pillar = Pick(source.structureCorners, random);
            if (pillar != null)
                foreach (Vector3 corner in corners) PlaceStructurePillar(pillar, corner);

            PlaceStructureFloor(source, area, floor, random);
            PlaceStructureProps(source, area, random);
        }

        // 한 칸의 벽은 충돌 상자를 가진 빈 부모 아래에 돌 기단과 (있으면) 그 위 목재 벽을 붙인 묶음입니다.
        // 열린 문 외의 벽은 경로 복구 단계에서도 유지합니다.
        private void PlaceStructureWall(GameObject basePrefab, GameObject timberPrefab, Vector3 a, Vector3 b)
        {
            if (basePrefab == null) return;

            Vector3 along = b - a;
            Vector3 middle = (a + b) * 0.5f;
            float bottom = float.PositiveInfinity;
            for (int step = 0; step <= 4; step++)
                bottom = Mathf.Min(bottom, GroundY(Vector3.Lerp(a, b, step / 4f), middle.y));

            // 목재 벽 바깥면이 바깥을 향하도록 진행 방향의 오른쪽(바깥)을 forward로 둡니다.
            Quaternion facing = Quaternion.LookRotation(Vector3.Cross(Vector3.up, along.normalized));
            var wall = new GameObject("StructureWall");
            wall.transform.SetParent(root.transform, false);
            wall.transform.SetPositionAndRotation(new Vector3(middle.x, bottom - 0.05f, middle.z), facing);

            float top = FitAlong(basePrefab, wall.transform, along.magnitude, 0f, 0.6f);
            if (timberPrefab != null) top = FitAlong(timberPrefab, wall.transform, along.magnitude, top, 1f);

            var box = wall.AddComponent<BoxCollider>();
            box.size = new Vector3(along.magnitude, StructureWallHeight, StructureWallThickness);
            box.center = new Vector3(0f, StructureWallHeight * 0.5f, 0f);
            // 완성 벽은 경로 복구가 임의로 허물지 않습니다. 문을 통한 도달성은 최종 검사에서 확인합니다.
        }

        // 프리팹의 긴 변을 부모의 x축 length에 맞추고, 밑면을 height에 둡니다. 두께는 depthScale만큼 줄입니다.
        // 새로 올린 조각의 윗면 높이를 돌려줍니다.
        private float FitAlong(GameObject prefab, Transform parent, float length, float height, float depthScale)
        {
            if (!TryLocalBounds(prefab, out Bounds bounds)) return height;

            GameObject piece = Spawn(prefab, parent.position, 0f, 1f);
            piece.transform.SetParent(parent, false);
            piece.transform.localRotation = Quaternion.identity;
            piece.transform.localScale = new Vector3(length / Mathf.Max(0.01f, bounds.size.x), 1f, depthScale);

            // 피벗이 조각 끝에 있어도 부모 중심선에 오도록 맞춥니다.
            Vector3 offset = Vector3.Scale(piece.transform.localScale, new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            piece.transform.localPosition = new Vector3(-offset.x, height - offset.y, -offset.z);
            return height + bounds.size.y;
        }

        private void PlaceStructurePillar(GameObject prefab, Vector3 corner)
        {
            corner.y = GroundY(corner, corner.y) - 0.05f;
            GameObject pillar = Spawn(prefab, corner, 0f, 1f);
            pillar.name = "StructurePillar";
            pillar.transform.position = corner;

            var box = pillar.AddComponent<BoxCollider>();
            box.size = new Vector3(0.5f, StructureWallHeight, 0.5f);
            box.center = new Vector3(0f, StructureWallHeight * 0.5f, 0f);
        }

        // 모든 판을 깔아 내부 바닥을 빈틈 없이 메웁니다. 실제 보행 충돌은 아래 Terrain을 유지합니다.
        private void PlaceStructureFloor(FieldDressingSet source, Rect area, float floor, System.Random random)
        {
            GameObject prefab = Pick(source.structureFloors, random);
            if (prefab == null || !TryLocalBounds(prefab, out Bounds bounds)) return;

            Rect inner = Rect.MinMaxRect(area.xMin + 0.15f, area.yMin + 0.15f, area.xMax - 0.15f, area.yMax - 0.15f);
            int columns = Mathf.Max(1, Mathf.RoundToInt(inner.width / 2.5f));
            int rows = Mathf.Max(1, Mathf.RoundToInt(inner.height / 2.5f));
            var scale = new Vector3(inner.width / columns / bounds.size.x, 1f, inner.height / rows / bounds.size.z);
            for (int x = 0; x < columns; x++)
            for (int z = 0; z < rows; z++)
            {
                var cell = new Vector3(inner.xMin + (x + 0.5f) * inner.width / columns, 0f,
                    inner.yMin + (z + 0.5f) * inner.height / rows);
                GameObject board = Spawn(prefab, cell, 0f, 1f);
                board.transform.localScale = scale;
                Vector3 offset = Vector3.Scale(scale, new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
                board.transform.position = new Vector3(cell.x - offset.x, floor + 0.02f - offset.y, cell.z - offset.z);
            }
        }

        // 안쪽 모서리 칸에 통을 몇 개 둡니다. 나무·엄폐물과 같은 칸 사분면 자리(±0.75m)라 경로 표본을 막지 않습니다.
        private void PlaceStructureProps(FieldDressingSet source, Rect area, System.Random random)
        {
            float tile = MicroTerrainTile.Size;
            Vector2 first = new Vector2(area.xMin + StructureLine, area.yMin + StructureLine);
            Vector2 last = new Vector2(area.xMax - StructureLine, area.yMax - StructureLine);
            foreach (Vector2 corner in new[] { first, new Vector2(last.x, first.y), last, new Vector2(first.x, last.y) })
            {
                if (random.NextDouble() >= 0.45) continue;
                GameObject prefab = Pick(source.cover, random);
                if (prefab == null) continue;

                // 모서리 칸 중심에서 건물 안쪽 사분면으로 0.75m 들어간 자리입니다.
                float inwardX = corner.x < area.center.x ? 0.75f : -0.75f;
                float inwardZ = corner.y < area.center.y ? 0.75f : -0.75f;
                var position = new Vector3(Mathf.Round(corner.x / tile) * tile + inwardX, 0f, Mathf.Round(corner.y / tile) * tile + inwardZ);
                AddBlocker(Spawn(prefab, position, random.Next(4) * 90f, 1f), trunkOnly: false);
            }
        }
    }
}
