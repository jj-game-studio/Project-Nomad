using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Terrain
{
    [DisallowMultipleComponent]
    public sealed class TerrainGenerator : MonoBehaviour
    {
        private const int CandidatesPerPiece = 50;
        private const int MaxRegenerations = 3;

        [SerializeField] private TerrainPiece[] prefabs;
        [SerializeField, Min(1)] private int pieceCount = 12;
        [SerializeField] private int seed = 14;
        [SerializeField] private Bounds generationBounds = new Bounds(Vector3.zero, new Vector3(400f, 100f, 400f));
        [SerializeField] private bool generateOnStart;

        [SerializeField, HideInInspector] private GameObject generatedRoot;
        private bool busy;

        public TerrainGenerationState State { get; private set; }
        public TerrainGenerationResult LastResult { get; private set; }
        public event Action<string> GenerationStarted;
        public event Action<TerrainGenerationResult> GenerationCompleted;

        public void Configure(TerrainPiece[] sources, int count, int randomSeed, Bounds bounds)
        {
            if (busy)
                throw new InvalidOperationException("생성 중에는 설정을 변경할 수 없습니다.");

            prefabs = sources == null ? null : (TerrainPiece[])sources.Clone();
            pieceCount = count;
            seed = randomSeed;
            generationBounds = bounds;
        }

        private void Start()
        {
            if (generateOnStart)
                Generate();
        }

        public TerrainGenerationResult Generate()
        {
            if (busy)
                throw new InvalidOperationException("생성 콜백에서 재진입할 수 없습니다.");

            busy = true;
            string id = Guid.NewGuid().ToString("N");
            var pieces = new List<TerrainPiece>();
            var links = new List<TerrainLink>();
            int attempts = 0;
            int checks = 0;

            try
            {
                LastResult = null;
                State = TerrainGenerationState.Generating;
                ClearGenerated();
                GenerationStarted?.Invoke(id);

                string error = ValidateSettings();
                bool success = false;
                var random = new System.Random(seed);

                if (error == null)
                {
                    for (int retry = 0; retry <= MaxRegenerations; retry++)
                    {
                        attempts++;
                        ClearGenerated();
                        pieces.Clear();
                        links.Clear();
                        generatedRoot = new GameObject("GeneratedTerrain_" + id);
                        // 완성 전 후보는 렌더링·물리·Start에서 사용하지 않도록 비활성 상태로 구성합니다.
                        generatedRoot.SetActive(false);

                        if (TryGenerate(random, pieces, links, ref checks))
                        {
                            success = true;
                            break;
                        }
                    }

                    if (!success)
                        error = "후보 또는 배치 공간 부족으로 최초 생성과 재생성 3회에서 지정 수량을 연결하지 못했습니다.";
                }

                if (!success)
                {
                    ClearGenerated();
                    pieces.Clear();
                    links.Clear();
                }

                LastResult = new TerrainGenerationResult(id, seed, success, error ?? string.Empty,
                    attempts, checks, pieces, links);
                State = success ? TerrainGenerationState.Complete : TerrainGenerationState.Failed;
                if (success)
                    generatedRoot.SetActive(true);

                GenerationCompleted?.Invoke(LastResult);
                return LastResult;
            }
            finally
            {
                busy = false;
            }
        }

        private bool TryGenerate(System.Random random, List<TerrainPiece> pieces,
            List<TerrainLink> links, ref int checks)
        {
            var open = new List<TerrainConnection>();
            var first = Instantiate(prefabs[random.Next(prefabs.Length)], generatedRoot.transform);
            first.transform.SetPositionAndRotation(transform.position,
                Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            if (!InsideBounds(first.WorldOccupancy))
                return false;

            pieces.Add(first);
            open.AddRange(first.Connections);

            while (pieces.Count < pieceCount)
            {
                bool placed = false;
                for (int candidateIndex = 0; candidateIndex < CandidatesPerPiece && open.Count > 0; candidateIndex++)
                {
                    checks++;
                    TerrainConnection target = open[random.Next(open.Count)];
                    TerrainPiece candidate = Instantiate(prefabs[random.Next(prefabs.Length)], generatedRoot.transform);
                    TerrainConnection incoming = candidate.Connections[random.Next(candidate.Connections.Count)];
                    TerrainPlacement.Align(candidate, incoming, target);

                    bool valid = incoming.Matches(target) && InsideBounds(candidate.WorldOccupancy);
                    foreach (TerrainPiece existing in pieces)
                    {
                        if (valid && TerrainPlacement.Overlaps(existing, candidate,
                            target.transform.IsChildOf(existing.transform) ? target : null, incoming))
                            valid = false;
                    }

                    if (!valid)
                    {
                        Release(candidate.gameObject);
                        continue;
                    }

                    pieces.Add(candidate);
                    links.Add(new TerrainLink(target, incoming));
                    open.Remove(target);
                    foreach (TerrainConnection point in candidate.Connections)
                    {
                        if (point != incoming)
                            open.Add(point);
                    }

                    placed = true;
                    break;
                }

                if (!placed)
                    return false;
            }

            return true;
        }

        private string ValidateSettings()
        {
            if (prefabs == null || prefabs.Length == 0 || pieceCount < 1)
                return "프리팹과 양의 조각 수가 필요합니다.";

            if (!Finite(generationBounds.center) || !Finite(generationBounds.size)
                || generationBounds.size.x <= 0f || generationBounds.size.y <= 0f || generationBounds.size.z <= 0f)
                return "생성 영역은 유한한 양의 크기여야 합니다.";

            if (transform.lossyScale != Vector3.one || !Cardinal(transform))
                return "생성기 배율은 1, 회전은 Y축 90도 단위여야 합니다.";

            foreach (TerrainPiece prefab in prefabs)
            {
                if (prefab == null || prefab.transform.localScale != Vector3.one || !Cardinal(prefab.transform)
                    || prefab.Connections == null || prefab.Connections.Count == 0
                    || !Finite(prefab.LocalOccupancy.center) || !Finite(prefab.LocalOccupancy.size)
                    || prefab.LocalOccupancy.size.x <= 0f || prefab.LocalOccupancy.size.y <= 0f
                    || prefab.LocalOccupancy.size.z <= 0f)
                    return "프리팹의 연결점·점유 영역·배율·회전을 확인하십시오.";

                var unique = new HashSet<TerrainConnection>();
                foreach (TerrainConnection point in prefab.Connections)
                {
                    if (point == null || !point.transform.IsChildOf(prefab.transform) || !unique.Add(point)
                        || !Cardinal(point.transform) || !Finite(point.Position)
                        || !Finite(point.transform.lossyScale) || point.transform.lossyScale != Vector3.one)
                        return "연결점은 조각 내부의 고유한 직교 좌표여야 합니다.";
                }
            }

            return null;
        }

        private bool InsideBounds(Bounds bounds)
        {
            return generationBounds.Contains(bounds.min) && generationBounds.Contains(bounds.max);
        }

        private static bool Cardinal(Transform value)
        {
            float yaw = value.eulerAngles.y;
            return Quaternion.Angle(value.rotation,
                Quaternion.Euler(0f, Mathf.Round(yaw / 90f) * 90f, 0f)) < 0.001f;
        }

        private static bool Finite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private void ClearGenerated()
        {
            if (generatedRoot == null)
                return;

            Release(generatedRoot);
            generatedRoot = null;
        }

        private static void Release(GameObject value)
        {
            value.SetActive(false);
            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }

        private void OnDestroy()
        {
            ClearGenerated();
        }
    }
}
