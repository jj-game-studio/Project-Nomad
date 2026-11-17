using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nomad.Loot
{
    internal sealed class LootStackButton : Button, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private bool dragged;
        public Action<Vector2> BeginDrag { get; set; }
        public Action<Vector2> MoveDrag { get; set; }
        public Action<Vector2> EndDrag { get; set; }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                dragged = false;
            base.OnPointerDown(eventData);
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (!dragged && eventData.clickCount == 2)
                base.OnPointerClick(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            dragged = true;
            BeginDrag?.Invoke(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                MoveDrag?.Invoke(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                EndDrag?.Invoke(eventData.position);
        }
    }
}
