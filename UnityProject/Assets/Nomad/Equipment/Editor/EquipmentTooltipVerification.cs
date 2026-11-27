using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Nomad.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Nomad.Equipment.Editor
{
    internal static class EquipmentTooltipVerification
    {
        private const string SessionKey = "Nomad.Equipment.TooltipVerify";
        private static EquipmentPanel panel;
        private static PlayerEquipment equipment;
        private static PlayerInventory inventory;
        private static InventoryPanel bagView;
        private static RectTransform canvas;
        private static Mouse mouse;
        private static Keyboard keyboard;
        private static ItemDefinition candidate;
        private static ItemDefinition equipped;
        private static int stage;
        private static int frame;
        private static int checks;
        private static double deadline;

        [MenuItem("Nomad/Equipment/Add Tooltip Comparison Samples")]
        public static void AddReviewSamples()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Equipment Demo를 Play한 뒤 비교 샘플을 추가하세요.");
                return;
            }

            var player = UnityEngine.Object.FindAnyObjectByType<PlayerEquipment>();
            if (player == null) return;
            var bag = player.GetComponent<PlayerInventory>();
            int[] empty = Enumerable.Range(0, PlayerInventory.Capacity).Where(index => bag.GetSlot(index).Item == null).Take(2).ToArray();
            if (empty.Length < 2)
            {
                Debug.LogWarning("비교 샘플을 추가하려면 가방에 빈칸 2개가 필요합니다.");
                return;
            }

            ItemDefinition armor = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Nomad/Equipment/Items" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .First(item => item.ItemId == "gear-chain-mail");
            var worn = UnityEngine.Object.Instantiate(armor);
            var other = UnityEngine.Object.Instantiate(armor);
            Set(worn, "displayName", "갑옷 B");
            Set(other, "displayName", "갑옷 A");
            Set(other, "defense", 30);
            var add = typeof(PlayerInventory).GetMethod("AddToSlotForTransfer", BindingFlags.Instance | BindingFlags.NonPublic);
            add.Invoke(bag, new object[] { empty[0], worn, 1 });
            add.Invoke(bag, new object[] { empty[1], other, 1 });
            player.TryEquip(empty[0], EquipmentSlot.Body);
            Debug.Log("비교 샘플 준비: 갑옷 B(사슬 갑옷 사본) 장착, 가방 갑옷 A(방어력30)에 커서를 올리세요. 원본 에셋은 변경하지 않습니다.");
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene(EquipmentAuthoring.ScenePath);
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
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("stage " + stage);
                if (!Application.isPlaying || Time.frameCount < frame || Time.frameCount < 5) return;
                if (panel == null)
                {
                    Initialize();
                    Keys(Key.Tab);
                    frame = Time.frameCount + 3;
                    return;
                }

                switch (stage++)
                {
                    case 0:
                        Check(panel.IsOpen, "Tab opens equipment and bag");
                        Keys();
                        Pointer(Bag(19));
                        break;
                    case 1:
                        Check(Card(0).gameObject.activeInHierarchy && Title(0).Contains("갑옷 A"), "single candidate tooltip");
                        Check(canvas.Find("EquippedComparison1") == null && canvas.Find("EquippedComparison2") == null,
                            "no separate equipped tooltip objects");
                        Check(Stats.Contains("방어력 30") && Stats.Contains("▲10") && !Stats.Contains("장착") && !Stats.Contains("20"),
                            "only candidate stat and positive difference");
                        Check(Stats.Contains("#80D99A") && !Stats.Contains("갑옷 B"), "positive color without equipped name");
                        Check(!canvas.Find("InventoryTooltip").gameObject.activeSelf, "no duplicate bag tooltip");
                        RenderComparison();
                        Set(equipped, "defense", 35);
                        break;
                    case 2:
                        Check(Stats.Contains("▼5") && Stats.Contains("#F08383") && !Stats.Contains("▲10"), "stationary downgrade updates");
                        RenderComparison("decrease");
                        Set(equipped, "defense", 30);
                        break;
                    case 3:
                        Check(Stats.Contains("(-)") && !Stats.Contains("▲") && !Stats.Contains("▼"), "equal stat has dash and no arrow");
                        Set(candidate, "defense", 0);
                        break;
                    case 4:
                        Check(Stats.Contains("방어력 0") && Stats.Contains("▼30"), "zero candidate stat still shows loss");
                        Set(candidate, "defense", 30);
                        Pointer(Gear(EquipmentSlot.Body));
                        break;
                    case 5:
                        Check(Title(0).Contains("갑옷 B") && Body(0).Contains("방어력 30") &&
                            !Card(0).Find("ComparisonStats").gameObject.activeSelf, "equipped slot keeps standalone hover");
                        Pointer(Gear(EquipmentSlot.Head));
                        break;
                    case 6:
                        Check(!Card(0).gameObject.activeSelf, "empty equipment slot hides");
                        Pointer(Bag(Find("demo-sword")));
                        break;
                    // 무기는 장비칸 없이 가방에 있으므로 바로 가방 무기 비교를 확인합니다.
                    case 7:
                        Pointer(Bag(Find("demo-sword")));
                        break;
                    case 8:
                        Check(!Card(0).gameObject.activeSelf && canvas.Find("InventoryTooltip").gameObject.activeSelf,
                            "bag weapon comparison stays deferred");
                        Pointer(Bag(19));
                        break;
                    case 9:
                        Check(Card(0).gameObject.activeSelf && Stats.Contains("(-)"), "comparison restored");
                        Pointer(Bag(19), true);
                        break;
                    case 10:
                        Check(!Card(0).gameObject.activeSelf, "press hides comparison");
                        Keys(Key.Escape);
                        Pointer(Vector2.zero);
                        break;
                    case 11:
                        Keys();
                        Pointer(Bag(19));
                        break;
                    case 12:
                        Check(Card(0).gameObject.activeSelf, "hover after cancel");
                        panel.SendMessage("OnApplicationFocus", false);
                        bagView.SendMessage("OnApplicationFocus", false);
                        Check(!Card(0).gameObject.activeSelf, "focus loss hides immediately");
                        break;
                    case 13:
                        Check(!Card(0).gameObject.activeSelf, "stays hidden without focus");
                        panel.SendMessage("OnApplicationFocus", true);
                        bagView.SendMessage("OnApplicationFocus", true);
                        break;
                    case 14:
                        Check(Card(0).gameObject.activeSelf, "focus return restores");
                        Check(equipment.TryUnequip(EquipmentSlot.Body), "unequip baseline fixture");
                        break;
                    case 15:
                        Check(!Card(0).gameObject.activeSelf && canvas.Find("InventoryTooltip").gameObject.activeSelf,
                            "no equipped match has no comparison");
                        Check(equipment.TryEquip(19, EquipmentSlot.Body), "equip candidate fixture");
                        break;
                    case 16:
                        Check(!Card(0).gameObject.activeSelf && !canvas.Find("InventoryTooltip").gameObject.activeSelf,
                            "removed candidate hides without moving pointer");
                        SetupAccessories();
                        Pointer(Bag(19));
                        break;
                    case 17:
                        Check(Card(0).gameObject.activeSelf && Stats.Contains("장착 1") && Stats.Contains("장착 2"),
                            "two accessory comparisons inside one tooltip");
                        Keys(Key.Tab);
                        break;
                    case 18:
                        Check(!Card(0).gameObject.activeInHierarchy, "closing hides tooltip");
                        int used = inventory.UsedSlots;
                        AddReviewSamples();
                        Check(inventory.UsedSlots == used + 2, "review sample menu preserves old gear");
                        Check(equipment.GetItem(EquipmentSlot.Body).DisplayName == "갑옷 B" &&
                            equipment.GetItem(EquipmentSlot.Body).Defense == 3, "review sample B equipped");
                        Check(Enumerable.Range(0, PlayerInventory.Capacity).Any(index => inventory.GetSlot(index).Item?.DisplayName == "갑옷 A" &&
                            inventory.GetSlot(index).Item.Defense == 30), "review sample A in bag");
                        Debug.Log($"EQUIPMENT_TOOLTIP_PASS: {checks} checks, single tooltip, delta/color, weapon deferred, lifecycle, 3 resolutions");
                        Finish(0);
                        return;
                }
                frame = Time.frameCount + 3;
            }
            catch (Exception exception)
            {
                Debug.LogError($"EQUIPMENT_TOOLTIP_FAIL stage={stage}: {exception}");
                Finish(1);
            }
        }

        private static void Initialize()
        {
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            panel = UnityEngine.Object.FindAnyObjectByType<EquipmentPanel>();
            equipment = panel.GetComponent<PlayerEquipment>();
            inventory = panel.GetComponent<PlayerInventory>();
            // 데모 가방에는 비교용으로 등급이 다른 갑옷·투구가 3개씩 있습니다.
            foreach (var sample in new[] { ("gear-padded-armor", 2), ("gear-chain-mail", 3), ("gear-plate-armor", 4),
                         ("gear-leather-cap", 1), ("gear-iron-helm", 1), ("gear-knight-helm", 2) })
                Check(inventory.GetSlot(Find(sample.Item1)).Item.Defense == sample.Item2, "different tier sample " + sample.Item1);
            bagView = UnityEngine.Object.FindAnyObjectByType<InventoryPanel>();
            canvas = (RectTransform)bagView.GetComponent<Canvas>().transform;
            equipped = UnityEngine.Object.Instantiate(inventory.GetSlot(Find("gear-chain-mail")).Item);
            Set(equipped, "displayName", "갑옷 B");
            candidate = UnityEngine.Object.Instantiate(equipped);
            Set(equipped, "defense", 20);
            Set(candidate, "displayName", "갑옷 A");
            Set(candidate, "defense", 30);
            Transfer(18, equipped);
            Transfer(19, candidate);
            Check(equipment.TryEquip(18, EquipmentSlot.Body), "equipped B fixture");
            mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            panel.SendMessage("OnApplicationFocus", true);
            bagView.SendMessage("OnApplicationFocus", true);
        }

        private static void SetupAccessories()
        {
            candidate = UnityEngine.Object.Instantiate(inventory.GetSlot(Find("gear-copper-ring")).Item);
            Set(candidate, "displayName", "비교용 장신구");
            Transfer(19, candidate);
            Transfer(18, candidate);
            Check(equipment.TryEquip(18, EquipmentSlot.Accessory1), "first accessory fixture");
            Transfer(18, candidate);
            Check(equipment.TryEquip(18, EquipmentSlot.Accessory2), "second accessory fixture");
        }

        private static void Transfer(int slot, ItemDefinition item)
        {
            typeof(PlayerInventory).GetMethod("AddToSlotForTransfer", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(inventory, new object[] { slot, item, 1 });
            typeof(PlayerInventory).GetMethod("NotifyChanged", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(inventory, null);
        }

        private static void Set(ItemDefinition item, string field, object value)
        {
            typeof(ItemDefinition).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, value);
        }

        private static RectTransform Card(int index) => (RectTransform)canvas.Find(index == 0 ? "EquipmentTooltip" : "EquippedComparison" + index);
        private static string Title(int index) => Card(index).Find("Title").GetComponent<UnityEngine.UI.Text>().text;
        private static string Body(int index) => Card(index).Find("Description").GetComponent<UnityEngine.UI.Text>().text;
        private static int Find(string id) => Enumerable.Range(0, PlayerInventory.Capacity).First(index => inventory.GetSlot(index).Item?.ItemId == id);
        private static Vector2 Bag(int index) => Screen((RectTransform)panel.BagGrid.GetChild(index));
        private static Vector2 Gear(EquipmentSlot slot) => Screen((RectTransform)panel.EquipmentGrid.GetChild((int)slot));
        private static Vector2 Screen(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        private static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private static void Pointer(Vector2 point, bool held = false) => InputSystem.QueueStateEvent(mouse,
            new MouseState { position = point }.WithButton(MouseButton.Left, held));

        private static string Stats => Card(0).Find("ComparisonStats").GetComponent<UnityEngine.UI.Text>().text;

        private static void RenderComparison(string suffix = "increase")
        {
            // 실제 호버 검증과 별개로 같은 항목을 표시 전용 사본에서 렌더합니다.
            Type type = typeof(InventoryPanel).Assembly.GetType("Nomad.Inventory.InventoryTooltip");
            foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1080) })
            {
                var root = new GameObject("Delta Render", typeof(RectTransform), typeof(Canvas));
                var cameraRoot = new GameObject("Delta Camera", typeof(Camera));
                Camera camera = cameraRoot.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(31, 36, 39, 255);
                camera.cullingMask = 1 << 5;
                var target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = target;
                var renderCanvas = root.GetComponent<Canvas>();
                renderCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                renderCanvas.worldCamera = camera;
                renderCanvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                object card = Activator.CreateInstance(type, root.GetComponent<RectTransform>(),
                    Card(0).Find("Title").GetComponent<UnityEngine.UI.Text>().font);
                foreach (Vector2 point in new[] { Vector2.one * 2, new Vector2(size.x - 2, size.y - 2), new Vector2(size.x - 2, 2) })
                {
                    type.GetMethod("ShowCompared").Invoke(card, new object[] { candidate, point, camera, new[] { equipped }, 1 });
                    var rect = (RectTransform)root.transform.GetChild(0);
                    var corners = new Vector3[4];
                    rect.GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corner);
                        Check(screen.x >= 0 && screen.x <= size.x && screen.y >= 0 && screen.y <= size.y, "delta bounds " + size);
                    }
                    var stats = rect.Find("ComparisonStats").GetComponent<UnityEngine.UI.Text>();
                    Check(stats.preferredHeight <= stats.rectTransform.rect.height + 1, "delta text fits " + size);
                }
                foreach (Transform child in root.GetComponentsInChildren<Transform>()) child.gameObject.layer = 5;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Artifacts/tooltip-stats");
                File.WriteAllBytes($"Artifacts/tooltip-stats/delta-{suffix}-{size.x}.png", pixels.EncodeToPNG());
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraRoot);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            checks++;
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(code);
        }
    }
}
