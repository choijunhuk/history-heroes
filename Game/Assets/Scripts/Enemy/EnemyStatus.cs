using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 적 상태 이상 (Skills.md 2장·4-4): 둔화, 빙결, 독(중첩), 끌어당김.
    /// - 둔화: 이동 속도 −비율 (여러 개면 가장 센 것 하나). 서리 창은 둔화가 3번 쌓이면 1초 빙결
    /// - 빙결: 움직이지도 공격하지도 않는다 (예고 중이던 공격도 멈춤). 빙결한 적에게 서리 창은 치명타 확정
    /// - 독: 초당 피해, 최대 3번 중첩
    /// - 끌어당김: 회오리·눈보라 중심으로 끌려간다
    /// 보스는 빙결·끌어당김에 덜 걸린다 (빙결 시간 절반, 끌어당김 30%).
    /// </summary>
    public sealed class EnemyStatus : MonoBehaviour
    {
        public const int MaxPoisonStacks = 3;
        public const float PoisonTick = 0.5f;

        private EnemyBase _enemy;
        private Rigidbody2D _body;
        private SpriteRenderer _tint;
        private Color _baseColor;

        private float _slowFraction;
        private float _slowUntil;
        private float _freezeUntil;
        private int _chillStacks;
        private float _chillUntil;

        private int _poisonStacks;
        private float _poisonDamagePerStack;
        private float _poisonUntil;
        private float _poisonTimer;

        private Vector2 _pullCenter;
        private float _pullSpeed;
        private float _pullUntil;

        public bool IsFrozen => Time.time < _freezeUntil;
        public float SpeedMultiplier => Time.time < _slowUntil ? 1f - _slowFraction : 1f;
        public bool IsPulled => Time.time < _pullUntil;

        private bool IsBoss => _enemy != null && !_enemy.CanBeKnockedBack && !_enemy.IsElite;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            _body = GetComponent<Rigidbody2D>();
            _tint = GetComponent<SpriteRenderer>();
            if (_tint != null) _baseColor = _tint.color;
        }

        /// <summary>다시 나올 때 (오브젝트 풀) 상태를 비운다</summary>
        public void Clear()
        {
            _slowUntil = _freezeUntil = _chillUntil = _poisonUntil = _pullUntil = 0f;
            _chillStacks = _poisonStacks = 0;
            if (_tint != null) _tint.color = _baseColor;
        }

        public void ApplySlow(float fraction, float duration)
        {
            if (Time.time >= _slowUntil || fraction >= _slowFraction) _slowFraction = Mathf.Clamp01(fraction);
            _slowUntil = Mathf.Max(_slowUntil, Time.time + duration);
        }

        /// <summary>서리 창: 둔화를 1번 쌓는다. stacksToFreeze번 쌓이면 빙결</summary>
        public void AddChill(int stacksToFreeze, float freezeDuration)
        {
            if (Time.time > _chillUntil) _chillStacks = 0;
            _chillStacks++;
            _chillUntil = Time.time + 3f;
            if (_chillStacks < stacksToFreeze) return;
            _chillStacks = 0;
            ApplyFreeze(freezeDuration);
        }

        public void ApplyFreeze(float duration)
        {
            if (IsBoss) duration *= 0.5f;
            _freezeUntil = Mathf.Max(_freezeUntil, Time.time + duration);
        }

        /// <param name="damagePerSecond">한 겹당 초당 피해 (이미 굴린 값)</param>
        public void ApplyPoison(float damagePerSecond, float duration)
        {
            _poisonStacks = Mathf.Min(MaxPoisonStacks, _poisonStacks + 1);
            _poisonDamagePerStack = Mathf.Max(_poisonDamagePerStack, damagePerSecond);
            _poisonUntil = Mathf.Max(_poisonUntil, Time.time + duration);
        }

        public void Pull(Vector2 center, float speed, float duration)
        {
            if (IsBoss) speed *= 0.3f;
            _pullCenter = center;
            _pullSpeed = speed;
            _pullUntil = Mathf.Max(_pullUntil, Time.time + duration);
        }

        private void Update()
        {
            if (GamePause.IsPaused || _enemy == null || _enemy.Health.IsDead) return;
            float now = Time.time;

            // 독: 0.5초마다 (겹 수 × 초당 피해 × 0.5)
            if (_poisonStacks > 0)
            {
                if (now >= _poisonUntil)
                {
                    _poisonStacks = 0;
                }
                else
                {
                    _poisonTimer -= Time.deltaTime;
                    if (_poisonTimer <= 0f)
                    {
                        _poisonTimer += PoisonTick;
                        var info = new DamageInfo { Amount = _poisonDamagePerStack * _poisonStacks * PoisonTick, Kind = DamageKind.Active, AttackerTeam = Team.Player };
                        _enemy.Health.TryTakeDamage(info);
                    }
                }
            }

            // 색으로 보여주기: 빙결 하늘색, 독 초록, 둔화 연한 파랑
            if (_tint != null && !IsFlashing())
            {
                Color target = _baseColor;
                if (IsFrozen) target = new Color(0.6f, 0.85f, 1f);
                else if (_poisonStacks > 0) target = Color.Lerp(_baseColor, new Color(0.45f, 0.9f, 0.35f), 0.5f);
                else if (now < _slowUntil) target = Color.Lerp(_baseColor, new Color(0.6f, 0.8f, 1f), 0.4f);
                if (_tint.color != target) _tint.color = target;
            }
        }

        private void FixedUpdate()
        {
            if (GamePause.IsPaused || !IsPulled || _body == null) return;
            Vector2 to = _pullCenter - _body.position;
            if (to.sqrMagnitude < 0.04f) return;
            _body.MovePosition(_body.position + to.normalized * Mathf.Min(_pullSpeed * Time.fixedDeltaTime, to.magnitude));
        }

        /// <summary>맞았을 때 번쩍이는 중이면 색을 건드리지 않는다 (HitFlash가 원래 색으로 돌려놓는다)</summary>
        private bool IsFlashing() => TryGetComponent(out HitFlash flash) && flash.IsFlashing;
    }
}
