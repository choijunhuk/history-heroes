using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using HistoryHeroes.Heroes;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Player
{
    /// <summary>
    /// 플레이어: 능력치, 체력, 이동, 멈춤 판정, 기본 공격을 묶는다.
    /// 기본 공격은 멈춰야 나가고, 쿨다운은 이동 중에도 흐른다. (Stats.md 1-1)
    /// 영웅마다 기본 공격 모양과 대표 효과가 다르다 (Heroes.md):
    /// - 칭기즈 칸: 화살. 멈추는 순간 바로 쏘고 첫 발 +50%
    /// - 관우: 근접 부채꼴 베기 (맞은 적 전부). 받는 피해 -15%, 체력 50% 아래면 공격력 +20%
    /// - 나폴레옹: 총알이 적을 살짝 밀어냄, 3발마다 작은 포탄 (범위)
    /// - 테슬라: 전기 구체가 맞으면 근처 적에게 2번 튕김
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private HeroData hero;
        [SerializeField] private FloatingJoystick joystick;
        [Tooltip("영웅에게 따로 정한 투사체가 없을 때 (화살)")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private LayerMask enemyMask;
        [SerializeField] private LayerMask wallMask;
        [SerializeField] private SpriteRenderer bodyRenderer;

        [Header("멀티샷 부채꼴 각도")]
        [SerializeField] private float spreadDegrees = 10f;

        [Header("근접 (관우)")]
        [Tooltip("멀티샷(연속 베기) 사이 간격")]
        [SerializeField] private float swingInterval = 0.12f;
        [SerializeField] private Color slashColor = new Color(1f, 0.95f, 0.8f, 0.7f);

        [Header("나폴레옹·테슬라 효과 색")]
        [SerializeField] private Color cannonColor = new Color(1f, 0.6f, 0.2f, 0.55f);
        [SerializeField] private Color chainColor = new Color(0.55f, 0.9f, 1f, 0.9f);

        private Rigidbody2D _body;
        private ComponentPool<Projectile> _projectiles;
        private TargetSelector _targets;
        private float _attackCooldown;
        private bool _firstShotPending = true;
        private float _lifestealBudget;
        private float _searchDelay;
        private int _bulletsFired;
        private float _lastShotCoefficient = 1f;
        private bool _lowHpActive;
        private bool _awake;
        private bool _undyingUsed;
        private float _momentumUntil;
        private bool _momentumActive;
        private readonly object _momentumModifier = new object();

        private readonly object _heroModifiers = new object();
        private readonly object _lowHpModifier = new object();
        private readonly List<Collider2D> _overlap = new List<Collider2D>(32);
        private readonly HashSet<Health> _chainVisited = new HashSet<Health>();
        private ContactFilter2D _enemyFilter;

        /// <summary>표적이 없을 때 다시 찾기까지 (매 프레임 물리 검사를 하지 않게)</summary>
        private const float SearchRetryInterval = 0.1f;

        public HeroData Hero => hero;

        private readonly object _metaModifiers = new object();

        /// <summary>이번 판의 판 밖 성장 (던전 = 지금 계정, 보스 = 기록). 기록을 만들 때 이걸 그대로 고정 저장한다</summary>
        public Records.MetaSnapshot Meta { get; private set; } = new Records.MetaSnapshot();

        /// <summary>
        /// 판 밖 성장 보정값을 갈아 끼운다 (v0.4). BattleBootstrap이 SetHero 다음, Awake 전에 부른다 → 체력이 처음부터 반영된다.
        /// </summary>
        public void ApplyMeta(Records.MetaSnapshot snapshot, HistoryHeroes.Meta.MetaTable table)
        {
            Meta = snapshot ?? new Records.MetaSnapshot();
            Stats.ReplaceFrom(_metaModifiers, HistoryHeroes.Meta.MetaStats.Build(Meta, table, _metaModifiers));
            if (_awake) SetHero(hero); // Awake 뒤라면 별(대표 효과 강화)까지 다시 만든다 (개발·테스트용)
        }
        public StatSheet Stats { get; } = new StatSheet();
        public AttackModifiers Attack { get; } = new AttackModifiers();
        public StopDetector Stop { get; } = new StopDetector();
        public Health Health { get; private set; }
        public bool IsStopped => Stop.IsStopped;
        public LayerMask EnemyMask => enemyMask;
        public LayerMask WallMask => wallMask;

        /// <summary>
        /// 이번 판 영웅을 정한다. 전투 장면이 열릴 때 BattleBootstrap이 Awake보다 먼저 부른다
        /// (던전 = 로비에서 고른 영웅, 보스 = 기록의 영웅).
        /// </summary>
        public void SetHero(HeroData newHero)
        {
            if (newHero == null) return;
            hero = newHero;
            if (bodyRenderer != null) bodyRenderer.color = hero.color;
            ShowHeroModel();
            if (!_awake) return;

            // Awake 뒤에 바뀌면 능력치를 다시 만든다 (개발·테스트용)
            Stats.RemoveAllFrom(_heroModifiers);
            Stats.RemoveAllFrom(_lowHpModifier);
            _lowHpActive = false;
            ApplyHeroBaseStats();
            Health.Init(Team.Player, Stats.Get(StatType.MaxHp), Stats, invulnerableDuration: 0.5f);
            CreateProjectilePool();
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _enemyFilter = new ContactFilter2D { useLayerMask = true, layerMask = enemyMask, useTriggers = true };

            Health = GetComponent<Health>();
            ApplyHeroBaseStats();
            Health.Init(Team.Player, Stats.Get(StatType.MaxHp), Stats, invulnerableDuration: 0.5f);
            Stats.Changed += OnStatsChanged;

            _targets = new TargetSelector(enemyMask, wallMask);
            CreateProjectilePool();
            if (bodyRenderer != null) bodyRenderer.color = hero.color;
            ShowHeroModel();

            Stop.Moved += () => _firstShotPending = true;
            Stop.Stopped += () => _momentumUntil = Time.time + MomentumSeconds;
            Health.PreventDeath = TryUndying;
            _awake = true;
        }

        // ---------------------------------------------------------------- 3D 그림 (Art3D-Plan 4번)

        private Combat.Visual3D _visual;
        private bool _visualLooked;

        /// <summary>3D 모델 (없으면 임시 도형 그대로)</summary>
        public Combat.Visual3D Visual
        {
            get
            {
                if (!_visualLooked)
                {
                    _visual = GetComponent<Combat.Visual3D>();
                    _visualLooked = true;
                }

                return _visual;
            }
        }

        /// <summary>영웅 모델로 바꾼다. 모델이 없는 영웅이면 이전 모델을 내려놓고 임시 도형을 다시 보인다 (Codex 3D 코드 리뷰 2번)</summary>
        private void ShowHeroModel()
        {
            if (Visual != null && hero != null) Visual.SetModel(hero.model3D, hero.modelHeight);
        }

        private void PlayAttack(Vector2 aim)
        {
            if (Visual != null) Visual.Attack(aim);
        }

        private void CreateProjectilePool()
        {
            Projectile prefab = hero.projectilePrefab != null ? hero.projectilePrefab : projectilePrefab;
            var poolRoot = new GameObject("PlayerProjectiles").transform;
            _projectiles = new ComponentPool<Projectile>(prefab, poolRoot, prewarm: 32);
        }

        private void OnDestroy()
        {
            Stats.Changed -= OnStatsChanged;
        }

        /// <summary>별(각성)까지 반영한 대표 효과 값. 판 밖 성장(Meta)의 별을 쓴다</summary>
        private Heroes.HeroSignature _sig;
        private bool _firstShotThisShot;

        private void ApplyHeroBaseStats()
        {
            _sig = Heroes.HeroSignature.For(hero, Meta.heroStars);
            Stats.SetBase(StatType.MaxHp, hero.maxHp);
            Stats.SetBase(StatType.Attack, hero.attack);
            Stats.SetBase(StatType.AttackSpeed, hero.attackSpeed);
            Stats.SetBase(StatType.MoveSpeed, hero.moveSpeed);
            Stats.SetBase(StatType.Range, hero.range);
            Stats.SetBase(StatType.CritRate, hero.critRate);
            Stats.SetBase(StatType.CritDamage, hero.critDamage);

            // 대표 효과 중 늘 켜져 있는 것 (관우: 받는 피해 -15%)
            if (_sig.DamageReduction > 0f)
            {
                Stats.Add(new StatModifier(StatType.DamageReduction, ModifierKind.Flat, _sig.DamageReduction, ModifierSource.Hero, _heroModifiers));
            }
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

            float regen = Stats.Get(StatType.HpRegen) * Health.Max + Stats.Get(StatType.HpRegenFlat); // 비율(패시브) + 고정(수련)
            if (regen > 0f) Health.Heal(regen * dt);

            UpdateLowHpBonus();
            UpdateMomentum();
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

        private const float MomentumSeconds = 3f;

        /// <summary>영웅의 기세 (전설): 멈춘 뒤 3초간 기본 공격 속도 +50%, 기본 공격 피해 +30% (Skills.md 1장)</summary>
        private void UpdateMomentum()
        {
            bool active = Attack.Get(AttackModifierKind.Momentum) > 0 && Stop.IsStopped && Time.time < _momentumUntil;
            if (active == _momentumActive) return;
            _momentumActive = active;
            if (active)
            {
                Stats.Add(new StatModifier(StatType.AttackSpeed, ModifierKind.Percent, 0.5f, ModifierSource.Skill, _momentumModifier, StatTarget.BasicAttack));
                Stats.Add(new StatModifier(StatType.Attack, ModifierKind.Percent, 0.3f, ModifierSource.Skill, _momentumModifier, StatTarget.BasicAttack));
            }
            else
            {
                Stats.RemoveAllFrom(_momentumModifier);
            }
        }

        /// <summary>불굴 (전설): 판에서 한 번, 죽는 순간 체력 50%로 부활</summary>
        private bool TryUndying()
        {
            if (_undyingUsed || Attack.Get(AttackModifierKind.Undying) <= 0) return false;
            _undyingUsed = true;
            Health.Revive(Health.Max * 0.5f);
            AttackFx.Burst(transform.position, 1.6f, new Color(1f, 0.85f, 0.3f, 0.7f), 0.5f);
            return true;
        }

        public bool UndyingUsed => _undyingUsed;

        /// <summary>관우 의리의 투지: 체력이 절반 아래면 공격력 +20%</summary>
        private void UpdateLowHpBonus()
        {
            if (_sig.LowHpAttackBonus <= 0f) return;
            bool low = Health.Current < Health.Max * hero.lowHpThreshold;
            if (low == _lowHpActive) return;
            _lowHpActive = low;
            if (low) Stats.Add(new StatModifier(StatType.Attack, ModifierKind.Percent, _sig.LowHpAttackBonus, ModifierSource.Hero, _lowHpModifier));
            else Stats.RemoveAllFrom(_lowHpModifier);
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

            if (hero.attackKind == BasicAttackKind.MeleeArc) Slash(target);
            else Fire(target);

            // 나폴레옹 포병의 눈: 쏜 총알 3발마다 작은 포탄 (멀티샷이면 더 자주)
            if (_sig.CannonEvery > 0)
            {
                while (_bulletsFired >= _sig.CannonEvery)
                {
                    _bulletsFired -= _sig.CannonEvery;
                    FireCannon(target.transform.position);
                }
            }

            _attackCooldown = 1f / Stats.Get(StatType.AttackSpeed, StatTarget.BasicAttack);
        }

        /// <summary>멀티샷 수에 따른 한 발(한 번 베기)의 피해 배수 + 첫 발 보너스</summary>
        private float NextShotCoefficient(int extra)
        {
            float coefficient = 1f - 0.1f * extra; // 멀티샷: 발당 -10% (Skills.md 1장)
            if (_firstShotPending) coefficient *= 1f + _sig.FirstShotBonus;
            _firstShotThisShot = _firstShotPending;
            _firstShotPending = false;
            return coefficient;
        }

        // ---------------------------------------------------------------- 투사체 (궁수·총잡이·법사)

        private void Fire(Health target)
        {
            Audio.AudioManager.Play("shoot", 0.35f, 0.1f);
            int extra = Attack.Get(AttackModifierKind.ExtraProjectiles);
            int count = 1 + extra;
            float perProjectile = NextShotCoefficient(extra);
            _lastShotCoefficient = perProjectile;
            _bulletsFired += count;

            Vector2 origin = transform.position;
            Vector2 aim = ((Vector2)target.transform.position - origin).normalized;
            PlayAttack(aim);
            float start = -spreadDegrees * (count - 1) * 0.5f;
            bool needsTarget = hero.knockback > 0f || _sig.ChainBounces > 0;

            for (int i = 0; i < count; i++)
            {
                Vector2 direction = Quaternion.Euler(0f, 0f, start + spreadDegrees * i) * aim;
                DamageInfo damage = DamageCalculator.Roll(Stats, perProjectile, DamageKind.BasicAttack);
                Projectile p = _projectiles.Get(origin, Quaternion.identity);
                int pierce = Attack.Get(AttackModifierKind.Pierce) + (_sig.FirstShotPierce && _firstShotThisShot ? 1 : 0); // 칭기즈 칸 ★5: 첫 발 관통
                p.Launch(direction, hero.projectileSpeed, damage, enemyMask, wallMask,
                    pierce, Attack.Get(AttackModifierKind.Ricochet),
                    _projectiles.Release, OnBasicAttackHit, onHitTarget: needsTarget ? OnProjectileHitTarget : null);
            }
        }

        /// <summary>나폴레옹 밀어내기, 테슬라 튕김</summary>
        private void OnProjectileHitTarget(Health target, Vector2 point)
        {
            if (hero.knockback > 0f) Knockback(target, (Vector2)target.transform.position - (Vector2)transform.position, hero.knockback);
            if (_sig.ChainBounces > 0) Chain(target, _lastShotCoefficient);
        }

        private static void Knockback(Health target, Vector2 direction, float distance)
        {
            if (!target.TryGetComponent(out EnemyBase enemy) || !enemy.CanBeKnockedBack) return;
            if (target.TryGetComponent(out Rigidbody2D rb)) rb.MovePosition(rb.position + direction.normalized * distance);
        }

        /// <summary>테슬라 교류: 맞은 적에서 가장 가까운 다른 적에게 튕긴다 (튕길 때마다 피해 -30%)</summary>
        private void Chain(Health first, float coefficient)
        {
            _chainVisited.Clear();
            _chainVisited.Add(first);
            Health current = first;
            float radiusSqr = hero.chainRadius * hero.chainRadius;

            for (int bounce = 0; bounce < _sig.ChainBounces; bounce++)
            {
                Vector2 from = current.transform.position;
                Health next = null;
                float best = radiusSqr;
                IReadOnlyList<EnemyBase> alive = EnemyBase.Alive;
                for (int i = 0; i < alive.Count; i++)
                {
                    Health h = alive[i].Health;
                    if (h == null || h.IsDead || _chainVisited.Contains(h)) continue;
                    float d = ((Vector2)h.transform.position - from).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        next = h;
                    }
                }

                if (next == null) return;
                coefficient *= 1f - _sig.ChainFalloff;
                AttackFx.Line(from, next.transform.position, 0.09f, chainColor);
                DamageInfo damage = DamageCalculator.Roll(Stats, coefficient, DamageKind.BasicAttack);
                if (next.TryTakeDamage(damage, out float dealt)) OnBasicAttackHit(damage, dealt);
                _chainVisited.Add(next);
                current = next;
            }
        }

        /// <summary>나폴레옹: 표적 자리에 작은 포탄 (범위 피해, 기본 공격으로 침)</summary>
        private void FireCannon(Vector2 point)
        {
            AttackFx.Burst(point, _sig.CannonRadius, cannonColor);
            _overlap.Clear();
            Physics2D.OverlapCircle(point, _sig.CannonRadius, _enemyFilter, _overlap);
            for (int i = 0; i < _overlap.Count; i++)
            {
                if (!_overlap[i].TryGetComponent(out Health h) || h.IsDead) continue;
                DamageInfo damage = DamageCalculator.Roll(Stats, hero.cannonCoefficient, DamageKind.BasicAttack, source: "sig_" + hero.id);
                if (h.TryTakeDamage(damage, out float dealt)) OnBasicAttackHit(damage, dealt);
            }
        }

        // ---------------------------------------------------------------- 근접 베기 (전사)

        /// <summary>
        /// 관우 기본 공격: 표적 쪽 부채꼴(120°) 안의 적 전부. 투사체 패시브는 근접식으로 바뀐다 (Skills.md 1-1):
        /// 멀티샷 = 베기 1회 추가 (회당 -10%), 관통 = 각도 160°, 벽 튕김 = 베기마다 검기 1발 (공격력 40%, 벽에 튕김)
        /// </summary>
        private void Slash(Health target)
        {
            Audio.AudioManager.Play("shoot", 0.4f, 0.15f);
            int extra = Attack.Get(AttackModifierKind.ExtraProjectiles);
            float coefficient = NextShotCoefficient(extra);
            float arc = hero.meleeArcDegrees + (Attack.Get(AttackModifierKind.Pierce) > 0 ? 40f : 0f);
            int waveBounces = Attack.Get(AttackModifierKind.Ricochet);
            StartCoroutine(Swings(target, 1 + extra, coefficient, arc, waveBounces));
        }

        private IEnumerator Swings(Health target, int swings, float coefficient, float arc, int waveBounces)
        {
            for (int s = 0; s < swings; s++)
            {
                // 기본 공격은 멈췄을 때만: 연속 베기 도중에 움직이면 남은 베기는 취소 (GameDesign 3-3)
                if (Health.IsDead || (s > 0 && !Stop.IsStopped)) yield break;
                Vector2 origin = transform.position;
                Vector2 aim = target != null && !target.IsDead ? ((Vector2)target.transform.position - origin) : (Vector2)transform.up;
                if (aim.sqrMagnitude < 0.0001f) aim = Vector2.up;
                aim.Normalize();

                SlashOnce(origin, aim, coefficient, arc);
                if (waveBounces > 0) FireWave(origin, aim, waveBounces);
                if (s < swings - 1) yield return new WaitForSeconds(swingInterval);
            }
        }

        private void SlashOnce(Vector2 origin, Vector2 aim, float coefficient, float arc)
        {
            float reach = Stats.Get(StatType.Range) + 0.4f;
            AttackFx.Arc(origin, aim, reach, arc, slashColor);
            PlayAttack(aim);

            _overlap.Clear();
            Physics2D.OverlapCircle(origin, reach, _enemyFilter, _overlap);
            float half = arc * 0.5f;
            for (int i = 0; i < _overlap.Count; i++)
            {
                if (!_overlap[i].TryGetComponent(out Health h) || h.IsDead) continue;
                Vector2 to = (Vector2)h.transform.position - origin;
                // 몸에 붙은 적은 각도와 상관없이 맞는다
                if (to.sqrMagnitude > 0.5f && Vector2.Angle(aim, to) > half) continue;

                DamageInfo damage = DamageCalculator.Roll(Stats, coefficient, DamageKind.BasicAttack);
                damage.Direction = to.sqrMagnitude > 0.0001f ? to.normalized : aim;
                damage.HasDirection = true;
                if (h.TryTakeDamage(damage, out float dealt)) OnBasicAttackHit(damage, dealt);
            }

            // 부서지는 장애물 (Modes 2-3): 근접도 깎는다
            _overlap.Clear();
            Physics2D.OverlapCircle(origin, reach, new ContactFilter2D { useLayerMask = true, layerMask = wallMask }, _overlap);
            for (int i = 0; i < _overlap.Count; i++)
            {
                if (!_overlap[i].TryGetComponent(out Dungeon.Breakable b)) continue;
                Vector2 to = (Vector2)b.transform.position - origin;
                if (to.sqrMagnitude > 0.5f && Vector2.Angle(aim, to) > half) continue;
                b.Hit();
            }
        }

        /// <summary>근접 + 벽 튕김: 검기 (공격력 40%) 한 발이 날아가 벽에 튕긴다</summary>
        private void FireWave(Vector2 origin, Vector2 aim, int bounces)
        {
            DamageInfo damage = DamageCalculator.Roll(Stats, hero.meleeWaveCoefficient, DamageKind.BasicAttack);
            Projectile p = _projectiles.Get(origin, Quaternion.identity);
            p.Launch(aim, 12f, damage, enemyMask, wallMask, 0, bounces, _projectiles.Release, OnBasicAttackHit);
        }

        // ---------------------------------------------------------------- 흡혈

        /// <summary>흡혈은 기본 공격에만, 초당 최대체력 3%까지 (Stats.md 1장).</summary>
        private void OnBasicAttackHit(DamageInfo info, float dealt)
        {
            float lifesteal = Stats.Get(StatType.Lifesteal, StatTarget.BasicAttack);
            if (lifesteal <= 0f) return;

            float heal = Mathf.Min(dealt * lifesteal, _lifestealBudget);
            _lifestealBudget -= heal;
            Health.Heal(heal);
        }

        // ---------------------------------------------------------------- 스킬이 쓰는 투사체

        /// <summary>액티브 스킬(만궁 연사 등)이 플레이어의 투사체를 빌려 쏜다. owner = 쏜 스킬 (스킬이 사라지면 같이 거둔다)</summary>
        public void LaunchSkillProjectile(Vector2 origin, Vector2 direction, float speed, DamageInfo damage, int pierce = 0,
            System.Action<Health, Vector2> onHitTarget = null, object owner = null)
        {
            Projectile p = _projectiles.Get(origin, Quaternion.identity);
            Track(p, owner, _projectiles);
            p.Launch(direction, speed, damage, enemyMask, wallMask, pierce, 0, ReleaseFor(_projectiles), null, 0.2f, onHitTarget);
        }

        /// <summary>다른 투사체 프리팹으로 쏜다 (얼음 파편 등, 프리팹마다 풀을 따로 둔다)</summary>
        public void LaunchSkillProjectile(Projectile prefab, Vector2 origin, Vector2 direction, float speed, DamageInfo damage, int pierce,
            System.Action<Health, Vector2> onHitTarget, object owner = null)
        {
            if (!_extraPools.TryGetValue(prefab, out ComponentPool<Projectile> pool))
            {
                pool = new ComponentPool<Projectile>(prefab, new GameObject(prefab.name + " Pool").transform, prewarm: 8);
                _extraPools[prefab] = pool;
            }

            Projectile p = pool.Get(origin, Quaternion.identity);
            Track(p, owner, pool);
            p.Launch(direction, speed, damage, enemyMask, wallMask, pierce, 0, ReleaseFor(pool), null, 0f, onHitTarget);
        }

        /// <summary>스킬이 조합 재료로 사라지거나 기록을 다시 불러올 때, 그 스킬이 쏜 투사체를 모두 거둔다 (12-2 정리 계약)</summary>
        public void CancelSkillProjectiles(object owner)
        {
            if (owner == null) return;
            _cancelBuffer.Clear();
            foreach (KeyValuePair<Projectile, object> pair in _skillShots)
            {
                if (pair.Value == owner) _cancelBuffer.Add(pair.Key);
            }

            foreach (Projectile p in _cancelBuffer) p.Cancel();
        }

        private void Track(Projectile p, object owner, ComponentPool<Projectile> pool)
        {
            if (owner != null) _skillShots[p] = owner;
            else _skillShots.Remove(p);
        }

        private System.Action<Projectile> ReleaseFor(ComponentPool<Projectile> pool)
        {
            if (!_releasers.TryGetValue(pool, out System.Action<Projectile> release))
            {
                release = p =>
                {
                    _skillShots.Remove(p);
                    pool.Release(p);
                };
                _releasers[pool] = release;
            }

            return release;
        }

        private readonly Dictionary<Projectile, ComponentPool<Projectile>> _extraPools = new Dictionary<Projectile, ComponentPool<Projectile>>();
        private readonly Dictionary<Projectile, object> _skillShots = new Dictionary<Projectile, object>();
        private readonly Dictionary<ComponentPool<Projectile>, System.Action<Projectile>> _releasers = new Dictionary<ComponentPool<Projectile>, System.Action<Projectile>>();
        private readonly List<Projectile> _cancelBuffer = new List<Projectile>();
    }
}
