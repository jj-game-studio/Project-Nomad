using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Terrain
{
    public enum TerrainFloorState { None, Low, High }

    [Serializable]
    public sealed class TerrainFloorRegion
    {
        public GameObject low;
        public GameObject high;
        public TerrainConnection[] lowPorts;
        public TerrainConnection[] highPorts;
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(TerrainPiece))]
    public sealed class ModularTerrainTile : MonoBehaviour
    {
        [SerializeField] private TerrainFloorRegion[] regions = Array.Empty<TerrainFloorRegion>();
        [SerializeField] private TerrainConnection[] fixedPorts = Array.Empty<TerrainConnection>();
        [SerializeField] private int combination;

        public int Combination => combination;
        public int RegionCount => regions.Length;
        public int CombinationCount
        {
            get
            {
                int count = 1;
                for (int index = 0; index < regions.Length; index++)
                    count *= 3;

                return count;
            }
        }

        public TerrainFloorState GetState(int region)
        {
            if (region < 0 || region >= regions.Length)
                throw new ArgumentOutOfRangeException(nameof(region));

            int value = combination;
            for (int index = 0; index < region; index++)
                value /= 3;

            return (TerrainFloorState)(value % 3);
        }

        public void Configure(TerrainFloorRegion[] floorRegions, TerrainConnection[] ports)
        {
            regions = floorRegions;
            fixedPorts = ports;
            ApplyCombination(0);
        }

        public void ApplyCombination(int value)
        {
            if (value < 0 || value >= CombinationCount)
                throw new ArgumentOutOfRangeException(nameof(value));

            combination = value;
            var ports = new List<TerrainConnection>(fixedPorts);
            for (int index = 0; index < regions.Length; index++)
            {
                TerrainFloorRegion region = regions[index];
                TerrainFloorState state = GetState(index);
                // 외형과 충돌·연결점을 같은 그룹으로 전환해 보이지 않는 바닥을 방지합니다.
                region.low.SetActive(state == TerrainFloorState.Low);
                region.high.SetActive(state == TerrainFloorState.High);
                if (state != TerrainFloorState.None)
                    ports.AddRange(state == TerrainFloorState.Low ? region.lowPorts : region.highPorts);
            }

            TerrainPiece piece = GetComponent<TerrainPiece>();
            piece.Configure(piece.Kind, piece.LocalOccupancy, ports.ToArray());
        }

        private void Awake()
        {
            ApplyCombination(combination);
        }
    }
}
