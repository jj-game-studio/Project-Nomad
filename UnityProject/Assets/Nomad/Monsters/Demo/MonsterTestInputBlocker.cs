using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Nomad.Monsters.Demo
{
    // IMGUI 패널도 기존 PlayerCombat의 EventSystem UI 검사에 참여시킵니다.
    public sealed class MonsterTestInputBlocker : BaseRaycaster
    {
        public MonsterTestPanel Panel { get; set; }
        public System.Func<Vector2, bool> AdditionalBlocker { get; set; }
        public override Camera eventCamera => null;

        public override void Raycast(PointerEventData data, List<RaycastResult> results)
        {
            if (!(Panel != null && Panel.BlocksPointer(data.position)) &&
                AdditionalBlocker?.Invoke(data.position) != true)
                return;

            results.Add(new RaycastResult { gameObject = gameObject, module = this,
                distance = 0f, index = results.Count, screenPosition = data.position });
        }
    }
}
