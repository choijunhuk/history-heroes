using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>영웅 목록은 가로로 넘기고, 그 위에서 시작한 세로 드래그는 성장 화면에 넘긴다.</summary>
    public sealed class HeroRosterScroll : ScrollRect
    {
        private ScrollRect _verticalParent;
        private PointerEventData _forwardedEvent;

        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            base.OnInitializePotentialDrag(eventData);
            _verticalParent = transform.parent != null ? transform.parent.GetComponentInParent<ScrollRect>() : null;
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (_verticalParent == null && transform.parent != null)
                _verticalParent = transform.parent.GetComponentInParent<ScrollRect>();

            Vector2 delta = eventData.position - eventData.pressPosition;
            if (eventData.button == PointerEventData.InputButton.Left &&
                _verticalParent != null && _verticalParent.isActiveAndEnabled && _verticalParent.vertical &&
                Mathf.Abs(delta.y) > Mathf.Abs(delta.x))
            {
                _forwardedEvent = eventData;
                _verticalParent.OnInitializePotentialDrag(eventData);
                _verticalParent.OnBeginDrag(eventData);
                return;
            }

            base.OnBeginDrag(eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (_forwardedEvent != null)
            {
                if (_verticalParent != null) _verticalParent.OnDrag(eventData);
            }
            else base.OnDrag(eventData);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (_forwardedEvent != null)
            {
                if (_verticalParent != null) _verticalParent.OnEndDrag(eventData);
            }
            else base.OnEndDrag(eventData);
            _forwardedEvent = null;
            _verticalParent = null;
        }

        protected override void OnDisable()
        {
            if (_forwardedEvent != null && _verticalParent != null)
                _verticalParent.OnEndDrag(_forwardedEvent);
            _forwardedEvent = null;
            _verticalParent = null;
            base.OnDisable();
        }
    }
}
