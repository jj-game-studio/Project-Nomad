using System;
using UnityEngine;

namespace Nomad.Shop
{
    [DisallowMultipleComponent]
    public sealed class PlayerWallet : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingGold = 1000;

        public int Gold { get; private set; }
        public event Action Changed;
        internal bool Trading { get; private set; }

        private void Awake() => Gold = Mathf.Max(0, startingGold);

        // 퀘스트·보상 등 다른 시스템도 재화를 지급할 수 있습니다.
        public bool TryAddGold(int amount)
        {
            if (Trading || amount <= 0 || (long)Gold + amount > int.MaxValue)
                return false;

            Gold += amount;
            Changed?.Invoke();
            return true;
        }

        internal void BeginTrade() => Trading = true;
        internal void SetGold(int value) => Gold = value;
        internal void NotifyChanged() => Changed?.Invoke();
        internal void EndTrade() => Trading = false;
    }
}
