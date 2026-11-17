using System.Collections.Generic;
using Nomad.Combat;
using Nomad.Interaction;
using Nomad.Inventory;
using Nomad.Monsters;
using Nomad.Monsters.Demo;
using Nomad.Player;
using Nomad.Terrain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Nomad.Gameplay
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(TerrainIntegrationControls), typeof(MonsterActorFactory))]
    public sealed class GameplayIntegration : MonoBehaviour
    {
        [SerializeField] private int normalCount = 6;
        [SerializeField] private int eliteCount = 1;
        [SerializeField] private int bossCount = 1;
        [SerializeField] private GameObject[] diagnosticPanels = System.Array.Empty<GameObject>();
        private readonly List<Health> enemies = new List<Health>();
        private TerrainIntegrationControls controls;
        private MonsterActorFactory factory;
        private Health player;
        private PlayerCombat shooting;
        private PlayerInteraction interaction;
        private PlayerInventory inventory;
        private GameObject enemyRoot;
        private string generationId;
        private bool restarting;
        private bool diagnostics;
        private bool detection = true;
        private bool travel;
        private bool flash = true;
        private bool numbers = true;
        private bool shards = true;
        private string message = "Preparing field...";

        public IReadOnlyList<Health> Enemies => enemies;
        public string GenerationId => generationId;
        public bool IsDefeated { get; private set; }
        public Health PlayerHealth => player;
        public bool Ready => controls.Navigation.State == FieldGenerationState.Ready;
        public int AliveEnemies
        {
            get
            {
                int count = 0;
                foreach (Health enemy in enemies) if (enemy != null && !enemy.IsDead) count++;
                return count;
            }
        }

        private float UiScale => Mathf.Min(1f, Screen.width / 1280f, Screen.height / 800f);
        private Rect StatusRect => new Rect(diagnostics ? Screen.width / UiScale - 330f : 12f, 12f, 318f, 155f);
        private Rect DebugRect => new Rect(Screen.width / UiScale - 330f, 175f, 318f, 180f);
        private Rect RetryRect => new Rect((Screen.width / UiScale - 350f) * 0.5f,
            (Screen.height / UiScale - 150f) * 0.5f, 350f, 150f);

        private void Awake()
        {
            controls = GetComponent<TerrainIntegrationControls>();
            factory = GetComponent<MonsterActorFactory>();
            player = controls.Player.GetComponent<Health>();
            shooting = controls.Player.GetComponent<PlayerCombat>();
            interaction = controls.Player.GetComponent<PlayerInteraction>();
            inventory = controls.Player.GetComponent<PlayerInventory>();
            controls.Player.GetComponent<SegmentedHealthBar>().Compact = true;
            player.Died += OnPlayerDied;
            controls.Navigation.GenerationStarted += OnGenerationStarted;
            controls.Navigation.GenerationCompleted += OnGenerationCompleted;

            if (EventSystem.current == null)
            {
                var events = new GameObject("Gameplay EventSystem");
                SceneManager.MoveGameObjectToScene(events, gameObject.scene);
                events.AddComponent<EventSystem>();
            }
            gameObject.AddComponent<MonsterTestInputBlocker>().AdditionalBlocker = BlocksPointer;
            foreach (GameObject panel in diagnosticPanels) if (panel != null) panel.SetActive(false);
        }

        private void Start()
        {
            // 생성이 먼저 끝난 경우도 동일 세대를 중복 배치하지 않고 소비합니다.
            if (controls.Navigation.LastResult != null)
                OnGenerationCompleted(controls.Navigation.LastResult);
        }

        private void Update()
        {
            if (Keyboard.current?.f1Key.wasPressedThisFrame == true)
            {
                diagnostics = !diagnostics;
                foreach (GameObject panel in diagnosticPanels) if (panel != null) panel.SetActive(diagnostics);
            }
            if (IsDefeated && Keyboard.current?.rKey.wasPressedThisFrame == true)
                RestartRun();

            bool playable = Ready && !IsDefeated && controls.GameplayView;
            shooting.enabled = playable;
            interaction.enabled = playable;
            foreach (Health enemy in enemies)
            {
                if (enemy == null) continue;
                enemy.GetComponent<MonsterHitFeedback>().SetEffects(flash, numbers, shards);
                var ranges = enemy.GetComponent<MonsterTestRanges>();
                ranges.ShowDetection = detection && !enemy.IsDead;
                ranges.ShowTravel = diagnostics && travel && !enemy.IsDead;
            }
        }

        private void OnGenerationStarted(string id)
        {
            ClearGeneration();
            generationId = null;
            message = "Preparing field...";
        }

        private void OnGenerationCompleted(FieldGenerationResult field)
        {
            if (!field.Success)
            {
                message = "Field failed: " + field.FailureReason;
                return;
            }
            if (generationId == field.GenerationId) return;
            ClearGeneration();
            generationId = field.GenerationId;
            enemyRoot = new GameObject("Field enemies " + generationId);
            SceneManager.MoveGameObjectToScene(enemyRoot, gameObject.scene);
            var candidates = new List<FieldSpawnPoint>(field.EnemyPoints);
            var random = new System.Random(field.Seed);
            for (int index = candidates.Count - 1; index > 0; index--)
            {
                int other = random.Next(index + 1);
                FieldSpawnPoint point = candidates[index];
                candidates[index] = candidates[other];
                candidates[other] = point;
            }

            int normals = 0;
            int elites = 0;
            int bosses = 0;
            foreach (FieldSpawnPoint point in candidates)
            {
                if (normals >= normalCount && elites >= eliteCount && bosses >= bossCount) break;
                if (Vector3.Distance(point.Position, field.PlayerPosition) < 12f) continue;
                if (Physics.CheckCapsule(point.Position + Vector3.up * 0.4f,
                    point.Position + Vector3.up * 1.5f, 0.35f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore)) continue;

                MonsterRank rank = normals < normalCount ? MonsterRank.Normal :
                    elites < eliteCount ? MonsterRank.Elite : MonsterRank.Boss;
                MonsterTestSettings settings = MonsterTestSettings.Defaults(rank);
                if (rank == MonsterRank.Normal) settings.normalStyle = normals % 3;
                Health enemy = factory.CreateRanked(player, settings, rank,
                    point.Position, point.Rotation, rank + " " + point.Id, enemyRoot.transform);
                enemy.gameObject.AddComponent<MonsterTestRanges>();
                enemies.Add(enemy);
                if (rank == MonsterRank.Boss) bosses++;
                else if (rank == MonsterRank.Elite) elites++;
                else normals++;
            }
            message = $"Normal {normals} / Elite {elites} / Boss {bosses} (test)\nDeath drops: visual only, no pickup.";
        }

        private void ClearGeneration()
        {
            // NavMesh가 교체되는 프레임에 이전 세대 개체가 계속 이동하지 않도록 즉시 끕니다.
            if (enemyRoot != null)
            {
                enemyRoot.SetActive(false);
                Destroy(enemyRoot);
                enemyRoot = null;
            }
            enemies.Clear();
            foreach (MonsterBolt bolt in FindObjectsByType<MonsterBolt>(FindObjectsSortMode.None))
            {
                if (bolt.gameObject.scene != gameObject.scene) continue;
                bolt.gameObject.SetActive(false);
                Destroy(bolt.gameObject);
            }
            foreach (Projectile projectile in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            {
                if (projectile.gameObject.scene != gameObject.scene) continue;
                projectile.gameObject.SetActive(false);
                Destroy(projectile.gameObject);
            }
        }

        private void OnPlayerDied(Health value)
        {
            IsDefeated = true;
            controls.enabled = false;
            shooting.enabled = false;
            interaction.enabled = false;
            message = "Defeated. Restart resets this run and inventory.";
        }

        public void RestartRun()
        {
            if (restarting) return;
            restarting = true;
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(gameObject.scene.path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadSceneAsync(gameObject.scene.buildIndex);
#endif
        }

        private bool BlocksPointer(Vector2 pointer)
        {
            Vector2 gui = new Vector2(pointer.x, Screen.height - pointer.y) / UiScale;
            return StatusRect.Contains(gui) || (diagnostics && DebugRect.Contains(gui)) ||
                (IsDefeated && RetryRect.Contains(gui));
        }

        private void OnGUI()
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * UiScale);
            int scrap = 0;
            if (inventory != null)
                for (int i = 0; i < inventory.UsedSlots; i++)
                    if (inventory.GetStack(i).Item.ItemId == "scrap") scrap += inventory.GetStack(i).Count;
            GUI.Box(StatusRect, "NOMAD - INTEGRATED PLAYTEST");
            GUILayout.BeginArea(new Rect(StatusRect.x + 10f, 35f, 298f, 125f));
            GUILayout.Label($"HP {player.CurrentHealth}/{player.MaxHealth} | Enemies {AliveEnemies}/{enemies.Count}");
            GUILayout.Label($"Scrap {scrap} | Inventory {inventory.UsedSlots}/20 | Seed {controls.Seed}");
            GUILayout.Label(Ready && AliveEnemies == 0 && !IsDefeated ? "Patrols cleared. Loot crates or explore another field." : message);
            GUILayout.Label("WASD / LMB shoot / E loot / Shift roll\nR new field / T replay / F1 diagnostics");
            GUILayout.EndArea();
            if (diagnostics)
            {
                GUI.Box(DebugRect, "DEBUG DISPLAY");
                GUILayout.BeginArea(new Rect(DebugRect.x + 10f, DebugRect.y + 25f, 298f, 150f));
                detection = GUILayout.Toggle(detection, "Detection radius");
                travel = GUILayout.Toggle(travel, "Target gap / home radius");
                flash = GUILayout.Toggle(flash, "Hit flash");
                numbers = GUILayout.Toggle(numbers, "Damage numbers");
                shards = GUILayout.Toggle(shards, "Hit shards");
                GUILayout.Label("O overview | G gallery | P props | L legacy");
                GUILayout.EndArea();
            }
            if (IsDefeated)
            {
                GUI.Box(RetryRect, "DEFEATED");
                GUILayout.BeginArea(new Rect(RetryRect.x + 20f, RetryRect.y + 30f, 310f, 100f));
                GUILayout.Label("This run has ended. Restart clears carried loot.");
                if (GUILayout.Button("Restart run (R)", GUILayout.Height(35f))) RestartRun();
                GUILayout.EndArea();
            }
            GUI.matrix = previousMatrix;
        }

        private void OnDestroy()
        {
            if (player != null) player.Died -= OnPlayerDied;
            if (controls != null && controls.Navigation != null)
            {
                controls.Navigation.GenerationStarted -= OnGenerationStarted;
                controls.Navigation.GenerationCompleted -= OnGenerationCompleted;
            }
        }
    }
}
