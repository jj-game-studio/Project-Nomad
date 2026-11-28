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
        public bool Unlimited { get; set; }
        private int maximumBonus;
        private float recoveryBonus;

        public int BaseMaximum => maximum;
        public float BaseRecoveryPerSecond => recoveryPerSecond;
        public int Maximum => Mathf.Max(1, maximum + maximumBonus);
        public float RecoveryPerSecond => Mathf.Max(0, recoveryPerSecond + recoveryBonus);
        public int RollCost => rollCost;

        // 스태미나 부족으로 행동이 거절되었을 때 HUD 알림용으로 호출됩니다.
        public event Action Insufficient;

        private void Awake()
        {
            TryGetComponent(out health);
            Current = Maximum;
        }

        // 장비 보너스입니다. 최대치가 줄면 현재 값을 새 최대치로 맞춥니다.
        public void SetBonus(int maximumAmount, float recoveryAmount)
        {
            maximumBonus = maximumAmount;
            recoveryBonus = recoveryAmount;
            Current = Mathf.Min(Current, Maximum);
        }

        private void Update()
        {
            if ((health == null || !health.IsDead) && Time.time >= recoveryTime)
                Current = Mathf.Min(Maximum, Current + RecoveryPerSecond * Time.deltaTime);
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0)
                return true;

            if (!isActiveAndEnabled || (health != null && health.IsDead) || (!Unlimited && Current < amount))
            {
                Insufficient?.Invoke();
                return false;
            }

            if (!Unlimited) Current -= amount;
            recoveryTime = Time.time + recoveryDelay;
            return true;
        }
    }
}
