namespace Nomad.Monsters
{
    public enum MonsterRank { Normal, Elite, Boss }
    public enum MonsterWeaponStyle { Sword, Crossbow, Staff }

    public sealed class MonsterIdentity : UnityEngine.MonoBehaviour
    {
        public MonsterRank Rank { get; private set; }
        public MonsterWeaponStyle Weapon { get; set; }
        public void Configure(MonsterRank rank) => Rank = rank;
    }
}
