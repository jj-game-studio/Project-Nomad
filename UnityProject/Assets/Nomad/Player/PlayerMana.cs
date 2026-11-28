using Nomad.Combat;
using UnityEngine;

namespace Nomad.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(Health))]
    public sealed class PlayerMana : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maximum = 100;
        [SerializeField, Min(0)] private float recoveryPerSecond = 8f;
        [SerializeField, Min(0)] private float recoveryDelay = 2f;

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

        private void Awake()
        {
            health = GetComponent<Health>();
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
            if (!health.IsDead && Time.time >= recoveryTime)
                Current = Mathf.Min(Maximum, Current + RecoveryPerSecond * Time.deltaTime);
        }

        public void RestoreFull()
        {
            if (!health.IsDead)
                Current = Maximum;
        }

        public bool CanSpend(int amount) => isActiveAndEnabled && !health.IsDead && amount > 0 && (Unlimited || Current >= amount);

        public bool TrySpend(int amount)
        {
            if (!CanSpend(amount))
                return false;

            if (!Unlimited) Current -= amount;
            recoveryTime = Time.time + recoveryDelay;
            return true;
        }
    }
}
