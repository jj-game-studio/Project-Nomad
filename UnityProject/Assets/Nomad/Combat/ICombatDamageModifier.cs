namespace Nomad.Combat
{
    // 피해 수신자에 붙은 선택적 보정입니다. 낙하·무적·사망 판정은 Health가 유지합니다.
    // 0 이하를 반환하면 회피로 처리해 피해를 주지 않습니다.
    public interface ICombatDamageModifier
    {
        int ModifyCombatDamage(int amount);
    }
}
