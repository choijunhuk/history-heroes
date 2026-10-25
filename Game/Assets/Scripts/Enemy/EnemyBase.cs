using System;
using HistoryHeroes.Combat;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 적 공통. 모든 적은 "다가감 → 예고 → 공격 → 빈틈" 순서를 지킨다. (GameDesign 3-2)
    /// 빈틈이 있어야 플레이어가 "지금 멈춰서 쏠 수 있다"고 판단할 수 있다.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(Rigidbody2D))]
    public abstract class EnemyBase : MonoBehaviour
    {
        protected enum State { Approach, Telegraph, Attack, Recover }

        [SerializeField] protected EnemyData data;
        [SerializeField] protected LayerMask playerMask;
        [SerializeField] protected LineRenderer telegraphLine;

        protected Health health;
        protected Rigidbody2D body;
        protected Transform player;
        protected State state;
        protected float stateTimer;
        protected float attackMultiplier = 1f;

        public EnemyData Data => data;
        public Health Health => health;
        public bool IsElite { get; private set; }
        public float Damage => data.damage * attackMultiplier;

        public event Action<EnemyBase> Killed;

        protected virtual void Awake()
        {
            health = GetComponent<Health>();
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            health.Died += OnDied;
        }

        /// <param name="hpMultiplier">구간·방 강화 (Stats.md 8장)</param>
        public virtual void Spawn(Transform target, float hpMultiplier, float atkMultiplier, bool elite)
        {
            player = target;
            attackMultiplier = atkMultiplier * (elite ? 1.5f : 1f);
            IsElite = elite;
            health.Init(Team.Enemy, data.maxHp * hpMultiplier * (elite ? 5f : 1f));
            transform.localScale = Vector3.one * (elite ? 1.5f : 1f);
            Enter(State.Approach);
        }

        protected virtual void Update()
        {
            if (player == null || health.IsDead) return;
            stateTimer -= Time.deltaTime;
            Tick();
        }

        protected abstract void Tick();

        protected void Enter(State next, float duration = 0f)
        {
            state = next;
            stateTimer = duration;
            if (telegraphLine != null) telegraphLine.enabled = next == State.Telegraph;
            if (next != State.Approach && next != State.Attack) body.linearVelocity = Vector2.zero;
        }

        protected Vector2 ToPlayer => player.position - transform.position;

        protected void MoveToward(Vector2 direction, float speed)
        {
            body.linearVelocity = direction.normalized * speed;
        }

        protected void DrawTelegraphLine(Vector2 from, Vector2 to)
        {
            if (telegraphLine == null) return;
            telegraphLine.positionCount = 2;
            telegraphLine.SetPosition(0, from);
            telegraphLine.SetPosition(1, to);
        }

        protected virtual void OnDied(Health h)
        {
            body.linearVelocity = Vector2.zero;
            Killed?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
