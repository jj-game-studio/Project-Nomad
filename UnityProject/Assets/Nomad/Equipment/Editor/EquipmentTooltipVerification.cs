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
                .First(item => item.ItemId == "demo-armor");
            var worn = UnityEngine.Object.Instantiate(armor);
            var other = UnityEngine.Object.Instantiate(armor);
            Set(worn, "displayName", "갑옷 B");
            Set(other, "displayName", "갑옷 A");
            Set(other, "defense", 30);
            var add = typeof(PlayerInventory).GetMethod("AddToSlotForTransfer", BindingFlags.Instance | BindingFlags.NonPublic);
            add.Invoke(bag, new object[] { empty[0], worn, 1 });
            add.Invoke(bag, new object[] { empty[1], other, 1 });
            player.TryEquip(empty[0], EquipmentSlot.Body);
            Debug.Log("비교 샘플 준비: 갑옷 B(방어력20) 장착, 가방 갑옷 A(방어력30)에 커서를 올리세요. 원본 에셋은 변경하지 않습니다.");
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
                        Check(Card(0).gameObject.activeInHierarchy && Card(1).gameObject.activeInHierarchy,
                            "candidate and equipped comparison visible");
                        Check(Title(0).Contains("갑옷 A") && Title(1).Contains("장착 중 · 신체") && Title(1).Contains("갑옷 B"),
                            "candidate and equipped heading");
                        Check(Body(0).Contains("방어력 30") && Body(1).Contains("방어력 20"), "separate actual stats");
                        bool opensLeft = OpensLeft(2);
                        Check((Card(1).position.x - Card(0).position.x) * (opensLeft ? -1 : 1) > 0,
                            "equipped card extends away from cursor");
                        Check(!canvas.Find("InventoryTooltip").gameObject.activeSelf, "single bag tooltip replaced without overlap");
                        Set(equipped, "defense", 21);
                        break;
                    case 2:
                        Check(Body(1).Contains("방어력 21"), "same pointer updates equipped stats");
                        RenderGroups();
                        Pointer(Gear(EquipmentSlot.Body));
                        break;
                    case 3:
                        Check(Card(0).gameObject.activeInHierarchy && Title(0).Contains("갑옷 B") && !Card(1).gameObject.activeSelf,
                            "equipped slot hover shows single tooltip");
                        Pointer(Gear(EquipmentSlot.Head));
                        break;
                    case 4:
                        Check(!Card(0).gameObject.activeSelf && !Card(1).gameObject.activeSelf, "empty equipment slot hides");
                        Pointer(Gear(EquipmentSlot.Weapon1));
                        break;
                    case 5:
                        Check(Title(0).Contains("검") && Body(0).Contains("공격력 25") && !Card(1).gameObject.activeSelf,
                            "equipped weapon hover supported without comparison");
                        Check(equipment.TryUnequip(EquipmentSlot.Weapon1), "weapon to bag fixture");
                        Pointer(Bag(Find("demo-sword")));
                        break;
                    case 6:
                        Check(!Card(0).gameObject.activeSelf && canvas.Find("InventoryTooltip").gameObject.activeSelf,
                            "bag weapon uses single tooltip without comparison");
                        Pointer(Bag(19));
                        break;
                    case 7:
                        Check(Card(1).gameObject.activeSelf, "comparison restored");
                        Pointer(Bag(19), true);
                        break;
                    case 8:
                        Check(!Card(0).gameObject.activeSelf && !Card(1).gameObject.activeSelf, "press hides all comparisons");
                        Keys(Key.Escape);
                        Pointer(Vector2.zero);
                        break;
                    case 9:
                        Keys();
                        Pointer(Bag(19));
                        break;
                    case 10:
                        Check(Card(1).gameObject.activeSelf, "hover after cancel");
                        panel.SendMessage("OnApplicationFocus", false);
                        bagView.SendMessage("OnApplicationFocus", false);
                        Check(!Card(0).gameObject.activeSelf && !Card(1).gameObject.activeSelf, "focus loss immediately hides");
                        break;
                    case 11:
                        Check(!Card(0).gameObject.activeSelf, "focus remains hidden");
                        panel.SendMessage("OnApplicationFocus", true);
                        bagView.SendMessage("OnApplicationFocus", true);
                        break;
                    case 12:
                        Check(Card(1).gameObject.activeSelf, "focus return restores");
                        Check(equipment.TryUnequip(EquipmentSlot.Body), "armor unequip fixture");
                        break;
                    case 13:
                        Check(!Card(1).gameObject.activeSelf && canvas.Find("InventoryTooltip").gameObject.activeSelf,
                            "no equipped match uses only candidate");
                        Check(equipment.TryEquip(19, EquipmentSlot.Body), "equip candidate with pointer stationary");
                        break;
                    case 14:
                        Check(!Card(0).gameObject.activeSelf && !canvas.Find("InventoryTooltip").gameObject.activeSelf,
                            "removed candidate hides without pointer movement");
                        SetupTotems();
                        Pointer(Bag(19));
                        break;
                    case 15:
                        Check(Card(0).gameObject.activeSelf && Card(1).gameObject.activeSelf && Card(2).gameObject.activeSelf,
                            "two totems compare both equipped slots");
                        Check(Title(1).Contains("토템 1") && Title(2).Contains("토템 2"), "totem slot order");
                        bool totemsLeft = OpensLeft(3);
                        Check((Card(2).position.x - Card(1).position.x) * (totemsLeft ? -1 : 1) > 0,
                            "totems extend outward in display direction");
                        RenderGroups();
                        Keys(Key.Tab);
                        break;
                    case 16:
                        Check(!Card(0).gameObject.activeInHierarchy && !Card(1).gameObject.activeInHierarchy &&
                            !Card(2).gameObject.activeInHierarchy, "closing hides whole group");
                        int used = inventory.UsedSlots;
                        AddReviewSamples();
                        Check(inventory.UsedSlots == used + 2, "review sample menu preserves old gear and adds two samples");
                        Check(equipment.GetItem(EquipmentSlot.Body).DisplayName == "갑옷 B" &&
                            equipment.GetItem(EquipmentSlot.Body).Defense == 20, "review menu equips armor B");
                        Check(Enumerable.Range(0, PlayerInventory.Capacity).Any(index => inventory.GetSlot(index).Item?.DisplayName == "갑옷 A" &&
                            inventory.GetSlot(index).Item.Defense == 30), "review menu provides armor A in bag");
                        Debug.Log($"EQUIPMENT_TOOLTIP_PASS: {checks} checks, real hover, comparison, lifecycle, 3 resolutions");
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
            Check(Enumerable.Range(0, PlayerInventory.Capacity).Count(index => inventory.GetSlot(index).Item?.ItemId == "demo-armor") == 2, "demo starts with two armors for comparison");
            Check(Enumerable.Range(0, PlayerInventory.Capacity).Count(index => inventory.GetSlot(index).Item?.ItemId == "demo-helmet") == 2, "demo starts with two helmets for comparison");
            bagView = UnityEngine.Object.FindAnyObjectByType<InventoryPanel>();
            canvas = (RectTransform)bagView.GetComponent<Canvas>().transform;
            equipped = UnityEngine.Object.Instantiate(inventory.GetSlot(Find("demo-armor")).Item);
            Set(equipped, "displayName", "갑옷 B");
            candidate = UnityEngine.Object.Instantiate(equipped);
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

        private static void SetupTotems()
        {
            candidate = UnityEngine.Object.Instantiate(inventory.GetSlot(Find("demo-totem")).Item);
            Set(candidate, "displayName", "비교용 토템");
            Transfer(19, candidate);
            Transfer(18, candidate);
            Check(equipment.TryEquip(18, EquipmentSlot.Totem1), "first totem fixture");
            Transfer(18, candidate);
            Check(equipment.TryEquip(18, EquipmentSlot.Totem2), "second totem fixture");
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

        private static bool OpensLeft(int count)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, Bag(19), null, out Vector2 pointer);
            float width = Enumerable.Range(0, count).Sum(index => Card(index).sizeDelta.x) + 8 * (count - 1);
            return pointer.x + 18 + width > canvas.rect.xMax - 8;
        }

        private static void RenderGroups()
        {
            // 실제 호버에서 검사한 카드들을 표시 전용 복제본으로 렌더합니다.
            Type type = typeof(InventoryPanel).Assembly.GetType("Nomad.Inventory.InventoryTooltip");
            int count = Card(2).gameObject.activeSelf ? 3 : 2;
            foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1080) })
            {
                var root = new GameObject("Comparison Render", typeof(RectTransform), typeof(Canvas));
                var renderCanvas = root.GetComponent<Canvas>();
                var cameraRoot = new GameObject("Comparison Camera", typeof(Camera));
                Camera camera = cameraRoot.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(31, 36, 39, 255);
                camera.cullingMask = 1 << 5;
                var target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = target;
                renderCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                renderCanvas.worldCamera = camera;
                renderCanvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                Array cards = Array.CreateInstance(type, count);
                float width = Mathf.Min(320, (size.x - 16 - 8 * (count - 1)) / count);
                for (int index = 0; index < count; index++)
                {
                    object card = Activator.CreateInstance(type, root.GetComponent<RectTransform>(),
                        Card(0).Find("Title").GetComponent<UnityEngine.UI.Text>().font, "Card" + index);
                    cards.SetValue(card, index);
                    ItemDefinition item = index == 0 || count == 3 ? candidate : equipped;
                    string heading = index == 0 ? null : count == 3 ? "장착 중 · 토템 " + index : "장착 중 · 신체";
                    type.GetMethod("ShowCard").Invoke(card, new object[] { item, 1, Vector2.one, camera, width, heading });
                }
                foreach (Vector2 point in new[] { Vector2.one * 2, new Vector2(size.x - 2, size.y - 2), new Vector2(size.x - 2, 2) })
                {
                    type.GetMethod("ArrangeGroup").Invoke(null, new object[] { cards, count, point, camera });
                    float direction = point.x > size.x / 2 ? -1 : 1;
                    for (int index = 1; index < count; index++)
                        Check((root.transform.GetChild(index).localPosition.x - root.transform.GetChild(index - 1).localPosition.x) * direction > 0,
                            "comparison follows cursor side " + size + " at " + point);
                    foreach (Transform child in root.transform)
                    {
                        var corners = new Vector3[4];
                        ((RectTransform)child).GetWorldCorners(corners);
                        foreach (Vector3 corner in corners)
                        {
                            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corner);
                            Check(screen.x >= 0 && screen.x <= size.x && screen.y >= 0 && screen.y <= size.y, "group bounds " + size);
                        }
                    }
                }
                foreach (Transform child in root.GetComponentsInChildren<Transform>()) child.gameObject.layer = 5;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Artifacts/tooltip-stats");
                File.WriteAllBytes($"Artifacts/tooltip-stats/comparison-{count}-{size.x}.png", pixels.EncodeToPNG());
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
