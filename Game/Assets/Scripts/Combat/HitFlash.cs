using UnityEngine;

namespace HistoryHeroes.Combat
{
    /// <summary>맞으면 잠깐 하얗게 번쩍인다. "맞혔다/맞았다"를 숫자 없이도 바로 알게. (Art.md 타격감)</summary>
    [RequireComponent(typeof(Health))]
    public sealed class HitFlash : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private float duration = 0.08f;
        [SerializeField] private Color flashColor = Color.white;

        private Health _health;
        private Color _baseColor;
        private float _timeLeft;

        /// <summary>지금 번쩍이는 중인가 (상태 이상 색칠이 덮어쓰지 않게)</summary>
        public bool IsFlashing => _timeLeft > 0f;

        private void Awake()
        {
            _health = GetComponent<Health>();
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();
            _baseColor = target.color;
        }

        private void OnEnable() => _health.Damaged += OnDamaged;

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _timeLeft = 0f;
            if (target != null) target.color = _baseColor;
        }

        private void OnDamaged(DamageInfo info, float dealt)
        {
            _timeLeft = duration;
            target.color = flashColor;
        }

        private void Update()
        {
            if (_timeLeft <= 0f) return;
            _timeLeft -= Time.unscaledDeltaTime; // 맞자마자 게임이 멈춰도(결과·카드 화면) 색이 돌아오게
            if (_timeLeft <= 0f) target.color = _baseColor;
        }
    }
}
