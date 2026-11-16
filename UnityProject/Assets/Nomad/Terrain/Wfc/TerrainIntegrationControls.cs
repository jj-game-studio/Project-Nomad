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
        [SerializeField, Range(12, 24)] private int width = 6;
        [SerializeField, Range(12, 24)] private int height = 6;

        [SerializeField] private TerrainPiece[] microSources;

        private Vector3 spawn;
        private bool legacy;
        private bool props = true;
        private int view;
        private GameObject gallery;
        public string NavigationFailure { get; private set; }
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
            bool micro = !legacy && catalog.microTiles;
            scatter.Configure(generator, propTemplate, props ? 2 : 0, micro ? 0.18f : PropScatter.PropRadius, micro ? 0.36f : PropScatter.PropHeight);
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(400f, 100f, 400f));
            if (legacy) generator.Configure(legacySources, 12, seed, bounds, newSeed);
            else generator.ConfigureWfc(catalog, width, height, seed, bounds, newSeed);
            TerrainGenerationResult result = generator.Generate();
            seed = result.Seed;
            Physics.SyncTransforms();

            string navigationError = string.Empty;
            bool CheckNavigation() => MicroTerrainWalkability.Validate(result.Pieces[0].transform.parent.gameObject,
                result.Pieces, out _, out navigationError, scatter.gameObject);
            bool navigation = !micro || !result.Success || CheckNavigation();
            int repairs = 0;
            while (!navigation && repairs++ < 16)
            {
                int removed = scatter.RemoveNear(MicroTerrainWalkability.BlockedPoint, 1.5f);
                if (removed == 0)
                    removed = scatter.RemoveNear(Vector3.zero, float.PositiveInfinity, Mathf.Max(1, scatter.Positions.Count / 2));
                if (removed == 0) break;
                navigation = CheckNavigation();
            }
            if (!navigation)
            {
                // 소품을 모두 제외해도 실패하면 플레이 가능으로 공개하지 않습니다.
                scatter.Configure(generator, propTemplate, 0, 0.18f, 0.36f);
                navigation = CheckNavigation();
            }
            NavigationFailure = navigationError;
            bool safe = false;
            if (result.Success && navigation)
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
                + "O Field overview/player | G 3m tile gallery/player\n"
                + "WASD Move | Space Jump | Left Shift Roll | Fall: respawn\n"
                + $"Levels 0/1/2m | Reachable samples {generator.ReachableSamples} | {NavigationFailure}{generator.WalkabilityFailure}";
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
                : legacy ? generator.transform.position : generator.transform.position + new Vector3((width - 1) * catalog.cellSize * 0.5f, 0f, (height - 1) * catalog.cellSize * 0.5f);
            if (view == 2 && catalog.microTiles) center = new Vector3(215f, 0f, 3f);
            float distance = view == 2 ? (catalog.microTiles ? 30f : 160f) : legacy ? 100f : Mathf.Max(width, height) * catalog.cellSize * 1.4f;
            viewCamera.transform.position = center + new Vector3(0f, distance, -distance * 0.6f);
            viewCamera.transform.LookAt(center);
        }

        private void CreateGallery()
        {
            gallery = new GameObject("IntegrationCombinationGallery");
            gallery.transform.SetParent(transform, false);
            gallery.SetActive(false);
            if (catalog.microTiles)
            {
                for (int index = 0; index < microSources.Length; index++)
                {
                    TerrainPiece piece = Instantiate(microSources[index], gallery.transform);
                    piece.name = microSources[index].name;
                    piece.transform.position = new Vector3(200f + index % 6 * 6f, 0f, index / 6 * 6f);
                }
                gallery.SetActive(true);
                return;
            }
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
