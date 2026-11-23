using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Nomad.Shop
{
    public sealed class ShopPopupBackdrop : MonoBehaviour, IPointerClickHandler
    {
        private RectTransform card;
        private Action cancel;

        public void Configure(RectTransform content, Action onCancel)
        {
            card = content;
            cancel = onCancel;
        }

        public void OnPointerClick(PointerEventData data)
        {
            // 카드의 빈 곳에서 올라오는 클릭은 무시하고 바깥 좌클릭만 취소합니다.
            if (data.button == PointerEventData.InputButton.Left && card != null &&
                !RectTransformUtility.RectangleContainsScreenPoint(card, data.position, data.pressEventCamera))
                cancel?.Invoke();
        }
    }
}
