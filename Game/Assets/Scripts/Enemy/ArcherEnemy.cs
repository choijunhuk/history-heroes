using HistoryHeroes.Combat;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 궁병 (Content.md 4장): 거리를 유지하다가 0.6초 조준선 → 직선 화살 1발 → 1초 재장전(빈틈).
    /// 노리는 플레이: 옆으로 피하기. 조준선은 쏘기 0.15초 전에 방향을 고정한다.
    /// </summary>
    public sealed class ArcherEnemy : EnemyBase
    {
        [SerializeField] private float preferredMin = 4f;
        [SerializeField] private float preferredMax = 7f;
        [SerializeField] private float arrowSpeed = 7f;
        [SerializeField] private float lockBeforeFire = 0.15f;

        private Vector2 _aim;

        protected override void Tick()
        {
            float distance = ToPlayer.magnitude;

            switch (state)
            {
                case State.Approach:
                    if (distance > preferredMax) MoveToward(ToPlayer, data.moveSpeed);
                    else if (distance < preferredMin) MoveToward(-ToPlayer, data.moveSpeed);
                    else Enter(State.Telegraph, data.telegraphTime);
                    break;

                case State.Telegraph:
                    if (stateTimer > lockBeforeFire) _aim = ToPlayer.normalized;
                    DrawTelegraphLine(transform.position, (Vector2)transform.position + _aim * 12f);
                    if (stateTimer <= 0f)
                    {
                        EnemyProjectiles.Fire(transform.position, _aim, arrowSpeed, DamageCalculator.Enemy(Damage), playerMask);
                        Enter(State.Recover, data.recoverTime);
                    }
                    break;

                case State.Recover:
                    if (stateTimer <= 0f) Enter(State.Approach);
                    break;
            }
        }
    }
}
