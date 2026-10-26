using HistoryHeroes.Combat;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 분열체 (Content 4장): 다가와서 짧게 덮친다. 쓰러지면 작은 분열체 2마리로 갈라진다 (작은 것은 더 갈라지지 않는다).
    /// 노리는 플레이: 광역 피해. 작은 것은 smallPrefab (자기 자신을 가리키면 Unity가 복제본으로 바꿔 버려서 따로 둔다).
    /// </summary>
    public sealed class SplitterEnemy : EnemyBase
    {
        [SerializeField] private EnemyBase smallPrefab;
        [SerializeField] private int splitCount = 2;
        [Tooltip("작은 것의 체력 (이 적 배수 × 이 값)")]
        [SerializeField] private float childHp = 0.4f;
        [SerializeField] private float pounceRange = 1.6f;
        [SerializeField] private float pounceSpeed = 7.5f;
        [SerializeField] private float pounceTime = 0.25f;
        [SerializeField] private float hitRadius = 0.55f;

        private Vector2 _dir;
        private bool _hit;

        protected override void Tick()
        {
            switch (state)
            {
                case State.Approach:
                    MoveToward(ToPlayer, data.moveSpeed);
                    if (ToPlayer.magnitude <= pounceRange)
                    {
                        _dir = ToPlayer.normalized;
                        Enter(State.Telegraph, data.telegraphTime * TelegraphScale);
                    }

                    break;

                case State.Telegraph:
                    DrawTelegraphLine(transform.position, (Vector2)transform.position + _dir * pounceSpeed * pounceTime);
                    if (stateTimer <= 0f)
                    {
                        _hit = false;
                        Enter(State.Attack, pounceTime);
                    }

                    break;

                case State.Attack:
                    MoveToward(_dir, pounceSpeed);
                    if (!_hit)
                    {
                        Collider2D c = Physics2D.OverlapCircle(transform.position, hitRadius * transform.localScale.x, playerMask);
                        if (c != null && c.TryGetComponent(out Health target)) _hit = target.TryTakeDamage(DamageCalculator.Enemy(Damage, data.id));
                    }

                    if (stateTimer <= 0f) Enter(State.Recover, data.recoverTime);
                    break;

                case State.Recover:
                    if (stateTimer <= 0f) Enter(State.Approach);
                    break;
            }
        }

        protected override void OnDied(Health h)
        {
            // 갈라진 것을 먼저 내보낸 뒤 쓰러진다 (먼저 쓰러지면 방이 "전멸"로 끝나 버린다)
            if (smallPrefab != null && ChildSpawner != null)
            {
                Vector2 at = transform.position;
                float hp = HpMultiplierUsed * childHp;
                float atk = AttackMultiplierUsed;
                for (int i = 0; i < splitCount; i++)
                {
                    float a = i * Mathf.PI * 2f / splitCount + 0.6f;
                    ChildSpawner(smallPrefab, at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.45f, hp, atk);
                }
            }

            base.OnDied(h);
        }
    }
}
