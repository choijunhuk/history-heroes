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
        private readonly List<EnemyBase> _thunderHits = new List<EnemyBase>(8);
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
            ClearSignatureState();
            Stats.RemoveAllFrom(_heroModifiers);
            Stats.RemoveAllFrom(_lowHpModifier);
            _lowHpActive = false;
            ApplyHeroBaseStats();
            Health.Init(Team.Player, Stats.Get(StatType.MaxHp), Stats, invulnerableDuration: 0.5f);
            Health.Guard = BlockAthenaProjectile;
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
            Health.Guard = BlockAthenaProjectile;
            Stats.Changed += OnStatsChanged;

            _targets = new TargetSelector(enemyMask, wallMask);
            HeroSignatureHud.Create(this); // 머리 위 대표 효과 표시 (새 영웅 화면 점검 2번)
            CreateProjectilePool();
            if (bodyRenderer != null) bodyRenderer.color = hero.color;
            ShowHeroModel();

            Stop.Moved += () => _firstShotPending = true;
            Stop.Moved += _thunder.Moved;
            Stop.Moved += _windSlash.Moved;
            Stop.Moved += _athenaShield.CancelPreparation;
            Stop.Moved += ResetGuardian;
            Stop.Stopped += () => _momentumUntil = Time.time + MomentumSeconds;
            Health.Damaged += OnDamagedIronclad; // 이순신 철갑
            Stop.Moved += OnMovedCharge; // 이성계 백발백중: 움직이기 시작하면 모은 화살을 놓는다
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
            if (Visual == null || hero == null) return;
            Visual.SetModel(hero.model3D, hero.modelHeight);
            Visual.SetOutline(Skills.SkillArt.Get != null ? Skills.SkillArt.Get.heroOutline : null); // 밝은 외곽선: 적 무리 속에서도 영웅이 먼저 보이게
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
            ResetStopSignatures();
            Stats.Changed -= OnStatsChanged;
            if (_sig.WindRadius > 0f) Enemy.EnemyProjectiles.ClearSlowZone();
        }

        /// <summary>
        /// 새 영웅 대표 효과의 남은 상태를 비운다 (영웅 바꾸기 · 코드 검토 9번): 철갑 방어 보정과 쌓인 피해, 모으던 화살과 신궁 표식,
        /// 부품과 비행 기계, 동남풍 느림 구역
        /// </summary>
        private void ClearSignatureState()
        {
            ResetStopSignatures();
            Stats.RemoveAllFrom(_ironOwner);
            _ironOn = false;
            _ironStore = 0f;
            _charge = 0f;
            _chargeStage = 0;
            _marks.Clear();
            _parts = 0;
            _partTimer = 0f;
            foreach (FlyingMachine m in _machines)
            {
                if (m != null) Destroy(m.gameObject);
            }

            _machines.Clear();
            Enemy.EnemyProjectiles.ClearSlowZone();
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
            ResetStopSignatures();
            _body.position = position;
            _body.linearVelocity = Vector2.zero;
            transform.position = position;
        }

        private void Update()
        {
            // 카드 화면 등으로 멈춘 동안에는 쏘지도, 쿨다운이 돌지도 않는다
            if (Health.IsDead || GamePause.IsPaused)
            {
                if (Health.IsDead) ResetStopSignatures();
                if (Health.IsDead && _sig.WindRadius > 0f) Enemy.EnemyProjectiles.ClearSlowZone(); // 동남풍은 살아 있을 때만
                return;
            }

            float dt = Time.deltaTime;
            Stop.Tick(ReadInput(), dt);
            _observation.Tick(dt, Stop.IsStopped, hero);
            _reclaim.Tick(dt, Stop.IsStopped, hero);
            _athenaShield.Tick(dt, Stop.IsStopped, Time.time, _sig.ShieldPrepareSeconds);
            if (!Stop.IsStopped) ResetGuardian();

            // 쿨다운은 이동 중에도 흐른다 → 멈췄다 움직이기를 반복해도 공격속도보다 빨리 쏠 수 없다.
            _attackCooldown -= dt;
            _lifestealBudget = Mathf.Min(_lifestealBudget + dt * Health.Max * 0.03f, Health.Max * 0.03f);

            float regen = Stats.Get(StatType.HpRegen) * Health.Max + Stats.Get(StatType.HpRegenFlat); // 비율(패시브) + 고정(수련)
            if (regen > 0f) Health.Heal(regen * dt);

            UpdateLowHpBonus();
            UpdateMomentum();
            UpdateIronclad(dt);
            UpdateCharge(dt);
            UpdateWorkbench(dt);
            UpdateBanner(dt);
            // 제갈량 동남풍: 멈춰 있는 동안 둘레 적 탄이 느려진다
            if (_sig.WindRadius > 0f)
            {
                if (Stop.IsStopped) Enemy.EnemyProjectiles.SetSlowZone(transform.position, _sig.WindRadius, 1f - _sig.WindSlow);
                else Enemy.EnemyProjectiles.ClearSlowZone();
            }
            if (Stop.IsStopped) TryBasicAttack();
        }

        private void FixedUpdate()
        {
            if (Health.IsDead)
            {
                _body.linearVelocity = Vector2.zero;
                return;
            }

            if (Time.time < _rootedUntil)
            {
                _body.linearVelocity = Vector2.zero; // 기절·묶임 (이동 속도 하한 0.5와 상관없이 멈춘다)
                return;
            }

            _body.linearVelocity = ReadInput() * Stats.Get(StatType.MoveSpeed);
        }

        private float _rootedUntil;

        /// <summary>seconds 동안 못 움직인다 (기믹 "거울 분신" 기절 · 덩굴). 더 긴 쪽이 남는다</summary>
        public void Root(float seconds) => _rootedUntil = Mathf.Max(_rootedUntil, Time.time + seconds);

        public bool IsRooted => Time.time < _rootedUntil;

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

        // ---------------------------------------------------------------- 이순신 철갑

        private readonly object _ironOwner = new object();
        private bool _ironOn;
        private float _ironStore;
        private float _ironDecay;

        /// <summary>쌓인 철갑 피해 (다음 기본 공격에 더한다, 화면 표시용)</summary>
        public float IroncladStore => _ironStore;

        /// <summary>쌓을 수 있는 철갑 피해 상한 (철갑이 없는 영웅은 0)</summary>
        public float IroncladCap => _sig.StopGuard > 0f ? Stats.Get(StatType.Attack) * _sig.StopGuardCap : 0f;

        /// <summary>제갈량 동남풍 반경 (없으면 0)</summary>
        public float WindRadius => _sig.WindRadius;

        /// <summary>멈춰 있으면 받는 피해 -25%, 움직이면 쌓인 것이 2초에 걸쳐 사라진다</summary>
        private void UpdateIronclad(float dt)
        {
            if (_sig.StopGuard <= 0f) return;
            bool stopped = Stop.IsStopped;
            if (stopped != _ironOn)
            {
                _ironOn = stopped;
                Stats.RemoveAllFrom(_ironOwner);
                if (stopped) Stats.Add(new StatModifier(StatType.DamageReduction, ModifierKind.Flat, _sig.StopGuard, ModifierSource.Hero, _ironOwner));
                else _ironDecay = _ironStore / Mathf.Max(0.1f, hero.stopGuardDecay);
            }

            if (!stopped && _ironStore > 0f) _ironStore = Mathf.Max(0f, _ironStore - _ironDecay * dt);
        }

        /// <summary>
        /// 철갑이 막은 피해의 절반을 쌓는다 (상한 공격력 200%). 막은 피해 = 감소 전 피해 × (철갑 포함 감소 − 철갑 뺀 감소):
        /// 다른 피해 감소·감소 상한 60%·받는 피해 증가까지 같이 계산하고, 보호막이 받아 낸 피해도 철갑이 줄인 몫은 쌓는다 (코드 검토 8번)
        /// </summary>
        private void OnDamagedIronclad(DamageInfo info, float dealt)
        {
            if (!_ironOn || _sig.StopGuard <= 0f) return;
            float share = Stats.Get(StatType.DamageReduction) - Stats.GetWithout(StatType.DamageReduction, _ironOwner);
            float blocked = Health.LastBeforeReduction * share;
            if (blocked <= 0f) return;
            float cap = Stats.Get(StatType.Attack) * _sig.StopGuardCap;
            _ironStore = Mathf.Min(cap, _ironStore + blocked * hero.stopGuardStore);
        }

        /// <summary>쌓인 것을 이번 한 발에 싣고 비운다 ("맞으며 버티다 크게 한 방")</summary>
        private float TakeIroncladStore()
        {
            if (_ironStore <= 0f) return 0f;
            float store = _ironStore;
            _ironStore = 0f;
            AttackFx.Burst(transform.position, 0.9f, new Color(1f, 0.6f, 0.2f, 0.8f), 0.12f);
            return store;
        }

        // ---------------------------------------------------------------- 잔 다르크 성녀의 깃발

        private readonly JeanneBannerState _banner = new JeanneBannerState();
        private readonly object _bannerOwner = new object();
        private bool _bannerBuffActive;
        private JeanneBannerView _bannerView;

        public bool HasBanner => _banner.IsPlanted;
        public Vector2 BannerCenter => _banner.Center;
        public bool BannerBuffActive => _bannerBuffActive;

        private void UpdateBanner(float dt)
        {
            if (hero.bannerDeploySeconds <= 0f) return;
            bool active = _banner.Tick(dt, Stop.IsStopped, transform.position, hero, _sig);
            if (active != _bannerBuffActive)
            {
                _bannerBuffActive = active;
                Stats.RemoveAllFrom(_bannerOwner);
                if (active)
                {
                    // 공격력 전체를 올리면 이동 중 액티브까지 강해지므로 기본 공격으로 한정한다.
                    Stats.Add(new StatModifier(StatType.Attack, ModifierKind.Percent, _sig.BannerAttackBonus,
                        ModifierSource.Hero, _bannerOwner, StatTarget.BasicAttack));
                    Stats.Add(new StatModifier(StatType.DamageReduction, ModifierKind.Flat, hero.bannerDamageReduction,
                        ModifierSource.Hero, _bannerOwner));
                }
            }

            if (_banner.IsPlanted && _bannerView == null) _bannerView = JeanneBannerView.Create(this);
            if (_bannerView != null) _bannerView.gameObject.SetActive(_banner.IsPlanted);
        }

        private void ClearBanner()
        {
            _banner.Reset();
            _bannerBuffActive = false;
            Stats.RemoveAllFrom(_bannerOwner);
            if (_bannerView != null) _bannerView.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            ResetStopSignatures();
        }

        // ---------------------------------------------------------------- 선덕여왕 별자리 관측

        private readonly SeondeokObservationState _observation = new SeondeokObservationState();
        public float ObservationProgress => _observation.Progress;
        public bool ObservationReady => _observation.IsReady;

        // ---------------------------------------------------------------- 부디카 되찾는 자리

        private readonly BoudicaReclaimState _reclaim = new BoudicaReclaimState();
        public float ReclaimProgress => _reclaim.Progress;
        public bool ReclaimReady => _reclaim.IsReady;
        public float ReclaimCooldownRemaining => _reclaim.CooldownRemaining;

        private readonly ThorThunderState _thunder = new ThorThunderState();
        public int ThunderCount => _thunder.Count;
        private readonly SusanooWindState _windSlash = new SusanooWindState();
        public int WindSlashCount => _windSlash.Count;
        private readonly AthenaShieldState _athenaShield = new AthenaShieldState();
        public bool AthenaShieldReady => _athenaShield.IsReady;
        public float AthenaShieldProgress => _athenaShield.Progress;
        public float AthenaShieldCooldownRemaining => _athenaShield.CooldownRemaining(Time.time);
        private readonly AthenaOwlState _owlMarks = new AthenaOwlState();
        private readonly AnubisMarkState _mark = new AnubisMarkState();
        public int MarkCount => _mark.Hits;
        public bool MarkReady => _mark.IsReady(Time.time, _sig.MarkHits);

        // ---------------------------------------------------------------- 클레오파트라 기록의 수호상

        private readonly CleopatraGuardianState _guardian = new CleopatraGuardianState();
        private GameObject _guardianVisual;
        public int GuardianCount => _guardian.Count;
        public bool GuardianReady => _guardian.IsReady;

        /// <summary>
        /// 멈춤에 묶인 대표 효과 상태를 비운다 (잔 다르크 깃발 · 선덕여왕 관측 · 부디카 되찾는 자리 · 토르 천둥 · 아누비스 저울 · 클레오파트라 수호상).
        /// 방 이동 · 영웅 변경 · 사망 · 꺼짐 · 파괴가 모두 여기로 온다 — 새 영웅의 상태는 여기 한 곳에만 더한다
        /// (예전엔 같은 6줄이 다섯 군데 있어서 하나를 빠뜨리기 쉬웠다, 2026-10-29 Codex 리뷰).
        /// </summary>
        private void ResetStopSignatures()
        {
            ClearBanner();
            _observation.Reset();
            _reclaim.Reset();
            _thunder.Moved();
            _windSlash.Moved();
            _athenaShield.CancelPreparation();
            _owlMarks.Clear();
            _mark.Reset();
            ResetGuardian();
        }

        private void ResetGuardian()
        {
            _guardian.Reset();
            if (_guardianVisual != null) Destroy(_guardianVisual);
            _guardianVisual = null;
        }

        private void ShowGuardian()
        {
            if (!_guardian.IsReady || _guardianVisual != null) return;
            _guardianVisual = new GameObject("RecordGuardian");
            _guardianVisual.transform.SetParent(transform, false);
            _guardianVisual.transform.localPosition = new Vector3(0.75f, -0.25f, 0f);
            if (Skills.SkillArt.Attach(hero.helperModel, _guardianVisual.transform, 0.5f) == null)
            {
                var sprite = _guardianVisual.AddComponent<SpriteRenderer>();
                sprite.sprite = PlaceholderSprites.RimCircle;
                sprite.color = hero.color;
                _guardianVisual.transform.localScale = Vector3.one * 0.55f;
            }
        }

        /// <summary>표적을 찾은 발사 시작에서 한 번만 부른다. 명중·멀티샷 콜백은 준비 횟수를 늘리지 않는다.</summary>
        private void JoinGuardianAttack(Health target)
        {
            bool joins = _guardian.RecordAttack(Stop.IsStopped, true, hero);
            if (!joins)
            {
                ShowGuardian();
                return;
            }

            Vector2 origin = _guardianVisual != null ? _guardianVisual.transform.position : transform.position;
            ResetGuardian();
            Health nearby = null;
            Vector2 center = target.transform.position;
            if (_sig.GuardianTargets > 1)
            {
                float closest = hero.guardianNearbyRadius * hero.guardianNearbyRadius;
                foreach (EnemyBase enemy in EnemyBase.Alive)
                {
                    Health candidate = enemy != null ? enemy.Health : null;
                    if (candidate == null || candidate == target || candidate.IsDead) continue;
                    Vector2 to = (Vector2)candidate.transform.position - center;
                    float distance = to.sqrMagnitude;
                    if (distance > closest) continue;
                    if (Physics2D.Raycast(center, to.normalized, to.magnitude, wallMask).collider != null) continue;
                    nearby = candidate;
                    closest = distance;
                }
            }

            HitWithGuardian(target, origin);
            if (nearby != null && !nearby.IsDead) HitWithGuardian(nearby, origin);
        }

        private void HitWithGuardian(Health target, Vector2 origin)
        {
            AttackFx.Line(origin, target.transform.position, 0.08f, hero.color, 0.18f);
            DamageInfo damage = DamageCalculator.Roll(Stats, _sig.GuardianCoefficient, DamageKind.BasicAttack, source: "sig_" + hero.id);
            if (target.TryTakeDamage(damage, out float dealt)) OnBasicAttackHit(damage, dealt);
        }

        // ---------------------------------------------------------------- 다빈치 작업대

        private float _partTimer;
        private int _parts;
        private readonly List<FlyingMachine> _machines = new List<FlyingMachine>();

        /// <summary>지금 모은 부품 수 · 떠 있는 비행 기계 수 (화면·QA)</summary>
        public int WorkbenchParts => _parts;
        public int MachineCount => _machines.Count;
        public int PartsPerMachine => hero.partsPerMachine;
        public int MaxMachines => _sig.MaxMachines;

        /// <summary>도우미(비행 기계)가 쓸 표적: 영웅이 노리는 것과 같은 적</summary>
        public Health HelperTarget(float range) => _targets.Find(transform.position, range);

        private void UpdateWorkbench(float dt)
        {
            if (!hero.workbench) return;
            _machines.RemoveAll(m => m == null);
            if (!Stop.IsStopped)
            {
                _partTimer = 0f; // 부품은 남고, 만들던 것만 처음부터
                return;
            }

            _partTimer += dt;
            if (_partTimer < hero.partEvery) return;
            _partTimer -= hero.partEvery;
            if (_machines.Count >= _sig.MaxMachines && _parts >= hero.partsPerMachine - 1) return; // 가득: 마지막 부품은 기다린다
            _parts++;
            AttackFx.Burst(transform.position + Vector3.right * 0.4f, 0.35f, new Color(0.95f, 0.8f, 0.45f, 0.8f), 0.08f);
            if (_parts < hero.partsPerMachine || _machines.Count >= _sig.MaxMachines) return;
            _parts -= hero.partsPerMachine;
            var machine = FlyingMachine.Create(this, hero.helperModel, _machines.Count, hero.machineLife, _sig.MachineDamage, hero.machineFireRate);
            _machines.Add(machine);
            Audio.AudioManager.Play("card", 0.7f, 0.05f);
        }

        /// <summary>비행 기계 탄의 출처 ID. 거울 분신 기절은 영웅이 직접 쏜 기본 공격만 (도우미는 고를 수 없으니 빼 준다)</summary>
        public const string HelperSourceId = "davinci_machine";

        /// <summary>도우미 탄 (기본 공격 종류: 기본 공격 비중으로 센다)</summary>
        public void LaunchHelperShot(Vector2 origin, Vector2 direction, float coefficient)
        {
            DamageInfo damage = DamageCalculator.Roll(Stats, coefficient, DamageKind.BasicAttack, source: HelperSourceId);
            Projectile p = _projectiles.Get(origin, Quaternion.identity);
            p.Launch(direction, hero.projectileSpeed, damage, enemyMask, wallMask, 0, 0, _projectiles.Release, null);
        }

        // ---------------------------------------------------------------- 이성계 백발백중

        private float _charge;
        private int _chargeStage;
        private readonly Dictionary<Health, (float until, int life)> _marks = new Dictionary<Health, (float, int)>();
        private readonly List<Health> _expiredMarks = new List<Health>();

        /// <summary>지금 모은 단계 (0 = 아직, 1·2·3)</summary>
        public int ChargeStage => _chargeStage;

        /// <summary>신궁: 이 적의 다음 3단 화살 피해 +100% (seconds 동안)</summary>
        public void MarkForCharge(Health target, float seconds)
        {
            if (target == null) return;
            // 끝난 표식 · 이미 죽었거나 풀에서 다시 나온 적의 표식은 치운다 (코드 검토 4번)
            _expiredMarks.Clear();
            foreach (KeyValuePair<Health, (float until, int life)> kv in _marks)
            {
                if (kv.Key == null || Time.time > kv.Value.until || kv.Key.Life != kv.Value.life || kv.Key.IsDead) _expiredMarks.Add(kv.Key);
            }

            foreach (Health h in _expiredMarks) _marks.Remove(h);
            _marks[target] = (Time.time + seconds, target.Life);
        }

        private int StageOf(float charge)
        {
            float full = _sig.ChargeFullTime > 0f ? _sig.ChargeFullTime : hero.chargeTimes.z;
            float scale = full / Mathf.Max(0.01f, hero.chargeTimes.z); // ★5: 3단까지 빨라지면 1·2단도 같은 비율로
            return charge >= full ? 3 : charge >= hero.chargeTimes.y * scale ? 2 : charge >= hero.chargeTimes.x * scale ? 1 : 0;
        }

        /// <summary>멈춰 있는 동안 활을 모은다 (공격속도가 빠르면 빨리). 3단이 되면 바로 쏜다</summary>
        private void UpdateCharge(float dt)
        {
            if (!hero.chargeShot) return;
            if (!Stop.IsStopped) return;
            float speed = Stats.Get(StatType.AttackSpeed, StatTarget.BasicAttack) / Mathf.Max(0.1f, hero.attackSpeed);
            _charge += dt * speed;
            int stage = StageOf(_charge);
            if (stage != _chargeStage)
            {
                _chargeStage = stage;
                Color c = stage >= 3 ? new Color(1f, 0.85f, 0.3f, 0.9f) : stage == 2 ? new Color(0.5f, 0.9f, 1f, 0.8f) : new Color(1f, 1f, 1f, 0.6f);
                AttackFx.Burst(transform.position, 0.45f + 0.2f * stage, c, 0.12f);
                if (stage >= 3) Audio.AudioManager.Play("card", 0.6f, 0.05f);
            }

            if (_chargeStage >= 3) ReleaseCharge();
        }

        private void OnMovedCharge()
        {
            if (!hero.chargeShot) return;
            if (_chargeStage >= 1) ReleaseCharge(); // 1·2단은 움직이기 시작할 때 놓는다
            _charge = 0f;
            _chargeStage = 0;
        }

        /// <summary>모은 단계로 쏜다: 2단 관통, 3단 관통 + 치명타 확정 + 큰 피해. 표적이 없으면 3단은 들고 기다린다</summary>
        private void ReleaseCharge()
        {
            Health target = _targets.Find(transform.position, Stats.Get(StatType.Range));
            if (target == null) return;
            int stage = _chargeStage;
            float coefficient = stage >= 3 ? _sig.ChargeFull : stage == 2 ? hero.chargeMidMultiplier : 1f;
            Audio.AudioManager.Play("shoot", stage >= 3 ? 0.7f : 0.4f, 0.08f);
            int extra = Attack.Get(AttackModifierKind.ExtraProjectiles);
            int count = 1 + extra;
            float perProjectile = coefficient * (1f - 0.1f * extra);
            Vector2 origin = transform.position;
            Vector2 aim = ((Vector2)target.transform.position - origin).normalized;
            PlayAttack(aim);
            float start = -spreadDegrees * (count - 1) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = Quaternion.Euler(0f, 0f, start + spreadDegrees * i) * aim;
                DamageInfo damage = DamageCalculator.Roll(Stats, perProjectile, DamageKind.BasicAttack, forceCrit: stage >= 3);
                Projectile p = _projectiles.Get(origin, Quaternion.identity);
                int pierce = Attack.Get(AttackModifierKind.Pierce) + (stage >= 2 ? 1 : 0);
                // 신궁 추가 피해는 이 화살이 그 적에게 준 피해와 같게 (멀티샷 · 관통 감소까지, 코드 검토 3번)
                p.Launch(direction, hero.projectileSpeed * (stage >= 3 ? 1.3f : 1f), damage, enemyMask, wallMask,
                    pierce, Attack.Get(AttackModifierKind.Ricochet), _projectiles.Release, OnBasicAttackHit,
                    onHitTarget: stage >= 3 ? (h, point) => OnFullArrowHit(h, point, p.Damage) : (System.Action<Health, Vector2>)null);
            }

            if (stage >= 3)
            {
                AttackFx.Burst(origin + aim * 0.6f, 0.8f, new Color(1f, 0.85f, 0.3f, 0.9f), 0.15f);
                CameraShake.Add(0.08f);
            }

            _charge = 0f;
            _chargeStage = 0;
        }

        /// <summary>신궁 표식이 있는 적: 3단 화살 피해 +100% (한 번 쓰면 사라진다)</summary>
        private void OnFullArrowHit(Health target, Vector2 point, DamageInfo arrow)
        {
            if (target == null || !_marks.TryGetValue(target, out (float until, int life) mark)) return;
            _marks.Remove(target);
            if (Time.time > mark.until || target.IsDead || target.Life != mark.life) return;
            target.TryTakeDamage(arrow); // +100% = 같은 화살 한 번 더
            AttackFx.Burst(point, 0.9f, new Color(1f, 0.5f, 0.25f, 0.9f), 0.2f);
        }

        private void TryBasicAttack()
        {
            if (hero.chargeShot) return; // 이성계: 공격속도가 아니라 모으는 시간 (UpdateCharge)
            if (_attackCooldown > 0f) return;
            if (Stop.StoppedTime < hero.firstShotDelay) return;

            _searchDelay -= Time.deltaTime;
            if (_searchDelay > 0f) return;

            Health target = _targets.Find(transform.position, BasicAttackRange,
                requireCenterInRange: hero.attackKind == BasicAttackKind.MeleeThrust);
            if (target == null)
            {
                _searchDelay = SearchRetryInterval;
                return;
            }

            if (hero.attackKind == BasicAttackKind.MeleeArc || hero.attackKind == BasicAttackKind.MeleeThrust) Slash(target);
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

        /// <summary>멀티샷 하나마다 한 발(한 번 베기)의 피해가 줄어드는 비율 (Skills.md 1장). 레벨업 카드도 이 값을 쓴다</summary>
        public const float MultishotPenalty = 0.1f;

        /// <summary>멀티샷 수에 따른 한 발(한 번 베기)의 피해 배수 + 첫 발 보너스</summary>
        private float NextShotCoefficient(int extra)
        {
            float coefficient = 1f - MultishotPenalty * extra; // 멀티샷: 발당 -10% (Skills.md 1장)
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
            int count = Mathf.Max(1, hero.fanShots) + extra; // 제갈량 깃털 부채: 3갈래
            // 표적을 찾고 실제로 발사할 때만 준비를 쓰므로 빈 방에서 관통을 잃지 않는다.
            bool observedShot = _observation.TryConsume();
            float perProjectile = NextShotCoefficient(extra) * (hero.fanShots > 1 ? hero.fanCoefficient : 1f);
            if (observedShot) perProjectile *= 1f + _sig.ObservationAttackBonus;
            _lastShotCoefficient = perProjectile;
            _bulletsFired += count;

            Vector2 origin = transform.position;
            Vector2 aim = ((Vector2)target.transform.position - origin).normalized;
            PlayAttack(aim);
            if (observedShot) AttackFx.Burst(origin + aim * 0.6f, 0.55f, new Color(0.78f, 0.63f, 1f, 0.5f), 0.15f);
            ShotSpread(hero.fanShots, hero.fanSpread, spreadDegrees, count, out float start, out float spread);
            bool needsTarget = hero.knockback > 0f || _sig.ChainBounces > 0;

            for (int i = 0; i < count; i++)
            {
                Vector2 direction = Quaternion.Euler(0f, 0f, start + spread * i) * aim;
                DamageInfo damage = DamageCalculator.Roll(Stats, perProjectile, DamageKind.BasicAttack);
                if (i == 0) damage.Amount += TakeIroncladStore(); // 이순신 철갑: 첫 발에 쌓인 피해
                Projectile p = _projectiles.Get(origin, Quaternion.identity);
                int pierce = Attack.Get(AttackModifierKind.Pierce) + (_sig.FirstShotPierce && _firstShotThisShot ? 1 : 0)
                    + (observedShot ? 1 : 0); // 관측은 멀티샷 묶음 하나의 기본 공격에 적용한다.
                p.Launch(direction, hero.projectileSpeed, damage, enemyMask, wallMask,
                    pierce, Attack.Get(AttackModifierKind.Ricochet),
                    _projectiles.Release, OnBasicAttackHit,
                    onHitTarget: i == 0 && _sig.MarkHits > 0 ? OnMarkProjectileHitTarget : needsTarget ? OnProjectileHitTarget : null,
                    notifyFirstTargetOnly: i == 0 && _sig.MarkHits > 0);
            }

            // 모든 탄을 먼저 보낸 뒤 같은 표적에 동반 타격한다. 표적을 쓰러뜨려도 멀티샷 수는 바뀌지 않는다.
            if (hero.guardianAttacks > 0) JoinGuardianAttack(target);
        }

        /// <summary>
        /// 여러 발의 각도: 보통은 한 발 간격 normalSpread. 부채 영웅(제갈량)은 부채 넓이(fanSpread × (fanShots−1))를 그대로 두고
        /// 멀티샷으로 늘어난 발을 그 안에 촘촘히 넣는다 → 가운데 적을 놓치지 않는다 (코드 검토 2번: 4발이 ±27°·±9°로 벌어져 정면이 비던 것)
        /// </summary>
        public static void ShotSpread(int fanShots, float fanSpread, float normalSpread, int count, out float start, out float step)
        {
            step = normalSpread;
            if (fanShots > 1 && count > 1) step = fanSpread * (fanShots - 1) / (count - 1);
            start = -step * (count - 1) * 0.5f;
        }

        /// <summary>나폴레옹 밀어내기, 테슬라 튕김</summary>
        private void OnProjectileHitTarget(Health target, Vector2 point)
        {
            if (hero.knockback > 0f) Knockback(target, (Vector2)target.transform.position - (Vector2)transform.position, hero.knockback);
            if (_sig.ChainBounces > 0) Chain(target, _lastShotCoefficient);
        }

        private void OnMarkProjectileHitTarget(Health target, Vector2 point)
        {
            OnProjectileHitTarget(target, point);
            // 죽인 명중도 표식 대상은 바꾼다 (예전엔 건너뛰어서: A에 3회 → B를 처치 → A 다시 맞히면 바로 둔화, Codex 리뷰). 둔화만 산 적에게
            bool fire = _mark.RecordHit(target, target.Life, Time.time, true, _sig.MarkHits, _sig.MarkCooldown);
            if (!fire || target.IsDead || !target.TryGetComponent(out EnemyBase enemy)) return;
            float slow = AnubisMarkState.EffectiveSlow(_sig, hero.markBossScale, enemy is IPhasedBoss);
            enemy.Status.ApplySlow(slow, AnubisMarkState.EffectiveDuration(_sig), _mark);
            AttackFx.Burst(point, 0.45f, new Color(0.82f, 0.67f, 0.34f, 0.5f), 0.12f);
        }

        private static void Knockback(Health target, Vector2 direction, float distance)
        {
            if (!target.TryGetComponent(out EnemyBase enemy) || !enemy.CanBeKnockedBack) return;
            enemy.Push(direction.normalized * distance);
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
            // 표적을 찾은 실제 공격만 준비를 쓴다. 연속 베기와 검기에 효과를 복제하지 않는다.
            bool reclaimed = _reclaim.TryConsume(hero);
            bool thunder = Stop.IsStopped && _thunder.RecordAttack(hero.thunderEvery);
            StartCoroutine(Swings(target, 1 + extra, coefficient, arc, waveBounces, reclaimed, thunder));
        }

        private IEnumerator Swings(Health target, int swings, float coefficient, float arc, int waveBounces, bool reclaimed, bool thunder)
        {
            for (int s = 0; s < swings; s++)
            {
                // 기본 공격은 멈췄을 때만: 연속 베기 도중에 움직이면 남은 베기는 취소 (GameDesign 3-3)
                if (Health.IsDead || (s > 0 && !Stop.IsStopped)) yield break;
                Vector2 origin = transform.position;
                Vector2 aim = target != null && !target.IsDead ? ((Vector2)target.transform.position - origin) : (Vector2)transform.up;
                if (aim.sqrMagnitude < 0.0001f) aim = Vector2.up;
                aim.Normalize();

                bool reclaim = reclaimed && s == 0;
                float swingCoefficient = coefficient * (reclaim ? 1f + _sig.ReclaimAttackBonus : 1f);
                if (hero.attackKind == BasicAttackKind.MeleeThrust)
                    ThrustOnce(origin, aim, swingCoefficient);
                else
                {
                    float swingArc = arc;
                    if (hero.windFirstArc > 0f)
                    {
                        SusanooWindState.SlashShape shape = _windSlash.RecordSlash(hero.windFirstArc, _sig.WindFirstCoefficient,
                            hero.windFollowArc, _sig.WindFollowCoefficient);
                        swingArc = shape.Arc + (Attack.Get(AttackModifierKind.Pierce) > 0 ? 40f : 0f);
                        swingCoefficient *= shape.Coefficient;
                    }
                    SlashOnce(origin, aim, swingCoefficient, swingArc, reclaim);
                }
                if (thunder && s == 0) ThunderShockwave(origin, aim);
                if (waveBounces > 0) FireWave(origin, aim, waveBounces);
                if (s < swings - 1) yield return new WaitForSeconds(swingInterval);
            }
        }

        private void ThunderShockwave(Vector2 origin, Vector2 aim)
        {
            Vector2 center = origin + aim * 1.2f;
            float radius = _sig.ThunderRadius;
            AttackFx.Burst(center, radius, new Color(0.52f, 0.75f, 0.95f, 0.35f));
            Skills.CombatQuery.EnemiesWithin(center, radius, _thunderHits);
            foreach (EnemyBase enemy in _thunderHits)
            {
                DamageInfo damage = DamageCalculator.Roll(Stats, _sig.ThunderCoefficient, DamageKind.BasicAttack, source: "sig_thor");
                if (enemy.Health.TryTakeDamage(damage, out float dealt)) OnBasicAttackHit(damage, dealt);
            }
        }

        private void SlashOnce(Vector2 origin, Vector2 aim, float coefficient, float arc, bool reclaim)
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
                if (h.TryTakeDamage(damage, out float dealt))
                {
                    OnBasicAttackHit(damage, dealt);
                    if (reclaim) ReclaimSpace(h, damage.Direction);
                }
            }

            // 부서지는 장애물 (Modes 2-3): 근접도 깎는다
            _overlap.Clear();
            Physics2D.OverlapCircle(origin, reach, new ContactFilter2D { useLayerMask = true, layerMask = wallMask }, _overlap);
            for (int i = 0; i < _overlap.Count; i++)
            {
                if (!_overlap[i].TryGetComponent(out Dungeon.IShotTarget b)) continue;
                Vector2 to = (Vector2)b.transform.position - origin;
                if (to.sqrMagnitude > 0.5f && Vector2.Angle(aim, to) > half) continue;
                b.Hit();
            }
        }

        /// <summary>관통은 창 폭을 넓히는 대신 뒤의 적까지 닿게 한다. 여러 관통 효과가 있어도 한 번만 늘린다.</summary>
        public static float SpearReach(float range, int pierce, float pierceReachBonus)
            => range * (pierce > 0 ? 1f + pierceReachBonus : 1f);

        private float BasicAttackRange => hero.attackKind == BasicAttackKind.MeleeThrust
            ? SpearReach(Stats.Get(StatType.Range), Attack.Get(AttackModifierKind.Pierce), hero.spearPierceReachBonus)
            : Stats.Get(StatType.Range);

        /// <summary>표적을 고를 때와 같은 중심 거리로, 창끝의 직선 안에 있는 적만 맞는다.</summary>
        public static bool InSpearPath(Vector2 origin, Vector2 aim, Vector2 point, float reach, float width)
        {
            Vector2 to = point - origin;
            float along = Vector2.Dot(aim, to);
            return TargetSelector.IsWithinRange(origin, point, reach)
                && along >= 0f && along <= reach && Mathf.Abs(aim.x * to.y - aim.y * to.x) <= width * 0.5f;
        }

        private void ThrustOnce(Vector2 origin, Vector2 aim, float coefficient)
        {
            float reach = BasicAttackRange;
            AttackFx.Line(origin, origin + aim * reach, hero.spearWidth, slashColor, 0.16f);
            PlayAttack(aim);

            _overlap.Clear();
            Physics2D.OverlapCircle(origin, reach, _enemyFilter, _overlap);
            for (int i = 0; i < _overlap.Count; i++)
            {
                if (!_overlap[i].TryGetComponent(out Health target) || target.IsDead
                    || !InSpearPath(origin, aim, target.transform.position, reach, hero.spearWidth)) continue;
                DamageInfo damage = DamageCalculator.Roll(Stats, coefficient, DamageKind.BasicAttack);
                damage.Direction = aim;
                damage.HasDirection = true;
                if (!target.TryTakeDamage(damage, out float dealt)) continue;
                OnBasicAttackHit(damage, dealt);
                ApplyOwlBonus(target);
            }

            _overlap.Clear();
            Physics2D.OverlapCircle(origin, reach, new ContactFilter2D { useLayerMask = true, layerMask = wallMask }, _overlap);
            for (int i = 0; i < _overlap.Count; i++)
                if (_overlap[i].TryGetComponent(out Dungeon.IShotTarget obstacle)
                    && InSpearPath(origin, aim, _overlap[i].transform.position, reach, hero.spearWidth)) obstacle.Hit();
        }

        private void ApplyOwlBonus(Health target)
        {
            if (_sig.OwlBonusCoefficient <= 0f || target.IsDead) return;
            float coefficient = _owlMarks.BonusFor(target, target.Life, Time.time, DamageKind.BasicAttack, _sig.OwlBonusCoefficient);
            if (coefficient <= 0f) return;
            // 추가 타격은 따로 치명타를 굴리지 않아 기본 공격 보너스가 ★5에서도 25%를 넘지 않는다.
            var bonus = new DamageInfo
            {
                Amount = Stats.Get(StatType.Attack, StatTarget.BasicAttack) * coefficient,
                Kind = DamageKind.BasicAttack,
                AttackerTeam = Team.Player,
                SourceId = "sig_athena",
            };
            if (target.TryTakeDamage(bonus, out float dealt)) OnBasicAttackHit(bonus, dealt);
        }

        /// <summary>올빼미의 눈 발동마다 지난 표식을 버린다.</summary>
        public void BeginOwlMarks(object owner) => _owlMarks.Begin(owner);

        public void ClearOwlMarks(object owner) => _owlMarks.ClearFor(owner);

        public void AddOwlMark(Health target, float duration, int maxTargets)
        {
            if (target != null && !target.IsDead) _owlMarks.Add(target, target.Life, Time.time, duration, maxTargets);
        }

        private DamageInfo BlockAthenaProjectile(DamageInfo info)
        {
            if (_sig.ShieldPrepareSeconds <= 0f || info.AttackerTeam != Team.Enemy || !info.IsProjectile
                || !_athenaShield.TryBlock(Time.time, hero.shieldReuseSeconds)) return info;
            info.Amount = 0f;
            AttackFx.Burst(transform.position, 0.6f, new Color(0.85f, 0.72f, 0.4f, 0.45f), 0.18f);
            return info;
        }

        private void ReclaimSpace(Health target, Vector2 direction)
        {
            if (target.IsDead || !target.TryGetComponent(out EnemyBase enemy)) return;
            // 기존 보스 밀침 면역은 다른 공격에 남겨 두고, 이 대표 효과만 30%로 적용한다.
            bool isBoss = enemy is IPhasedBoss;
            if (!isBoss && !enemy.CanBeKnockedBack) return;
            if (!target.TryGetComponent(out Rigidbody2D body)) return;
            float distance = BoudicaReclaimState.KnockbackDistance(hero, _sig, isBoss);
            if (distance <= 0f) return;

            // 큰 보스도 몸 전체가 벽 안에 남도록 이동 경로를 확인한다.
            float radius = 0.4f;
            if (target.TryGetComponent(out Collider2D shape))
                radius = Mathf.Max(shape.bounds.extents.x, shape.bounds.extents.y);
            Vector2 aim = direction.normalized;
            RaycastHit2D wall = Physics2D.CircleCast(body.position, radius, aim, distance, wallMask);
            if (wall.collider != null) distance = Mathf.Max(0f, wall.distance - 0.02f);
            enemy.Push(aim * distance);
        }

        /// <summary>근접 + 벽 튕김: 검기 (공격력 40%) 한 발이 날아가 벽에 튕긴다</summary>
        private void FireWave(Vector2 origin, Vector2 aim, int bounces)
        {
            DamageInfo damage = DamageCalculator.Roll(Stats, hero.meleeWaveCoefficient, DamageKind.BasicAttack);
            Projectile p = _projectiles.Get(origin, Quaternion.identity);
            p.Launch(aim, 12f, damage, enemyMask, wallMask, 0, bounces, _projectiles.Release, OnBasicAttackHit,
                onHitTarget: _sig.OwlBonusCoefficient > 0f ? (target, _) => ApplyOwlBonus(target) : null);
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
            System.Action<Health, Vector2> onHitTarget, object owner = null, float maxDistance = float.PositiveInfinity,
            bool hitOncePerTarget = false)
        {
            if (!_extraPools.TryGetValue(prefab, out ComponentPool<Projectile> pool))
            {
                pool = new ComponentPool<Projectile>(prefab, new GameObject(prefab.name + " Pool").transform, prewarm: 8);
                _extraPools[prefab] = pool;
            }

            Projectile p = pool.Get(origin, Quaternion.identity);
            Track(p, owner, pool);
            p.Launch(direction, speed, damage, enemyMask, wallMask, pierce, 0, ReleaseFor(pool), null, 0f, onHitTarget,
                maxDistance: maxDistance, hitOncePerTarget: hitOncePerTarget);
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
