using UnityEngine;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 나타날 때 "통" 튀어나오기 (그래픽 평가 3번: 정지된 html 같은 화면 → 움직이는 게임 화면).
    /// 켜질 때마다 작게 시작해서 살짝 넘쳤다가 제 크기로. CanvasGroup이 있으면 같이 서서히 보인다.
    /// 레벨 업처럼 게임이 멈춘 화면에서도 움직이게 실제 시간을 쓴다.
    /// </summary>
    public sealed class UiPop : MonoBehaviour
    {
        [SerializeField] private float from = 0.7f;
        [SerializeField] private float duration = 0.3f;
        [SerializeField] private float delay;
        [SerializeField] private bool fade = true;

        private CanvasGroup _group;
        private Vector3 _baseScale = Vector3.one;
        private float _start = -1f;
        private bool _ready;

        public float Delay
        {
            get => delay;
            set => delay = value;
        }

        private void Awake() => Init();

        private void Init()
        {
            if (_ready) return;
            _ready = true;
            _baseScale = transform.localScale;
            _group = GetComponent<CanvasGroup>();
        }

        private void OnEnable() => Play();

        private void OnDisable()
        {
            _start = -1f;
            Apply(1f);
        }

        /// <summary>처음부터 다시 튀어나온다 (카드를 다시 뽑을 때)</summary>
        public void Play()
        {
            Init();
            _start = Time.unscaledTime + delay;
            Apply(0f);
        }

        private void Update()
        {
            if (_start < 0f) return;
            float t = (Time.unscaledTime - _start) / Mathf.Max(0.01f, duration);
            if (t < 0f) return;
            if (t >= 1f)
            {
                Apply(1f);
                _start = -1f;
                return;
            }

            Apply(t);
        }

        private void Apply(float t)
        {
            transform.localScale = _baseScale * Mathf.LerpUnclamped(from, 1f, EaseOutBack(t));
            if (_group != null && fade) _group.alpha = Mathf.Clamp01(t * 2.5f);
        }

        /// <summary>끝에서 살짝 넘쳤다 돌아오는 곡선 (0→1)</summary>
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
