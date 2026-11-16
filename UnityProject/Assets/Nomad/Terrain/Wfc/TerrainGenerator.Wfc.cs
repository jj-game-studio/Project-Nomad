using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Terrain
{
    public sealed partial class TerrainGenerator
    {
        [SerializeField] private WfcTerrainCatalog wfcCatalog;
        [SerializeField] private int gridWidth = 6;
        [SerializeField] private int gridHeight = 6;

        public bool UsesWfc => wfcCatalog != null;

        public void ConfigureWfc(WfcTerrainCatalog catalog, int width, int height, int randomSeed, Bounds bounds,
            bool useRandomSeed = false)
        {
            if (busy) throw new InvalidOperationException("생성 중에는 설정을 변경할 수 없습니다.");
            wfcCatalog = catalog;
            gridWidth = width;
            gridHeight = height;
            seed = randomSeed;
            randomizeSeed = useRandomSeed;
            generationBounds = bounds;
        }

        public static bool FootprintsOverlap(Bounds a, Bounds b) =>
            Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x) > 0.01f
            && Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z) > 0.01f;

        private string ValidateWfcSettings()
        {
            if (gridWidth < 1 || gridHeight < 1 || gridWidth > 16 || gridHeight > 16)
                return "WFC 격자 크기는 축마다 1~16이어야 합니다.";
            if (wfcCatalog.variants == null || wfcCatalog.variants.Length == 0 || wfcCatalog.variants.Length > 1024)
                return "WFC 후보는 1~1024개여야 합니다.";
            if (transform.lossyScale != Vector3.one || !Cardinal(transform)
                || !Finite(generationBounds.center) || !Finite(generationBounds.size)
                || generationBounds.size.x <= 0f || generationBounds.size.y <= 0f || generationBounds.size.z <= 0f)
                return "생성 영역·배율·90도 회전을 확인하십시오.";

            foreach (WfcTileVariant variant in wfcCatalog.variants)
            {
                if (variant == null || variant.prefab == null || variant.surfaces == null || variant.required == null
                    || variant.surfaces.Length != 12 || variant.required.Length != 12
                    || !float.IsFinite(variant.weight) || variant.weight <= 0f)
                    return "WFC 후보의 원형·표면·출입구·가중치 데이터가 유효하지 않습니다.";
                if (variant.prefab.transform.localScale != Vector3.one || variant.turn < 0 || variant.turn > 3)
                    return "WFC 원형 배율과 회전을 확인하십시오.";
            }
            return null;
        }

        private bool TryGenerateWfc(System.Random random, List<TerrainPiece> pieces, List<TerrainLink> links, ref int checks)
        {
            var solver = new WfcTerrainSolver(wfcCatalog.variants);
            if (!solver.Solve(gridWidth, gridHeight, random, out int[] choices, ref checks)) return false;
            Quaternion rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            for (int cell = 0; cell < choices.Length; cell++)
            {
                WfcTileVariant variant = wfcCatalog.variants[choices[cell]];
                TerrainPiece piece = Instantiate(variant.prefab, generatedRoot.transform);
                if (piece.TryGetComponent(out ModularTerrainTile modular)) modular.ApplyCombination(variant.combination);
                // Y 기준은 모든 셀에 동일합니다. 고저차는 타일 내부 형상으로만 표현합니다.
                Vector3 offset = new Vector3((cell % gridWidth) * 10f, 0f, (cell / gridWidth) * 10f);
                piece.transform.SetPositionAndRotation(transform.position + rotation * offset,
                    rotation * Quaternion.Euler(0f, variant.turn * 90f, 0f));
                if (!InsideBounds(piece.WorldOccupancy)) return false;
                piece.name = $"{variant.prefab.name}_C{variant.combination}_R{variant.turn}";
                pieces.Add(piece);
            }

            for (int cell = 0; cell < pieces.Count; cell++)
            {
                foreach (int side in new[] { 0, 1 })
                {
                    int neighbour = WfcTerrainSolver.Neighbour(cell, side, gridWidth, gridHeight);
                    if (neighbour < 0) continue;
                    foreach (TerrainConnection from in pieces[cell].Connections)
                    foreach (TerrainConnection to in pieces[neighbour].Connections)
                        if (from.Matches(to)) links.Add(new TerrainLink(from, to));
                }
            }
            return Connected(pieces, links);
        }

        private static bool Connected(List<TerrainPiece> pieces, List<TerrainLink> links)
        {
            var reached = new HashSet<TerrainPiece> { pieces[0] };
            bool changed;
            do
            {
                changed = false;
                foreach (TerrainLink link in links)
                {
                    TerrainPiece a = link.From.GetComponentInParent<TerrainPiece>(true);
                    TerrainPiece b = link.To.GetComponentInParent<TerrainPiece>(true);
                    if (reached.Contains(a)) changed |= reached.Add(b);
                    if (reached.Contains(b)) changed |= reached.Add(a);
                }
            } while (changed);
            return reached.Count == pieces.Count;
        }
    }
}
