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
    public sealed partial class BossDebugBootstrap : MonoBehaviour
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
            GameObject rig = Instantiate(PlayerPrefab, Arena.GroundPoint(Arena.Center - Vector3.forward * 8) + Vector3.up * 0.05f, Quaternion.identity, encounter.transform);
            Player = rig.GetComponentInChildren<PlayerEquipmentCombat>();
            Player.GetComponent<Health>().IsInvulnerable = godMode;
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
            if (!restarting && Keyboard.current?.rKey.wasPressedThisFrame == true) StartCoroutine(Spawn());
        }

        public void Equip(string item)
        {
            var bag = Player.GetComponent<PlayerInventory>();
            bag.Add(item, 2);
            bag.Add(PlayerEquipmentCombat.ArrowItemId, 60);
            int slot = Enumerable.Range(0, bag.SlotCount).First(index => bag.GetSlot(index).Item?.ItemId == item && bag.GetSlot(index).Item.EnhancementLevel == 0);
            var gear = Player.GetComponent<PlayerEquipment>();
            if (gear.TryEquip(slot, EquipmentSlot.Weapon1)) gear.TryEnhance(EquipmentSlot.Weapon1);
            Player.GetComponent<PlayerQuickSlots>()?.RegisterEquipped(EquipmentSlot.Weapon1, 0);
            Player.GetComponent<PlayerMana>().RestoreFull();
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (appearance != null) Destroy(appearance);
        }
    }
}
