using Nomad.Terrain;
using Nomad.Props;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Loot
{
    // B 검토 장면 전용입니다. 실제 필드 Ready/플레이어 생성 계약은 #18에서 연결합니다.
    public sealed class FieldPreview : MonoBehaviour
    {
        [SerializeField] private TerrainGenerator generator;
        [SerializeField] private TerrainPiece[] pieces;
        [SerializeField] private PropScatter scatter;
        [SerializeField] private GameObject player;
        [SerializeField] private UnityEngine.UI.Text status;
        [SerializeField] private int seed = 16;

        private Vector3 spawn;
        public int Seed => seed;
        public string Summary => status.text;

        private void Start() => Regenerate(true);

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard?.rKey.wasPressedThisFrame == true) Regenerate(true);
            else if (keyboard?.tKey.wasPressedThisFrame == true) Regenerate(false);

            if (player.activeSelf && player.transform.position.y < spawn.y - 15f)
                PlacePlayer(spawn);
        }

        public void Regenerate(bool newSeed)
        {
            // 이동/구르기 상태와 이전 충돌체를 끊은 뒤 새 지형으로 재배치합니다.
            player.SetActive(false);
            generator.Configure(pieces, 12, seed,
                new Bounds(Vector3.zero, new Vector3(400, 100, 400)), newSeed);
            TerrainGenerationResult result = generator.Generate();
            seed = result.Seed;
            Physics.SyncTransforms();

            bool ready = result.Success && TryFindSpawn(result, out spawn);
            if (ready) PlacePlayer(spawn);
            string state = ready ? "Ready" : result.Success ? "No safe player start" : result.FailureReason;
            status.text = $"FIELD REVIEW   |   Seed {seed}\n"
                + $"{state}   |   Tiles {result.Pieces.Count}   Props {scatter.Positions.Count}\n"
                + "R  New random field     T  Replay current seed\n"
                + "WASD  Move     Space  Jump     Left Shift  Roll\n"
                + "E interaction: pending A integration";
        }

        private void PlacePlayer(Vector3 position)
        {
            player.SetActive(false);
            player.transform.SetPositionAndRotation(position, Quaternion.identity);
            player.SetActive(true);
            Physics.SyncTransforms();
        }

        private static bool TryFindSpawn(TerrainGenerationResult result, out Vector3 point)
        {
            foreach (TerrainPiece piece in result.Pieces)
            {
                if (piece.Kind == TerrainPieceKind.Stairs) continue;
                Vector3 origin = piece.WorldOccupancy.center;
                origin.y = piece.WorldOccupancy.max.y + 1f;
                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f, ~0,
                    QueryTriggerInteraction.Ignore) || !FieldGround.BelongsTo(hit.collider, hit.point, piece)
                    || hit.normal.y < 0.99f) continue;

                point = hit.point + Vector3.up * 0.05f;
                if (!Physics.CheckCapsule(point + Vector3.up * 0.35f,
                    point + Vector3.up * 1.45f, 0.35f, ~0, QueryTriggerInteraction.Ignore))
                    return true;
            }
            point = default;
            return false;
        }
    }
}
