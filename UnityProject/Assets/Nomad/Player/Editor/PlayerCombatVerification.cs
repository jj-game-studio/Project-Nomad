using System;
using Nomad.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Nomad.Player.Editor
{
    internal static class PlayerCombatVerification
    {
        private const string SessionKey = "Nomad.Player.VerifyCombat";

        private static PlayerCombat combat;
        private static PlayerMovement movement;
        private static Mouse mouse;
        private static Keyboard keyboard;
        private static Health player;
        private static Health high;
        private static Health low;
        private static Health shielded;
        private static Camera camera;
        private static int stage;
        private static int deaths;
        private static float started;
        private static double deadline;
        private static int result;
        private static GameObject uiEvents;
        private static GameObject uiCanvas;

        [MenuItem("Nomad/Player/Verify Combat Demo")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(PlayerCombatAuthoring.ScenePath);
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false))
                return;

            deadline = EditorApplication.timeSinceStartup + 45;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new InvalidOperationException($"Combat verification timed out at stage {stage}.");

                if (!Application.isPlaying)
                    return;

                if (combat == null)
                    Initialize();

                float elapsed = Time.time - started;
                if (elapsed >= 0.45f || stage == 8)
                    VerifyStage();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void Initialize()
        {
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Time.captureFramerate = 60;

            combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            movement = combat.GetComponent<PlayerMovement>();
            player = combat.GetComponent<Health>();
            camera = Camera.main;
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse.MakeCurrent();
            keyboard.MakeCurrent();

            high = GameObject.Find("High Target").GetComponent<Health>();
            low = GameObject.Find("Low Target").GetComponent<Health>();
            shielded = GameObject.Find("Shielded Target").GetComponent<Health>();
            low.Died += _ => deaths++;
            stage = 0;
            deaths = 0;
            started = Time.time;
        }

        private static void VerifyStage()
        {
            switch (stage)
            {
                case 0:
                    Check(player.CurrentHealth == 100, "initial health");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = ScreenPoint(high) }
                        .WithButton(MouseButton.Left));
                    break;
                case 1:
                    Check(high.CurrentHealth == 75, "held mouse fires once and hits elevated target");
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    Check(combat.TryShoot(ScreenPoint(low)), "launch toward low target");
                    break;
                case 2:
                    Check(low.CurrentHealth == 75, "low target loses health");
                    Check(combat.TryShoot(ScreenPoint(shielded)), "camera can aim over thin wall");
                    Check(combat.AimPoint is Vector3 aim &&
                        shielded.GetComponent<Collider>().bounds.Contains(aim), "camera aim hits shielded target");
                    break;
                case 3:
                    Check(shielded.CurrentHealth == 100, "thin wall blocks actual projectile");
                    VerifyBlockedMuzzle();
                    CreateUi();
                    break;
                case 4:
                    Check(!combat.TryShoot(ScreenPoint(high)), "UI intercepts click");
                    uiCanvas.SetActive(false);
                    uiEvents.SetActive(false);
                    Check(combat.TryShoot(ScreenPoint(low)) && combat.TryShoot(ScreenPoint(low)) &&
                        combat.TryShoot(ScreenPoint(low)) && combat.TryShoot(ScreenPoint(low)), "lethal shots launched");
                    break;
                case 5:
                    Check(low.IsDead && deaths == 1 && !low.TryApplyDamage(25), "death event only once");
                    PlacePlayer(new Vector3(0f, 5.1f, 0f));
                    break;
                case 6:
                    Check(player.CurrentHealth == 100, "fall damage waits until landing");
                    break;
                case 7:
                    Check(player.CurrentHealth is >= 78 and <= 80, "fall damage applies once");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                    break;
                case 8:
                    // 구르기 창은 0.3초이므로 다음 프레임에서 바로 상태를 확인합니다.
                    if (!movement.IsRolling)
                    {
                        Check(Time.time - started < 0.25f, "roll input consumed");
                        return;
                    }

                    Check(!player.TryApplyDamage(10), "rolling blocks combat damage");
                    Check(!combat.TryShoot(ScreenPoint(high)), "rolling blocks attack");
                    Check(player.TryApplyDamage(1, DamageKind.Fall), "rolling allows fall damage");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    break;
                case 9:
                    Check(!movement.IsRolling && player.TryApplyDamage(1), "combat damage resumes after roll");
                    PlacePlayer(new Vector3(0f, -21f, 0f));
                    break;
                case 10:
                    Check(player.IsDead && !combat.TryShoot(ScreenPoint(high)), "kill plane and dead player firing");
                    Debug.Log("Nomad player combat verification passed.");
                    Finish(0);
                    return;
            }

            stage++;
            started = Time.time;
        }

        private static void VerifyBlockedMuzzle()
        {
            Transform muzzle = combat.transform.Find("Muzzle");
            Vector3 original = muzzle.position;
            muzzle.position = new Vector3(0f, 1.2f, 2f);
            Check(!combat.TryShoot(ScreenPoint(high)), "muzzle inside wall blocks launch");
            muzzle.position = new Vector3(0f, 1.2f, 2.3f);
            Check(!combat.TryShoot(ScreenPoint(high)), "muzzle crossing thin wall blocks launch");
            muzzle.position = original;
        }

        private static void CreateUi()
        {
            uiEvents = new GameObject("Verification Event System", typeof(EventSystem));
            uiCanvas = new GameObject("Verification Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            uiCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            GameObject image = new GameObject("Raycast Blocker", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(uiCanvas.transform, false);
            RectTransform rect = (RectTransform)image.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
        }

        private static void PlacePlayer(Vector3 position)
        {
            CharacterController controller = combat.GetComponent<CharacterController>();
            controller.enabled = false;
            combat.transform.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static Vector2 ScreenPoint(Health target) => camera.WorldToScreenPoint(target.transform.position);

        private static void Check(bool condition, string description)
        {
            if (!condition)
                throw new InvalidOperationException($"Combat verification failed at stage {stage}: {description}");
        }

        private static void Finish(int exitCode)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Tick;
            if (Application.isBatchMode)
            {
                result = exitCode;
                EditorApplication.playModeStateChanged += ExitBatch;
            }

            EditorApplication.ExitPlaymode();
        }

        private static void ExitBatch(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.Exit(result);
        }
    }
}
