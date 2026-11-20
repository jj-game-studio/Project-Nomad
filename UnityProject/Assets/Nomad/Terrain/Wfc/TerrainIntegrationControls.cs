using System.Linq;
using Nomad.Player;
using Nomad.Loot;
using Nomad.Props;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Terrain
{
    // 지형·소품 검사가 끝난 뒤 최종 경로와 Ready 결과를 연결합니다.
    [RequireComponent(typeof(FieldNavigation))]
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
        private GameObject exitRoot;
        public string NavigationFailure { get; private set; }
        public string LastReachedExit { get; private set; }
        public int Seed => seed;
        public bool Legacy => legacy;
        public string Summary => status.text;
        public TerrainGenerator Generator => generator;
        public GameObject Player => player;
        public FieldNavigation Navigation => GetComponent<FieldNavigation>();

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
            ClearExits();
            GetComponent<TerrainLootIntegration>()?.Refresh(null);
            if (Navigation == null) gameObject.AddComponent<FieldNavigation>();
            Navigation.Configure(generator);
            bool micro = !legacy && catalog.microTiles;
            float crateSize = micro ? MicroTerrainTile.Size * 0.4f : 1f;
            Transform crate = propTemplate.transform.GetChild(0);
            crate.localScale = Vector3.one * crateSize;
            crate.localPosition = Vector3.up * (crateSize * 0.5f);
            scatter.Configure(generator, propTemplate, props ? (micro ? 1 : 2) : 0,
                micro ? crateSize * 0.5f : PropScatter.PropRadius, micro ? crateSize : PropScatter.PropHeight);
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
                scatter.Configure(generator, propTemplate, 0, crateSize * 0.5f, crateSize);
                navigation = CheckNavigation();
            }
            FieldGenerationResult field = Navigation.Prepare(result, scatter.gameObject);
            NavigationFailure = field.FailureReason;
            bool safe = field.Success;
            spawn = field.PlayerPosition + Vector3.up * 0.05f;
            if (safe) CreateExits(field);
            if (safe) PlacePlayer();
            SetView(safe ? view : 1);
            status.text = $"TERRAIN INTEGRATION | {(legacy ? "Legacy" : "WFC")} | Seed {seed}\n"
                + $"{Navigation.State} | Tiles {result.Pieces.Count} | Props {scatter.Positions.Count} | Enemy slots {field.EnemyPoints.Count} | Chest slots {field.ChestPoints.Count}"
                + $" | Exits {field.ExitPoints.Count} ({string.Join("/", field.ExitPoints.Select(point => point.Id.Replace("exit-", "")))})\n"
                + $"Attempts {result.Attempts} | Candidate checks {result.CandidateChecks}\n"
                + "R New seed | T Replay | L WFC/Legacy | P Props on/off\n"
                + "O Field overview/player | G 3m tile gallery/player\n"
                + "WASD Move | Space Jump | Left Shift Roll | Fall: respawn\n"
                + $"Levels 0/1/2m | Reachable samples {generator.ReachableSamples} | {NavigationFailure}{generator.WalkabilityFailure}";
            // 상자는 Ready 결과의 상자 지점에만 배치합니다. 실패하면 상자를 만들지 않습니다.
            GetComponent<TerrainLootIntegration>()?.Refresh(safe ? field : null);
            return result;
        }

        // 출구 결과를 임시 표식으로 보여 주고, 플레이어 진입을 HUD에 표시합니다.
        private void CreateExits(FieldGenerationResult field)
        {
            exitRoot = new GameObject("FieldExits");
            exitRoot.transform.SetParent(transform, false);
            foreach (FieldSpawnPoint point in field.ExitPoints)
                FieldExit.Create(point, exitRoot.transform).Entered += OnExitEntered;
        }

        private void OnExitEntered(FieldExit exit, Collider other)
        {
            if (!other.transform.IsChildOf(player.transform)) return;
            LastReachedExit = exit.Point.Id;
            Debug.Log($"FIELD_EXIT_REACHED {exit.Point.Id} seed={seed}");

            // 이전 도달 표시 줄만 바꿔 다른 HUD 내용은 유지합니다.
            int line = status.text.IndexOf("\nExit reached");
            status.text = (line < 0 ? status.text : status.text.Substring(0, line)) + "\nExit reached: " + exit.Point.Id;
        }

        private void ClearExits()
        {
            LastReachedExit = null;
            if (exitRoot == null) return;

            exitRoot.SetActive(false);
            if (Application.isPlaying) Destroy(exitRoot);
            else DestroyImmediate(exitRoot);
            exitRoot = null;
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
