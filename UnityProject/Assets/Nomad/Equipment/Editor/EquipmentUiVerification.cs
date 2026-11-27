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
            SessionState.SetBool(SessionKey + ".CaptureUnequip", false);
            SessionState.SetBool(SessionKey + ".CaptureDrag", false);
            EquipmentAuthoring.Create();
            Begin();
        }

        public static void RunExisting()
        {
            SessionState.SetBool(SessionKey + ".CaptureUnequip", false);
            SessionState.SetBool(SessionKey + ".CaptureDrag", false);
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(EquipmentAuthoring.ScenePath);
            Begin();
        }

        public static void RunDragCapture()
        {
            SessionState.SetBool(SessionKey + ".CaptureUnequip", false);
            SessionState.SetBool(SessionKey + ".CaptureDrag", true);
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(EquipmentAuthoring.ScenePath);
            Begin();
        }

        public static void RunUnequipCapture()
        {
            SessionState.SetBool(SessionKey + ".CaptureUnequip", true);
            SessionState.SetBool(SessionKey + ".CaptureDrag", false);
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
                        Check(panel.IsOpen && panel.EquipmentGrid.childCount == 10 && ActiveBagSlots() == 20, "Tab 장비10+가방20");
                        Check(!combat.TryAttack(Point(new Vector3(0, 1, 1.5f))), "가방 열린 동안 공격 차단");
                        Capture(1920, 1080);
                        Capture(1280, 720);
                        Capture(2560, 1080);
                        source = Find("gear-chain-mail");
                        Pointer(Bag(source), true);
                        Keys();
                        break;
                    case 1: Pointer(Bag(source)); break;
                    case 2: Pointer(Gear(EquipmentSlot.Body), true); break;
                    case 3: Pointer(Gear(EquipmentSlot.Body)); break;
                    case 4:
                        Check(equipment.GetItem(EquipmentSlot.Body)?.ItemId == "gear-chain-mail" && inventory.GetSlot(source).Item == null,
                            "가방 선택 후 장비칸 클릭 장착");
                        Pointer(Gear(EquipmentSlot.Body), right: true);
                        break;
                    case 5:
                        Check(equipment.GetItem(EquipmentSlot.Body) == null && Find("gear-chain-mail") >= 0, "우클릭 해제");
                        source = Find("gear-chain-mail");
                        Pointer(Bag(source));
                        break;
                    case 6:
                        var tooltip = UnityEngine.Object.FindAnyObjectByType<InventoryPanel>().GetComponent<Canvas>()
                            .transform.Find("InventoryTooltip");
                        Check(tooltip != null && tooltip.gameObject.activeInHierarchy, "장비창과 가방 호버 툴팁 동시 표시");
                        string body = tooltip.Find("Description").GetComponent<UnityEngine.UI.Text>().text;
                        Check(body.Contains("방어력 20") && !body.Contains("공격력"), "실제 갑옷 CSV 방어력 표시");
                        Pointer(Bag(source), true);
                        break;
                    case 7:
                        Pointer(Gear(EquipmentSlot.Head), true);
                        stage = 70;
                        break;
                    case 70:
                        Check(panel.PreviewSlot == (int)EquipmentSlot.Head && !panel.CanDrop, "잘못된 도착 부위 불가 표시");
                        Check(equipment.GetItem(EquipmentSlot.Head) == null && inventory.GetSlot(source).Item?.ItemId == "gear-chain-mail",
                            "불가 미리보기 소유 무변경");
                        Pointer(Gear(EquipmentSlot.Body), true);
                        stage = 71;
                        break;
                    case 71:
                        Check(panel.PreviewSlot == (int)EquipmentSlot.Body && panel.CanDrop, "도착 장비칸 유효 표시");
                        Check(panel.EquipmentGrid.GetChild((int)EquipmentSlot.Body).Find("Icon").GetComponent<UnityEngine.UI.Image>().sprite ==
                            inventory.GetSlot(source).Item.Icon,
                            "도착 칸 아이템 미리보기");
                        Check(panel.DragPreview.gameObject.activeInHierarchy &&
                            panel.DragPreview.GetSiblingIndex() == panel.DragPreview.parent.childCount - 1, "드래그 아이템 UI 최상단");
                        Check(equipment.GetItem(EquipmentSlot.Body) == null && inventory.GetSlot(source).Item?.ItemId == "gear-chain-mail",
                            "유효 미리보기 소유 무변경");
                        if (SessionState.GetBool(SessionKey + ".CaptureDrag", false))
                        {
                            CaptureDragSnapshot();
                            Debug.Log("EQUIPMENT_DRAG_RENDER_PASS: held pointer, frontmost preview, valid target, 3 renders");
                            Finish(0);
                            return;
                        }
                        Pointer(Bag(source), true);
                        break;
                    case 72:
                        Check(panel.PreviewSlot == -1 && panel.EquipmentGrid.GetChild((int)EquipmentSlot.Body).Find("ItemName")
                            .GetComponent<UnityEngine.UI.Text>().text == "—", "포인터 이탈 시 원래 표시 복원");
                        Pointer(Gear(EquipmentSlot.Body), true);
                        break;
                    case 73:
                        Check(panel.CanDrop, $"재진입 미리보기: preview={panel.PreviewSlot}, ghost={panel.DragPreview.gameObject.activeSelf}, mouse={Mouse.current?.deviceId}/{mouse.deviceId}, held={mouse.leftButton.isPressed}, point={mouse.position.ReadValue()}, target={Gear(EquipmentSlot.Body)}, notice={panel.Notice}");
                        Keys(UnityEngine.InputSystem.Key.Escape);
                        break;
                    case 74:
                        Check(panel.PreviewSlot == -1 && !panel.DragPreview.gameObject.activeSelf &&
                            equipment.GetItem(EquipmentSlot.Body) == null, "Escape 취소와 미리보기 복원");
                        Keys();
                        Pointer(Bag(source));
                        break;
                    case 75: Pointer(Bag(source), true); break;
                    case 76:
                        Pointer(Gear(EquipmentSlot.Body), true);
                        stage = 8;
                        break;
                    case 8: Pointer(Gear(EquipmentSlot.Body)); break;
                    case 9:
                        Check(equipment.GetItem(EquipmentSlot.Body)?.ItemId == "gear-chain-mail", "가방→장비 드래그");
                        Pointer(Bag(Find("gear-iron-helm")), true);
                        stage = 88;
                        break;
                    case 88:
                        Pointer(Bag(Find("gear-iron-helm")));
                        stage = 89;
                        break;
                    case 89:
                        Pointer(Gear(EquipmentSlot.Body), true);
                        stage = 10;
                        break;
                    case 10: Pointer(Bag(source), true); break;
                    case 11:
                        Check(panel.EquipmentDragPreview.gameObject.activeInHierarchy && panel.EquipmentDragPreview.GetSiblingIndex() ==
                            panel.EquipmentDragPreview.parent.childCount - 1, "해제 드래그 아이템 최상단");
                        Check(panel.BagPreviewSlot == source && panel.CanDropToBag && panel.BagDropPreview.gameObject.activeInHierarchy &&
                            panel.BagDropPreview.Find("Icon").GetComponent<UnityEngine.UI.Image>().sprite == equipment.GetItem(EquipmentSlot.Body).Icon, "빈 가방 도착 칸 반투명 미리보기");
                        Check(equipment.GetItem(EquipmentSlot.Body)?.ItemId == "gear-chain-mail" && inventory.GetSlot(source).Item == null,
                            "해제 미리보기 소유 무변경/이전 가방 선택과 분리");
                        if (SessionState.GetBool(SessionKey + ".CaptureUnequip", false))
                        {
                            CaptureDragSnapshot(true);
                            Debug.Log("EQUIPMENT_UNEQUIP_RENDER_PASS: frontmost gear drag, bag destination preview, 3 renders");
                            Finish(0);
                            return;
                        }
                        Pointer(Bag(Find("gear-iron-helm")), true);
                        stage = 80;
                        break;
                    case 80:
                        Check(panel.BagPreviewSlot == Find("gear-iron-helm") && !panel.CanDropToBag, "불가 가방 교체 빨간 표시");
                        Pointer(Bag(Find("gear-iron-helm")));
                        break;
                    case 81:
                        Check(equipment.GetItem(EquipmentSlot.Body)?.ItemId == "gear-chain-mail" && Find("gear-iron-helm") >= 0 &&
                            !panel.EquipmentDragPreview.gameObject.activeSelf, "불가 드롭 무변경/드래그 정리");
                        Pointer(Gear(EquipmentSlot.Body), true);
                        break;
                    case 82: Pointer(Bag(source), true); break;
                    case 83:
                        Check(panel.CanDropToBag, "해제 드래그 재시도");
                        Keys(UnityEngine.InputSystem.Key.Escape);
                        break;
                    case 84:
                        Check(!panel.EquipmentDragPreview.gameObject.activeSelf && !panel.BagDropPreview.gameObject.activeSelf &&
                            equipment.GetItem(EquipmentSlot.Body)?.ItemId == "gear-chain-mail" && inventory.GetSlot(source).Item == null,
                            "해제 Escape 취소/양쪽 소유 유지");
                        Keys();
                        Pointer(Gear(EquipmentSlot.Body));
                        break;
                    case 85: Pointer(Gear(EquipmentSlot.Body), true); break;
                    case 86:
                        Pointer(Bag(source));
                        stage = 12;
                        break;
                    case 12:
                        Check(equipment.GetItem(EquipmentSlot.Body) == null && inventory.GetSlot(source).Item?.ItemId == "gear-chain-mail",
                            "장비→지정 가방칸 빠른 드래그 드롭 실제 해제");
                        Check(!panel.EquipmentDragPreview.gameObject.activeSelf && panel.BagPreviewSlot == -1, "해제 성공 표시 정리");
                        inventory.Add("demo-sword", 1);
                        source = Find("demo-sword");
                        Pointer(Gear(EquipmentSlot.Weapon2), true);
                        stage = 90;
                        break;
                    case 90: Pointer(Bag(source), true); break;
                    case 91:
                        Check(panel.CanDropToBag && equipment.GetItem(EquipmentSlot.Weapon2)?.ItemId == "demo-bow" &&
                            inventory.GetSlot(source).Item?.ItemId == "demo-sword", "호환 가방 교체 미리보기/소유 유지");
                        Pointer(Bag(source));
                        break;
                    case 92:
                        Check(equipment.GetItem(EquipmentSlot.Weapon2)?.ItemId == "demo-sword" && inventory.GetSlot(source).Item?.ItemId == "demo-bow",
                            "장비→호환 가방 아이템 실제 교체");
                        Check(equipment.TryEquip(source, EquipmentSlot.Weapon2) && inventory.Remove("demo-sword", 1) == 1, "교체 소유 보존/검사 상태 복원");
                        source = Find("demo-armor");
                        Pointer(Bag(source), true);
                        stage = 93;
                        frame = Time.frameCount + 1;
                        return;
                    // 더블클릭은 0.3초 안에 끝나도록 한 프레임 간격으로 누르고 뗍니다.
                    case 93: Pointer(Bag(source)); frame = Time.frameCount + 1; return;
                    case 94: Pointer(Bag(source), true); frame = Time.frameCount + 1; return;
                    case 95: Pointer(Bag(source)); break;
                    case 96:
                        Check(equipment.GetItem(EquipmentSlot.Body)?.ItemId == "demo-armor" && inventory.GetSlot(source).Item == null,
                            "가방 더블클릭 장착");
                        Pointer(Gear(EquipmentSlot.Body), true);
                        frame = Time.frameCount + 1;
                        return;
                    case 97: Pointer(Gear(EquipmentSlot.Body)); frame = Time.frameCount + 1; return;
                    case 98: Pointer(Gear(EquipmentSlot.Body), true); frame = Time.frameCount + 1; return;
                    case 99: Pointer(Gear(EquipmentSlot.Body)); break;
                    case 100:
                        Check(equipment.GetItem(EquipmentSlot.Body) == null && Find("demo-armor") >= 0, "장비칸 더블클릭 해제");
                        Keys(UnityEngine.InputSystem.Key.Tab);
                        Pointer(Point(new Vector3(0, 1, 1.5f)));
                        stage = 13;
                        break;
                    case 13:
                        Check(!panel.IsOpen, "Tab 함께 닫힘");
                        // F2 단축키 대신 설정 > 디버그 메뉴가 쓰는 속성으로 켭니다.
                        visuals.ShowRange = true;
                        break;
                    case 14:
                        Check(visuals.ShowRange && visuals.RangeLine.enabled && visuals.RangeLine.positionCount == 35, "범위 표시 켜기");
                        // 범위 표시는 바닥에 붙여 그리므로 수평 거리로 사거리를 비교합니다.
                        for (int index = 0; index < visuals.RangeLine.positionCount; index++)
                            Check(Vector3.ProjectOnPlane(visuals.RangeLine.GetPosition(index) - combat.AttackOrigin, Vector3.up).magnitude <=
                                  combat.CurrentWeapon.AttackRange + 0.02f, "표시 사거리 일치");
                        Capture(1920, 1080, "range");
                        visuals.ShowRange = false;
                        break;
                    case 15: break;
                    case 16:
                        Check(!visuals.ShowRange && !visuals.RangeLine.enabled, "범위 표시 끄기");
                        Pointer(Point(new Vector3(0, 1, 1.5f)));
                        break;
                    case 17:
                        // 장검 베기는 VFX로, 두 대상 명중은 선 효과로 표시합니다.
                        Check(combat.TryAttack(Point(new Vector3(0, 1, 1.5f))) && visuals.ActiveEffects >= 2 &&
                              UnityEngine.Object.FindAnyObjectByType<SwordSlashVfx>() != null, "실제 공격/명중 효과");
                        Capture(1920, 1080, "attack");
                        frame = Time.frameCount + 35;
                        return;
                    case 18:
                        Check(visuals.ActiveEffects == 0, "효과 수명 종료 정리");
                        Check(equipment.SelectWeapon(EquipmentSlot.Weapon2), "원거리 선택");
                        visuals.ShowRange = true;
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
                        Debug.Log($"EQUIPMENT_UI_PASS: {checks} checks; Tab30, pointer equip/unequip/drag, range toggle, effects, 3 renders");
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

        // 가방 칸은 최대 용량만큼 만들고 열리지 않은 칸은 숨기므로 보이는 칸만 셉니다.
        private static int ActiveBagSlots() =>
            panel.BagGrid.Cast<Transform>().Count(slot => slot.gameObject.activeSelf);
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

        private static void CaptureDragSnapshot(bool unequip = false)
        {
            // RenderTexture 전환이 GameView 입력을 리셋하므로, 검사된 실제 UI의 그리기 상태만 복제합니다.
            // 복제본에는 InventoryPanel 스크립트가 없어 렌더 중 드래그 취소가 발생하지 않습니다.
            var sourceCanvas = panel.EquipmentGrid.GetComponentInParent<Canvas>();
            var root = (RectTransform)panel.EquipmentGrid.parent;
            var snapshot = new GameObject("Drag Snapshot", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            var canvas = snapshot.GetComponent<Canvas>();
            var scaler = snapshot.GetComponent<UnityEngine.UI.CanvasScaler>();
            var sourceScaler = sourceCanvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = sourceScaler.uiScaleMode;
            scaler.referenceResolution = sourceScaler.referenceResolution;
            scaler.screenMatchMode = sourceScaler.screenMatchMode;
            scaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
            var clone = UnityEngine.Object.Instantiate(root, snapshot.transform, false);
            var preview = clone.Find(unequip ? "EquipmentDragPreview" : "DragPreview");
            Check(preview.gameObject.activeSelf && preview.GetSiblingIndex() == clone.childCount - 1,
                "실제 드래그 그리기 순서 스냅샷 유지");

            Camera camera = Camera.main;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            bool wasEnabled = sourceCanvas.enabled;
            try
            {
                sourceCanvas.enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(2560, 1080) })
                {
                    var texture = new RenderTexture(size.x, size.y, 24);
                    var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    try
                    {
                        camera.targetTexture = texture;
                        Canvas.ForceUpdateCanvases();
                        scaler.SendMessage("Update");
                        Canvas.ForceUpdateCanvases();
                        clone.localScale = Vector3.one * Mathf.Min(1f, (size.y / canvas.scaleFactor - 48) / 752f);
                        Canvas.ForceUpdateCanvases();
                        Check(preview.gameObject.activeInHierarchy && preview.GetSiblingIndex() == clone.childCount - 1,
                            "렌더 드래그 최상단 " + size.x);
                        camera.Render();
                        RenderTexture.active = texture;
                        pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                        pixels.Apply();
                        string kind = unequip ? "unequip-drag" : "drag";
                        File.WriteAllBytes($"Artifacts/equipment/equipment-{kind}-{size.x}.png", pixels.EncodeToPNG());
                    }
                    finally
                    {
                        camera.targetTexture = previousTarget;
                        RenderTexture.active = previousActive;
                        UnityEngine.Object.DestroyImmediate(texture);
                        UnityEngine.Object.DestroyImmediate(pixels);
                    }
                }
            }
            finally
            {
                sourceCanvas.enabled = wasEnabled;
                UnityEngine.Object.DestroyImmediate(snapshot);
                Canvas.ForceUpdateCanvases();
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
