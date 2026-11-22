using System;
using System.Collections.Generic;
using System.Linq;
using Nomad.Combat;
using Nomad.Terrain;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters.Demo
{
    public static class MonsterEncounterLayout
    {
        private sealed class Candidate
        {
            internal FieldSpawnPoint Point;
            internal float Distance;
        }

        public static List<Health> Spawn(FieldGenerationResult field, Transform parent, MonsterActorFactory factory,
            Health player, int normalCount, int eliteCount, int bossCount, float safeRadius = 12)
        {
            var actors = new List<Health>();
            var candidates = Routes(field).OrderBy(candidate => candidate.Distance).ToList();
            if (candidates.Count == 0)
                return actors;

            BossArena arena = bossCount > 0 ? CreateArena(candidates, field, parent) : null;
            float endDistance = arena != null ? PathLength(field.PlayerPosition, arena.Entrance) : candidates[^1].Distance;
            var reserved = new List<Vector3>();

            // 엘리트 자리를 먼저 예약해 일반 몬스터가 마지막 조우 구간을 차지하지 않게 합니다.
            var elites = new List<Candidate>();
            foreach (Candidate candidate in candidates.OrderByDescending(point => point.Distance))
            {
                if (elites.Count >= eliteCount) break;
                if (candidate.Distance < Mathf.Max(safeRadius + 6, endDistance * 0.6f) ||
                    candidate.Distance > endDistance * 0.9f || !Clear(candidate.Point.Position, 1.5f, 4f) ||
                    !Separated(candidate.Point.Position, reserved, 8) || NearArena(candidate.Point.Position, arena, 8)) continue;
                elites.Add(candidate);
                reserved.Add(candidate.Point.Position);
            }

            float normalEnd = elites.Count > 0 ? elites.Min(candidate => candidate.Distance) - 3 : endDistance * 0.65f;
            var random = new System.Random(field.Seed);
            var shuffled = candidates.ToList();
            for (int index = shuffled.Count - 1; index > 0; index--)
            {
                int other = random.Next(index + 1);
                (shuffled[index], shuffled[other]) = (shuffled[other], shuffled[index]);
            }
            int normals = 0;
            foreach (Candidate candidate in shuffled)
            {
                if (normals >= normalCount) break;
                Vector3 position = candidate.Point.Position;
                if (candidate.Distance > normalEnd || Horizontal(position, field.PlayerPosition) < safeRadius ||
                    !Separated(position, reserved, 4) || NearArena(position, arena, 8) || !Clear(position, 0.6f, 2.1f)) continue;
                MonsterTestSettings settings = MonsterTestSettings.Defaults(MonsterRank.Normal);
                settings.normalStyle = normals % 3;
                actors.Add(factory.CreateRanked(player, settings, MonsterRank.Normal, position, candidate.Point.Rotation,
                    "Normal " + candidate.Point.Id, parent));
                reserved.Add(position);
                normals++;
            }
            foreach (Candidate elite in elites)
                actors.Add(factory.CreateRanked(player, MonsterTestSettings.Defaults(MonsterRank.Elite), MonsterRank.Elite,
                    elite.Point.Position, elite.Point.Rotation, "Elite " + elite.Point.Id, parent));

            // 현재 보스 구역은 한 마리의 최종 보스를 위한 공간입니다.
            if (arena != null)
            {
                MonsterTestSettings settings = MonsterTestSettings.Defaults(MonsterRank.Boss);
                settings.homeRange = BossArena.HalfSize - 2;
                actors.Add(factory.CreateRanked(player, settings, MonsterRank.Boss, arena.Center, Quaternion.identity,
                    "Boss - final arena", parent));
            }
            if (normals != normalCount || elites.Count != eliteCount || (bossCount > 0 && arena == null))
                Debug.LogWarning($"안전한 조우 배치: 일반 {normals}/{normalCount}, 엘리트 {elites.Count}/{eliteCount}, 보스 {(arena != null ? 1 : 0)}/{bossCount}. 부족한 자리에 겹쳐 생성하지 않습니다.", parent);
            return actors;
        }

        private static List<Candidate> Routes(FieldGenerationResult field)
        {
            var result = new List<Candidate>();
            foreach (FieldSpawnPoint point in field.EnemyPoints)
            {
                float distance = PathLength(field.PlayerPosition, point.Position);
                if (!float.IsInfinity(distance)) result.Add(new Candidate { Point = point, Distance = distance });
            }
            return result;
        }

        public static float PathLength(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                return float.PositiveInfinity;
            float length = 0;
            Vector3[] corners = path.corners;
            for (int index = 1; index < corners.Length; index++)
                length += Vector3.Distance(corners[index - 1], corners[index]);
            return length;
        }

        private static BossArena CreateArena(List<Candidate> candidates, FieldGenerationResult field, Transform parent)
        {
            TerrainGenerator generator = parent.GetComponentInParent<TerrainGenerator>();
            if (generator == null)
                generator = UnityEngine.Object.FindObjectsByType<TerrainGenerator>(FindObjectsSortMode.None)
                    .FirstOrDefault(source => source.gameObject.scene == parent.gameObject.scene);
            if (generator?.LastResult == null || generator.LastResult.Pieces.Count == 0) return null;
            Bounds bounds = generator.LastResult.Pieces[0].WorldOccupancy;
            foreach (TerrainPiece piece in generator.LastResult.Pieces) bounds.Encapsulate(piece.WorldOccupancy);

            int outerCandidates = 0, clearCorridors = 0, meshAttempts = 0;
            string obstruction = "none";
            foreach (Candidate candidate in candidates.OrderByDescending(point => point.Distance))
            {
                Vector3 start = candidate.Point.Position;
                if (field.ExitPoints.Any(exit => Horizontal(start, exit.Position) < 5)) continue;
                float[] gaps = { start.x - bounds.min.x, bounds.max.x - start.x, start.z - bounds.min.z, bounds.max.z - start.z };
                foreach (int side in Enumerable.Range(0, 4).OrderBy(index => gaps[index]))
                {
                    if (gaps[side] > 8f || candidate.Distance < candidates.Max(point => point.Distance) * 0.55f) continue;
                    outerCandidates++;
                    Vector3 outward = side switch { 0 => Vector3.left, 1 => Vector3.right, 2 => Vector3.back, _ => Vector3.forward };
                    // 필드 외곽 벽을 지우지 않고, 그 위를 넘는 완만한 접근 경사로 높이를 구합니다.
                    float floorHeight = start.y;
                    for (float step = 0; step <= gaps[side] + 1; step += 0.5f)
                    {
                        Vector3 probe = start + outward * step + Vector3.up * 7;
                        if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 8,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                            floorHeight = Mathf.Max(floorHeight, hit.point.y + 0.3f);
                    }
                    float rampLength = Mathf.Max(0.5f, gaps[side]);
                    if ((floorHeight - start.y) / rampLength > 0.65f) continue;
                    bool clear = true;
                    for (float step = 0; step < gaps[side] + 1; step += 0.75f)
                    {
                        Vector3 point = start + outward * step;
                        point.y = Mathf.Lerp(start.y, floorHeight, Mathf.Clamp01((step + 0.5f) / rampLength));
                        if (Clear(point, 0.55f, 2.1f)) continue;
                        Collider[] hits = Physics.OverlapCapsule(point + Vector3.up * 0.65f, point + Vector3.up * 1.55f,
                            0.55f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                        obstruction = string.Join(",", hits.Select(hit => hit.name).Distinct());
                        clear = false;
                        break;
                    }
                    if (!clear) continue;
                    clearCorridors++;

                    var root = new GameObject("Final Boss Arena");
                    root.transform.SetParent(parent, false);
                    BossArena arena = root.AddComponent<BossArena>();
                    meshAttempts++;
                    if (arena.Build(start, outward, gaps[side], floorHeight)) return arena;
                    root.SetActive(false);
                    UnityEngine.Object.Destroy(root);
                }
            }
            Debug.LogWarning($"보스 구역 후보: 외곽 {outerCandidates}, 통로 {clearCorridors}, 경로 생성 {meshAttempts}, 가림 {obstruction}", parent);
            return null;
        }

        public static bool Clear(Vector3 point, float radius, float height) => !Physics.CheckCapsule(
            point + Vector3.up * (radius + 0.1f), point + Vector3.up * Mathf.Max(radius + 0.1f, height - radius),
            radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        private static bool Separated(Vector3 point, List<Vector3> reserved, float gap) =>
            reserved.All(other => Horizontal(point, other) >= gap);
        private static bool NearArena(Vector3 point, BossArena arena, float gap) =>
            arena != null && Horizontal(point, arena.Entrance) < gap;
        private static float Horizontal(Vector3 first, Vector3 second) =>
            Vector3.ProjectOnPlane(first - second, Vector3.up).magnitude;
    }
}
