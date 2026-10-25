using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Heroes;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Player
{
    /// <summary>
    /// 플레이어: 능력치, 체력, 이동, 멈춤 판정, 기본 공격을 묶는다.
    /// 기본 공격은 멈춰야 나가고, 쿨다운은 이동 중에도 흐른다. (Stats.md 1-1)
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private HeroData hero;
        [SerializeField] private FloatingJoystick joystick;
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private LayerMask enemyMask;
        [SerializeField] private LayerMask wallMask;

        [Header("멀티샷 부채꼴 각도")]
        [SerializeField] private float spreadDegrees = 10f;

        private Rigidbody2D _body;
        private ComponentPool<Projectile> _projectiles;
        private TargetSelector _targets;
        private float _attackCooldown;
        private bool _firstShotPending = true;
        private float _lifestealBudget;
        private float _searchDelay;

        /// <summary>표적이 없을 때 다시 찾기까지 (매 프레임 물리 검사를 하지 않게)</summary>
        private const float SearchRetryInterval = 0.1f;

        public HeroData Hero => hero;
        public StatSheet Stats { get; } = new StatSheet();
        public AttackModifiers Attack { get; } = new AttackModifiers();
        public StopDetector Stop { get; } = new StopDetector();
        public Health Health { get; private set; }
        public bool IsStopped => Stop.IsStopped;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;

            Health = GetComponent<Health>();
            ApplyHeroBaseStats();
            Health.Init(Team.Player, Stats.Get(StatType.MaxHp), Stats, invulnerableDuration: 0.5f);
            Stats.Changed += OnStatsChanged;

            _targets = new TargetSelector(enemyMask, wallMask);
            var poolRoot = new GameObject("PlayerProjectiles").transform;
            _projectiles = new ComponentPool<Projectile>(projectilePrefab, poolRoot, prewarm: 32);

            Stop.Moved += () => _firstShotPending = true;
        }

        private void OnDestroy()
        {
            Stats.Changed -= OnStatsChanged;
        }

        private void ApplyHeroBaseStats()
        {
            Stats.SetBase(StatType.MaxHp, hero.maxHp);
            Stats.SetBase(StatType.Attack, hero.attack);
            Stats.SetBase(StatType.AttackSpeed, hero.attackSpeed);
            Stats.SetBase(StatType.MoveSpeed, hero.moveSpeed);
            Stats.SetBase(StatType.Range, hero.range);
            Stats.SetBase(StatType.CritRate, hero.critRate);
            Stats.SetBase(StatType.CritDamage, hero.critDamage);
        }

        private void OnStatsChanged()
        {
            if (Health != null) Health.SetMax(Stats.Get(StatType.MaxHp));
        }

        /// <summary>방을 옮길 때 시작 위치로 (물리 위치까지 같이 옮겨야 다음 프레임에 되돌아가지 않는다).</summary>
        public void Teleport(Vector2 position)
        {
            _body.position = position;
            _body.linearVelocity = Vector2.zero;
            transform.position = position;
        }

        private void Update()
        {
            // 카드 화면 등으로 멈춘 동안에는 쏘지도, 쿨다운이 돌지도 않는다
            if (Health.IsDead || GamePause.IsPaused) return;
            float dt = Time.deltaTime;
            Stop.Tick(ReadInput(), dt);

            // 쿨다운은 이동 중에도 흐른다 → 멈췄다 움직이기를 반복해도 공격속도보다 빨리 쏠 수 없다.
            _attackCooldown -= dt;
            _lifestealBudget = Mathf.Min(_lifestealBudget + dt * Health.Max * 0.03f, Health.Max * 0.03f);

            float regen = Stats.Get(StatType.HpRegen);
            if (regen > 0f) Health.Heal(regen * dt);

            if (Stop.IsStopped) TryBasicAttack();
        }

        private void FixedUpdate()
        {
            if (Health.IsDead)
            {
                _body.linearVelocity = Vector2.zero;
                return;
            }

            _body.linearVelocity = ReadInput() * Stats.Get(StatType.MoveSpeed);
        }

        /// <summary>이동과 멈춤 판정이 같은 데드존을 쓴다 (약하게 밀면서 쏘기 방지, Codex 리뷰).</summary>
        private Vector2 ReadInput()
        {
            Vector2 raw = joystick != null ? joystick.Direction : Vector2.zero;
            return StopDetector.ApplyDeadZone(raw);
        }

        private void TryBasicAttack()
        {
            if (_attackCooldown > 0f) return;
            if (Stop.StoppedTime < hero.firstShotDelay) return;

            _searchDelay -= Time.deltaTime;
            if (_searchDelay > 0f) return;

            Health target = _targets.Find(transform.position, Stats.Get(StatType.Range));
            if (target == null)
            {
                _searchDelay = SearchRetryInterval;
                return;
            }

            Fire(target);
            _attackCooldown = 1f / Stats.Get(StatType.AttackSpeed, StatTarget.BasicAttack);
        }

        private void Fire(Health target)
        {
            int extra = Attack.Get(AttackModifierKind.ExtraProjectiles);
            int count = 1 + extra;
            float perProjectile = 1f - 0.1f * extra; // 멀티샷: 발당 -10% (Skills.md 1장)
            if (_firstShotPending) perProjectile *= 1f + hero.firstShotBonus;
            _firstShotPending = false;

            Vector2 origin = transform.position;
            Vector2 aim = ((Vector2)target.transform.position - origin).normalized;
            float start = -spreadDegrees * (count - 1) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                Vector2 direction = Quaternion.Euler(0f, 0f, start + spreadDegrees * i) * aim;
                DamageInfo damage = DamageCalculator.Roll(Stats, perProjectile, DamageKind.BasicAttack);
                Projectile p = _projectiles.Get(origin, Quaternion.identity);
                p.Launch(direction, hero.projectileSpeed, damage, enemyMask, wallMask,
                    Attack.Get(AttackModifierKind.Pierce), Attack.Get(AttackModifierKind.Ricochet),
                    _projectiles.Release, OnBasicAttackHit);
            }
        }

        /// <summary>흡혈은 기본 공격에만, 초당 최대체력 3%까지 (Stats.md 1장).</summary>
        private void OnBasicAttackHit(DamageInfo info, float dealt)
        {
            float lifesteal = Stats.Get(StatType.Lifesteal, StatTarget.BasicAttack);
            if (lifesteal <= 0f) return;

            float heal = Mathf.Min(dealt * lifesteal, _lifestealBudget);
            _lifestealBudget -= heal;
            Health.Heal(heal);
        }
    }
}
