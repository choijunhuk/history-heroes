using UnityEngine;
using UnityEngine.EventSystems;

namespace HistoryHeroes.UI
{
    /// <summary>영웅 초상을 끌면 가로 움직임을 알려 준다 (HeroPortraits가 영웅을 돌린다). 누르기는 부모 줄 버튼이 받는다</summary>
    public sealed class PortraitDrag : MonoBehaviour, IDragHandler
    {
        public System.Action<float> Dragged;

        public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(eventData.delta.x);
    }
}
