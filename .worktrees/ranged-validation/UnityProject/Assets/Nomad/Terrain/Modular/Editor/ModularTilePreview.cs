using System.Collections.Generic;
using Nomad.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nomad.Terrain.Editor
{
    [CustomEditor(typeof(ModularTerrainTile))]
    public sealed class ModularTerrainTileInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var tile = (ModularTerrainTile)target;
            var root = new VisualElement();
            var slider = new SliderInt("조합 번호", 0, tile.CombinationCount - 1);
            slider.SetValueWithoutNotify(tile.Combination);
            var summary = new Label();
            root.Add(slider);
            root.Add(summary);
            var fields = new List<EnumField>();
            string[] names = tile.RegionCount == 4
                ? new[] { "남서", "남동", "북동", "북서" }
                : new[] { "서쪽 막힌 영역", "남동", "북동" };
            for (int index = 0, place = 1; index < tile.RegionCount; index++, place *= 3)
            {
                int region = index;
                int weight = place;
                var field = new EnumField(names[index], tile.GetState(index));
                field.RegisterValueChangedCallback(evt =>
                    slider.value = tile.Combination + ((int)(TerrainFloorState)evt.newValue - (int)tile.GetState(region)) * weight);
                fields.Add(field);
                root.Add(field);
            }
            root.Add(new Button(() => ModularTilePreview.Open(tile.RegionCount == 4 ? "ModularCross" : "ModularTee", tile.Combination))
                { text = "이 조합으로 플레이 검토 장면 열기" });

            void Refresh()
            {
                slider.SetValueWithoutNotify(tile.Combination);
                summary.text = $"{tile.CombinationCount}개 조합 · None=없음 / Low=0m / High=2m";
                for (int index = 0; index < tile.RegionCount; index++)
                    fields[index].SetValueWithoutNotify(tile.GetState(index));
            }

            slider.RegisterValueChangedCallback(evt =>
            {
                Undo.RegisterFullObjectHierarchyUndo(tile.gameObject, "Change floor combination");
                tile.ApplyCombination(evt.newValue);
                EditorUtility.SetDirty(tile);
                EditorUtility.SetDirty(tile.GetComponent<TerrainPiece>());
                foreach (Transform child in tile.GetComponentsInChildren<Transform>(true))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);

                PrefabUtility.RecordPrefabInstancePropertyModifications(tile);
                PrefabUtility.RecordPrefabInstancePropertyModifications(tile.GetComponent<TerrainPiece>());
                if (tile.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(tile.gameObject.scene);
                Refresh();
            });

            void UndoRedo() { if (tile != null) Refresh(); }
            root.RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += UndoRedo);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= UndoRedo);
            Refresh();
            return root;
        }
    }

    public static class ModularTilePreview
    {
        [MenuItem("Nomad/Terrain/Modular/Open All 108 Combinations")]
        public static void Gallery()
        {
            if (!CanOpen()) return;
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            int slot = 0;
            foreach (string name in new[] { "ModularCross", "ModularTee" })
            {
                GameObject prefab = Load(name);
                int count = prefab.GetComponent<ModularTerrainTile>().CombinationCount;
                for (int combination = 0; combination < count; combination++, slot++)
                {
                    GameObject tile = Object.Instantiate(prefab);
                    tile.name = name;
                    tile.transform.position = new Vector3(slot % 12 * 14f, 0f, slot / 12 * 14f);
                    tile.GetComponent<ModularTerrainTile>().ApplyCombination(combination);
                    var label = new GameObject("CombinationLabel").AddComponent<TextMesh>();
                    label.text = $"{name} {combination}";
                    label.characterSize = 0.4f;
                    label.fontSize = 32;
                    label.transform.position = tile.transform.position + new Vector3(-5f, 0.1f, -6f);
                    label.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                }
            }

            Camera.main.transform.position = new Vector3(77f, 150f, -35f);
            Camera.main.transform.LookAt(new Vector3(77f, 0f, 56f));
            Camera.main.farClipPlane = 400f;
            SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(77f, 0f, 56f), new Vector3(170f, 10f, 130f)), false);
        }

        [MenuItem("Nomad/Terrain/Modular/Open Cross Playground")]
        public static void Cross() => Open("ModularCross", 40);

        [MenuItem("Nomad/Terrain/Modular/Open Tee Playground")]
        public static void Tee() => Open("ModularTee", 13);

        [MenuItem("Nomad/Terrain/Modular/Open Wide Stairs Playground")]
        public static void Wide() => Open("WideStairs", 0);

        public static void Open(string name, int combination)
        {
            if (!CanOpen()) return;
            GameObject prefab = Load(name);
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            GameObject tile = Object.Instantiate(prefab);
            tile.name = name;
            if (tile.TryGetComponent(out ModularTerrainTile modular))
                modular.ApplyCombination(combination);

            TerrainPiece piece = tile.GetComponent<TerrainPiece>();
            foreach (TerrainConnection port in piece.Connections)
                AddNeighbour(port);

            Camera camera = Camera.main;
            var player = new GameObject("ReviewPlayer_WASD_Space_Shift");
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = Vector3.up * 0.9f;
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.02f;
            TerrainConnection start = piece.Connections[0];
            player.transform.position = start.Position + start.Outward + Vector3.up * 0.03f;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(player.transform, false);
            body.transform.localPosition = Vector3.up * 0.9f;
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            Set(player.AddComponent<PlayerMovement>(), "viewCamera", camera);
            Set(camera.gameObject.AddComponent<TopDownCamera>(), "target", player.transform);
            camera.transform.position = player.transform.position + new Vector3(0f, 12f, -8f);
            camera.transform.LookAt(player.transform.position);
            Selection.activeGameObject = tile;
        }

        public static GameObject AddNeighbour(TerrainConnection port)
        {
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "MatchingFloor";
            pad.transform.SetPositionAndRotation(port.Position + port.Outward * 1.5f - Vector3.up * 0.125f,
                Quaternion.LookRotation(port.Outward));
            pad.transform.localScale = new Vector3(2f, 0.25f, 3f);
            return pad;
        }

        private static bool CanOpen() => !EditorApplication.isPlaying
            && (Application.isBatchMode || EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo());

        private static GameObject Load(string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModularTileAuthoring.Output}/{name}.prefab");
            if (prefab == null)
                throw new System.InvalidOperationException("먼저 Nomad > Terrain > Modular > Create Shared Tile Prefabs를 실행해 주세요.");
            return prefab;
        }

        private static void Set(Object target, string field, Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
