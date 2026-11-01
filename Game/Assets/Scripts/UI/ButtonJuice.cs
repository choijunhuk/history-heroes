using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 버튼 손맛 (그래픽 평가 3번 "UI 스킨"): 누르면 살짝 눌려 작아지고, 떼면 통 튀며 돌아온다.
    /// 게임이 멈춘 화면(레벨 업·일시 정지)에서도 움직이게 실제 시간을 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ButtonJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const float PressedScale = 0.93f;
        private const float Stiffness = 380f;
        private const float Damping = 18f;

        private Selectable _selectable;
        private Vector3 _baseScale = Vector3.one;
        private float _scale = 1f;
        private float _velocity;
        private float _target = 1f;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            _baseScale = transform.localScale;
        }

        private void OnDisable()
        {
            _target = 1f;
            _scale = 1f;
            _velocity = 0f;
            transform.localScale = _baseScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.IsInteractable()) return;
            _target = PressedScale;
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData) => Release();

        private void Release()
        {
            if (_target >= 1f) return;
            _target = 1f;
            _velocity += 2.2f; // 떼는 순간 살짝 튀어 오른다
        }

        private void Update()
        {
            if (Mathf.Abs(_scale - _target) < 0.0005f && Mathf.Abs(_velocity) < 0.0005f) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            // 용수철: 목표 크기로 당기고 속도를 줄인다
            _velocity += ((_target - _scale) * Stiffness - _velocity * Damping) * dt;
            _scale += _velocity * dt;
            transform.localScale = _baseScale * _scale;
        }
    }
}
