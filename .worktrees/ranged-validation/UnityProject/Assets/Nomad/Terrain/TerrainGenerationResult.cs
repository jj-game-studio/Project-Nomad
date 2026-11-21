using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Terrain
{
    public enum TerrainGenerationState { Idle, Generating, Complete, Failed }

    public sealed class TerrainLink
    {
        public TerrainConnection From { get; }
        public TerrainConnection To { get; }

        public TerrainLink(TerrainConnection from, TerrainConnection to)
        {
            From = from;
            To = to;
        }
    }

    // 지형 단계 결과입니다. 소품·경로·스폰 지점 검증 후의 최종 Ready와 구분합니다.
    public sealed class TerrainGenerationResult
    {
        public string GenerationId { get; }
        public int Seed { get; }
        public bool Success { get; }
        public string FailureReason { get; }
        public int Attempts { get; }
        public int CandidateChecks { get; }
        public IReadOnlyList<TerrainPiece> Pieces { get; }
        public IReadOnlyList<TerrainLink> Links { get; }

        internal TerrainGenerationResult(string generationId, int seed, bool success, string reason,
            int attempts, int checks, List<TerrainPiece> pieces, List<TerrainLink> links)
        {
            GenerationId = generationId;
            Seed = seed;
            Success = success;
            FailureReason = reason;
            Attempts = attempts;
            CandidateChecks = checks;
            Pieces = Array.AsReadOnly(pieces.ToArray());
            Links = Array.AsReadOnly(links.ToArray());
        }
    }
}
