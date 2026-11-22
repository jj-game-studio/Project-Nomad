using System;
using System.IO;
using Nomad.Combat;
using Nomad.Inventory;
using Nomad.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomad.Equipment.Editor
{
    // 경사면 근접 범위, 대검 베기 VFX, 지팡이 마법탄 VFX를 통합 지형 씬에서 검증하고 확인용 화면을 남깁니다.
    // 실행: Unity -batchmode -executeMethod Nomad.Equipment.Editor.WeaponVfxVerification.Run
    // 화면: UnityProject/Artifacts/weapon-vfx/*.png
    internal static class WeaponVfxVerification
    {
        private const string Key = "Nomad.Equipment.WeaponVfxVerification";
        private const string ScenePath = "Assets/Nomad/Terrain/Wfc/TerrainIntegration.unity";

        private static PlayerEquipmentCombat combat;
        private static PlayerEquipment equipment;
        private static PlayerInventory inventory;
        private static EquipmentCombatVisuals visuals;
        private static Health target;
        private static GameObject ramp;
        private static GameObject wall;
        private static Vector3 forward;
        private static int stage;
        private static int checks;
        private static int frame;
        private static double deadline;

        public static void Run()
        {
            WeaponContentAuthoring.CreateVfxMaterial();
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool(Key, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Key, false))
                return;

            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException($"무기 VFX 검증 stage{stage}");
                if (!Application.isPlaying || Time.time < 3f || Time.frameCount < frame)
                    return;

                frame = Time.frameCount + 2;
                switch (stage++)
                {
                    case 0:
                        Initialize();
                        Equip("weapon-dagger");
                        visuals.ShowRange = true;
                        break;
                    case 1:
                        // 현재 범위 표시 방향으로 45도 오르막과 수직 벽을 놓습니다.
                        Vector3 middle = visuals.RangeLine.GetPosition(17) - visuals.RangeLine.GetPosition(0);
                        forward = Vector3.ProjectOnPlane(middle, Vector3.up).normalized;
                        Check(forward.sqrMagnitude > 0.5f, "범위 표시 방향");
                        // 경사면이 캡슐과 겹쳐 캐릭터가 밀려 올라가지 않도록 검사 중에는 이동을 멈춥니다.
                        combat.GetComponent<PlayerMovement>().enabled = false;
                        ramp = Ramp();
                        Physics.SyncTransforms();
                        break;
                    case 2:
                        Check(combat.MeleeReach(forward, 1.25f) > 1.24f, "오를 수 있는 경사면은 근접 범위를 자르지 않음");
                        Vector3 feet = combat.transform.position;
                        for (int index = 1; index < visuals.RangeLine.positionCount - 1; index++)
                        {
                            Vector3 offset = visuals.RangeLine.GetPosition(index) - feet;
                            Check(Mathf.Abs(Vector3.ProjectOnPlane(offset, Vector3.up).magnitude - 1.25f) < 0.05f, "경사면 단검 부채꼴 전체 표시");
                        }
                        Vector3 tip = visuals.RangeLine.GetPosition(17) - feet;
                        Check(tip.y > 1f && Vector3.Dot(Vector3.ProjectOnPlane(tip, Vector3.up).normalized, forward) > 0.99f,
                            $"오르막 범위 표시가 지면 위에 보임 ({tip})");
                        Capture("dagger-slope-range");
                        Object.Destroy(ramp);
                        wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        wall.transform.SetPositionAndRotation(combat.transform.position + forward * 0.8f + Vector3.up, Quaternion.LookRotation(forward));
                        wall.transform.localScale = new Vector3(4f, 3f, 0.1f);
                        Physics.SyncTransforms();
                        break;
                    case 3:
                        Check(combat.MeleeReach(forward, 1.25f) < 0.85f, "수직 벽은 근접 범위를 계속 자름");
                        Object.Destroy(wall);
                        combat.GetComponent<PlayerMovement>().enabled = true;
                        visuals.ShowRange = false;
                        target = Target(combat.transform.position + forward * 2.2f);
                        Equip("weapon-greatsword");
                        frame = Time.frameCount + 70;
                        break;
                    case 4:
                        Check(combat.TryAttack(Screen(target.transform.position + Vector3.up)), "대검 공격");
                        Check(Object.FindAnyObjectByType<SwordSlashVfx>() != null, "대검 베기 VFX 생성");
                        frame = Time.frameCount + 7;
                        break;
                    case 5:
                        Check(target.CurrentHealth < target.MaxHealth, "대검 피해 유지");
                        Capture("greatsword-slash");
                        frame = Time.frameCount + 60;
                        break;
                    case 6:
                        Check(Object.FindAnyObjectByType<SwordSlashVfx>() == null, "베기 VFX 수명 종료 정리");
                        target.transform.position = combat.transform.position + forward * 7f;
                        Physics.SyncTransforms();
                        Equip("weapon-archmage-staff");
                        frame = Time.frameCount + 70;
                        break;
                    case 7:
                        Check(combat.TryAttack(Screen(target.transform.position + Vector3.up)), "대마법사 지팡이 발사");
                        MagicBoltVfx bolt = Object.FindAnyObjectByType<MagicBoltVfx>();
                        Check(bolt != null && bolt.Following, "마법탄 VFX가 발사체를 따라감");
                        frame = Time.frameCount + 5;
                        break;
                    case 8:
                        Capture("staff-bolt");
                        frame = Time.frameCount + 9;
                        break;
                    case 9:
                        Check(GameObject.Find("Magic Burst VFX") != null, "마법 명중 VFX 생성");
                        Check(target.CurrentHealth < target.MaxHealth - 42, "마법탄 피해 유지");
                        Capture("staff-impact");
                        frame = Time.frameCount + 90;
                        break;
                    case 10:
                        Check(Object.FindAnyObjectByType<MagicBoltVfx>() == null && GameObject.Find("Magic Burst VFX") == null,
                            "마법 VFX 수명 종료 정리");
                        Debug.Log($"WEAPON_VFX_PASS: {checks} checks; slope range, wall clip, greatsword slash, staff bolt/impact");
                        Finish(0);
                        return;
                }
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
            combat = Object.FindAnyObjectByType<PlayerEquipmentCombat>();
            equipment = combat.GetComponent<PlayerEquipment>();
            inventory = combat.GetComponent<PlayerInventory>();
            visuals = combat.GetComponent<EquipmentCombatVisuals>();
        }

        private static void Equip(string itemId)
        {
            int slot = -1;
            for (int index = 0; index < PlayerInventory.Capacity; index++)
                if (inventory.GetSlot(index).Item?.ItemId == itemId)
                    slot = index;
            Check(slot >= 0 && equipment.TryEquip(slot, EquipmentSlot.Weapon1) && equipment.SelectWeapon(EquipmentSlot.Weapon1) &&
                  combat.CurrentWeapon.ItemId == itemId, $"{itemId} 장착");
        }

        private static GameObject Ramp()
        {
            // 플레이어 발밑에서 시작하는 40도 오르막입니다(통합 씬 경사 한계 45도 이내).
            // 1.19m 앞에서 가슴 높이(1m)를 넘으므로 지면을 벽으로 보면 단검 범위가 잘립니다.
            Quaternion rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(-40f, 0f, 0f);
            Vector3 surface = combat.transform.position + forward * 1.4f + Vector3.up * (1.4f * Mathf.Tan(40f * Mathf.Deg2Rad));
            var slope = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slope.name = "Verification Ramp";
            slope.transform.SetPositionAndRotation(surface - rotation * Vector3.up * 0.1f, rotation);
            slope.transform.localScale = new Vector3(4f, 0.2f, 4f);
            return slope;
        }

        private static Health Target(Vector3 position)
        {
            var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.name = "VFX Target";
            dummy.transform.position = position + Vector3.up;
            Physics.SyncTransforms();
            return dummy.AddComponent<Health>();
        }

        private static Vector2 Screen(Vector3 world) => Camera.main.WorldToScreenPoint(world);

        private static void Capture(string name)
        {
            Camera camera = Camera.main;
            var texture = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                const string folder = "Artifacts/weapon-vfx";
                Directory.CreateDirectory(folder);
                File.WriteAllBytes($"{folder}/{name}.png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                Object.Destroy(texture);
                Object.Destroy(pixels);
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException($"stage{stage - 1}: {message}");
            checks++;
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(code);
        }
    }
}
