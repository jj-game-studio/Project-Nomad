using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Terrain
{
    public enum FieldGenerationState { Idle, Generating, Ready, Failed }

    public sealed class FieldSpawnPoint
    {
        public string Id { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public float Radius { get; }
        public Vector3 ApproachPosition { get; }

        internal FieldSpawnPoint(string id, Vector3 position, Quaternion rotation, float radius, Vector3 approach)
        {
            Id = id;
            Position = position;
            Rotation = rotation;
            Radius = radius;
            ApproachPosition = approach;
        }
    }

    // 지형 Complete가 아니라 소품·경로·생성 지점 검증을 모두 마친 세대 결과입니다.
    public sealed class FieldGenerationResult
    {
        public string GenerationId { get; }
        public int Seed { get; }
        public bool Success { get; }
        public string FailureReason { get; }
        public Vector3 PlayerPosition { get; }
        public Quaternion PlayerRotation { get; }
        public IReadOnlyList<FieldSpawnPoint> EnemyPoints { get; }
        public IReadOnlyList<FieldSpawnPoint> ChestPoints { get; }
        // 필드 가장자리의 출구입니다. Rotation의 forward가 필드 바깥을 향합니다.
        public IReadOnlyList<FieldSpawnPoint> ExitPoints { get; }

        internal FieldGenerationResult(TerrainGenerationResult terrain, bool success, string reason,
            Vector3 start, List<FieldSpawnPoint> enemies, List<FieldSpawnPoint> chests, List<FieldSpawnPoint> exits)
        {
            GenerationId = terrain.GenerationId;
            Seed = terrain.Seed;
            Success = success;
            FailureReason = reason;
            PlayerPosition = start;
            PlayerRotation = Quaternion.identity;
            EnemyPoints = Array.AsReadOnly(enemies.ToArray());
            ChestPoints = Array.AsReadOnly(chests.ToArray());
            ExitPoints = Array.AsReadOnly(exits.ToArray());
        }
    }
}
