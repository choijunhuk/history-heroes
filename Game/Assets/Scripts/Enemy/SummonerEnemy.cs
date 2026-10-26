using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 소환사 (Content 4장): 거리를 벌리며 도망다니다가, 1초 마법진 예고 → 졸개 2마리 소환 → 1.5초 서 있는 빈틈.
    /// 졸개는 한 소환사당 최대 4마리. 노리는 플레이: 우선 처치 대상 (빈틈에 몰아치기).
    /// </summary>
    public sealed class SummonerEnemy : EnemyBase
    {
        [SerializeField] private EnemyBase minion;
        [SerializeField] private int perSummon = 2;
        [SerializeField] private int maxMinions = 4;
        [SerializeField] private float interval = 5f;
        [SerializeField] private float fleeDistance = 4f;
        [SerializeField] private float approachDistance = 7f;
        [Tooltip("졸개 체력 (이 적 배수 × 이 값)")]
        [SerializeField] private float minionHp = 0.5f;

        private readonly List<EnemyBase> _minions = new List<EnemyBase>();
        private readonly List<SpriteRenderer> _circles = new List<SpriteRenderer>();
        private readonly List<Vector2> _points = new List<Vector2>();
        private float _cooldown;

        public override void Spawn(Transform target, float hpMultiplier, float atkMultiplier, bool elite)
        {
            base.Spawn(target, hpMultiplier, atkMultiplier, elite);
            _minions.Clear();
            _cooldown = interval * 0.4f; // 첫 소환은 조금 빨리
        }

        protected override void Tick()
        {
            _minions.RemoveAll(m => m == null || m.Health.IsDead || !m.gameObject.activeInHierarchy);
            switch (state)
            {
                case State.Approach:
                    float d = ToPlayer.magnitude;
                    if (d < fleeDistance) MoveToward(-ToPlayer, data.moveSpeed);
                    else if (d > approachDistance) MoveToward(ToPlayer, data.moveSpeed);
                    else body.linearVelocity = Vector2.zero;

                    _cooldown -= Time.deltaTime;
                    if (_cooldown <= 0f && _minions.Count < maxMinions) BeginSummon();
                    break;

                case State.Telegraph:
                    if (stateTimer <= 0f) Release();
                    break;

                case State.Recover:
                    if (stateTimer <= 0f)
                    {
                        _cooldown = interval;
                        Enter(State.Approach);
                    }

                    break;
            }
        }

        private void BeginSummon()
        {
            _points.Clear();
            int count = Mathf.Min(perSummon, maxMinions - _minions.Count);
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                _points.Add(UntilWall(transform.position, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 1.2f));
                _circles.Add(Telegraphs.Circle(_points[i], 0.5f, Telegraphs.Summon));
            }

            Enter(State.Telegraph, 1f * TelegraphScale);
        }

        private void Release()
        {
            foreach (SpriteRenderer c in _circles) Telegraphs.Hide(c);
            _circles.Clear();
            if (ChildSpawner != null && minion != null)
            {
                foreach (Vector2 p in _points)
                {
                    EnemyBase m = ChildSpawner(minion, p, HpMultiplierUsed * minionHp, AttackMultiplierUsed);
                    if (m != null) _minions.Add(m);
                }
            }

            Enter(State.Recover, data.recoverTime); // 소환 직후 빈틈
        }

        protected override void OnDisable()
        {
            foreach (SpriteRenderer c in _circles) Telegraphs.Hide(c);
            _circles.Clear();
            base.OnDisable();
        }
    }
}
