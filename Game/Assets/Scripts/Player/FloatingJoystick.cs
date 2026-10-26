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
        private Vector3 _baseScale = Vector3.one;
        private float _baseAlpha = -1f;
        private UnityEngine.UI.Image _backgroundImage;

        // v1.0 설정 (UX 5장): 방식 · 크기 · 진하기. 판 중에 설정을 바꿔도 바로 따른다
        private static readonly float[] SizeScale = { 0.8f, 1f, 1.25f };
        private static readonly float[] OpacityScale = { 0.5f, 1f, 1.6f };

        private bool FixedMode => Records.SaveSystem.Data.settings.joystickFixed;
        private float Scale => SizeScale[Mathf.Clamp(Records.SaveSystem.Data.settings.joystickSize, 0, 2)];
        private float Radius => radiusPixels * Scale * (Screen.height / 1920f);

        /// <summary>고정 방식의 자리 (화면 아래 가운데)</summary>
        private Vector2 FixedCenter => new Vector2(Screen.width * 0.5f, Screen.height * 0.17f);

        private void Awake()
        {
            if (background != null)
            {
                _baseScale = background.localScale;
                _backgroundImage = background.GetComponent<UnityEngine.UI.Image>();
                if (_backgroundImage != null) _baseAlpha = _backgroundImage.color.a;
            }
        }

        private void ApplyLook()
        {
            if (background == null) return;
            background.localScale = _baseScale * Scale;
            if (_backgroundImage != null && _baseAlpha >= 0f)
            {
                Color c = _backgroundImage.color;
                c.a = Mathf.Clamp01(_baseAlpha * OpacityScale[Mathf.Clamp(Records.SaveSystem.Data.settings.joystickOpacity, 0, 2)]);
                _backgroundImage.color = c;
            }

            if (FixedMode && !_dragging)
            {
                background.gameObject.SetActive(true);
                background.position = FixedCenter;
                if (knob != null) knob.anchoredPosition = Vector2.zero;
            }
        }

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
            ApplyLook();

            if (pointer.press.wasPressedThisFrame && position.y <= Screen.height * activeAreaHeight)
            {
                if (!FixedMode)
                {
                    _dragging = true;
                    _origin = position;
                    ShowAt(position);
                }
                else if ((position - FixedCenter).magnitude <= Radius * 2.2f)
                {
                    _dragging = true; // 고정: 조이스틱 근처를 눌렀을 때만
                    _origin = FixedCenter;
                }
            }

            if (_dragging && pointer.press.isPressed)
            {
                float radius = Radius;
                Vector2 offset = Vector2.ClampMagnitude(position - _origin, radius);
                Direction = offset / radius;
                if (knob != null) knob.anchoredPosition = offset / Mathf.Max(0.01f, Scale * (Screen.height / 1920f)); // 화면 픽셀 → 조이스틱 안 좌표
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
            if (background != null) background.gameObject.SetActive(FixedMode); // 고정 방식은 늘 보인다
            if (FixedMode && background != null)
            {
                background.position = FixedCenter;
                if (knob != null) knob.anchoredPosition = Vector2.zero;
            }
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
