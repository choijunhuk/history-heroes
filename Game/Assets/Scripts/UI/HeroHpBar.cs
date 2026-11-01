using HistoryHeroes.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 영웅 머리 위 체력 막대 (Archero 식). 실기기 시험 "체력이 잘 안 보인다" → 시선이 가 있는 영웅 바로 위에 둔다.
    /// 3D 모델 머리 위(월드, 위 = -Z)를 전투 카메라로 화면 좌표로 바꿔 매 프레임 따라간다. 오버레이 캔버스라 3D에 묻히지 않는다.
    /// 색: 초록(>50%) · 노랑(25~50%) · 빨강(&lt;25%). 맞으면 흰 잔상이 늦게 따라 내려온다. 보호막은 위쪽 하늘색 띠. 25% 미만이면 은은하게 뛴다
    /// ("번쩍임 줄이기"면 뛰지 않는다). 현재 체력 숫자를 막대 안에 쓴다 (굵은 글꼴 + 어두운 테두리).
    /// 막대는 하나, 매 프레임 새로 만드는 것 없음.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class HeroHpBar : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private RectTransform parentRect;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image trail;
        [SerializeField] private Image fill;
        [SerializeField] private Image shield;
        [Tooltip("막대 안 현재 체력 숫자")]
        [SerializeField] private TMP_Text label;
        [Tooltip("머리 위로 띄우는 간격 (1080 기준 px)")]
        [SerializeField] private float gap = 18f;

        public static readonly Color High = new Color(0.35f, 0.85f, 0.4f);
        public static readonly Color Mid = new Color(1f, 0.82f, 0.25f);
        public static readonly Color Low = new Color(0.95f, 0.27f, 0.27f);
        private const float TrailHold = 0.35f, TrailSpeed = 0.9f, PulseHz = 1.4f;

        private Camera _camera;
        private RectTransform _rect;
        private float _shownRatio = 1f, _trail = 1f, _holdUntil;
        private int _shownHp = -1;
        private bool _reduceFlash;
        private float _flashCheckAt;

        /// <summary>비율에 따른 막대 색 (위쪽 HUD 막대와 같은 규칙)</summary>
        public static Color ColorFor(float ratio) => ratio > 0.5f ? High : ratio >= 0.25f ? Mid : Low;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            if (group != null) group.alpha = 0f;
        }

        private void LateUpdate()
        {
            Combat.Health health = player != null ? player.Health : null;
            // 쓰러졌거나, 레벨업 창 · 일시정지 등으로 멈춘 동안(오버레이가 덮는 화면)에는 숨긴다
            if (health == null || health.IsDead || !player.isActiveAndEnabled || Common.GamePause.IsPaused)
            {
                if (group.alpha > 0f) group.alpha = 0f;
                _shownRatio = -1f;
                return;
            }

            if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
            if (_camera == null) return;

            float max = Mathf.Max(1f, health.Max);
            float ratio = Mathf.Clamp01(health.Current / max);
            if (_shownRatio < 0f) _trail = ratio; // 다시 나타날 때 잔상 없이
            if (ratio < _shownRatio - 0.0001f) _holdUntil = Time.unscaledTime + TrailHold;
            _shownRatio = ratio;

            if (ratio >= _trail) _trail = ratio;
            else if (Time.unscaledTime >= _holdUntil) _trail = Mathf.Max(ratio, _trail - TrailSpeed * Time.unscaledDeltaTime);

            int hp = Mathf.CeilToInt(health.Current);
            if (label != null && hp != _shownHp)
            {
                _shownHp = hp;
                label.SetText("{0}", hp); // 바뀔 때만 (할당 없음)
            }

            fill.fillAmount = ratio;
            trail.fillAmount = _trail;
            Color color = ColorFor(ratio);

            float shieldAmount = health.Shield;
            bool hasShield = shieldAmount > 0.01f;
            if (shield.enabled != hasShield) shield.enabled = hasShield;
            if (hasShield) shield.fillAmount = Mathf.Clamp01(shieldAmount / max);

            // 25% 미만: 은은한 맥박 (번쩍임 줄이기면 없음)
            float pulse = 0f;
            if (ratio < 0.25f && !ReduceFlashes()) pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseHz * Mathf.PI * 2f);
            fill.color = Color.Lerp(color, Color.white, pulse * 0.25f);
            _rect.localScale = Vector3.one * (1f + 0.06f * pulse);
            group.alpha = 1f;

            // 모델 머리 위 → 화면 → 캔버스 좌표
            Combat.Visual3D visual = player.Visual;
            float height = visual != null && visual.HasModel ? visual.Height : 1.5f;
            Vector3 head = player.transform.position + new Vector3(0f, 0f, -height);
            Vector3 screen = _camera.WorldToScreenPoint(head);
            if (screen.z <= 0f) { group.alpha = 0f; return; }
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, null, out Vector2 local))
            {
                // 안전 영역 안쪽에 머물게 (화면 맨 위에서 영웅이 잘려도 막대가 가려지지 않게)
                Rect r = parentRect.rect;
                Vector2 half = _rect.sizeDelta * 0.5f;
                local.x = Mathf.Clamp(local.x, r.xMin + half.x, r.xMax - half.x);
                local.y = Mathf.Clamp(local.y + gap, r.yMin, r.yMax - _rect.sizeDelta.y);
                _rect.anchoredPosition = local;
            }
        }

        private bool ReduceFlashes()
        {
            if (Time.unscaledTime >= _flashCheckAt)
            {
                _flashCheckAt = Time.unscaledTime + 0.5f;
                _reduceFlash = Records.SaveSystem.Data != null && Records.SaveSystem.Data.settings.reduceFlashes;
            }

            return _reduceFlash;
        }
    }
}
