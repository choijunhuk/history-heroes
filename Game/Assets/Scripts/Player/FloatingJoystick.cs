using HistoryHeroes.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HistoryHeroes.Player
{
    /// <summary>
    /// 화면 아래쪽 어디든 엄지를 대면 그 자리에 생기는 조이스틱 (Docs/Design/UX.md 2장).
    /// 에디터에서는 마우스 드래그와 WASD/방향키로도 움직일 수 있다.
    /// </summary>
    public sealed class FloatingJoystick : MonoBehaviour
    {
        [SerializeField, Range(0.3f, 1f)] private float activeAreaHeight = 0.65f;
        [SerializeField] private float radiusPixels = 110f;
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform knob;

        private bool _dragging;
        private Vector2 _origin;

        public Vector2 Direction { get; private set; }

        /// <summary>테스트용: 값이 있으면 실제 입력 대신 이 방향으로 움직인다 ("이동만" vs "정지" 비교, GameDesign 3-3).</summary>
        public static Vector2? DevOverride { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => DevOverride = null;

        private void Update()
        {
            if (GamePause.IsPaused)
            {
                // 카드 고르는 터치가 이동으로 이어지지 않게, 멈춘 동안의 누름은 무시한다.
                EndDrag();
                return;
            }

            if (DevOverride.HasValue)
            {
                Direction = Vector2.ClampMagnitude(DevOverride.Value, 1f);
                return;
            }

            Vector2 keyboard = ReadKeyboard();
            if (keyboard.sqrMagnitude > 0f)
            {
                Direction = Vector2.ClampMagnitude(keyboard, 1f);
                return;
            }

            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                Direction = Vector2.zero;
                return;
            }

            Vector2 position = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame && position.y <= Screen.height * activeAreaHeight)
            {
                _dragging = true;
                _origin = position;
                ShowAt(position);
            }

            if (_dragging && pointer.press.isPressed)
            {
                Vector2 offset = Vector2.ClampMagnitude(position - _origin, radiusPixels);
                Direction = offset / radiusPixels;
                if (knob != null) knob.anchoredPosition = offset;
            }
            else
            {
                EndDrag();
            }
        }

        private void EndDrag()
        {
            _dragging = false;
            Direction = Vector2.zero;
            if (background != null) background.gameObject.SetActive(false);
        }

        private void ShowAt(Vector2 screenPosition)
        {
            if (background == null) return;
            background.gameObject.SetActive(true);
            background.position = screenPosition;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
        }

        private static Vector2 ReadKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;

            Vector2 v = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) v.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) v.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) v.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) v.x -= 1f;
            return v;
        }
    }
}
