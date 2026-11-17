namespace Nomad.Monsters
{
    public enum MonsterRank { Normal, Elite, Boss }
    public enum MonsterWeaponStyle { Sword, Crossbow, Staff }

    public sealed class MonsterIdentity : UnityEngine.MonoBehaviour
    {
        public MonsterRank Rank { get; private set; }
        public MonsterWeaponStyle Weapon { get; set; }
        public string DisplayName => Rank == MonsterRank.Boss ? "DRAGON" : Rank == MonsterRank.Elite ? "GOLEM" :
            Weapon == MonsterWeaponStyle.Sword ? "SWORD" : Weapon == MonsterWeaponStyle.Crossbow ? "CROSSBOW" : "MAGE";
        public void Configure(MonsterRank rank) => Rank = rank;
    }
}
