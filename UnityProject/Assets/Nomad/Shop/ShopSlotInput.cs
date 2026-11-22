using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Nomad.Shop
{
    // Button은 좌클릭 선택을 담당하고, 우클릭만 별도 거래 경로로 보냅니다.
    internal sealed class ShopSlotInput : MonoBehaviour, IPointerClickHandler
    {
        internal Action RightClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                RightClick?.Invoke();
        }
    }
}
