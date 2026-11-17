using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nomad.Loot
{
    internal sealed class LootStackButton : Button
    {
        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.clickCount == 2)
                base.OnPointerClick(eventData);
        }
    }
}
