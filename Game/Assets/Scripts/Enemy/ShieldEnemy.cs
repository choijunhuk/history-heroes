using HistoryHeroes.Combat;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 방패병 (Content 4장): 앞을 막는 방패를 들고 천천히 다가와 방패로 들이받는다 → 방패를 내리는 빈틈.
    /// 정면에서 날아온 투사체·베기는 막는다 (관통 투사체와 범위 공격은 막지 못한다). 몸을 천천히 돌리므로 옆으로 돌아가면 맞는다.
    /// 노리는 플레이: 옆으로 돌아가기 · 벽 튕김 · 관통.
    /// </summary>
    public sealed class ShieldEnemy : EnemyBase
    {
        [Tooltip("정면 기준 막는 각도 (반각, 도)")]
        [SerializeField] private float blockHalfAngle = 65f;
        [Tooltip("몸 돌리는 빠르기 (도/초)")]
        [SerializeField] private float turnSpeed = 110f;
        [SerializeField] private float bashRange = 1.4f;
        [SerializeField] private float bashSpeed = 7f;
        [SerializeField] private float bashTime = 0.22f;
        [SerializeField] private float bashHitRadius = 0.7f;
        [SerializeField] private SpriteRenderer shieldVisual;

        private Vector2 _facing = Vector2.down;
        private bool _bashHit;

        /// <summary>방패를 내린 빈틈 (들이받은 뒤)</summary>
        public bool ShieldDown => state == State.Recover;
        public Vector2 Facing => _facing;

        public override void Spawn(Transform target, float hpMultiplier, float atkMultiplier, bool elite)
        {
            base.Spawn(target, hpMultiplier, atkMultiplier, elite);
            _facing = ToPlayer.sqrMagnitude > 0.01f ? ToPlayer.normalized : Vector2.down;
            health.Guard = Block;
            UpdateShield();
        }

        private DamageInfo Block(DamageInfo info)
        {
            if (!info.HasDirection || info.Piercing || ShieldDown) return info;
            // 날아온 방향의 반대가 정면 쪽이면 막는다
            if (Vector2.Angle(-info.Direction, _facing) > blockHalfAngle) return info;
            info.Amount = 0f;
            AttackFx.Burst((Vector2)transform.position + _facing * 0.55f * transform.localScale.x, 0.25f, new Color(0.85f, 0.85f, 0.95f, 0.8f), 0.1f);
            return info;
        }

        protected override void Tick()
        {
            switch (state)
            {
                case State.Approach:
                    Turn(ToPlayer, turnSpeed);
                    MoveToward(_facing, data.moveSpeed);
                    if (ToPlayer.magnitude <= bashRange) Enter(State.Telegraph, data.telegraphTime * TelegraphScale);
                    break;

                case State.Telegraph:
                    Turn(ToPlayer, turnSpeed * 0.5f);
                    DrawTelegraphLine(transform.position, (Vector2)transform.position + _facing * (bashSpeed * bashTime + 0.6f));
                    if (stateTimer <= 0f)
                    {
                        _bashHit = false;
                        Enter(State.Attack, bashTime);
                    }

                    break;

                case State.Attack:
                    MoveToward(_facing, bashSpeed);
                    if (!_bashHit)
                    {
                        Collider2D hit = Physics2D.OverlapCircle((Vector2)transform.position + _facing * 0.4f, bashHitRadius * transform.localScale.x, playerMask);
                        if (hit != null && hit.TryGetComponent(out Health target)) _bashHit = target.TryTakeDamage(DamageCalculator.Enemy(Damage, data.id));
                    }

                    if (stateTimer <= 0f) Enter(State.Recover, data.recoverTime);
                    break;

                case State.Recover:
                    if (stateTimer <= 0f) Enter(State.Approach);
                    break;
            }

            UpdateShield();
        }

        private void Turn(Vector2 toward, float degreesPerSecond)
        {
            if (toward.sqrMagnitude < 0.0001f) return;
            float current = Mathf.Atan2(_facing.y, _facing.x) * Mathf.Rad2Deg;
            float goal = Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg;
            float next = Mathf.MoveTowardsAngle(current, goal, degreesPerSecond * Time.deltaTime);
            _facing = new Vector2(Mathf.Cos(next * Mathf.Deg2Rad), Mathf.Sin(next * Mathf.Deg2Rad));
        }

        private void UpdateShield()
        {
            if (shieldVisual == null) return;
            Transform t = shieldVisual.transform;
            t.localPosition = _facing * 0.55f;
            t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_facing.y, _facing.x) * Mathf.Rad2Deg);
            Color c = shieldVisual.color;
            c.a = ShieldDown ? 0.25f : 0.95f; // 방패를 내리면 흐리게 (지금 쏘세요)
            shieldVisual.color = c;
        }
    }
}
