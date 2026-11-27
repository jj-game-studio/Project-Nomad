using System;
using System.Collections.Generic;
using System.Linq;
using Nomad.Game;
using Nomad.Inventory;
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

        // 티어별 가중치입니다. 판매 아이템과 무기·장비가 같은 표를 쓰며 상대값입니다(#120). 수치는 Play 검토 후 사람이 조정합니다.
        [Serializable]
        public struct TierWeight
        {
            public ItemTier Tier;
            [Min(0f)] public float Weight;

            public TierWeight(ItemTier tier, float weight)
            {
                Tier = tier;
                Weight = weight;
            }
        }

        [SerializeField] private TierWeight[] tierWeights =
        {
            new TierWeight(ItemTier.C, 50f),
            new TierWeight(ItemTier.B, 25f),
            new TierWeight(ItemTier.A, 13f),
            new TierWeight(ItemTier.S, 7f),
            new TierWeight(ItemTier.SS, 4f),
            new TierWeight(ItemTier.SSS, 1f),
        };

        [Tooltip("상자마다 넣는 판매용 아이템 개수 범위입니다. 하나씩 티어를 뽑아 같은 아이템은 합칩니다.")]
        [SerializeField, Min(0)] private int sellItemsMin = 2;
        [SerializeField, Min(0)] private int sellItemsMax = 5;

        [Tooltip("상자에 무기·장비가 1개 들어 있을 확률입니다.")]
        [SerializeField, Range(0f, 1f)] private float gearChance = 0.4f;

        // 고철·장비 외에 함께 나오는 소모품입니다.
        [SerializeField] private LootEntry[] extras =
        {
            new LootEntry("weapon-arrow", 5, 15, 0.3f),
        };

        [Tooltip("제공된 상자 지점 중 최소로 사용할 비율입니다. 나머지는 seed에 따라 랜덤입니다.")]
        [SerializeField, Range(0f, 1f)] private float minimumFill = 0.5f;

        // 출격 때 고른 필드 변형입니다(#194). null이면 위 기본값을 그대로 씁니다.
        private FieldPreset preset;

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

            preset = GameManager.Instance != null ? GameManager.Instance.SelectedField : null;
            var random = new System.Random(unchecked(seed * 7919 + 20));
            int[] order = Shuffle(points.Count, random).OrderBy(index => points[index].Id.StartsWith("building-") ? 0 : 1).ToArray();
            int count = fillAllPoints ? points.Count
                : random.Next(Mathf.CeilToInt(points.Count * minimumFill), points.Count + 1);

            count = Mathf.Max(count, points.Count(point => point.Id.StartsWith("building-")));
            var selected = new List<FieldSpawnPoint>();
            foreach (int index in order)
            {
                FieldSpawnPoint point = points[index];
                if (selected.Any(other => Vector2.Distance(new Vector2(point.Position.x, point.Position.z),
                    new Vector2(other.Position.x, other.Position.z)) < 2.5f)) continue;
                selected.Add(point);
                if (selected.Count >= count) break;
            }
            count = selected.Count;

            int bonusIndex = preset != null && count > 0 ? random.Next(count) : -1;
            root = new GameObject("FieldChests");
            root.transform.SetParent(transform, false);
            for (int i = 0; i < count; i++)
            {
                FieldSpawnPoint point = selected[i];
                Quaternion rotation = point.Rotation * Quaternion.Euler(0f, random.Next(4) * 90f, 0f);
                LootChest chest = CreateChest($"{generationId}/{point.Id}", point.Position, rotation);
                LootStack[] contents = Roll(random);

                // 필드 변형이 있으면 상자 하나에 확정 티어 이상 장비 1개를 더 넣습니다(#194).
                if (i == bonusIndex)
                {
                    ItemDefinition bonus = PickAtLeast(GearPool(random), (ItemTier)(preset.GuaranteedTier + 1), random);
                    if (bonus != null)
                        contents = contents.Append(new LootStack(bonus.ItemId, 1)).ToArray();
                }

                chest.Initialize(chest.name, contents);
                chests.Add(chest);
            }

            return chests;
        }

        public static LootChest CreateBossReward(Transform parent, Vector3 position, string id, LootStack[] contents)
        {
            var owner = new GameObject("Boss Reward Chest");
            owner.transform.SetParent(parent, false);
            var spawner = owner.AddComponent<ChestSpawner>();
            spawner.root = new GameObject("Reward");
            spawner.root.transform.SetParent(owner.transform, false);
            spawner.bodyMaterial = CreateMaterial(new Color(0.22f, 0.12f, 0.42f));
            spawner.lidMaterial = CreateMaterial(new Color(1f, 0.75f, 0.12f));
            LootChest chest = spawner.CreateChest(id, position, Quaternion.identity);
            chest.transform.localScale = Vector3.one * 2.2f;
            chest.gameObject.AddComponent<BossTreasurePresentation>().Build();
            chest.Initialize(id, contents);
            return chest;
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
                if (material != null)
                {
                    if (Application.isPlaying) Destroy(material);
                    else DestroyImmediate(material);
                }
            }
        }

        // 후보 아이템 카탈로그입니다. 티어가 있는 판매 아이템(장비 아님)과 gear-/weapon- 장비만 씁니다.
        private readonly List<ItemDefinition> sellPool = new();
        private readonly List<ItemDefinition> gearPool = new();

        public void SetCatalog(IEnumerable<ItemDefinition> items)
        {
            sellPool.Clear();
            gearPool.Clear();
            if (items == null)
                return;

            // 같은 seed면 같은 내용물이 나오도록 ID 순으로 고정합니다.
            foreach (ItemDefinition item in items.Where(item => item != null && item.Tier != ItemTier.None)
                         .OrderBy(item => item.ItemId, StringComparer.Ordinal))
            {
                if (item.EquipmentType == EquipmentType.None)
                    sellPool.Add(item);
                else if (item.ItemId.StartsWith("gear-", StringComparison.Ordinal) ||
                         item.ItemId.StartsWith("weapon-", StringComparison.Ordinal))
                    gearPool.Add(item);
            }
        }

        // 판매 아이템 여러 개, 확률로 장비 1개, 추가 소모품을 넣습니다. 티어는 같은 가중치 표로 고릅니다.
        private LootStack[] Roll(System.Random random)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var order = new List<string>();
            void Put(string id, int count)
            {
                if (!counts.ContainsKey(id))
                    order.Add(id);
                counts[id] = counts.GetValueOrDefault(id) + count;
            }

            int sellItems = random.Next(Mathf.Min(sellItemsMin, sellItemsMax), Mathf.Max(sellItemsMin, sellItemsMax) + 1);
            if (preset != null)
                sellItems = Mathf.RoundToInt(sellItems * preset.ScrapAmount);

            for (int i = 0; i < sellItems; i++)
            {
                ItemDefinition item = Pick(sellPool, random);
                if (item != null)
                    Put(item.ItemId, 1);
            }

            if (random.NextDouble() < (preset != null ? preset.GearChance : gearChance))
            {
                ItemDefinition item = Pick(GearPool(random), random);
                if (item != null)
                    Put(item.ItemId, 1);
            }

            foreach (LootEntry entry in extras)
            {
                if (string.IsNullOrWhiteSpace(entry.ItemId) || entry.Max <= 0 || random.NextDouble() >= entry.Chance)
                    continue;

                int min = Mathf.Max(1, entry.Min);
                Put(entry.ItemId, random.Next(min, Mathf.Max(min, entry.Max) + 1));
            }

            return order.Select(id => new LootStack(id, counts[id])).ToArray();
        }

        // 필드 변형의 무기 비율로 무기(weapon-)와 방어구(gear-) 중 한쪽 후보를 고릅니다. 한쪽이 비면 전체를 씁니다.
        private List<ItemDefinition> GearPool(System.Random random)
        {
            if (preset == null)
                return gearPool;

            bool weapon = random.NextDouble() < preset.WeaponShare;
            List<ItemDefinition> side = gearPool
                .Where(item => item.ItemId.StartsWith("weapon-", StringComparison.Ordinal) == weapon).ToList();
            return side.Count > 0 ? side : gearPool;
        }

        // 필드 변형이 있으면 그 티어 가중치를, 없으면 기본 표를 씁니다.
        private float WeightOf(ItemTier tier)
        {
            int index = (int)tier - 1;
            if (preset != null && index >= 0 && index < preset.TierWeights.Length)
                return preset.TierWeights[index];

            return tierWeights.FirstOrDefault(entry => entry.Tier == tier).Weight;
        }

        // minimum 이상 티어만 남겨 고르고, 그런 후보가 없으면 가장 높은 티어에서 고릅니다.
        private ItemDefinition PickAtLeast(List<ItemDefinition> pool, ItemTier minimum, System.Random random)
        {
            List<ItemDefinition> high = pool.Where(item => item.Tier >= minimum).ToList();
            if (high.Count == 0 && pool.Count > 0)
                high = pool.Where(item => item.Tier == pool.Max(other => other.Tier)).ToList();

            // 최소 티어 이상에서는 가중치가 0인 티어도 뽑힐 수 있게 1을 더합니다.
            return Pick(high, random, 1f);
        }

        // 후보가 있는 티어 중에서 가중치로 하나를 고르고, 그 티어 안에서는 균등하게 고릅니다.
        private ItemDefinition Pick(List<ItemDefinition> pool, System.Random random, float weightBonus = 0f)
        {
            List<(List<ItemDefinition> Items, float Weight)> tiers = tierWeights
                .Select(entry => (pool.Where(item => item.Tier == entry.Tier).ToList(), Mathf.Max(0f, WeightOf(entry.Tier)) + weightBonus))
                .Where(tier => tier.Item1.Count > 0 && tier.Item2 > 0f).ToList();
            if (tiers.Count == 0)
                return null;

            double pick = random.NextDouble() * tiers.Sum(tier => tier.Weight);
            List<ItemDefinition> chosen = tiers[^1].Items;
            foreach ((List<ItemDefinition> items, float weight) in tiers)
            {
                pick -= weight;
                if (pick < 0)
                {
                    chosen = items;
                    break;
                }
            }

            return chosen[random.Next(chosen.Count)];
        }

        // 테스트·검증용: 지정한 seed로 상자 하나의 내용물을 뽑습니다.
        public LootStack[] RollContents(int seed) => Roll(new System.Random(seed));

        public IReadOnlyList<TierWeight> TierWeights => tierWeights;
        public IReadOnlyList<LootEntry> Extras => extras;

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
