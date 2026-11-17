using Nomad.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nomad.Village.Editor
{
    // PolygonFantasyKingdom 프리팹으로 걸어다닐 수 있는 마을 씬을 생성합니다.
    internal static class VillageAuthoring
    {
        internal const string ScenePath = "Assets/Nomad/Village/Village.unity";
        private const string KingdomRoot = "Assets/ThirdParty/Synty_Kingdom/PolygonFantasyKingdom/Prefabs";

        // 마을 울타리 반경(정사각형 반 변)과 남쪽 필드 입구 폭
        private const float Half = 34f;
        private const float GateHalfWidth = 4f;

        [MenuItem("Nomad/Village/Create Village")]
        public static void Create()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Transform root = new GameObject("Village").transform;

            CreateGround(root);
            CreatePlaza(root);
            CreateBuildings(root);
            CreateFence(root);
            CreateFieldEntrance(root);
            CreateNature(root);

            CreatePlayer(new Vector3(0f, 0.1f, -14f));

            Light sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void CreateGround(Transform root)
        {
            // 보이지 않는 바닥 충돌체로 이동 가능 영역을 보장합니다.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Ground Collider";
            floor.transform.SetParent(root, false);
            floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(Half * 2f + 40f, 1f, Half * 2f + 40f);
            Object.DestroyImmediate(floor.GetComponent<MeshRenderer>());

            // 잔디 바닥 조각을 크기에 맞춰 타일링합니다.
            Transform tiles = Group(root, "Ground");
            GameObject tile = Load("SM_Env_Ground_Flat_01");
            Vector3 size = MeasureSize(tile);
            float extent = Half + 20f;

            for (float x = -extent; x <= extent; x += size.x)
            {
                for (float z = -extent; z <= extent; z += size.z)
                    Place(tile, tiles, new Vector3(x, 0f, z), 0f, false, false);
            }
        }

        private static void CreatePlaza(Transform root)
        {
            Transform plaza = Group(root, "Plaza");

            // 광장 중앙의 큰 나무와 포장 바닥
            Place(Load("SM_Env_Big_Tree_Base_Paving_01"), plaza, Vector3.zero, 0f, true, false);
            Place(Load("SM_Env_Big_Tree_01"), plaza, Vector3.zero, 0f, true, false);

            // 시장(거래 거점): 광장 둘레에 노점을 둥글게 배치
            Transform market = Group(plaza, "Market");
            // 남쪽(입구 방향, 270도)은 비워 동선을 확보합니다.
            float[] angles = { -30f, 15f, 60f, 120f, 165f, 210f };

            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 position = Polar(angles[i], 10f);
                Place(Load("SM_Prop_Market_Stall_0" + (i + 1)), market, position, FaceCenter(position), true, true);
            }

            // 광장에서 남쪽 입구까지 돌길
            Transform path = Group(plaza, "Path To Gate");
            GameObject brick = Load("SM_Env_Path_Brick_Group_01");
            float step = Mathf.Max(2f, MeasureSize(brick).z);

            for (float z = -6f; z > -Half - 2f; z -= step)
                Place(brick, path, new Vector3(0f, 0.01f, z), 0f, false, false);

            // 소품: 상자·통 더미
            Place(Load("SM_Prop_Barrel_Stack_01"), plaza, new Vector3(6f, 0f, -6f), 20f, true, true);
            Place(Load("SM_Prop_Crate_Stack_01"), plaza, new Vector3(-6f, 0f, -6f), -15f, true, true);
            Place(Load("SM_Prop_Hay_Cart_01"), plaza, new Vector3(-13f, 0f, -14f), 35f, true, true);
        }

        private static void CreateBuildings(Transform root)
        {
            Transform buildings = Group(root, "Buildings");

            // 각도는 동쪽(+X)=0도, 북쪽(+Z)=90도 기준이며 남쪽 270도 부근은 입구 길로 비웁니다.
            AddBuilding(buildings, "Blacksmith (Maintenance)", "SM_Bld_Preset_Blacksmith_01_Optimized", 0f, 23f);
            AddBuilding(buildings, "House 01", "SM_Bld_Preset_House_01_A_Optimized", 45f, 24f);
            AddBuilding(buildings, "Tavern (Inn/Storage)", "SM_Bld_Preset_Tavern_01_Optimized", 90f, 23f);
            AddBuilding(buildings, "Church (Recovery)", "SM_Bld_Preset_Church_01_A_Optimized", 135f, 24f);
            AddBuilding(buildings, "Stables", "SM_Bld_Preset_Stables_01_Optimized", 180f, 23f);
            AddBuilding(buildings, "House 03", "SM_Bld_Preset_House_03_Optimized", 215f, 24f);
            AddBuilding(buildings, "House 07", "SM_Bld_Preset_House_07_Optimized", 250f, 25f);
            AddBuilding(buildings, "House 05", "SM_Bld_Preset_House_05_Optimized", 290f, 25f);
            AddBuilding(buildings, "House 08", "SM_Bld_Preset_House_08_Optimized", 325f, 24f);
        }

        private static void AddBuilding(Transform parent, string label, string prefab, float angle, float radius)
        {
            Vector3 position = Polar(angle, radius);
            GameObject building = Place(Load(prefab), parent, position, FaceCenter(position), true, true);
            building.name = label;
        }

        private static void CreateFence(Transform root)
        {
            Transform fence = Group(root, "Fence");
            GameObject piece = Load("SM_Prop_Fence_01");
            float length = Mathf.Max(1f, MeasureSize(piece).x);

            for (float t = -Half + length * 0.5f; t < Half; t += length)
            {
                Place(piece, fence, new Vector3(t, 0f, Half), 0f, true, false);
                Place(piece, fence, new Vector3(-Half, 0f, t), 90f, true, false);
                Place(piece, fence, new Vector3(Half, 0f, t), 90f, true, false);

                // 남쪽 울타리는 필드 입구 폭만큼 비웁니다.
                if (Mathf.Abs(t) > GateHalfWidth + length * 0.5f)
                    Place(piece, fence, new Vector3(t, 0f, -Half), 0f, true, false);
            }
        }

        private static void CreateFieldEntrance(Transform root)
        {
            Transform entrance = Group(root, "Field Entrance");
            entrance.localPosition = new Vector3(0f, 0f, -Half);

            Place(Load("SM_Prop_Fence_Gate_01"), entrance, new Vector3(-GateHalfWidth - 0.5f, 0f, 0f), 90f, true, false);
            Place(Load("SM_Prop_Direction_Sign_01"), entrance, new Vector3(GateHalfWidth + 1.5f, 0f, 2f), 180f, true, false);

            // 장면 전환은 범위 밖이므로 위치만 표시하는 트리거를 둡니다.
            BoxCollider trigger = new GameObject("Field Exit Trigger").AddComponent<BoxCollider>();
            trigger.transform.SetParent(entrance, false);
            trigger.transform.localPosition = new Vector3(0f, 1f, -1.5f);
            trigger.size = new Vector3(GateHalfWidth * 2f, 2f, 1f);
            trigger.isTrigger = true;
        }

        private static void CreateNature(Transform root)
        {
            Transform nature = Group(root, "Nature");
            string[] trees = { "SM_Env_Tree_Large_01", "SM_Env_Tree_Large_02", "SM_Env_Tree_Round_01", "SM_Env_Tree_Pine_Cluster_01" };
            Random.State saved = Random.state;
            Random.InitState(80);

            // 울타리 바깥에 나무를 둘러 마을 경계를 표현합니다.
            for (int i = 0; i < 48; i++)
            {
                float angle = i * 7.5f + Random.Range(-3f, 3f);
                Vector3 dir = Polar(angle, 1f);
                float scale = Half / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.z));
                Vector3 position = dir * (scale + Random.Range(4f, 12f));

                // 남쪽 입구 앞 길은 비웁니다.
                if (position.z < 0f && Mathf.Abs(position.x) < GateHalfWidth + 4f)
                    continue;

                Place(Load(trees[i % trees.Length]), nature, position, Random.Range(0f, 360f), true, false);
            }

            // 마을 안쪽 덤불·꽃은 충돌 없이 장식만 합니다.
            for (int i = 0; i < 24; i++)
            {
                Vector3 position = Polar(Random.Range(0f, 360f), Random.Range(14f, Half - 3f));
                if (Mathf.Abs(position.x) < 3f && position.z < 0f)
                    continue;

                string name = i % 2 == 0 ? "SM_Env_Bush_0" + (1 + i % 5) : "SM_Env_Flowers_0" + (1 + i % 9);
                Place(Load(name), nature, position, Random.Range(0f, 360f), false, false);
            }

            Random.state = saved;
        }

        private static void CreatePlayer(Vector3 position)
        {
            Transform player = new GameObject("Player").transform;
            player.position = position;
            CharacterController controller = player.gameObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = Vector3.up * 0.9f;
            controller.stepOffset = 0.4f;
            controller.minMoveDistance = 0f;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Player Body";
            body.transform.SetParent(player, false);
            body.transform.localPosition = controller.center;
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            Object.DestroyImmediate(body.GetComponent<Collider>());

            Camera camera = new GameObject("Top Down Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            SetReference(camera.gameObject.AddComponent<TopDownCamera>(), "target", player);
            SetReference(player.gameObject.AddComponent<PlayerMovement>(), "viewCamera", camera);
        }

        // 프리팹을 배치하고, 렌더러 경계 중심이 목표 위치에 오도록 맞춥니다.
        private static GameObject Place(GameObject prefab, Transform parent, Vector3 position, float yaw, bool solid, bool center)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.SetPositionAndRotation(parent.TransformPoint(position), Quaternion.Euler(0f, yaw, 0f));

            if (center && TryGetBounds(instance, out Bounds bounds))
            {
                Vector3 offset = parent.TransformPoint(position) - bounds.center;
                offset.y = 0f;
                instance.transform.position += offset;
            }

            if (solid)
                AddColliders(instance);

            GameObjectUtility.SetStaticEditorFlags(instance, StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic);
            return instance;
        }

        // Synty 프리팹 대부분에 충돌체가 없어 메시 충돌체를 붙입니다.
        private static void AddColliders(GameObject instance)
        {
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null)
                    continue;

                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
        }

        private static Vector3 MeasureSize(GameObject prefab)
        {
            GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Vector3 size = TryGetBounds(probe, out Bounds bounds) ? bounds.size : Vector3.one;
            Object.DestroyImmediate(probe);
            return size;
        }

        private static bool TryGetBounds(GameObject instance, out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return false;

            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return true;
        }

        private static GameObject Load(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Prefab", new[] { KingdomRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == name)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            throw new System.InvalidOperationException("Kingdom 프리팹을 찾을 수 없습니다: " + name);
        }

        private static Transform Group(Transform parent, string name)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        // 위치에서 광장 중심을 바라보는 Y 회전값
        private static float FaceCenter(Vector3 position)
        {
            return Mathf.Atan2(-position.x, -position.z) * Mathf.Rad2Deg;
        }

        private static Vector3 Polar(float degrees, float radius)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius;
        }

        private static void SetReference(Object component, string property, Object value)
        {
            var serialized = new SerializedObject(component);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
