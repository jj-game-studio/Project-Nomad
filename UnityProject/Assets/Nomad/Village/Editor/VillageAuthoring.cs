using Nomad.Game;
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

        // 메인 게임 Player가 처음 서는 위치입니다. Player·카메라는 GameManager의 Player 모듈이 만듭니다.
        internal static readonly Vector3 StartPosition = new Vector3(0f, 0.1f, -14f);

        [MenuItem("Nomad/Village/Create Village")]
        public static void Create()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Transform root = new GameObject("Village").transform;
            Random.InitState(80);
            stairRamps = 0;

            CreateGround(root);
            CreatePlaza(root);
            CreateBuildings(root);
            CreateFence(root);
            CreateFieldEntrance(root);
            CreateNature(root);

            CreateEntrance("Player Entrance", null, StartPosition, Quaternion.identity);
            Nomad.Shop.Editor.VillageShopAuthoring.ApplyToScene(scene);

            Light sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Village] 계단 경사로 {stairRamps}개 생성");
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

            // Kingdom 데모처럼 흙 바닥을 먼저 깔고, 그 위에 잔디 조각을 무작위 회전·스케일로 겹칩니다.
            // 조각마다 높이를 조금씩 달리해 겹친 면이 깜빡이지 않게 합니다.
            Transform ground = Group(root, "Ground");
            float extent = Half + 20f;

            GameObject dirt = Load("SM_Env_Ground_Flat_Dirt_01");
            Vector3 dirtSize = MeasureSize(dirt);
            for (float x = -extent; x <= extent; x += dirtSize.x * 0.85f)
            {
                for (float z = -extent; z <= extent; z += dirtSize.z * 0.85f)
                    Place(dirt, ground, new Vector3(x, -0.05f, z), Random.Range(0, 4) * 90f, false, false);
            }

            GameObject[] grass = { Load("SM_Env_Ground_Flat_01"), Load("SM_Env_Ground_Flat_02") };
            float spacing = MeasureSize(grass[0]).x * 0.5f;
            for (float x = -extent; x <= extent; x += spacing)
            {
                for (float z = -extent; z <= extent; z += spacing)
                {
                    Vector3 position = new Vector3(x + Random.Range(-0.3f, 0.3f) * spacing, Random.Range(0f, 0.03f), z + Random.Range(-0.3f, 0.3f) * spacing);

                    // 흙길·광장 자리는 비워 흙 바닥이 드러나게 합니다.
                    if (IsDirtArea(position, spacing * 0.6f))
                        continue;

                    GameObject piece = Place(grass[Random.Range(0, grass.Length)], ground, position, Random.Range(0f, 360f), false, false);
                    piece.transform.localScale = new Vector3(Random.Range(1f, 1.4f), 1f, Random.Range(1f, 1.4f));
                }
            }

            // 울타리 밖은 솟은 잔디 언덕을 섞어 굴곡을 줍니다(장식, 충돌 없음).
            GameObject[] mounds = { Load("SM_Env_Ground_Raised_01"), Load("SM_Env_Ground_Raised_02") };
            for (int i = 0; i < 40; i++)
            {
                Vector3 dir = Polar(Random.Range(0f, 360f), 1f);
                float edge = Half / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.z));
                Vector3 position = dir * (edge + Random.Range(3f, 16f));
                if (IsDirtArea(position))
                    continue;

                GameObject mound = Place(mounds[i % 2], ground, position, Random.Range(0f, 360f), false, false);
                mound.transform.localScale = new Vector3(Random.Range(0.6f, 1.6f), Random.Range(0.4f, 1f), Random.Range(0.6f, 1.6f));
            }
        }

        // 남쪽 입구로 이어지는 구불구불한 흙길과 광장 포장 주변
        // margin은 잔디 조각 반경만큼 비워 실제 흙이 드러나게 하는 여유입니다.
        private static bool IsDirtArea(Vector3 position, float margin = 0f)
        {
            if (position.magnitude < 6.5f + margin)
                return true;

            float roadCenter = Mathf.Sin(position.z * 0.2f) * 1.2f;
            return position.z < -4f && position.z > -Half - 25f && Mathf.Abs(position.x - roadCenter) < 1.6f + margin;
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

            // 흙길 가장자리에 돌 조각을 드문드문 놓아 길을 강조합니다.
            Transform path = Group(plaza, "Path Stones");
            GameObject brick = Load("SM_Env_Path_Brick_Group_01");

            for (float z = -8f; z > -Half; z -= Random.Range(3f, 5f))
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                float x = Mathf.Sin(z * 0.2f) * 1.2f + side * Random.Range(1.6f, 2.4f);
                Place(brick, path, new Vector3(x, 0.02f, z), Random.Range(0f, 360f), false, false);
            }

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
            AddBuilding(buildings, "House 03", "SM_Bld_Preset_House_03_Optimized", 212f, 26f);
            AddBuilding(buildings, "House 07", "SM_Bld_Preset_House_07_Optimized", 250f, 27f);
            AddBuilding(buildings, "House 05", "SM_Bld_Preset_House_05_Optimized", 290f, 27f);
            AddBuilding(buildings, "House 08", "SM_Bld_Preset_House_08_Optimized", 328f, 26f);
        }

        private static void AddBuilding(Transform parent, string label, string prefab, float angle, float radius)
        {
            Vector3 position = Polar(angle, radius);
            GameObject building = Place(Load(prefab), parent, position, FaceCenter(position), true, true);
            building.name = label;
            AddStairRamps(building);
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

            BoxCollider trigger = new GameObject("Field Exit Trigger").AddComponent<BoxCollider>();
            trigger.transform.SetParent(entrance, false);
            trigger.transform.localPosition = new Vector3(0f, 1f, -1.5f);
            trigger.size = new Vector3(GateHalfWidth * 2f, 2f, 1f);
            trigger.isTrigger = true;
            LinkFieldExit(trigger.transform);
        }

        // 출구 트리거는 Field로 전환하고, 필드에서 돌아오면 문 안쪽에 마을을 바라보며 섭니다.
        internal static void LinkFieldExit(Transform trigger)
        {
            if (trigger.GetComponent<RegionExit>() == null)
                SetString(trigger.gameObject.AddComponent<RegionExit>(), "targetRegion", "Field");

            Transform gate = trigger.parent;
            if (gate.Find("Field Return Entrance") != null)
                return;

            RegionEntrance back = CreateEntrance("Field Return Entrance", "Field", Vector3.zero, Quaternion.identity);
            back.transform.SetParent(gate, false);
            back.transform.localPosition = new Vector3(0f, 0.1f, 3f);
        }

        internal static RegionEntrance CreateEntrance(string name, string fromRegion, Vector3 position, Quaternion rotation)
        {
            var entrance = new GameObject(name).AddComponent<RegionEntrance>();
            entrance.transform.SetPositionAndRotation(position, rotation);
            if (!string.IsNullOrEmpty(fromRegion))
                SetString(entrance, "fromRegion", fromRegion);
            return entrance;
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

        private static int stairRamps;
        private const float Cell = 0.1f;

        // 프리셋 건물은 계단이 건물 메시에 합쳐져 있고 단 높이(0.45~0.65m)가 stepOffset보다 높습니다.
        // 건물 주변 높이맵에서 계단을 찾아 평면을 맞추고, 그 위에 보이지 않는 경사로를 깝니다.
        private static void AddStairRamps(GameObject building)
        {
            Physics.SyncTransforms();
            TryGetBounds(building, out Bounds bounds);
            Vector3 min = bounds.min - new Vector3(2f, 0f, 2f);
            int width = Mathf.CeilToInt((bounds.size.x + 4f) / Cell);
            int depth = Mathf.CeilToInt((bounds.size.z + 4f) / Cell);
            float[,] height = new float[width, depth];

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    // 처마에 막히지 않도록 낮은 곳에서 아래로 쏩니다.
                    Vector3 origin = new Vector3(min.x + x * Cell, 2f, min.z + z * Cell);
                    height[x, z] = Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 3f) ? hit.point.y : -1f;
                }
            }

            // 0.15~0.7m 단차가 있는 칸 주변 0.3m까지를 계단 후보로 표시합니다.
            bool[,] step = new bool[width, depth];
            for (int x = 0; x < width - 1; x++)
            {
                for (int z = 0; z < depth - 1; z++)
                {
                    float rise = Mathf.Max(Mathf.Abs(height[x + 1, z] - height[x, z]), Mathf.Abs(height[x, z + 1] - height[x, z]));
                    if (height[x, z] < 0f || rise < 0.15f || rise > 0.7f)
                        continue;

                    for (int dx = -3; dx <= 3; dx++)
                        for (int dz = -3; dz <= 3; dz++)
                            if (x + dx >= 0 && x + dx < width && z + dz >= 0 && z + dz < depth && height[x + dx, z + dz] >= 0f && height[x + dx, z + dz] < 1.9f)
                                step[x + dx, z + dz] = true;
                }
            }

            bool[,] visited = new bool[width, depth];
            var region = new System.Collections.Generic.List<Vector3>();
            var queue = new System.Collections.Generic.Queue<Vector2Int>();
            Vector2Int[] directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    if (!step[x, z] || visited[x, z])
                        continue;

                    // 높이차 0.7m 미만으로 이어진 칸을 하나의 계단으로 묶습니다.
                    region.Clear();
                    queue.Enqueue(new Vector2Int(x, z));
                    visited[x, z] = true;

                    while (queue.Count > 0)
                    {
                        Vector2Int c = queue.Dequeue();
                        region.Add(new Vector3(min.x + c.x * Cell, height[c.x, c.y], min.z + c.y * Cell));
                        foreach (Vector2Int d in directions)
                        {
                            Vector2Int n = c + d;
                            if (n.x < 0 || n.x >= width || n.y < 0 || n.y >= depth || visited[n.x, n.y] || !step[n.x, n.y])
                                continue;
                            if (Mathf.Abs(height[n.x, n.y] - height[c.x, c.y]) >= 0.7f)
                                continue;

                            visited[n.x, n.y] = true;
                            queue.Enqueue(n);
                        }
                    }

                    TryAddRamp(building.transform, region);
                }
            }
        }

        // 계단 칸들에 평면 h = c + gx*x + gz*z 를 맞춰 경사 방향을 구하고,
        // 계단 맨 위(착지점)를 기준으로 최대 35도 경사로를 바닥까지 늘려 만듭니다.
        private static void TryAddRamp(Transform parent, System.Collections.Generic.List<Vector3> cells)
        {
            if (cells.Count < 40 || cells.Count > 4000)
                return;

            float lowest = float.MaxValue, highest = float.MinValue;
            Vector3 mean = Vector3.zero;
            foreach (Vector3 c in cells)
            {
                mean += c;
                lowest = Mathf.Min(lowest, c.y);
                highest = Mathf.Max(highest, c.y);
            }
            mean /= cells.Count;

            // 바닥에서 시작하지 않는 영역(건물 안 바닥·가구)은 계단으로 보지 않습니다.
            float rise = highest - lowest;
            if (lowest > 0.45f || rise < 0.6f)
                return;

            // 최소제곱 기울기
            float sxx = 0f, sxz = 0f, szz = 0f, sxh = 0f, szh = 0f;
            foreach (Vector3 c in cells)
            {
                float dx = c.x - mean.x, dz = c.z - mean.z, dh = c.y - mean.y;
                sxx += dx * dx; sxz += dx * dz; szz += dz * dz; sxh += dx * dh; szh += dz * dh;
            }

            float det = sxx * szz - sxz * sxz;
            if (Mathf.Abs(det) < 1e-5f)
                return;

            Vector2 gradient = new Vector2((sxh * szz - szh * sxz) / det, (szh * sxx - sxh * sxz) / det);
            float slope = gradient.magnitude;
            if (slope < 0.3f || slope > 1.6f)
                return;

            Vector3 forward = new Vector3(gradient.x, 0f, gradient.y) / slope;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            float sMax = float.MinValue, tMin = float.MaxValue, tMax = float.MinValue;
            foreach (Vector3 c in cells)
            {
                Vector3 offset = c - mean;
                offset.y = 0f;
                sMax = Mathf.Max(sMax, Vector3.Dot(offset, forward));
                tMin = Mathf.Min(tMin, Vector3.Dot(offset, side));
                tMax = Mathf.Max(tMax, Vector3.Dot(offset, side));
            }

            // 폭이 넓은 영역은 계단이 아니라 기단 턱입니다.
            if (tMax - tMin > 4f)
                return;

            float gentle = Mathf.Min(slope, 0.7f);
            float run = rise / gentle;
            sMax += 0.3f;
            const float thickness = 0.2f;
            Vector3 center = mean + forward * (sMax - run * 0.5f) + side * ((tMin + tMax) * 0.5f);
            center.y = lowest + rise * 0.5f;

            // 같은 계단이 여러 영역으로 잡히면 첫 경사로만 남깁니다.
            foreach (Transform child in parent)
            {
                if (child.name == "Stair Ramp" && Vector3.Distance(child.position, center) < 1.5f)
                    return;
            }

            Quaternion rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(-Mathf.Atan(gentle) * Mathf.Rad2Deg, 0f, 0f);
            BoxCollider ramp = new GameObject("Stair Ramp").AddComponent<BoxCollider>();
            ramp.transform.SetParent(parent, true);
            ramp.transform.SetPositionAndRotation(center - rotation * Vector3.up * (thickness * 0.5f), rotation);
            ramp.size = new Vector3(tMax - tMin + 0.2f, thickness, run * Mathf.Sqrt(1f + gentle * gentle));
            stairRamps++;
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

        private static void SetString(Object component, string property, string value)
        {
            var serialized = new SerializedObject(component);
            serialized.FindProperty(property).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
