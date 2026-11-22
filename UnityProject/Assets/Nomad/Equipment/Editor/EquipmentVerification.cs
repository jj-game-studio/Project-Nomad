using System;
using System.Linq;
using Nomad.Combat;
using Nomad.Inventory;
using Nomad.Inventory.Editor;
using Nomad.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Nomad.Equipment.Editor
{
    internal static class EquipmentVerification
    {
        private const string Key = "Nomad.Equipment.Verification";
        private static PlayerEquipment equipment;
        private static PlayerInventory inventory;
        private static PlayerEquipmentCombat combat;
        private static Health health;
        private static Health[] near;
        private static Health far;
        private static GameObject wall;
        private static Keyboard keyboard;
        private static int stage;
        private static int checks;
        private static float started;
        private static double deadline;

        public static void Run()
        {
            VerifyCsv();
            EditorSceneManager.OpenScene(EquipmentAuthoring.ScenePath);
            SessionState.SetBool(Key, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        private static void VerifyCsv()
        {
            string header = "item_id,display_name,description,max_stack,equipment_type,attack_power,defense,attack_mode,attack_range,attack_interval\n";
            Check(ItemCsv.Parse(header + "w,검,,1,Weapon,25,0,Melee,2,0.6")[0].AttackPower == 25, "장비 CSV");
            foreach (string row in new[] { "w,검,,2,Weapon,25,0,Melee,2,0.6", "w,검,,1,Weapon,0,0,Melee,2,0.6",
                         "w,검,,1,Body,25,0,None,0,0", "w,검,,1,Weapon,25,0,Melee,NaN,1",
                         "w,검,,1,Weapon,25,0,Melee,2,0", "w,검,,1,Bad,0,0,None,0,0" })
            {
                bool rejected = false;
                try { ItemCsv.Parse(header + row); }
                catch (FormatException) { rejected = true; }
                Check(rejected, "잘못된 장비 CSV 거절");
            }
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Key, false))
                return;
            deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                CheckTimeout();
                if (!Application.isPlaying || Time.frameCount < 3)
                    return;
                if (equipment == null)
                    Initialize();
                if (stage != 6 && Time.time - started < 0.95f)
                    return;

                switch (stage)
                {
                    case 0:
                        VerifyTransfers();
                        Check(combat.TryAttack(Point(new Vector3(0, 1, 1.5f))), "검 공격 실행");
                        Check(!combat.TryAttack(Point(near[0].transform.position)), "공격 간격");
                        break;
                    case 1:
                        Check(near.All(target => target.CurrentHealth == 75), "근접 다중타격/중복 콜라이더1회");
                        wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        wall.transform.position = new Vector3(0, 1, 0.7f);
                        wall.transform.localScale = new Vector3(4, 2, 0.1f);
                        Physics.SyncTransforms();
                        Check(combat.TryAttack(Point(new Vector3(0, 1, 1.5f))), "벽 방향 휘두르기");
                        Check(near.All(target => target.CurrentHealth == 75), "벽 너머 근접 차단");
                        UnityEngine.Object.Destroy(wall);
                        break;
                    case 2:
                        Check(equipment.TryUnequip(EquipmentSlot.Weapon1), "검 해제");
                        Check(combat.CurrentWeapon.ItemId == "demo-unarmed", "맨손 CSV 대체");
                        Check(combat.TryAttack(Point(new Vector3(0, 1, 1.5f))), "맨손 공격");
                        break;
                    case 3:
                        Check(near.All(target => target.CurrentHealth == 70), "맨손 실제 피해5");
                        foreach (Health target in near) target.transform.position += Vector3.right * 10;
                        Physics.SyncTransforms();
                        Check(equipment.SelectWeapon(EquipmentSlot.Weapon2), "활 선택");
                        combat.InputBlocked = true;
                        Check(!combat.TryAttack(Point(far.transform.position)), "외부 입력 차단");
                        combat.InputBlocked = false;
                        Time.timeScale = 0;
                        Check(!combat.TryAttack(Point(far.transform.position)), "일시정지 차단");
                        Time.timeScale = 1;
                        Check(combat.TryAttack(Point(far.transform.position)), "활 실제 발사체 발사");
                        equipment.SelectWeapon(EquipmentSlot.Weapon1);
                        Check(!combat.TryAttack(Point(far.transform.position)), "무기전환 공격간격 우회 차단");
                        equipment.SelectWeapon(EquipmentSlot.Weapon2);
                        break;
                    case 4:
                        Check(far.CurrentHealth == 65, "CSV 활 피해35");
                        equipment.enabled = false;
                        Check(!combat.TryAttack(Point(far.transform.position)), "비활성 장비 공격 차단");
                        equipment.enabled = true;
                        break;
                    case 5:
                        keyboard = InputSystem.AddDevice<Keyboard>();
                        keyboard.MakeCurrent();
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.LeftShift));
                        break;
                    case 6:
                        if (!equipment.GetComponent<PlayerMovement>().IsRolling)
                        {
                            Check(Time.time - started < 0.25f, "구르기 입력 소비");
                            return;
                        }
                        Check(!combat.TryAttack(Point(far.transform.position)), "구르기 중 장비 공격 차단");
                        Check(!health.TryApplyDamage(10), "구르기 무적 유지");
                        Check(health.TryApplyDamage(1, DamageKind.Fall), "구르기 중 낙하 피해 유지");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        break;
                    case 7:
                        Check(health.TryApplyDamage(1000) && health.IsDead, "장비 상태 사망");
                        Check(!combat.TryAttack(Point(far.transform.position)), "사망 공격 차단");
                        Debug.Log($"EQUIPMENT_PLAYMODE_PASS: {checks} checks; transfers, CSV, multi-melee, fists, ranged, armor, roll/death");
                        Finish(0);
                        return;
                }
                stage++;
                started = Time.time;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Finish(1);
            }
        }

        private static void Initialize()
        {
            Application.runInBackground = true;
            Time.captureFramerate = 60;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            equipment = UnityEngine.Object.FindAnyObjectByType<PlayerEquipment>();
            inventory = equipment.GetComponent<PlayerInventory>();
            combat = equipment.GetComponent<PlayerEquipmentCombat>();
            health = equipment.GetComponent<Health>();
            near = UnityEngine.Object.FindObjectsByType<Health>().Where(target => target.name.StartsWith("Melee Target")).ToArray();
            far = GameObject.Find("Low Target").GetComponent<Health>();
            var extra = new GameObject("Extra Hurtbox");
            extra.transform.SetParent(near[0].transform, false);
            extra.AddComponent<SphereCollider>().radius = 0.3f;
            Check(!equipment.GetComponent<PlayerCombat>().enabled, "기존 공격 중복 비활성");
            started = Time.time;
        }

        private static void VerifyTransfers()
        {
            Check(PlayerEquipment.Capacity == 10 && equipment.ActiveWeapon.AttackPower == 25, "10칸/CSV 검 장착");
            int original = inventory.UsedSlots;
            int armorCount = inventory.GetCount("demo-armor");
            int totemCount = inventory.GetCount("demo-totem");
            Check(!equipment.TryEquip(Find("demo-armor"), EquipmentSlot.Weapon1) && inventory.UsedSlots == original, "잘못된 부위 무변경");
            bool completeState = false;
            Action listener = () => completeState = equipment.GetItem(EquipmentSlot.Body)?.ItemId == "demo-armor" &&
                inventory.GetCount("demo-armor") == armorCount - 1;
            inventory.Changed += listener;
            Check(equipment.TryEquip(Find("demo-armor"), EquipmentSlot.Body), "몸통 장착");
            inventory.Changed -= listener;
            Check(completeState && equipment.TotalDefense == 20, "완료상태 알림/방어력");
            Check(health.TryApplyDamage(25) && health.CurrentHealth == 95, "피해25-방어20=실제피해5");
            Check(health.TryApplyDamage(1, DamageKind.Fall) && health.CurrentHealth == 94, "낙하 방어 제외");
            Check(equipment.ModifyCombatDamage(1) == 1 && equipment.ModifyCombatDamage(2) == 1 &&
                equipment.ModifyCombatDamage(200) == 180, "고정 차감/최소1");

            foreach (var pair in new[] { ("demo-helmet", EquipmentSlot.Head), ("demo-mask", EquipmentSlot.Face),
                         ("demo-charm", EquipmentSlot.Accessory), ("demo-ring", EquipmentSlot.Ring),
                         ("demo-bag", EquipmentSlot.Bag), ("demo-totem", EquipmentSlot.Totem1) })
                Check(equipment.TryEquip(Find(pair.Item1), pair.Item2), "각 분류 장착");
            inventory.Add("demo-totem", 1);
            Check(equipment.TryEquip(Find("demo-totem"), EquipmentSlot.Totem2), "토템2 장착");
            Check(equipment.TotalDefense == 60, "방어력 합산");

            inventory.Add("demo-bow", PlayerInventory.Capacity);
            Check(inventory.UsedSlots == PlayerInventory.Capacity && !equipment.TryUnequip(EquipmentSlot.Body), "가방 만석 해제 실패");
            int source = Find("demo-bow");
            Check(equipment.TryEquip(source, EquipmentSlot.Weapon1) && inventory.GetSlot(source).Item.ItemId == "demo-sword", "만석 교체 원칸 반환");
            Check(equipment.TryEquip(source, EquipmentSlot.Weapon1) && inventory.GetSlot(source).Item.ItemId == "demo-bow", "검 복원/중복 없음");
            inventory.Remove("demo-bow", PlayerInventory.Capacity);
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                if (slot != EquipmentSlot.Weapon1 && slot != EquipmentSlot.Weapon2 && slot != EquipmentSlot.Body)
                    Check(equipment.TryUnequip(slot), "분류 해제/가방 복귀");
            Check(equipment.TotalDefense == 20 && inventory.UsedSlots == original &&
                inventory.GetCount("demo-armor") == armorCount - 1 && inventory.GetCount("demo-totem") == totemCount + 1,
                "보유 수량 보존");
            Check(!equipment.TryEquip(-1, EquipmentSlot.Body) && !equipment.TryUnequip((EquipmentSlot)100), "잘못된 인덱스");

            ItemDefinition originalArmor = equipment.GetItem(EquipmentSlot.Body);
            var strongArmor = UnityEngine.Object.Instantiate(originalArmor);
            var serialized = new SerializedObject(strongArmor);
            serialized.FindProperty("defense").intValue = 150;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var receiver = new GameObject("High Defense Receiver");
            receiver.SetActive(false);
            var receiverInventory = receiver.AddComponent<PlayerInventory>();
            var receiverHealth = receiver.AddComponent<Health>();
            var receiverEquipment = receiver.AddComponent<PlayerEquipment>();
            var catalog = new SerializedObject(receiverInventory);
            catalog.FindProperty("items").arraySize = 1;
            catalog.FindProperty("items").GetArrayElementAtIndex(0).objectReferenceValue = strongArmor;
            catalog.ApplyModifiedPropertiesWithoutUndo();
            receiver.SetActive(true);
            receiverInventory.Add(strongArmor.ItemId, 1);
            Check(receiverEquipment.TryEquip(0, EquipmentSlot.Body) && receiverEquipment.ModifyCombatDamage(100) == 1,
                "방어력이 피해 이상일 때 최소1");
            Check(receiverHealth.TryApplyDamage(100) && receiverHealth.CurrentHealth == 99, "최소1 실제 피해");
            UnityEngine.Object.Destroy(receiver);
            UnityEngine.Object.Destroy(strongArmor);
        }

        private static int Find(string id)
        {
            for (int index = 0; index < PlayerInventory.Capacity; index++)
                if (inventory.GetSlot(index).Item?.ItemId == id) return index;
            return -1;
        }

        private static Vector2 Point(Vector3 world) => Camera.main.WorldToScreenPoint(world);

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"stage{stage}: {message}");
            checks++;
        }

        private static void CheckTimeout()
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException($"장비 검증 stage{stage}");
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            Time.timeScale = 1;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            EditorApplication.Exit(code);
        }
    }
}
