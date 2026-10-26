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

        /// <summary>공격 예고 중 / 공격 중 (화면에 보이는 예고선 그대로 — 벤치 조종 AI가 사람처럼 보고 피하는 데 쓴다)</summary>
        public bool IsTelegraphing => state == State.Telegraph;
        public bool IsAttacking => state == State.Attack;

        /// <summary>나폴레옹 총알에 밀리는가. 엘리트·보스는 밀리지 않는다</summary>
        public virtual bool CanBeKnockedBack => !IsElite;

        public event Action<EnemyBase> Killed;

        /// <summary>
        /// 판(던전·보스·서바이벌·무한)이 넣어 주는 "적 하나 더 내기" (소환사·분열체·보스 소환).
        /// (프리팹, 자리, 체력 배수, 공격력 배수) → 나온 적 (못 내면 null). 판이 끝나면 비운다.
        /// </summary>
        public static Func<EnemyBase, Vector2, float, float, EnemyBase> ChildSpawner;

        /// <summary>이 적의 체력·공격력 배수 (소환한 졸개가 같은 구간 강도를 따라가게)</summary>
        public float HpMultiplierUsed { get; private set; } = 1f;
        public float AttackMultiplierUsed => attackMultiplier;

        /// <summary>예고 시간 배수 (5구간 강화형은 빨라진다)</summary>
        public float TelegraphScale { get; set; } = 1f;

        private static readonly List<EnemyBase> _alive = new List<EnemyBase>();

        /// <summary>지금 살아 있는 적 (액티브 스킬 표적 고르기용). 물리 검사보다 싸다.</summary>
        public static IReadOnlyList<EnemyBase> Alive => _alive;

        // "도메인 다시 불러오기"를 끈 Play 모드에서도 지난 판의 목록이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _alive.Clear();
            ChildSpawner = null;
        }

        protected virtual void Awake()
        {
            health = GetComponent<Health>();
            body = GetComponent<Rigidbody2D>();
            status = GetComponent<EnemyStatus>();
            if (status == null) status = gameObject.AddComponent<EnemyStatus>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            health.Died += OnDied;
            health.Damaged += (info, dealt) => _lastHitKind = info.Kind;
        }

        /// <summary>마지막으로 맞은 피해 종류 (업적 "멈춤의 달인" = 기본 공격, 판 정리(Enemy 종류)는 처치로 안 센다)</summary>
        private DamageKind _lastHitKind;

        /// <param name="hpMultiplier">구간·방 강화 (Stats.md 8장)</param>
        public virtual void Spawn(Transform target, float hpMultiplier, float atkMultiplier, bool elite)
        {
            player = target;
            HpMultiplierUsed = hpMultiplier;
            TelegraphScale = 1f;
            attackMultiplier = atkMultiplier * (elite ? 1.5f : 1f);
            IsElite = elite;
            health.Init(Team.Enemy, data.maxHp * hpMultiplier * (elite ? 5f : 1f));
            health.IncomingModifier = null; // 풀에서 다시 쓸 때 지난 표식이 남지 않게
            health.Guard = null;
            transform.localScale = Vector3.one * (data.scale > 0f ? data.scale : 1f) * (elite ? 1.5f : 1f);
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

        // ---------------------------------------------------------------- 걷기 (장애물 돌아가기, v0.6 레이아웃)

        private static readonly RaycastHit2D[] SteerHits = new RaycastHit2D[2];
        private static ContactFilter2D _steerFilter;
        private static bool _steerReady;
        private Vector2 _steerWant, _steerDir;
        private float _steerUntil;
        private int _steerSide = 1;

        /// <summary>
        /// 걸어서 다가가기·물러나기: 앞에 기둥·벽이 있으면 옆으로 미끄러져 돌아간다 (길찾기 없이, 0.15초마다 다시 본다).
        /// 돌진·덮치기·밀치기는 MoveToward 그대로 (예고선과 같은 길).
        /// </summary>
        protected void Walk(Vector2 direction, float speed)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                MoveToward(direction, speed);
                return;
            }

            Vector2 want = direction.normalized;
            if (Time.time >= _steerUntil || Vector2.Dot(want, _steerWant) < 0.9f)
            {
                _steerWant = want;
                _steerDir = Steer(want);
                _steerUntil = Time.time + 0.15f;
            }

            MoveToward(_steerDir, speed);
        }

        private Vector2 Steer(Vector2 dir)
        {
            if (!_steerReady)
            {
                _steerFilter = new ContactFilter2D { useLayerMask = true, layerMask = LayerMask.GetMask("Wall") | 1, useTriggers = false };
                _steerReady = true;
            }

            float radius = Mathf.Max(0.2f, 0.4f * transform.localScale.x);
            if (Clear(dir, radius)) return dir;
            for (int k = 1; k <= 3; k++)
            {
                Vector2 a = Rotate(dir, 40f * k * _steerSide);
                if (Clear(a, radius)) return a;
                Vector2 b = Rotate(dir, -40f * k * _steerSide);
                if (Clear(b, radius))
                {
                    _steerSide = -_steerSide; // 그쪽으로 계속 돌아간다 (왔다 갔다 하지 않게)
                    return b;
                }
            }

            return dir;
        }

        private bool Clear(Vector2 dir, float radius) => Physics2D.CircleCast(body.position, radius, dir, _steerFilter, SteerHits, 0.7f) == 0;

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private static int _wallMask = -1;

        /// <summary>from에서 direction으로 maxDistance까지 가되 벽에 닿으면 그 자리 (예고선이 벽 밖·HUD까지 뻗지 않게)</summary>
        protected static Vector2 UntilWall(Vector2 from, Vector2 direction, float maxDistance)
        {
            if (_wallMask < 0) _wallMask = LayerMask.GetMask("Wall");
            RaycastHit2D hit = Physics2D.Raycast(from, direction, maxDistance, _wallMask);
            return hit.collider != null ? hit.point : from + direction * maxDistance;
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
            if (IsElite)
            {
                // 손맛 (조사 A9): 엘리트는 잠깐 멈춤 + 흔들림
                Common.HitStop.Freeze(Common.HitStop.EliteKill);
                Common.CameraShake.Add(Common.CameraShake.EliteKill);
            }

            status.NotifyDeath(transform.position); // 독 구름 안에서 죽었으면 번짐 (맹독 개화)
            _alive.Remove(this);
            body.linearVelocity = Vector2.zero;
            if (_lastHitKind != DamageKind.Enemy) Missions.MissionEvents.EnemyKilled(IsElite, _lastHitKind == DamageKind.BasicAttack);
            Killed?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
