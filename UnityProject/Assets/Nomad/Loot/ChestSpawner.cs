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

        // 티어별 상자 내용물입니다. 가중치는 상대값이며, 높은 티어일수록 드물게 나옵니다(#120). 수치는 Play 검토 후 사람이 조정합니다.
        [Serializable]
        public struct TierLoot
        {
            public string Tier;
            public string ScrapId;
            public int ScrapMin;
            public int ScrapMax;
            [Min(0f)] public float ScrapWeight;
            [Min(0f)] public float GearWeight;
            public string[] GearIds;
        }

        [SerializeField] private TierLoot[] tiers =
        {
            new TierLoot { Tier = "C", ScrapId = "scrap", ScrapMin = 4, ScrapMax = 12, ScrapWeight = 50, GearWeight = 45,
                GearIds = new[] { "weapon-dagger", "weapon-shortbow", "weapon-apprentice-staff", "gear-leather-cap",
                    "gear-padded-armor", "gear-cloth-pants", "gear-leather-shoes", "gear-belt-pouch", "gear-copper-ring",
                    "gear-leather-bracelet", "gear-lucky-stone" } },
            new TierLoot { Tier = "B", ScrapId = "scrap-copper", ScrapMin = 3, ScrapMax = 8, ScrapWeight = 25, GearWeight = 28,
                GearIds = new[] { "demo-sword", "demo-bow", "weapon-crystal-staff", "gear-iron-helm", "gear-chain-mail",
                    "gear-leather-pants", "gear-travel-boots", "gear-small-backpack", "gear-sapphire-pendant",
                    "gear-vigor-bracelet", "gear-wind-charm", "gear-mana-ring" } },
            new TierLoot { Tier = "A", ScrapId = "scrap-silver", ScrapMin = 2, ScrapMax = 5, ScrapWeight = 13, GearWeight = 15,
                GearIds = new[] { "weapon-greatsword", "weapon-longbow", "weapon-archmage-staff", "gear-knight-helm",
                    "gear-plate-armor", "gear-ranger-leggings", "gear-swift-boots", "gear-large-backpack", "gear-life-ring",
                    "gear-hawk-earring", "gear-guard-amulet" } },
            new TierLoot { Tier = "S", ScrapId = "scrap-gold", ScrapMin = 1, ScrapMax = 3, ScrapWeight = 7, GearWeight = 8,
                GearIds = new[] { "weapon-rune-sword", "weapon-elven-bow", "weapon-rune-staff", "gear-mithril-helm",
                    "gear-mithril-cuirass", "gear-mithril-greaves", "gear-pegasus-boots", "gear-expedition-pack",
                    "gear-sage-necklace", "gear-titan-bracelet", "gear-phoenix-feather" } },
            new TierLoot { Tier = "SS", ScrapId = "scrap-gem", ScrapMin = 1, ScrapMax = 2, ScrapWeight = 4, GearWeight = 3.5f,
                GearIds = new[] { "weapon-dragonfang-blade", "weapon-dragonbone-bow", "weapon-dragoneye-staff",
                    "gear-dragonscale-helm", "gear-dragonscale-armor", "gear-dragonscale-leggings", "gear-dragonscale-boots",
                    "gear-dimension-bag", "gear-starlight-ring", "gear-raptor-eye" } },
            new TierLoot { Tier = "SSS", ScrapId = "scrap-relic", ScrapMin = 1, ScrapMax = 1, ScrapWeight = 1, GearWeight = 0.5f,
                GearIds = new[] { "weapon-celestial-sword", "weapon-celestial-bow", "weapon-celestial-staff",
                    "gear-celestial-crown", "gear-celestial-plate", "gear-celestial-greaves", "gear-celestial-boots",
                    "gear-eternal-heart", "gear-storm-seal" } },
        };

        [Tooltip("상자에 무기·장비가 1개 들어 있을 확률입니다.")]
        [SerializeField, Range(0f, 1f)] private float gearChance = 0.4f;

        // 고철·장비 외에 함께 나오는 소모품입니다.
        [SerializeField] private LootEntry[] extras =
        {
            new LootEntry("weapon-arrow", 5, 15, 0.3f),
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

        // 고철 1스택은 항상, 장비 1개와 추가 소모품은 확률로 넣습니다. 티어는 가중치로 고릅니다.
        private LootStack[] Roll(System.Random random)
        {
            var stacks = new List<LootStack>();
            int scrapTier = PickTier(random, tier => string.IsNullOrWhiteSpace(tier.ScrapId) ? 0f : tier.ScrapWeight);
            if (scrapTier >= 0)
            {
                TierLoot tier = tiers[scrapTier];
                stacks.Add(new LootStack(tier.ScrapId, RollCount(random, tier.ScrapMin, tier.ScrapMax)));
            }

            if (random.NextDouble() < gearChance)
            {
                int gearTier = PickTier(random, tier => tier.GearIds == null || tier.GearIds.Length == 0 ? 0f : tier.GearWeight);
                if (gearTier >= 0)
                {
                    string[] ids = tiers[gearTier].GearIds;
                    stacks.Add(new LootStack(ids[random.Next(ids.Length)], 1));
                }
            }

            foreach (LootEntry entry in extras)
            {
                if (string.IsNullOrWhiteSpace(entry.ItemId) || entry.Max <= 0 || random.NextDouble() >= entry.Chance)
                    continue;

                stacks.Add(new LootStack(entry.ItemId, RollCount(random, entry.Min, entry.Max)));
            }

            return stacks.ToArray();
        }

        // 가중치가 모두 0이면 -1을 돌려줍니다.
        private int PickTier(System.Random random, Func<TierLoot, float> weight)
        {
            float total = 0f;
            foreach (TierLoot tier in tiers)
                total += Mathf.Max(0f, weight(tier));
            if (total <= 0f)
                return -1;

            double pick = random.NextDouble() * total;
            for (int i = 0; i < tiers.Length; i++)
            {
                pick -= Mathf.Max(0f, weight(tiers[i]));
                if (pick < 0)
                    return i;
            }

            return tiers.Length - 1;
        }

        private static int RollCount(System.Random random, int min, int max)
        {
            min = Mathf.Max(1, min);
            return random.Next(min, Mathf.Max(min, max) + 1);
        }

        // 테스트·검증용: 지정한 seed로 상자 하나의 내용물을 뽑습니다.
        public LootStack[] RollContents(int seed) => Roll(new System.Random(seed));

        public IReadOnlyList<TierLoot> Tiers => tiers;
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
