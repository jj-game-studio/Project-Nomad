using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Terrain
{
    public sealed partial class TerrainGenerator
    {
        public bool UsesMicro => wfcCatalog != null && wfcCatalog.microTiles;
        public int ReachableSamples { get; private set; }
        public string WalkabilityFailure { get; private set; }

        private bool TryGenerateMicro(System.Random random, List<TerrainPiece> pieces, List<TerrainLink> links, ref int checks)
        {
            ReachableSamples = 0;
            WalkabilityFailure = string.Empty;
            var plan = new MicroTerrainPlan(gridWidth, gridHeight, random);
            var solver = new WfcTerrainSolver(wfcCatalog.variants);
            if (!solver.Solve(gridWidth, gridHeight, random, out int[] choices, ref checks, plan.Allows)) return false;
            Quaternion rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            for (int cell = 0; cell < choices.Length; cell++)
            {
                WfcTileVariant variant = wfcCatalog.variants[choices[cell]];
                TerrainPiece piece = Instantiate(variant.prefab, generatedRoot.transform);
                piece.transform.SetPositionAndRotation(transform.position + rotation * new Vector3(cell % gridWidth * 3f, 0f, cell / gridWidth * 3f),
                    rotation * Quaternion.Euler(0f, variant.turn * 90f, 0f));
                piece.name = variant.prefab.name + "_R" + variant.turn;
                if (!InsideBounds(piece.WorldOccupancy)) return false;
                pieces.Add(piece);
            }
            MicroTerrainWalls.Build(generatedRoot.transform, gridWidth, gridHeight, choices, wfcCatalog);
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
            if (!MicroTerrainWalkability.Validate(generatedRoot, pieces, out int samples, out string reason))
            {
                WalkabilityFailure = reason;
                return false;
            }
            ReachableSamples = samples;
            return true;
        }
    }
}
