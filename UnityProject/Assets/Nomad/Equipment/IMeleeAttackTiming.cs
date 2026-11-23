using Nomad.Inventory;

namespace Nomad.Equipment
{
    // 모션이 있는 플레이어만 준비 시간을 제공합니다. 없으면 기존 즉시 타격을 유지합니다.
    public interface IMeleeAttackTiming
    {
        float GetWindup(ItemDefinition weapon);
    }
}
