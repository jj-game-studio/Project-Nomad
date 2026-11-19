using System;
using System.IO;
using System.Linq;
using Nomad.Inventory;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Nomad.Equipment.Editor
{
    internal static class EquipmentUiVerification
    {
        private const string SessionKey = "Nomad.Equipment.UiVerification";
        private static EquipmentPanel panel;
        private static PlayerEquipment equipment;
        private static PlayerEquipmentCombat combat;
        private static EquipmentCombatVisuals visuals;
        private static PlayerInventory inventory;
        private static Mouse mouse;
        private static Keyboard keyboard;
        private static int stage;
        private static int frame;
        private static int source;
        private static int checks;
        private static int impacts;
        private static double deadline;

        public static void Run()
        {
            EquipmentAuthoring.Create();
            Begin();
        }

        public static void RunExisting()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(EquipmentAuthoring.ScenePath);
            Begin();
        }

        private static void Begin()
        {
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("UI stage " + stage);
                if (!Application.isPlaying || Time.frameCount < 5 || Time.frameCount < frame) return;
                if (panel == null)
                {
                    Initialize();
                    Keys(UnityEngine.InputSystem.Key.Tab);
                    frame = Time.frameCount + 3;
                    return;
                }

                switch (stage++)
                {
                    case 0:
                        Check(panel.IsOpen && panel.EquipmentGrid.childCount == 10 && panel.BagGrid.childCount == 20, "Tab 장비10+가방20");
                        Check(!combat.TryAttack(Point(new Vector3(0, 1, 1.5f))), "가방 열린 동안 공격 차단");
                        Capture(1920, 1080);
                        Capture(1280, 720);
                        Capture(2560, 1080);
                        source = Find("demo-armor");
                        Pointer(Bag(source), true);
                        Keys();
                        break;
                    case 1: Pointer(Bag(source)); break;
                    case 2: Pointer(Gear(EquipmentSlot.Body), true); break;
                    case 3: Pointer(Gear(EquipmentSlot.Body)); break;
                    case 4:
                        Check(equipment.GetItem(EquipmentSlot.Body)?.ItemId == "demo-armor" && inventory.GetSlot(source).Item == null,
                            "가방 선택 후 장비칸 클릭 장착");
                        Pointer(Gear(EquipmentSlot.Body), right: true);
                        break;
                    case 5:
                        Check(equipment.GetItem(EquipmentSlot.Body) == null && Find("demo-armor") >= 0, "우클릭 해제");
                        source = Find("demo-armor");
                        Pointer(Bag(source));
                        break;
                    case 6: Pointer(Bag(source), true); break;
                    case 7: Pointer(Gear(EquipmentSlot.Body), true); break;
                    case 8: Pointer(Gear(EquipmentSlot.Body)); break;
                    case 9:
                        Check(equipment.GetItem(EquipmentSlot.Body)?.ItemId == "demo-armor", "가방→장비 드래그");
                        Pointer(Gear(EquipmentSlot.Body), true);
                        break;
                    case 10: Pointer(Bag(source), true); break;
                    case 11: Pointer(Bag(source)); break;
                    case 12:
                        Check(equipment.GetItem(EquipmentSlot.Body) == null && inventory.GetSlot(source).Item?.ItemId == "demo-armor",
                            "장비→지정 가방칸 드래그");
                        Keys(UnityEngine.InputSystem.Key.Tab);
                        Pointer(Point(new Vector3(0, 1, 1.5f)));
                        break;
                    case 13:
                        Check(!panel.IsOpen, "Tab 함께 닫힘");
                        Keys(UnityEngine.InputSystem.Key.F2);
                        break;
                    case 14:
                        Check(visuals.ShowRange && visuals.RangeLine.enabled && visuals.RangeLine.positionCount == 35, "F2 범위 표시");
                        for (int index = 0; index < visuals.RangeLine.positionCount; index++)
                            Check(Vector3.Distance(visuals.RangeLine.GetPosition(index), combat.AttackOrigin) <= combat.CurrentWeapon.AttackRange + 0.02f,
                                "표시 사거리 일치");
                        Capture(1920, 1080, "range");
                        Keys();
                        Pointer(Screen(panel.Toolbar), true);
                        break;
                    case 15: Pointer(Screen(panel.Toolbar)); break;
                    case 16:
                        Check(!visuals.ShowRange && !visuals.RangeLine.enabled, "화면 토글 범위 끄기");
                        Pointer(Point(new Vector3(0, 1, 1.5f)));
                        break;
                    case 17:
                        Check(combat.TryAttack(Point(new Vector3(0, 1, 1.5f))) && visuals.ActiveEffects >= 3, "실제 공격/명중 효과");
                        Capture(1920, 1080, "attack");
                        frame = Time.frameCount + 35;
                        return;
                    case 18:
                        Check(visuals.ActiveEffects == 0, "효과 수명 종료 정리");
                        Check(equipment.SelectWeapon(EquipmentSlot.Weapon2), "원거리 선택");
                        Keys(UnityEngine.InputSystem.Key.F2);
                        break;
                    case 19:
                        Check(visuals.RangeLine.enabled && visuals.RangeLine.positionCount == 2, "원거리 범위 선");
                        combat.Impacted += CountImpact;
                        Check(combat.TryAttack(Point(GameObject.Find("Low Target").transform.position)), "원거리 발사 효과");
                        Check(visuals.ActiveEffects > 0, "발사 섬광 생성");
                        frame = Time.frameCount + 30;
                        return;
                    case 20:
                        Check(impacts > 0, "실제 발사체 충돌 효과 콜백");
                        visuals.enabled = false;
                        Check(!visuals.RangeLine.enabled && visuals.ActiveEffects == 0, "비활성 시 표시/효과 정리");
                        Debug.Log($"EQUIPMENT_UI_PASS: {checks} checks; Tab30, pointer equip/unequip/drag, F2/button, range, effects, 3 renders");
                        Finish(0);
                        return;
                }
                frame = Time.frameCount + 3;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Finish(1);
            }
        }

        private static void CountImpact(Vector3 point) => impacts++;

        private static void Initialize()
        {
            Application.runInBackground = true;
            Time.captureFramerate = 60;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            panel = UnityEngine.Object.FindAnyObjectByType<EquipmentPanel>();
            equipment = panel.GetComponent<PlayerEquipment>();
            inventory = panel.GetComponent<PlayerInventory>();
            combat = panel.GetComponent<PlayerEquipmentCombat>();
            visuals = panel.GetComponent<EquipmentCombatVisuals>();
            mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
        }

        private static int Find(string id) => Enumerable.Range(0, PlayerInventory.Capacity).First(index => inventory.GetSlot(index).Item?.ItemId == id);
        private static Vector2 Gear(EquipmentSlot slot) => Screen((RectTransform)panel.EquipmentGrid.GetChild((int)slot));
        private static Vector2 Bag(int slot) => Screen((RectTransform)panel.BagGrid.GetChild(slot));
        private static Vector2 Screen(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        private static Vector2 Point(Vector3 point) => Camera.main.WorldToScreenPoint(point);
        private static void Keys(params UnityEngine.InputSystem.Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        private static void Pointer(Vector2 point, bool held = false, bool right = false)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }
                .WithButton(MouseButton.Left, held).WithButton(MouseButton.Right, right));
        }

        private static void Capture(int width, int height, string suffix = "ui")
        {
            Camera camera = Camera.main;
            var canvas = panel.EquipmentGrid.GetComponentInParent<Canvas>();
            var texture = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = texture;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                canvas.GetComponent<UnityEngine.UI.CanvasScaler>().SendMessage("Update");
                Canvas.ForceUpdateCanvases();
                panel.FitToCanvas();
                Canvas.ForceUpdateCanvases();
                var root = (RectTransform)panel.EquipmentGrid.parent;
                foreach (RectTransform slot in panel.EquipmentGrid.Cast<RectTransform>().Concat(panel.BagGrid.Cast<RectTransform>())
                             .Concat(new[] { root, (RectTransform)root.Find("Title"), (RectTransform)root.Find("EquipmentHelp") }))
                {
                    var corners = new Vector3[4];
                    slot.GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                        Check(point.x >= 0 && point.x <= width && point.y >= 0 && point.y <= height,
                            $"화면 내부 {slot.name} {point} view{width}x{height} canvas{canvas.pixelRect} factor{canvas.scaleFactor} scale{root.localScale}");
                    }
                }
                Check(panel.EquipmentGrid.position.y > panel.BagGrid.position.y, "장비가 가방 위");
                camera.Render();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Artifacts/equipment");
                File.WriteAllBytes($"Artifacts/equipment/equipment-{suffix}-{width}.png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                Canvas.ForceUpdateCanvases();
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException($"UI stage{stage - 1}: {label}");
            checks++;
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Tick;
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (combat != null) combat.Impacted -= CountImpact;
            EditorApplication.Exit(code);
        }
    }
}
