using System;
using Nomad.Combat;
using UnityEngine;

namespace Nomad.Player
{
    // 구르기 등 행동이 소모하는 스태미나입니다. 수치는 임시값이며 #146에서 확정합니다.
    [DisallowMultipleComponent]
    public sealed class PlayerStamina : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maximum = 100;
        [SerializeField, Min(0)] private float recoveryPerSecond = 25f;
        [SerializeField, Min(0)] private float recoveryDelay = 1f;
        [SerializeField, Min(0)] private int rollCost = 25;

        private Health health;
        private float recoveryTime;

        public float Current { get; private set; }
        public int Maximum => maximum;
        public int RollCost => rollCost;

        // 스태미나 부족으로 행동이 거절되었을 때 HUD 알림용으로 호출됩니다.
        public event Action Insufficient;

        private void Awake()
        {
            TryGetComponent(out health);
            Current = maximum;
        }

        private void Update()
        {
            if ((health == null || !health.IsDead) && Time.time >= recoveryTime)
                Current = Mathf.Min(maximum, Current + recoveryPerSecond * Time.deltaTime);
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0)
                return true;

            if (!isActiveAndEnabled || (health != null && health.IsDead) || Current < amount)
            {
                Insufficient?.Invoke();
                return false;
            }

            Current -= amount;
            recoveryTime = Time.time + recoveryDelay;
            return true;
        }
    }
}
