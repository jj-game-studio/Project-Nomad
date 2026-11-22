using System;
using System.Collections.Generic;
using Nomad.Terrain;
using UnityEngine;

namespace Nomad.Loot
{
    // 필드 Ready 결과의 상자 지점에만 파밍 상자를 seed 기반으로 배치합니다. 지점 선정은 FieldNavigation 담당입니다.
    [DisallowMultipleComponent]
    public sealed class ChestSpawner : MonoBehaviour
    {
        [Serializable]
        public struct LootEntry
        {
            public string ItemId;
            public int Min;
            public int Max;
            [Range(0f, 1f)] public float Chance;

            public LootEntry(string itemId, int min, int max, float chance)
            {
                ItemId = itemId;
                Min = min;
                Max = max;
                Chance = chance;
            }
        }

        // 임시 내용물 테이블입니다. 수량·확률은 Play 검토 후 사람이 조정합니다(#20 판단 2).
        [SerializeField] private LootEntry[] table =
        {
            new LootEntry("scrap", 4, 12, 1f),
            new LootEntry("field-knife", 1, 1, 0.35f),
            new LootEntry("field-vest", 1, 1, 0.2f),
        };

        [Tooltip("제공된 상자 지점 중 최소로 사용할 비율입니다. 나머지는 seed에 따라 랜덤입니다.")]
        [SerializeField, Range(0f, 1f)] private float minimumFill = 0.5f;

        private readonly List<LootChest> chests = new();
        private GameObject root;
        private Material bodyMaterial;
        private Material lidMaterial;
        private Material emptyMaterial;

        public IReadOnlyList<LootChest> Chests => chests;
        public string GenerationId { get; private set; }

        public IReadOnlyList<LootChest> Spawn(FieldGenerationResult field, bool fillAllPoints = false)
        {
            if (field == null || !field.Success)
            {
                Clear();
                return chests;
            }

            return Spawn(field.GenerationId, field.Seed, field.ChestPoints, fillAllPoints);
        }

        // 같은 세대에는 한 번만 배치하고, 지점이 없거나 부족하면 가능한 수만큼만 만듭니다.
        public IReadOnlyList<LootChest> Spawn(string generationId, int seed, IReadOnlyList<FieldSpawnPoint> points,
            bool fillAllPoints = false)
        {
            if (generationId == GenerationId && root != null)
                return chests;

            Clear();
            GenerationId = generationId;
            if (string.IsNullOrEmpty(generationId) || points == null || points.Count == 0)
                return chests;

            var random = new System.Random(unchecked(seed * 7919 + 20));
            int[] order = Shuffle(points.Count, random);
            int count = fillAllPoints ? points.Count
                : random.Next(Mathf.CeilToInt(points.Count * minimumFill), points.Count + 1);

            root = new GameObject("FieldChests");
            root.transform.SetParent(transform, false);
            for (int i = 0; i < count; i++)
            {
                FieldSpawnPoint point = points[order[i]];
                Quaternion rotation = point.Rotation * Quaternion.Euler(0f, random.Next(4) * 90f, 0f);
                LootChest chest = CreateChest($"{generationId}/{point.Id}", point.Position, rotation);
                chest.Initialize(chest.name, Roll(random));
                chests.Add(chest);
            }

            return chests;
        }

        public void Clear()
        {
            chests.Clear();
            GenerationId = null;
            if (root == null)
                return;

            // 같은 프레임의 상호작용 탐색에서도 이전 상자가 보이지 않도록 먼저 비활성화합니다.
            root.SetActive(false);
            if (Application.isPlaying) Destroy(root);
            else DestroyImmediate(root);
            root = null;
        }

        private void OnDestroy()
        {
            Clear();
            foreach (Material material in new[] { bodyMaterial, lidMaterial, emptyMaterial })
            {
                if (material != null) Destroy(material);
            }
        }

        private LootStack[] Roll(System.Random random)
        {
            var stacks = new List<LootStack>();
            foreach (LootEntry entry in table)
            {
                if (string.IsNullOrWhiteSpace(entry.ItemId) || entry.Max <= 0 || random.NextDouble() >= entry.Chance)
                    continue;

                int min = Mathf.Max(1, entry.Min);
                stacks.Add(new LootStack(entry.ItemId, random.Next(min, Mathf.Max(min, entry.Max) + 1)));
            }

            return stacks.ToArray();
        }

        private static int[] Shuffle(int length, System.Random random)
        {
            var order = new int[length];
            for (int i = 0; i < length; i++)
                order[i] = i;

            for (int i = length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            return order;
        }

        // 임시 외형: 1 × 0.8 × 0.7m 상자. 충돌체는 루트에만 두어 상태가 바뀌어도 유지합니다.
        private LootChest CreateChest(string id, Vector3 position, Quaternion rotation)
        {
            var chestObject = new GameObject(id);
            chestObject.transform.SetParent(root.transform, false);
            chestObject.transform.SetPositionAndRotation(position, rotation);

            var collider = chestObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.4f, 0f);
            collider.size = new Vector3(1f, 0.8f, 0.7f);

            var point = new GameObject("InteractionPoint").transform;
            point.SetParent(chestObject.transform, false);
            point.localPosition = collider.center;

            GameObject closed = CreateVisual(chestObject.transform, "Closed", bodyMaterial ??= CreateMaterial(new Color(0.55f, 0.34f, 0.16f)));
            AddBox(closed.transform, new Vector3(0f, 0.7f, 0f), new Vector3(1.02f, 0.2f, 0.72f), lidMaterial ??= CreateMaterial(new Color(0.85f, 0.62f, 0.2f)));

            // 열린 상태는 뚜껑을 뒤로 젖힌 모양, 빈 상태는 회색 몸체만 표시합니다.
            GameObject open = CreateVisual(chestObject.transform, "Open", bodyMaterial);
            AddBox(open.transform, new Vector3(0f, 0.85f, 0.42f), new Vector3(1.02f, 0.5f, 0.12f), lidMaterial);
            GameObject empty = CreateVisual(chestObject.transform, "Empty", emptyMaterial ??= CreateMaterial(new Color(0.35f, 0.35f, 0.35f)));

            var chest = chestObject.AddComponent<LootChest>();
            chest.ConfigureVisuals(point, closed, open, empty);
            return chest;
        }

        private static GameObject CreateVisual(Transform parent, string name, Material body)
        {
            var visual = new GameObject(name);
            visual.transform.SetParent(parent, false);
            AddBox(visual.transform, new Vector3(0f, 0.3f, 0f), new Vector3(1f, 0.6f, 0.7f), body);
            return visual;
        }

        private static void AddBox(Transform parent, Vector3 position, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material CreateMaterial(Color color)
        {
            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var material = new Material(probe.GetComponent<Renderer>().sharedMaterial) { color = color };
            DestroyImmediate(probe);
            return material;
        }
    }
}
