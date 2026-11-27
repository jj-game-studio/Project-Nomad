using System.Collections;
using System.Linq;
using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Inventory;
using Nomad.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Monsters.Demo
{
    public sealed class BossDebugBootstrap : MonoBehaviour
    {
        public GameObject PlayerPrefab;
        public GameObject DragonPrefab;
        private GameObject encounter;
        private Material material;
        private MonsterAppearanceSet appearance;
        private bool restarting;
        public DragonBossCombat Boss { get; private set; }
        public PlayerEquipmentCombat Player { get; private set; }
        public BossArena Arena { get; private set; }
        public bool Ready => !restarting && Boss != null;

        private IEnumerator Start()
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.white };
            appearance = ScriptableObject.CreateInstance<MonsterAppearanceSet>();
            appearance.boss = DragonPrefab;
            yield return Spawn();
        }

        private IEnumerator Spawn()
        {
            restarting = true;
            if (encounter != null) { encounter.SetActive(false); Destroy(encounter); yield return null; }
            encounter = new GameObject("Boss debug encounter");
            encounter.transform.SetParent(transform, false);
            var arenaRoot = new GameObject("Boss arena");
            arenaRoot.transform.SetParent(encounter.transform, false);
            Arena = arenaRoot.AddComponent<BossArena>();
            // 통로/마을 생성 없이 보스방의 바닥과 NavMesh만 사용합니다.
            Arena.BuildStandalone(new Vector3(0, 0, -BossArena.HalfSize - 3), Vector3.forward);
            GameObject rig = Instantiate(PlayerPrefab, Arena.Center - Vector3.forward * 8, Quaternion.identity, encounter.transform);
            Player = rig.GetComponentInChildren<PlayerEquipmentCombat>();
            var factory = encounter.AddComponent<MonsterActorFactory>();
            factory.ConfigureMaterials(material, material, material, material);
            factory.ConfigureAppearances(appearance);
            Health boss = factory.CreateRanked(Player.GetComponent<Health>(), MonsterTestSettings.Defaults(MonsterRank.Boss),
                MonsterRank.Boss, Arena.Center, Quaternion.LookRotation(Vector3.back), "용의 제단 보스", Arena.transform);
            Arena.PrepareCombat(boss, Player.GetComponent<Health>());
            Arena.ObserveEncounter(Player.GetComponent<Health>());
            Boss = boss.GetComponent<DragonBossCombat>();
            yield return null;
            Equip("demo-sword");
            restarting = false;
        }

        private void Update()
        {
            if (Player != null && Mouse.current != null)
            {
                Vector2 pointer = Mouse.current.position.ReadValue();
                Player.InputBlocked = new Rect(12, 80, 260, 410).Contains(new Vector2(pointer.x, Screen.height - pointer.y));
            }
            if (!restarting && Keyboard.current?.rKey.wasPressedThisFrame == true) StartCoroutine(Spawn());
        }

        public void Equip(string item)
        {
            var bag = Player.GetComponent<PlayerInventory>();
            bag.Add(item, 2);
            bag.Add(PlayerEquipmentCombat.ArrowItemId, 60);
            int slot = Enumerable.Range(0, bag.SlotCount).First(index => bag.GetSlot(index).Item?.ItemId == item && bag.GetSlot(index).Item.EnhancementLevel == 0);
            var gear = Player.GetComponent<PlayerEquipment>();
            // 무기는 가방에 둔 채 퀵바 1번에 등록하고 선택한 뒤 강화합니다.
            if (gear.EquipWeapon(slot, 0)) gear.TryEnhanceWeapon();
            Player.GetComponent<PlayerMana>().RestoreFull();
        }

        private void OnGUI()
        {
            if (restarting || Boss == null || Player == null) return;
            Font previousFont = GUI.skin.font;
            GUI.skin.font = Resources.Load<Font>("ShopFonts/Jua-Regular");
            GUILayout.BeginArea(new Rect(12, 80, 260, 410), GUI.skin.box);
            GUILayout.Label("보스 전용 테스트");
            GUILayout.Label("G 패링 · Shift 구르기 · C 스킬 · R 재시작");

            var health = Player.GetComponent<Health>();
            health.IsCombatImmune = GUILayout.Toggle(health.IsCombatImmune, "플레이어 무적");
            Boss.AutomaticPatterns = GUILayout.Toggle(Boss.AutomaticPatterns, "자동 패턴");
            if (GUILayout.Button("체력·마나 회복")) { health.RestoreFull(); Player.GetComponent<PlayerMana>().RestoreFull(); }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("검 +1")) Equip("demo-sword");
            if (GUILayout.Button("활 +1")) Equip("weapon-shortbow");
            if (GUILayout.Button("지팡이 +1")) Equip("weapon-apprentice-staff");
            GUILayout.EndHorizontal();

            string[] labels = { "기본 공격", "원형 강타", "운석 3갈래", "브레스", "불지대 4개", "화염우", "돌진" };
            for (int index = 0; index < labels.Length; index++)
                if (GUILayout.Button(labels[index])) { Boss.AutomaticPatterns = false; Boss.BeginPattern((DragonPattern)index); }
            if (GUILayout.Button("보스전 재시작")) StartCoroutine(Spawn());
            GUILayout.EndArea();
            GUI.skin.font = previousFont;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (appearance != null) Destroy(appearance);
        }
    }
}
