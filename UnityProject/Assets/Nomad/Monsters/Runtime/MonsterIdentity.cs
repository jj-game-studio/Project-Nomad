namespace Nomad.Monsters
{
    public enum MonsterRank { Normal, Elite, Boss }

    public sealed class MonsterIdentity : UnityEngine.MonoBehaviour
    {
        public MonsterRank Rank { get; private set; }
        public void Configure(MonsterRank rank) => Rank = rank;
    }
}
