using System;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
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
        protected EnemyStatus status;
        protected Transform player;
        protected State state;
        protected float stateTimer;
        protected float attackMultiplier = 1f;

        public EnemyData Data => data;
        public Health Health => health;
        public EnemyStatus Status => status;
        public bool IsElite { get; private set; }
        public float Damage => data.damage * attackMultiplier;

        /// <summary>나폴레옹 총알에 밀리는가. 엘리트·보스는 밀리지 않는다</summary>
        public virtual bool CanBeKnockedBack => !IsElite;

        public event Action<EnemyBase> Killed;

        private static readonly List<EnemyBase> _alive = new List<EnemyBase>();

        /// <summary>지금 살아 있는 적 (액티브 스킬 표적 고르기용). 물리 검사보다 싸다.</summary>
        public static IReadOnlyList<EnemyBase> Alive => _alive;

        // "도메인 다시 불러오기"를 끈 Play 모드에서도 지난 판의 목록이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _alive.Clear();

        protected virtual void Awake()
        {
            health = GetComponent<Health>();
            body = GetComponent<Rigidbody2D>();
            status = GetComponent<EnemyStatus>();
            if (status == null) status = gameObject.AddComponent<EnemyStatus>();
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
            status.Clear();
            if (!_alive.Contains(this)) _alive.Add(this);
            Enter(State.Approach);
        }

        protected virtual void Update()
        {
            if (player == null || health.IsDead || GamePause.IsPaused) return;

            // 빙결: 움직이지도, 공격 시간이 흐르지도 않는다 (예고 중이던 공격도 멈춤)
            if (status.IsFrozen)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

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
            // 끌려가는 중이면 스스로 움직이지 않는다 (회오리·눈보라). 둔화는 속도에 곱한다
            body.linearVelocity = status.IsPulled ? Vector2.zero : direction.normalized * speed * status.SpeedMultiplier;
        }

        protected void DrawTelegraphLine(Vector2 from, Vector2 to)
        {
            if (telegraphLine == null) return;
            telegraphLine.positionCount = 2;
            telegraphLine.SetPosition(0, from);
            telegraphLine.SetPosition(1, to);
        }

        protected virtual void OnDisable()
        {
            _alive.Remove(this);
        }

        protected virtual void OnDied(Health h)
        {
            status.NotifyDeath(transform.position); // 독 구름 안에서 죽었으면 번짐 (맹독 개화)
            _alive.Remove(this);
            body.linearVelocity = Vector2.zero;
            Killed?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
