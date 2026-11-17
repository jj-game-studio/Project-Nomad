using System;
using Nomad.Interaction.Demo;
using Nomad.Inventory;
using Nomad.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nomad.Interaction.Editor
{
    internal static class InteractionVerification
    {
        private const string InventoryScene = "Assets/Nomad/Inventory/A_PlayerInventory.unity";
        private const string SessionKey = "Nomad.Interaction.Verify";

        private static PlayerMovement player;
        private static PlayerInventory inventory;
        private static Keyboard keyboard;
        private static GameObject first;
        private static GameObject second;
        private static GameObject blocked;
        private static GameObject far;
        private static int frame;
        private static int stage;
        private static float rollStart;
        private static double deadline;

        [MenuItem("Nomad/Interaction/Open E Key Preview")]
        private static void OpenPreview()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            OpenInventoryScene();
            Scene preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(preview);
            preview.name = "E Key Preview";
            BuildTargets(Object.FindAnyObjectByType<PlayerInteraction>().transform.position);
            Debug.Log("E Key Preview: press E for the test pickups and Tab to view Scrap.");
        }

        private static void OpenInventoryScene()
        {
            EditorSceneManager.OpenScene(InventoryScene);
        }

        public static void Run()
        {
            OpenInventoryScene();
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false))
                return;

            deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "상호작용 검증 시간 초과");
                if (!Application.isPlaying || Time.frameCount <= frame)
                    return;

                frame = Time.frameCount;
                if (keyboard == null)
                {
                    player = Object.FindAnyObjectByType<PlayerMovement>();
                    inventory = player.GetComponent<PlayerInventory>();
                    if (!player.GetComponent<CharacterController>().isGrounded || inventory.UsedSlots == 0)
                        return;

                    Require(player.GetComponent<PlayerInteraction>() != null, "인벤토리 장면 플레이어의 입력 연결");
                    BuildTargets(player.transform.position);
                    Physics.SyncTransforms();
                    Time.captureFramerate = 60;
                    Application.runInBackground = true;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode =
                        InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    keyboard.MakeCurrent();
                    Queue(Key.E);
                    return;
                }

                Advance();
            }
            catch (Exception exception)
            {
                Debug.LogError($"PLAYER_INTERACTION_FAIL stage={stage}: {exception}");
                Finish(1);
            }
        }

        private static void Advance()
        {
            switch (++stage)
            {
                case 1:
                    Require(!first.activeSelf && second.activeSelf && blocked.activeSelf && far.activeSelf &&
                            Scrap() == 97, "거리 동률 ID 순서, 시야 차단, 단일 호출 및 인벤토리 전달");
                    break;
                case 2:
                    Require(second.activeSelf && Scrap() == 97, "E를 누르고 있어도 반복 호출하지 않음");
                    far.transform.position = player.transform.position + new Vector3(0.9f, 0.35f, 1.7f);
                    Physics.SyncTransforms();
                    Queue();
                    break;
                case 3:
                    Queue(Key.E);
                    break;
                case 4:
                    Require(!second.activeSelf && blocked.activeSelf && far.activeSelf && Scrap() == 98,
                            "두 번째 E로 더 가까운 유효 대상 한 개 호출");
                    far.transform.position = player.transform.position + new Vector3(0.9f, 0.35f, 2f);
                    Physics.SyncTransforms();
                    Queue();
                    break;
                case 5:
                    Queue(Key.E);
                    break;
                case 6:
                    Require(blocked.activeSelf && far.activeSelf && Scrap() == 98, "벽 및 2m 밖 대상 제외");
                    // 구르는 방향 옆에 대상을 두어 사거리 밖으로 움직여서 우연히 통과하는 일을 피합니다.
                    first.transform.position = player.transform.position + new Vector3(-0.9f, 0.35f, 0f);
                    first.SetActive(true);
                    Physics.SyncTransforms();
                    Queue();
                    break;
                case 7:
                    rollStart = Time.time;
                    Queue(Key.LeftShift, Key.E);
                    break;
                case 8:
                    Require(player.IsRolling && first.activeSelf && Scrap() == 98, "구르기 시작 프레임의 E 차단");
                    break;
                default:
                    if (Time.time - rollStart < 0.4f)
                        return;

                    Require(!player.IsRolling && first.activeSelf && Scrap() == 98, "구르기 중 입력 미예약");
                    Debug.Log($"PLAYER_INTERACTION_PASS frame={Time.frameCount}: E, hold, tie, closest, line of sight, 2m, inventory, roll");
                    Finish(0);
                    break;
            }
        }

        private static int Scrap() => inventory.GetStack(0).Count;

        private static void BuildTargets(Vector3 feet)
        {
            // 가까운 쪽에 벽을 두고, 보이는 두 표적은 서로 같은 거리에 둡니다.
            second = CreateTarget("pickup-b", feet + new Vector3(1f, 0.35f, 1.3f));
            first = CreateTarget("pickup-a", feet + new Vector3(-1f, 0.35f, 1.3f));
            blocked = CreateTarget("pickup-blocked", feet + new Vector3(0f, 0.35f, 1.1f));
            far = CreateTarget("pickup-outside-2m", feet + new Vector3(0.9f, 0.35f, 2f));
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Sight blocker";
            wall.transform.position = feet + new Vector3(0f, 0.5f, 0.7f);
            wall.transform.localScale = new Vector3(0.45f, 1f, 0.12f);
        }

        private static GameObject CreateTarget(string id, Vector3 position)
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = id;
            target.transform.position = position;
            target.transform.localScale = Vector3.one * 0.42f;
            Collider collider = target.GetComponent<Collider>();
            collider.isTrigger = true;
            var serialized = new SerializedObject(target.AddComponent<InteractionDemoTarget>());
            serialized.FindProperty("interactionId").stringValue = id;
            serialized.FindProperty("detectionCollider").objectReferenceValue = collider;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return target;
        }

        private static void Queue(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        }

        private static void Require(bool condition, string description)
        {
            if (!condition)
                throw new InvalidOperationException(description);
        }

        private static void Finish(int exitCode)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Update;
            EditorApplication.Exit(exitCode);
        }
    }
}
