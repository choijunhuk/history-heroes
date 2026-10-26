using UnityEngine;
using UnityEngine.EventSystems;

namespace HistoryHeroes.UI
{
    /// <summary>버튼 누르는 소리 (장면 빌더가 모든 버튼에 붙인다). "뒤로"류는 다른 소리</summary>
    public sealed class UiSound : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private string sound = "ui_click";

        public void OnPointerClick(PointerEventData eventData)
        {
            var button = GetComponent<UnityEngine.UI.Selectable>();
            Audio.AudioManager.Play(button != null && !button.interactable ? "ui_error" : sound, 0.8f, 0.02f);
        }
    }
}
