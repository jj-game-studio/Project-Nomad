using System;
using System.Collections.Generic;

namespace Nomad.Game
{
    // 출격 전에 고르는 필드 변형입니다(#194). 지형은 항상 랜덤이고, 파밍·몬스터 수치만 바꿉니다.
    // 아이템 등급과 난이도는 정비례하도록 짭니다. 수치는 임시값이며 Play 검토 후 사람이 조정합니다.
    public sealed class FieldPreset
    {
        public string Id;
        public string Name;
        public string Concept;

        // 1~4. 높을수록 고티어 확률과 몬스터·엘리트 수가 함께 높습니다.
        public int Difficulty;

        // 티어 가중치 C, B, A, S, SS, SSS 순서의 상대값입니다.
        public float[] TierWeights;

        // 상자마다 판매용 고철 개수에 곱하는 배율입니다.
        public float ScrapAmount = 1f;

        // 상자에 장비가 1개 들어 있을 확률과, 그 장비가 무기일 비율입니다(나머지는 방어구).
        public float GearChance = 0.4f;
        public float WeaponShare = 0.5f;

        // 필드마다 상자 하나에 이 티어 이상 장비 1개를 확정으로 넣습니다(0=C … 5=SSS).
        public int GuaranteedTier;

        public float MonsterAmount = 1f;
        public int EliteCount = 2;

        public static readonly IReadOnlyList<FieldPreset> All = new[]
        {
            new FieldPreset
            {
                Id = "outskirts", Name = "버려진 외곽", Concept = "몬스터가 적은 고철 집하장",
                Difficulty = 1, TierWeights = new[] { 60f, 25f, 10f, 4f, 1f, 0f },
                ScrapAmount = 1.5f, GearChance = 0.25f, WeaponShare = 0.5f,
                GuaranteedTier = 2, MonsterAmount = 0.75f, EliteCount = 1,
            },
            new FieldPreset
            {
                Id = "armory", Name = "무너진 병기고", Concept = "무기가 잘 나오는 옛 군 시설",
                Difficulty = 2, TierWeights = new[] { 50f, 25f, 13f, 7f, 4f, 1f },
                ScrapAmount = 0.8f, GearChance = 0.55f, WeaponShare = 0.8f,
                GuaranteedTier = 3, MonsterAmount = 1f, EliteCount = 2,
            },
            new FieldPreset
            {
                Id = "barracks", Name = "녹슨 막사", Concept = "방어구가 잘 나오는 수비대 숙소",
                Difficulty = 2, TierWeights = new[] { 50f, 25f, 13f, 7f, 4f, 1f },
                ScrapAmount = 0.8f, GearChance = 0.55f, WeaponShare = 0.2f,
                GuaranteedTier = 3, MonsterAmount = 1f, EliteCount = 2,
            },
            new FieldPreset
            {
                Id = "market", Name = "잿빛 시장터", Concept = "고철이 넘치지만 엘리트가 지키는 곳",
                Difficulty = 3, TierWeights = new[] { 35f, 28f, 18f, 11f, 6f, 2f },
                ScrapAmount = 1.6f, GearChance = 0.4f, WeaponShare = 0.5f,
                GuaranteedTier = 3, MonsterAmount = 1.2f, EliteCount = 3,
            },
            new FieldPreset
            {
                Id = "vault", Name = "봉인된 보물고", Concept = "고티어 장비와 엘리트 무리",
                Difficulty = 4, TierWeights = new[] { 20f, 25f, 22f, 16f, 11f, 6f },
                ScrapAmount = 1f, GearChance = 0.6f, WeaponShare = 0.5f,
                GuaranteedTier = 4, MonsterAmount = 1.35f, EliteCount = 4,
            },
        };

        // 프리셋 중 count개를 겹치지 않게 랜덤으로 고르고, 난이도 순으로 정렬해 돌려줍니다.
        public static List<FieldPreset> Roll(int count, Random random)
        {
            var pool = new List<FieldPreset>(All);
            var picked = new List<FieldPreset>();
            while (picked.Count < count && pool.Count > 0)
            {
                int index = random.Next(pool.Count);
                picked.Add(pool[index]);
                pool.RemoveAt(index);
            }

            picked.Sort((a, b) => a.Difficulty.CompareTo(b.Difficulty));
            return picked;
        }

        // 선택창 카드에 쓰는 요약 태그입니다.
        public IEnumerable<string> Tags()
        {
            if (ScrapAmount >= 1.3f) yield return "고철 多";
            if (GearChance >= 0.5f && WeaponShare >= 0.7f) yield return "무기 多";
            if (GearChance >= 0.5f && WeaponShare <= 0.3f) yield return "방어구 多";
            if (GearChance >= 0.5f && WeaponShare > 0.3f && WeaponShare < 0.7f) yield return "장비 多";

            yield return $"S 이상 {HighTierPercent():0}%";
            yield return $"{TierNames[GuaranteedTier]}+ 장비 1개 확정";
            yield return $"엘리트 {EliteCount}";
        }

        public static readonly string[] TierNames = { "C", "B", "A", "S", "SS", "SSS" };

        private float HighTierPercent()
        {
            float total = 0f, high = 0f;
            for (int i = 0; i < TierWeights.Length; i++)
            {
                total += TierWeights[i];
                if (i >= 3) high += TierWeights[i];
            }

            return total > 0f ? high / total * 100f : 0f;
        }
    }
}
