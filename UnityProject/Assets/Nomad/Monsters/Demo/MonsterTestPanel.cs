using System.Collections.Generic;
using System.Reflection;
using Nomad.Combat;
using Nomad.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Nomad.Monsters.Demo
{
    [DefaultExecutionOrder(50)]
    public sealed class MonsterTestPanel : MonoBehaviour
    {
        [SerializeField] private Health player;
        [SerializeField] private Health initialMonster;
        [SerializeField] private bool initialElite;
        [SerializeField] private Material normalMaterial;
        [SerializeField] private Material eliteMaterial;
        [SerializeField] private Material warningMaterial;
        [SerializeField] private Material fillMaterial;

        private sealed class Entry
        {
            public Health health;
            public MonsterTracker tracker;
            public MonsterMeleeAttack attack;
            public MonsterTestRanges ranges;
            public bool elite;
            public int previousHp;
            public string previousState;
        }

        private readonly List<Entry> monsters = new List<Entry>();
        private readonly List<string> logs = new List<string>();
        private readonly MonsterTestSettings[] settings =
            { MonsterTestSettings.Defaults(false), MonsterTestSettings.Defaults(true) };
        private Entry selected;
        private int kind;
        private int serial;
        private int previousPlayerHp;
        private int placementClickFrame = -1;
        private bool detection = true;
        private bool travel;
        private bool placing;
        private bool paused;
        private bool invincible;
        private bool restarting;
        private float previousTimeScale;
        private Vector2 scroll;
        private string notice = "Defaults are temporary test values.";

        public int MonsterCount => monsters.Count;
        public bool IsPlacing => placing;
        public bool Invincible => invincible;
        private float UiScale => Mathf.Min(1f, Screen.width / 1280f, Screen.height / 800f);
        public Rect SpawnPanel => new Rect(Screen.width / UiScale - 346f, 12f, 334f, Screen.height / UiScale - 24f);
        public Rect LogPanel => new Rect(12f, 12f, 365f, 270f);

        private void Start()
        {
            previousTimeScale = Time.timeScale;
            previousPlayerHp = player.CurrentHealth;
            var bar = player.GetComponent<SegmentedHealthBar>();
            if (bar == null)
                bar = player.gameObject.AddComponent<SegmentedHealthBar>();
            bar.Compact = true;

            if (EventSystem.current == null)
            {
                var events = new GameObject("Test EventSystem");
                SceneManager.MoveGameObjectToScene(events, gameObject.scene);
                events.AddComponent<EventSystem>();
            }
            gameObject.AddComponent<MonsterTestInputBlocker>().Panel = this;
            selected = Register(initialMonster, initialElite);
            kind = initialElite ? 1 : 0;
            MonsterTestSettings config = settings[kind];
            selected.tracker.Configure(config.sightRange, config.sightAngle, config.proximityRange,
                config.abandonRange, config.homeRange);
            Log("Ready: WASD / LMB shoot / Shift roll / R reset");
        }

        private void Update()
        {
            // PlayerMovement의 구르기 무적 갱신 뒤, 몬스터 타격 판정 전에 적용합니다.
            if (invincible)
                player.IsCombatImmune = true;
            Mouse mouse = Mouse.current;
            if (placing && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                placing = false;
                notice = "Placement cancelled.";
            }
            if (placing && mouse?.leftButton.wasPressedThisFrame == true &&
                !OverPanels(mouse.position.ReadValue()))
            {
                placementClickFrame = Time.frameCount;
                Camera camera = Camera.main;
                if (camera != null && Physics.Raycast(camera.ScreenPointToRay(mouse.position.ReadValue()),
                    out RaycastHit hit, 300f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    // 벽이나 캐릭터 대신 위를 향한 바닥만 소환 위치로 지정합니다.
                    if (hit.normal.y > 0.7f && SpawnAt(hit.point) != null)
                        placing = false;
                    else
                        notice = "Choose a clear, walkable floor position.";
                }
            }
            if (Keyboard.current?.rKey.wasPressedThisFrame == true)
                Restart();

            foreach (Entry entry in monsters)
            {
                entry.ranges.ShowDetection = detection && !entry.health.IsDead;
                entry.ranges.ShowTravel = travel && !entry.health.IsDead;
                string state = State(entry);
                if (state != entry.previousState)
                {
                    Log(entry.health.name + ": " + state);
                    entry.previousState = state;
                }
                if (entry.health.CurrentHealth != entry.previousHp)
                {
                    Log($"{entry.health.name}: HP {entry.previousHp} -> {entry.health.CurrentHealth}");
                    entry.previousHp = entry.health.CurrentHealth;
                }
            }
            if (player.CurrentHealth != previousPlayerHp)
            {
                Log($"Player: HP {previousPlayerHp} -> {player.CurrentHealth}" + (player.IsDead ? " DEAD" : ""));
                previousPlayerHp = player.CurrentHealth;
            }
        }

        private void LateUpdate()
        {
            if (invincible)
                player.IsCombatImmune = true;
        }

        private Entry Register(Health value, bool elite)
        {
            var entry = new Entry { health = value, elite = elite, previousHp = value.CurrentHealth,
                tracker = value.GetComponent<MonsterTracker>(), attack = value.GetComponent<MonsterMeleeAttack>() };
            entry.ranges = value.GetComponent<MonsterTestRanges>();
            if (entry.ranges == null)
                entry.ranges = value.gameObject.AddComponent<MonsterTestRanges>();
            monsters.Add(entry);
            return entry;
        }

        public Health SpawnAt(Vector3 point)
        {
            if (kind == 2)
            {
                notice = "Boss is not implemented.";
                return null;
            }
            if (monsters.Count >= 12)
            {
                notice = "Limit: 12 monsters. Remove some first.";
                return null;
            }
            Physics.SyncTransforms();
            if (Physics.CheckCapsule(point + Vector3.up * 0.4f, point + Vector3.up * 1.6f,
                    0.38f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                !NavMesh.SamplePosition(point, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) ||
                Mathf.Abs(hit.position.y - point.y) > 1f ||
                Physics.CheckCapsule(hit.position + Vector3.up * 0.4f,
                    hit.position + Vector3.up * 1.6f, 0.38f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                notice = "No clear NavMesh position here.";
                return null;
            }

            bool elite = kind == 1;
            MonsterTestSettings config = settings[kind];
            // 비활성 상태에서 설정하여 Awake가 지정한 최대 체력으로 초기화하도록 합니다.
            var root = new GameObject((elite ? "Elite #" : "Normal #") + ++serial);
            SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            root.SetActive(false);
            root.transform.position = hit.position;
            Vector3 facing = Vector3.ProjectOnPlane(player.transform.position - hit.position, Vector3.up);
            if (facing.sqrMagnitude > 0.01f)
                root.transform.rotation = Quaternion.LookRotation(facing);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up * (elite ? 0.99f : 0.9f);
            body.transform.localScale = elite ? new Vector3(0.875f, 0.99f, 0.875f) : new Vector3(0.7f, 0.9f, 0.7f);
            body.GetComponent<Collider>().enabled = false;
            Destroy(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = elite ? eliteMaterial : normalMaterial;
            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            capsule.center = Vector3.up * 0.9f;
            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.speed = config.moveSpeed;
            agent.stoppingDistance = Mathf.Min(1.2f, config.attackRange * 0.7f);
            agent.angularSpeed = 360f;
            Health health = root.AddComponent<Health>();
            JsonUtility.FromJsonOverwrite("{\"maxHealth\":" + config.health + "}", health);
            root.AddComponent<SegmentedHealthBar>();
            MonsterTracker tracker = root.AddComponent<MonsterTracker>();
            tracker.BindTarget(player.transform);
            tracker.Configure(config.sightRange, config.sightAngle, config.proximityRange,
                config.abandonRange, config.homeRange);
            MonsterMeleeAttack attack = root.AddComponent<MonsterMeleeAttack>();
            attack.Configure(player, elite, config.damage, config.heavyDamage,
                config.attackRange, config.heavyRange, config.attackSpeed);

            LineRenderer outline = new GameObject("Attack Outline").AddComponent<LineRenderer>();
            outline.transform.SetParent(root.transform, false);
            outline.useWorldSpace = true;
            outline.widthMultiplier = 0.06f;
            outline.sharedMaterial = warningMaterial;
            MeshFilter fill = new GameObject("Attack Fill").AddComponent<MeshFilter>();
            fill.transform.SetParent(root.transform, false);
            fill.gameObject.AddComponent<MeshRenderer>().sharedMaterial = fillMaterial;
            root.AddComponent<MonsterCombatDemo>().ConfigureDisplay(player, health, attack,
                body.GetComponent<Renderer>(), outline, fill);
            root.SetActive(true);
            selected = Register(health, elite);
            notice = "Spawned " + root.name;
            Log(notice);
            return health;
        }

        public void SetKind(int value)
        {
            kind = Mathf.Clamp(value, 0, 2);
            placing = false;
        }

        public void ClearMonsters()
        {
            foreach (Entry entry in monsters)
            {
                entry.health.gameObject.SetActive(false);
                Destroy(entry.health.gameObject);
            }
            monsters.Clear();
            selected = null;
            Log("All monsters removed.");
        }

        public void RestorePlayer()
        {
            // 공용 전투 API에 치트용 기능을 추가하지 않고 테스트 씬에서만 회복/부활합니다.
            typeof(Health).GetProperty(nameof(Health.CurrentHealth), BindingFlags.Instance | BindingFlags.Public)
                .SetValue(player, player.MaxHealth);
            Log("Player restored.");
        }

        public void SetInvincible(bool value)
        {
            invincible = value;
            player.IsCombatImmune = value || player.GetComponent<PlayerMovement>().IsRolling;
            Log("Player invincible: " + value);
        }

        public bool BlocksPointer(Vector2 pointer) => placing || placementClickFrame == Time.frameCount || OverPanels(pointer);

        private bool OverPanels(Vector2 pointer)
        {
            Vector2 gui = new Vector2(pointer.x, Screen.height - pointer.y) / UiScale;
            return SpawnPanel.Contains(gui) || LogPanel.Contains(gui);
        }

        private string State(Entry entry)
        {
            if (entry.health.IsDead) return "DEAD";
            if (entry.tracker.IsReturning) return "RETURN HOME";
            if (entry.attack.Phase != MeleePhase.Idle)
                return (entry.attack.IsHeavyAttack ? "HEAVY " : "LIGHT ") + entry.attack.Phase;
            if (entry.tracker.PathBlocked) return "PATH BLOCKED";
            return entry.tracker.IsTracking ? (entry.tracker.HasLineOfSight ? "CHASE" : "SEARCH LAST SEEN") : "IDLE";
        }

        private void Log(string message)
        {
            logs.Add($"[{Time.time:0.0}] {message}");
            if (logs.Count > 60)
                logs.RemoveAt(0);
        }

        private void OnGUI()
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * UiScale);
            GUI.Box(LogPanel, "ACTION LOG");
            GUILayout.BeginArea(new Rect(LogPanel.x + 10f, 35f, LogPanel.width - 20f, 235f));
            for (int i = Mathf.Max(0, logs.Count - 9); i < logs.Count; i++)
                GUILayout.Label(logs[i]);
            if (GUILayout.Button("Clear log")) logs.Clear();
            GUILayout.EndArea();

            GUI.Box(SpawnPanel, "MONSTER TEST SPAWNER");
            GUILayout.BeginArea(new Rect(SpawnPanel.x + 10f, 37f, SpawnPanel.width - 20f, SpawnPanel.height - 32f));
            scroll = GUILayout.BeginScrollView(scroll);
            int nextKind = GUILayout.Toolbar(kind, new[] { "Normal", "Elite", "Boss" });
            if (nextKind != kind) SetKind(nextKind);
            if (kind == 2)
                GUILayout.Label("BOSS: NOT IMPLEMENTED");

            GUI.enabled = kind != 2;
            if (GUILayout.Button(placing ? "Click floor to spawn (Esc cancels)" : "Place on floor..."))
                placing = !placing;
            if (GUILayout.Button("Spawn 5m in front of player"))
                SpawnAt(player.transform.position + player.transform.forward * 5f);
            GUI.enabled = true;
            GUILayout.Label(notice);
            detection = GUILayout.Toggle(detection, "Detection: cyan sight / green proximity");
            travel = GUILayout.Toggle(travel, "Travel: amber target gap / purple home");
            GUILayout.Label("SETTINGS: new spawn / apply + reset");
            if (kind != 2)
                DrawSettings(settings[kind]);
            GUILayout.Space(5f);
            GUILayout.Label($"MONSTERS ({monsters.Count}/12) - select to inspect");
            foreach (Entry entry in monsters)
            {
                string caption = (entry == selected ? "> " : "") + entry.health.name +
                    $"  {entry.health.CurrentHealth}/{entry.health.MaxHealth}";
                if (GUILayout.Button(caption))
                {
                    selected = entry;
                    SetKind(entry.elite ? 1 : 0);
                }
            }
            if (selected != null)
            {
                GUILayout.Label("Selected: " + State(selected));
                GUI.enabled = kind != 2 && kind == (selected.elite ? 1 : 0);
                if (GUILayout.Button("Apply settings + reset selected")) ApplySelected();
                GUI.enabled = true;
                if (GUILayout.Button("Remove selected")) RemoveSelected();
            }
            if (GUILayout.Button("Remove all monsters")) ClearMonsters();
            GUILayout.Space(5f);
            bool immune = GUILayout.Toggle(invincible, "Player invincible (combat only)");
            if (immune != invincible) SetInvincible(immune);
            if (GUILayout.Button("Restore player HP / revive")) RestorePlayer();
            if (GUILayout.Button(paused ? "Resume simulation" : "Pause simulation"))
                SetPaused(!paused);
            if (GUILayout.Button("Restart scene (R)")) Restart();
            GUILayout.Label("WASD move | LMB shoot | Shift roll\nChanges stay in this Play session only.");
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }

        private void DrawSettings(MonsterTestSettings c)
        {
            c.moveSpeed = Slider("Move speed (m/s)", c.moveSpeed, 0f, 8f);
            c.sightRange = Slider("Sight range (m)", c.sightRange, 1f, 20f);
            c.sightAngle = Slider("Sight angle (deg)", c.sightAngle, 30f, 360f);
            c.proximityRange = Slider("Proximity (m)", c.proximityRange, 0f, 8f);
            c.abandonRange = Slider("Target gap limit (m)", c.abandonRange, c.sightRange, 30f);
            c.homeRange = Slider("Home radius (m)", c.homeRange, 2f, 25f);
            c.attackSpeed = Slider("Attack speed (x)", c.attackSpeed, 0.1f, 4f);
            c.attackRange = Slider("Attack range (m)", c.attackRange, 0.5f, 5f);
            c.health = Mathf.RoundToInt(Slider("Max HP", c.health, 25f, 1000f));
            c.damage = Mathf.RoundToInt(Slider("Damage", c.damage, 1f, 100f));
            if (kind == 1)
            {
                c.heavyRange = Slider("Heavy range (m)", c.heavyRange, c.attackRange, 8f);
                c.heavyDamage = Mathf.RoundToInt(Slider("Heavy damage", c.heavyDamage, 1f, 150f));
            }
            if (GUILayout.Button("Reset type defaults")) settings[kind] = MonsterTestSettings.Defaults(kind == 1);
        }

        private float Slider(string caption, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(caption, GUILayout.Width(175f));
            GUILayout.Label(value.ToString("0.#"), GUILayout.Width(50f));
            GUILayout.EndHorizontal();
            return GUILayout.HorizontalSlider(value, min, max);
        }

        public void ApplySelected()
        {
            Entry previous = selected;
            previous.health.gameObject.SetActive(false);
            monsters.Remove(previous);
            if (SpawnAt(previous.tracker.HomePosition) == null)
            {
                previous.health.gameObject.SetActive(true);
                monsters.Add(previous);
                selected = previous;
                return;
            }
            Destroy(previous.health.gameObject);
            Log("Applied settings; selected monster reset at home.");
        }

        public void SetPaused(bool value)
        {
            paused = value;
            Time.timeScale = paused ? 0f : previousTimeScale;
            Log(paused ? "Paused." : "Resumed.");
        }

        private void RemoveSelected()
        {
            Entry entry = selected;
            monsters.Remove(entry);
            entry.health.gameObject.SetActive(false);
            Destroy(entry.health.gameObject);
            Log("Removed " + entry.health.name);
            selected = null;
        }

        private void Restart()
        {
            if (restarting) return;
            restarting = true;
            Time.timeScale = previousTimeScale;
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(gameObject.scene.path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadSceneAsync(gameObject.scene.buildIndex);
#endif
        }

        private void OnDestroy()
        {
            Time.timeScale = previousTimeScale;
        }
    }
}
