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
        [SerializeField] private MonsterAppearanceSet appearances;
        [SerializeField] private bool compactUi;
        private int page;
        private int tuningTab;
        private int skillTab;
        private bool showLogs;
        private bool skillWarnings = true;
        private bool healthBars = true;

        private sealed class Entry
        {
            public Health health;
            public MonsterTracker tracker;
            public MonsterMeleeAttack attack;
            public MonsterTestRanges ranges;
            public MonsterHitFeedback feedback;
            public bool elite;
            public MonsterRank rank;
            public int previousHp;
            public string previousState;
        }

        private readonly List<Entry> monsters = new List<Entry>();
        private readonly List<string> logs = new List<string>();
        private readonly MonsterTestSettings[] settings =
            { MonsterTestSettings.Defaults(false), MonsterTestSettings.Defaults(true), MonsterTestSettings.Defaults(MonsterRank.Boss) };
        private Entry selected;
        private MonsterActorFactory factory;
        private int kind;
        private int serial;
        private int previousPlayerHp;
        private int placementClickFrame = -1;
        private bool detection = true;
        private bool travel;
        private bool hitFlash = true;
        private bool damageNumbers = true;
        private bool hitShards = true;
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
        public Rect LogPanel => compactUi ? new Rect(12f, 12f, 290f, showLogs ? 170f : 34f) : new Rect(12f, 12f, 365f, 270f);

        private void Start()
        {
            previousTimeScale = Time.timeScale;
            previousPlayerHp = player.CurrentHealth;
            factory = GetComponent<MonsterActorFactory>();
            if (factory == null) factory = gameObject.AddComponent<MonsterActorFactory>();
            factory.ConfigureMaterials(normalMaterial, eliteMaterial, warningMaterial, fillMaterial);
            factory.ConfigureAppearances(appearances);
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
            if (initialMonster != null)
            {
                selected = Register(initialMonster, initialElite);
                kind = initialElite ? 1 : 0;
                MonsterTestSettings config = settings[kind];
                selected.tracker.Configure(config.sightRange, config.abandonRange, config.homeRange);
            }
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
            if (!compactUi && Keyboard.current?.rKey.wasPressedThisFrame == true)
                Restart();

            foreach (Entry entry in monsters)
            {
                entry.ranges.ShowDetection = detection && !entry.health.IsDead;
                entry.ranges.ShowTravel = travel && !entry.health.IsDead;
                entry.feedback.SetEffects(hitFlash, damageNumbers, hitShards);
                var display = entry.health.GetComponentInChildren<MonsterCombatDemo>();
                if (display != null) display.ShowSkillWarnings = skillWarnings;
                entry.health.GetComponent<SegmentedHealthBar>().enabled = healthBars;
                if (compactUi)
                {
                    var bar = entry.health.GetComponent<SegmentedHealthBar>();
                    bar.HudRightInset = (SpawnPanel.width + 24f) * UiScale;
                    bar.HudTop = (showLogs ? 190f : 52f) * UiScale;
                }
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
                rank = value.GetComponent<MonsterIdentity>()?.Rank ?? (elite ? MonsterRank.Elite : MonsterRank.Normal),
                tracker = value.GetComponent<MonsterTracker>(), attack = value.GetComponent<MonsterMeleeAttack>() };
            entry.ranges = value.GetComponent<MonsterTestRanges>();
            entry.feedback = value.GetComponent<MonsterHitFeedback>();
            if (entry.feedback == null)
                entry.feedback = value.gameObject.AddComponent<MonsterHitFeedback>();
            entry.feedback.SetEffects(hitFlash, damageNumbers, hitShards);
            if (entry.ranges == null)
                entry.ranges = value.gameObject.AddComponent<MonsterTestRanges>();
            if (value.GetComponent<MonsterDeathDrop>() == null)
                value.gameObject.AddComponent<MonsterDeathDrop>().Configure(transform);
            monsters.Add(entry);
            return entry;
        }

        public Health SpawnAt(Vector3 point)
        {
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

            bool elite = kind != 0;
            MonsterTestSettings config = settings[kind];
            Vector3 facing = Vector3.ProjectOnPlane(player.transform.position - hit.position, Vector3.up);
            Quaternion rotation = facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing) : Quaternion.identity;
            Health health = factory.CreateRanked(player, config, (MonsterRank)kind, hit.position, rotation,
                ((MonsterRank)kind + " #") + ++serial);
            selected = Register(health, elite);
            notice = "Spawned " + health.name;
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
            foreach (MonsterBolt bolt in FindObjectsByType<MonsterBolt>(FindObjectsSortMode.None))
            {
                if (bolt.gameObject.scene == gameObject.scene) Destroy(bolt.gameObject);
            }
            foreach (PlaceholderEquipmentDrop drop in GetComponentsInChildren<PlaceholderEquipmentDrop>())
            {
                drop.gameObject.SetActive(false);
                Destroy(drop.gameObject);
            }
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
            return SpawnPanel.Contains(gui) || LogPanel.Contains(gui) ||
                (GetComponent<MonsterDebugArena>() != null && new Rect(12f, Screen.height - 100f, 370f, 88f).Contains(gui));
        }

        private string State(Entry entry)
        {
            if (entry.health.IsDead) return "DEAD";
            if (entry.tracker.IsReturning) return "RETURN HOME";
            if (entry.attack.Phase != MeleePhase.Idle)
                return (entry.attack.Skill != MonsterSkill.None ? entry.attack.Skill.ToString() :
                    entry.attack.IsRangedAttack ? "RANGED" : entry.attack.IsHeavyAttack ? "HEAVY" : "BASIC") + " " + entry.attack.Phase;
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
            if (compactUi)
            {
                DrawCompact();
                GUI.matrix = previousMatrix;
                return;
            }
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
                GUILayout.Label("BOSS: temporary melee pattern / visual drop");

            GUI.enabled = true;
            if (GUILayout.Button(placing ? "Click floor to spawn (Esc cancels)" : "Place on floor..."))
                placing = !placing;
            if (GUILayout.Button("Spawn 5m in front of player"))
                SpawnAt(player.transform.position + player.transform.forward * 5f);
            GUI.enabled = true;
            GUILayout.Label(notice);
            detection = GUILayout.Toggle(detection, "Detection: cyan radius (walls block sight)");
            travel = GUILayout.Toggle(travel, "Travel: amber target gap / purple home");
            hitFlash = GUILayout.Toggle(hitFlash, "Hit flash");
            damageNumbers = GUILayout.Toggle(damageNumbers, "Damage numbers");
            hitShards = GUILayout.Toggle(hitShards, "Hit shards");
            GUILayout.Label("SETTINGS: new spawn / apply + reset");
            DrawSettings(settings[kind]);
            GUILayout.Space(5f);
            GUILayout.Label($"MONSTERS ({monsters.Count}/12) - select to inspect");
            foreach (Entry entry in monsters)
            {
                string caption = (entry == selected ? "> " : "") + (entry.health.GetComponent<MonsterIdentity>()?.DisplayName ?? entry.health.name) +
                    $"  {entry.health.CurrentHealth}/{entry.health.MaxHealth}";
                if (GUILayout.Button(caption))
                {
                    selected = entry;
                    SetKind((int)entry.rank);
                }
            }
            if (selected != null)
            {
                GUILayout.Label("Selected: " + State(selected));
                GUI.enabled = kind == (int)selected.rank;
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
            if (kind == 0)
            {
                GUILayout.Label("Normal weapon / attack style");
                c.normalStyle = GUILayout.Toolbar(c.normalStyle + 1, new[] { "Mix", "Sword", "Crossbow", "Staff" }) - 1;
                if (c.normalStyle == 0) GUILayout.Label("Sword uses melee swings; ranged settings apply to other styles.");
            }
            c.ranged = GUILayout.Toggle(c.ranged, "Ranged attacks enabled");
            if (c.ranged)
            {
                c.rangedRange = Slider("Ranged distance", c.rangedRange, 3f, 20f);
                c.projectileSpeed = Slider("Projectile speed", c.projectileSpeed, 2f, 20f);
                c.rangedDamage = Mathf.RoundToInt(Slider("Ranged damage", c.rangedDamage, 1f, 100f));
                c.rangedWindup = Slider("Ranged windup", c.rangedWindup, 0.3f, 3f);
                c.rangedRecovery = Slider("Ranged recovery", c.rangedRecovery, 0.3f, 4f);
            }
            c.moveSpeed = Slider("Move speed (m/s)", c.moveSpeed, 0f, 8f);
            c.sightRange = Slider("Detection radius (m)", c.sightRange, 1f, 20f);
            c.abandonRange = Slider("Target gap limit (m)", c.abandonRange, c.sightRange, 30f);
            c.homeRange = Slider("Home radius (m)", c.homeRange, 2f, 25f);
            c.attackSpeed = Slider("Attack speed (x)", c.attackSpeed, 0.1f, 4f);
            c.attackRange = Slider("Attack range (m)", c.attackRange, 0.5f, 5f);
            c.health = Mathf.RoundToInt(Slider("Max HP", c.health, 25f, 1000f));
            c.damage = Mathf.RoundToInt(Slider("Damage", c.damage, 1f, 100f));
            if (kind != 0)
            {
                c.heavyRange = Slider("Heavy range (m)", c.heavyRange, c.attackRange, 8f);
                c.heavyDamage = Mathf.RoundToInt(Slider("Heavy damage", c.heavyDamage, 1f, 150f));
            }
            if (GUILayout.Button("Reset type defaults")) settings[kind] = MonsterTestSettings.Defaults((MonsterRank)kind);
        }

        public void ShowDetection(bool value) => detection = value;

        public void SpawnNormalStyle(int style, Vector3 position)
        {
            SetKind(0);
            settings[0].normalStyle = style;
            SpawnAt(position);
        }

        private float Slider(string caption, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(caption, GUILayout.Width(compactUi ? 145f : 175f));
            GUILayout.Label(value.ToString("0.#"), GUILayout.Width(45f));
            GUILayout.EndHorizontal();
            return GUILayout.HorizontalSlider(value, min, max);
        }

        private void DrawCompact()
        {
            if (GUI.Button(new Rect(LogPanel.x, LogPanel.y, LogPanel.width, 30f), showLogs ? "Action log  [-]" : "Action log  [+]")) showLogs = !showLogs;
            if (showLogs)
            {
                GUI.Box(new Rect(LogPanel.x, LogPanel.y + 30f, LogPanel.width, 140f), GUIContent.none);
                GUILayout.BeginArea(new Rect(22f, 47f, 270f, 126f));
                for (int i = Mathf.Max(0, logs.Count - 5); i < logs.Count; i++) GUILayout.Label(logs[i]);
                GUILayout.EndArea();
            }
            GUI.Box(SpawnPanel, "MONSTER LAB");
            GUILayout.BeginArea(new Rect(SpawnPanel.x + 10f, 38f, SpawnPanel.width - 20f, SpawnPanel.height - 34f));
            page = GUILayout.Toolbar(page, new[] { "Spawn", "Tune", "Display" });
            GUILayout.Space(6f);
            if (kind == 0 && settings[0].normalStyle < 0) settings[0].normalStyle = 0;
            int type = kind == 0 ? settings[0].normalStyle : kind + 2;
            int next = GUILayout.Toolbar(type, new[] { "Sword", "Bow", "Mage", "Golem", "Dragon" });
            if (next != type) { SetKind(next < 3 ? 0 : next - 2); if (next < 3) settings[0].normalStyle = next; }
            scroll = GUILayout.BeginScrollView(scroll);
            if (page == 0)
            {
                if (GUILayout.Button(placing ? "Click walkable floor / Esc cancels" : "Place on map", GUILayout.Height(30f))) placing = !placing;
                if (GUILayout.Button("Spawn 5m ahead")) SpawnAt(player.transform.position + player.transform.forward * 5f);
                GUILayout.Label(notice);
                GUILayout.Space(6f);
                GUILayout.Label($"Actors {monsters.Count}/12 - select");
                foreach (Entry entry in monsters)
                    if (GUILayout.Button((selected == entry ? "> " : "") + entry.health.GetComponent<MonsterIdentity>().DisplayName +
                        $"   {entry.health.CurrentHealth}/{entry.health.MaxHealth}"))
                    {
                        selected = entry;
                        SetKind((int)entry.rank);
                        if (kind == 0) settings[0].normalStyle = (int)entry.health.GetComponent<MonsterIdentity>().Weapon;
                    }
                if (selected != null)
                {
                    GUILayout.Label(State(selected));
                    var skills = selected.health.GetComponent<MonsterTactics>();
                    if (skills != null) GUILayout.Label($"Charge {skills.ChargeRemaining:0.0}s | AoE {skills.AreaRemaining:0.0}s");
                    if (GUILayout.Button("Remove selected")) RemoveSelected();
                }
                if (GUILayout.Button("Remove all")) ClearMonsters();
            }
            else if (page == 1) DrawCompactTuning();
            else
            {
                detection = GUILayout.Toggle(detection, "Detection circles");
                travel = GUILayout.Toggle(travel, "Leash / travel limits");
                skillWarnings = GUILayout.Toggle(skillWarnings, "Skill telegraphs");
                healthBars = GUILayout.Toggle(healthBars, "Monster HP bars");
                hitFlash = GUILayout.Toggle(hitFlash, "Hit flash");
                damageNumbers = GUILayout.Toggle(damageNumbers, "Damage numbers");
                hitShards = GUILayout.Toggle(hitShards, "Hit shards");
            }
            GUILayout.EndScrollView();
            GUILayout.Space(4f);
            bool immune = GUILayout.Toggle(invincible, "Player invincible");
            if (immune != invincible) SetInvincible(immune);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Restore HP")) RestorePlayer();
            if (GUILayout.Button(paused ? "Resume" : "Pause")) SetPaused(!paused);
            GUILayout.EndHorizontal();
            GUILayout.Label("WASD / LMB / Shift\nR new map / T same seed / Esc cancel");
            GUILayout.EndArea();
        }

        private void DrawCompactTuning()
        {
            MonsterTestSettings c = settings[kind];
            tuningTab = GUILayout.Toolbar(tuningTab, new[] { "Move / HP", "Basic", "Skills" });
            if (tuningTab == 0)
            {
                c.health = Mathf.RoundToInt(Slider("Health", c.health, 25f, 2500f));
                c.moveSpeed = Slider("Move speed", c.moveSpeed, 0f, 10f);
                c.sightRange = Slider("Detect radius", c.sightRange, 1f, 25f);
                c.abandonRange = Slider("Target leash", c.abandonRange, c.sightRange, 35f);
                c.homeRange = Slider("Home leash", c.homeRange, 2f, 35f);
            }
            else if (tuningTab == 1)
            {
                c.attackSpeed = Slider("Attack speed", c.attackSpeed, 0.2f, 4f);
                c.damage = Mathf.RoundToInt(Slider("Basic damage", c.damage, 1f, 100f));
                c.attackRange = Slider("Basic reach", c.attackRange, 0.5f, 6f);
                c.basicWindup = Slider("Basic windup", c.basicWindup, 0.1f, 1.5f);
                c.basicRecovery = Slider("Basic recovery", c.basicRecovery, 0.1f, 2f);
                if (kind == 0) DrawProjectileTuning(c);
            }
            else if (kind != 0)
            {
                skillTab = GUILayout.Toolbar(skillTab, new[] { "Charge / AoE", "Projectile" });
                if (skillTab == 1) DrawProjectileTuning(c);
                else
                {
                    c.skills = GUILayout.Toggle(c.skills, "Charge / AoE / Heavy");
                    c.chargeMin = Slider("Charge min", c.chargeMin, 3f, 12f);
                    c.chargeMax = Slider("Charge max", c.chargeMax, c.chargeMin, 25f);
                    c.chargeSpeed = Slider("Charge speed", c.chargeSpeed, 5f, 25f);
                    c.chargeCooldown = Slider("Charge cooldown", c.chargeCooldown, 1f, 12f);
                    c.areaRadius = Slider("AoE radius", c.areaRadius, 2f, 9f);
                    c.areaCooldown = Slider("AoE cooldown", c.areaCooldown, 1f, 12f);
                    c.skillWindup = Slider("Skill windup", c.skillWindup, 0.3f, 2f);
                    c.skillRecovery = Slider("Skill recovery", c.skillRecovery, 0.1f, 2f);
                    c.heavyDamage = Mathf.RoundToInt(Slider("Skill damage", c.heavyDamage, 1f, 150f));
                }
            }
            else GUILayout.Label("Sword / bow / mage use basic attacks.");
            if (GUILayout.Button("Reset type defaults"))
            {
                int style = c.normalStyle;
                settings[kind] = MonsterTestSettings.Defaults((MonsterRank)kind);
                if (kind == 0) settings[0].normalStyle = style;
            }
            GUI.enabled = selected != null && (int)selected.rank == kind;
            if (GUILayout.Button("Apply + reset selected")) ApplySelected();
            GUI.enabled = true;
            GUILayout.Label("Changes apply to new/reset actors.");
        }

        private void DrawProjectileTuning(MonsterTestSettings c)
        {
            c.ranged = GUILayout.Toggle(c.ranged, "Projectiles (except sword)");
            if (!c.ranged) return;
            c.rangedRange = Slider("Shot range", c.rangedRange, 3f, 25f);
            c.projectileSpeed = Slider("Shot speed", c.projectileSpeed, 2f, 25f);
            c.rangedDamage = Mathf.RoundToInt(Slider("Shot damage", c.rangedDamage, 1f, 100f));
            c.rangedWindup = Slider("Shot windup", c.rangedWindup, 0.2f, 2f);
            c.rangedRecovery = Slider("Shot recovery", c.rangedRecovery, 0.1f, 2f);
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
