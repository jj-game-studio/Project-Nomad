using Nomad.Player;
using Nomad.Props;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Terrain
{
    // 지형 통합 씬 전용입니다. 실제 필드 Ready/NavMesh 계약은 #18에서 제공합니다.
    public sealed class TerrainIntegrationControls : MonoBehaviour
    {
        [SerializeField] private TerrainGenerator generator;
        [SerializeField] private WfcTerrainCatalog catalog;
        [SerializeField] private TerrainPiece[] legacySources;
        [SerializeField] private TerrainPiece[] modularSources;
        [SerializeField] private PropScatter scatter;
        [SerializeField] private GameObject propTemplate;
        [SerializeField] private GameObject player;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private UnityEngine.UI.Text status;
        [SerializeField] private int seed = 51;
        [SerializeField, Range(3, 16)] private int width = 6;
        [SerializeField, Range(3, 16)] private int height = 6;

        private Vector3 spawn;
        private bool legacy;
        private bool props = true;
        private int view;
        private GameObject gallery;
        public int Seed => seed;
        public bool Legacy => legacy;
        public string Summary => status.text;
        public TerrainGenerator Generator => generator;
        public GameObject Player => player;

        private void Start() => Regenerate(false);

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard?.rKey.wasPressedThisFrame == true) Regenerate(true);
            else if (keyboard?.tKey.wasPressedThisFrame == true) Regenerate(false);
            else if (keyboard?.lKey.wasPressedThisFrame == true) ToggleLegacy();
            else if (keyboard?.pKey.wasPressedThisFrame == true)
            {
                props = !props;
                Regenerate(false);
            }
            if (keyboard?.oKey.wasPressedThisFrame == true) SetView(view == 1 ? 0 : 1);
            if (keyboard?.gKey.wasPressedThisFrame == true) SetView(view == 2 ? 0 : 2);
            if (player.activeSelf && player.transform.position.y < spawn.y - 15f) PlacePlayer();
        }

        public void ToggleLegacy()
        {
            legacy = !legacy;
            Regenerate(false);
        }

        public TerrainGenerationResult Regenerate(bool newSeed)
        {
            player.SetActive(false);
            scatter.Configure(generator, propTemplate, props ? 2 : 0);
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(400f, 100f, 400f));
            if (legacy) generator.Configure(legacySources, 12, seed, bounds, newSeed);
            else generator.ConfigureWfc(catalog, width, height, seed, bounds, newSeed);
            TerrainGenerationResult result = generator.Generate();
            seed = result.Seed;
            Physics.SyncTransforms();

            bool safe = false;
            if (result.Success)
            {
                foreach (TerrainPiece piece in result.Pieces)
                {
                    if (piece.Kind == TerrainPieceKind.Stairs) continue;
                    Vector3 origin = piece.WorldOccupancy.center + Vector3.up * 5f;
                    if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f)
                        || !hit.collider.transform.IsChildOf(piece.transform)) continue;
                    spawn = hit.point + Vector3.up * 0.05f;
                    if (Physics.CheckCapsule(spawn + Vector3.up * 0.35f, spawn + Vector3.up * 1.45f, 0.35f)) continue;
                    safe = true;
                    break;
                }
            }
            if (safe) PlacePlayer();
            SetView(safe ? view : 1);
            status.text = $"TERRAIN INTEGRATION | {(legacy ? "Legacy" : "WFC")} | Seed {seed}\n"
                + $"{(safe ? "Playable" : result.Success ? "No safe start" : result.FailureReason)} | Tiles {result.Pieces.Count} | Props {scatter.Positions.Count}\n"
                + $"Attempts {result.Attempts} | Candidate checks {result.CandidateChecks}\n"
                + "R New seed | T Replay | L WFC/Legacy | P Props on/off\n"
                + "O Field overview/player | G 108 combinations + 4 new stairs/player\n"
                + "WASD Move | Space Jump | Left Shift Roll | Fall: respawn\n"
                + "Single surface per XZ cell | Navigation/monster integration pending";
            return result;
        }

        private void PlacePlayer()
        {
            player.SetActive(false);
            player.transform.SetPositionAndRotation(spawn, Quaternion.identity);
            player.SetActive(true);
            Physics.SyncTransforms();
        }

        public void SetView(int mode)
        {
            view = mode;
            viewCamera.GetComponent<TopDownCamera>().enabled = view == 0;
            if (gallery != null) gallery.SetActive(view == 2);
            if (view == 0)
            {
                viewCamera.transform.position = player.transform.position + new Vector3(0f, 12f, -8f);
                viewCamera.transform.LookAt(player.transform.position);
                return;
            }
            if (view == 2 && gallery == null) CreateGallery();
            Vector3 center = view == 2 ? new Vector3(277f, 0f, 56f)
                : legacy ? generator.transform.position : generator.transform.position + new Vector3((width - 1) * 5f, 0f, (height - 1) * 5f);
            float distance = view == 2 ? 160f : legacy ? 100f : Mathf.Max(width, height) * 13f;
            viewCamera.transform.position = center + new Vector3(0f, distance, -distance * 0.6f);
            viewCamera.transform.LookAt(center);
        }

        private void CreateGallery()
        {
            gallery = new GameObject("IntegrationCombinationGallery");
            gallery.transform.SetParent(transform, false);
            gallery.SetActive(false);
            int slot = 0;
            for (int kind = 0; kind < 2; kind++)
            {
                int count = modularSources[kind].GetComponent<ModularTerrainTile>().CombinationCount;
                for (int value = 0; value < count; value++, slot++)
                {
                    TerrainPiece piece = Instantiate(modularSources[kind], gallery.transform);
                    piece.transform.position = new Vector3(200f + slot % 12 * 14f, 0f, slot / 12 * 14f);
                    piece.GetComponent<ModularTerrainTile>().ApplyCombination(value);
                    piece.name = $"{modularSources[kind].name}_Combination_{value}";
                }
            }
            for (int index = 3; index < modularSources.Length; index++, slot++)
            {
                TerrainPiece piece = Instantiate(modularSources[index], gallery.transform);
                piece.transform.position = new Vector3(200f + slot % 12 * 14f, 0f, slot / 12 * 14f);
                piece.name = modularSources[index].name;
            }
            gallery.SetActive(true);
        }
    }
}
