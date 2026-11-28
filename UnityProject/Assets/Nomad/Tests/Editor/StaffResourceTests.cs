using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using Nomad.Equipment;
using Nomad.Hud;
using Nomad.Inventory;
using Nomad.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nomad.Tests
{
    public sealed class StaffResourceTests
    {
        [UnityTest]
        public IEnumerator ContinuousFreeStaffShotsKeepIntervalWithoutManaExhaustion()
        {
            string previousScene=EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            var muted=InputSystem.devices.Where(d=>d is Mouse||d is Keyboard).ToArray();
            foreach(var device in muted)InputSystem.DisableDevice(device);
            int previousCapture = Time.captureFramerate;
            Time.captureFramerate = 60;
            GameObject rig = null;
            GameObject cameraRoot = null;
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nomad/Game/Prefabs/PlayerRig.prefab");
                rig = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
                GameObject player = rig.GetComponentInChildren<PlayerEquipmentCombat>().gameObject;
                player.GetComponent<PlayerMovement>().enabled = false;
                var combat = player.GetComponent<PlayerEquipmentCombat>();
                var inventory = player.GetComponent<PlayerInventory>();
                var equipment = player.GetComponent<PlayerEquipment>();
                var mana = player.GetComponent<PlayerMana>();
                cameraRoot = new GameObject("Staff verification camera");
                Camera camera = cameraRoot.AddComponent<Camera>();
                foreach (Camera existing in rig.GetComponentsInChildren<Camera>()) existing.enabled = false;
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0, 12, -8);
                camera.transform.LookAt(Vector3.zero);
                TestKit.SetField(combat, "viewCamera", camera);
                yield return null;

                const string id = "weapon-apprentice-staff";
                inventory.Add(id, 1);
                int slot = -1;
                for (int index = 0; index < PlayerInventory.Capacity; index++)
                    if (inventory.GetSlot(index).Item?.ItemId == id) slot = index;
                Assert.That(slot, Is.GreaterThanOrEqualTo(0), "실제 수련 지팡이 등록");
                Assert.That(equipment.TryEquip(slot, EquipmentSlot.Weapon1), Is.True);
                equipment.SelectWeapon(EquipmentSlot.Weapon1);
                PlayerHud hud = player.GetComponentInChildren<PlayerHud>();
                if (hud == null) hud = player.AddComponent<PlayerHud>();
                mana.RestoreFull();
                Vector2 pointer = camera.WorldToScreenPoint(new Vector3(0, 1, 6));
                float interval = combat.CurrentWeapon.AttackInterval;
                var times = new List<float>();
                float deadline = Time.time + 7;
                while (times.Count < 16 && Time.time < deadline)
                {
                    if (combat.TryAttack(pointer)) times.Add(Time.time);
                    yield return null;
                }
                Assert.That(times.Count, Is.EqualTo(16), "무료 지팡이 평타16회 연사");
                for (int index = 1; index < times.Count; index++)
                    Assert.That(times[index] - times[index - 1], Is.InRange(interval - 0.001f, interval + 0.04f), "고갈 전 공격간격 유지");

                yield return Wait(interval + 0.05f);
                Assert.That(combat.TryAttack(pointer), Is.True, $"무료 평타 t={Time.time} last={times.Last()} interval={interval} mana={mana.Current} hp={player.GetComponent<Nomad.Combat.Health>().CurrentHealth} block={combat.InputBlocked} ui={combat.EquipmentUiOpen} aim={combat.TryGetAim(pointer,out _)}");
                Assert.That(mana.Current,Is.EqualTo(mana.Maximum));
                Assert.That(hud.AttackStatus,Is.EqualTo("CURRENT"));
                Nomad.Minimap.Editor.MinimapVerification.Capture("staff-mana-shortage-182.png");
                yield return Wait(interval+0.05f);
                Assert.That(combat.TryAttack(pointer), Is.True, "무료 평타 연사 유지");
                mana.RestoreFull();
                float nextShot = Time.time + interval + 0.04f;
                while (Time.time < nextShot) yield return null;
                Assert.That(combat.TryAttack(pointer), Is.True,
                    $"자원 복구 후 원래 간격으로 연사 재개 t={Time.time} mana={mana.Current} blocked={combat.InputBlocked} ui={combat.EquipmentUiOpen} hp={player.GetComponent<Nomad.Combat.Health>().CurrentHealth}");
                yield return null;
                Assert.That(hud.AttackStatus, Is.EqualTo("CURRENT"));
                Assert.That(combat.CurrentWeapon.AttackInterval, Is.EqualTo(interval), "공속 수치 변형 없음");
                Debug.Log("STAFF_RESOURCE_PASS shots=16 interval=" + interval + " mana=free recovery=pass");
            }
            finally
            {
                Time.captureFramerate = previousCapture;
                foreach(var device in muted)InputSystem.EnableDevice(device);
                if (rig != null) Object.DestroyImmediate(rig);
                if (cameraRoot != null) Object.DestroyImmediate(cameraRoot);
            }
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            if (!string.IsNullOrEmpty(previousScene)) EditorSceneManager.OpenScene(previousScene);
        }

        private static IEnumerator Wait(float seconds)
        {
            float end=Time.time+seconds;
            while(Time.time<end)yield return null;
        }
    }
}
