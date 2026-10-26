using HistoryHeroes.Combat;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 돌진병 (Content.md 4장): 다가가다가 0.8초 빨간 선으로 예고 → 직선 돌진 → 1.2초 멈춤(빈틈).
    /// 노리는 플레이: 피하고 → 멈춰서 쏘기.
    /// </summary>
    public sealed class ChargerEnemy : EnemyBase
    {
        [SerializeField] private float triggerDistance = 5f;
        [SerializeField] private float dashSpeed = 14f;
        [SerializeField] private float dashDuration = 0.4f;
        [SerializeField] private float hitRadius = 0.6f;

        private Vector2 _dashDirection;
        private bool _hitThisDash;

        /// <summary>예고선 방향·길이·맞는 반지름 (화면의 예고선 그대로, 벤치 조종 AI가 피하는 데 쓴다)</summary>
        public Vector2 DashDirection => _dashDirection;
        public float DashLength => dashSpeed * dashDuration;
        public float HitRadius => hitRadius * transform.localScale.x;

        protected override void Tick()
        {
            switch (state)
            {
                case State.Approach:
                    MoveToward(ToPlayer, data.moveSpeed);
                    if (ToPlayer.magnitude <= triggerDistance)
                    {
                        _dashDirection = ToPlayer.normalized;
                        Enter(State.Telegraph, data.telegraphTime);
                    }
                    break;

                case State.Telegraph:
                    DrawTelegraphLine(transform.position, UntilWall(transform.position, _dashDirection, dashSpeed * dashDuration));
                    if (stateTimer <= 0f)
                    {
                        _hitThisDash = false;
                        Enter(State.Attack, dashDuration);
                    }
                    break;

                case State.Attack:
                    MoveToward(_dashDirection, dashSpeed);
                    TryHitPlayer();
                    if (stateTimer <= 0f) Enter(State.Recover, data.recoverTime);
                    break;

                case State.Recover:
                    if (stateTimer <= 0f) Enter(State.Approach);
                    break;
            }
        }

        private void TryHitPlayer()
        {
            if (_hitThisDash) return;
            Collider2D hit = Physics2D.OverlapCircle(transform.position, hitRadius * transform.localScale.x, playerMask);
            if (hit != null && hit.TryGetComponent(out Health target))
            {
                _hitThisDash = target.TryTakeDamage(DamageCalculator.Enemy(Damage, data.id));
            }
        }
    }
}
