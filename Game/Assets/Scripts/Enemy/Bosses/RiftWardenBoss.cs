using System;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 1구간 보스 🛡️ 균열의 수문장 (Content.md 3장 "큰 빈틈 → 멈춤 타이밍 배우기", Modes.md 2-9).
    /// - 체력 2/3, 1/3에서 단계가 바뀐다 (3단계). 면역은 없다. 패턴으로 시험한다.
    /// - 대지 강타: 플레이어 자리에 원 예고 → 내려찍기 → 빈틈
    /// - 균열 돌진: 선 예고 → 돌진 → 벽에 부딪히면 기절 (가장 큰 빈틈)
    /// - 균열 파동 (2단계부터): 몸이 빛남 → 사방으로 파편 → 빈틈
    /// - 3단계: 예고가 짧아지고 강타가 2번 이어진다. 빈틈 길이는 그대로 (멈춰서 쏠 기회는 남긴다)
    /// 빈틈 동안 발밑에 노란 고리가 뜬다 = "지금 멈춰서 쏘세요".
    /// </summary>
    public sealed class RiftWardenBoss : EnemyBase, IPhasedBoss
    {
        public enum Pattern { Slam, Charge, Wave }

        [Header("크기")]
        [SerializeField] private float bossScale = 2.2f;

        [Header("대지 강타")]
        [SerializeField] private float slamRadius = 2.2f;
        [SerializeField] private float slamTelegraph = 1.1f;
        [SerializeField] private float slamDamage = 1.6f;
        [SerializeField] private float slamRecover = 1.5f;
        [SerializeField] private float doubleSlamGap = 0.45f;

        [Header("균열 돌진")]
        [SerializeField] private float chargeTelegraph = 0.9f;
        [SerializeField] private float chargeAimLock = 0.25f;
        [SerializeField] private float chargeSpeed = 12f;
        [SerializeField] private float chargeMaxTime = 1.1f;
        [SerializeField] private float chargeDamage = 1.4f;
        [SerializeField] private float chargeHitRadius = 0.5f;
        [SerializeField] private float stunTime = 2.2f;
        [SerializeField] private float chargeMissRecover = 1.0f;
        [SerializeField] private LayerMask wallMask;

        [Header("균열 파동 (2단계부터)")]
        [SerializeField] private int waveShards = 12;
        [SerializeField] private float waveTelegraph = 1.0f;
        [SerializeField] private float waveSpeed = 5.5f;
        [SerializeField] private float waveDamage = 0.8f;
        [SerializeField] private float waveRecover = 1.4f;
        [SerializeField] private float secondRingDelay = 0.35f;

        [Header("단계·이동")]
        [SerializeField] private float phase3TelegraphScale = 0.8f;
        [SerializeField] private float walkTime = 1.2f;
        [SerializeField] private float keepDistance = 2.5f;

        [Header("표시")]
        [SerializeField] private SpriteRenderer slamMarker;
        [SerializeField] private SpriteRenderer openingRing;
        [SerializeField] private SpriteRenderer waveGlow;

        private Pattern _pattern;
        private int _step;
        private int _phase = 1;
        private int _slamsLeft;
        private Vector2 _slamCenter;
        private Vector2 _chargeDirection;
        private bool _chargeHit;
        private bool _secondRingPending;
        private float _secondRingTimer;

        public int Phase => _phase;

        /// <summary>무한의 균열 버전: 2단계(파동)부터</summary>
        public bool RiftVersion { get; set; }
        public override bool CanBeKnockedBack => false;
        public Pattern CurrentPattern => _pattern;

        /// <summary>빈틈인가 (멈춰서 쏠 때)</summary>
        public bool IsOpen => state == State.Recover;

        // 읽기만: 검증 벤치의 피하기 AI가 화면 예고와 같은 정보를 본다 (사람이 보는 원·선과 같음)
        public bool IsTelegraphing => state == State.Telegraph;
        public bool IsAttacking => state == State.Attack;
        public Vector2 SlamCenter => _slamCenter;
        public float SlamRadius => slamRadius;
        public Vector2 ChargeDirection => _chargeDirection;
        public float ChargeHitRadius => chargeHitRadius * transform.localScale.x;

        public event Action<int> PhaseChanged;

        protected override void Awake()
        {
            base.Awake();
            if (slamMarker != null)
            {
                slamMarker.transform.SetParent(null, true); // 보스를 따라 움직이지 않게
                slamMarker.enabled = false;
            }

            if (openingRing != null) openingRing.enabled = false;
            if (waveGlow != null) waveGlow.enabled = false;
            health.Damaged += (_, __) => UpdatePhase();
        }

        private void OnDestroy()
        {
            if (slamMarker != null) Destroy(slamMarker.gameObject);
        }

        public override void Spawn(Transform target, float hpMultiplier, float atkMultiplier, bool elite)
        {
            base.Spawn(target, hpMultiplier, atkMultiplier, false);
            transform.localScale = Vector3.one * bossScale;
            _phase = RiftVersion ? 2 : 1;
            _step = 0;
            Enter(State.Approach, walkTime);
        }

        private void UpdatePhase()
        {
            float ratio = health.Max > 0f ? health.Current / health.Max : 0f;
            int phase = ratio > 2f / 3f ? 1 : ratio > 1f / 3f ? 2 : 3;
            if (phase <= _phase) return;
            _phase = phase;
            Common.HitStop.Freeze(Common.HitStop.BossPhase); // 손맛 (조사 A9)
            PhaseChanged?.Invoke(_phase);
        }

        private float PhaseTelegraph => (_phase >= 3 ? phase3TelegraphScale : 1f) * TelegraphScale;

        protected override void Tick()
        {
            if (openingRing != null) openingRing.enabled = state == State.Recover;
            if (_secondRingPending) TickSecondRing();

            switch (state)
            {
                case State.Approach:
                    if (ToPlayer.magnitude > keepDistance) Walk(ToPlayer, data.moveSpeed);
                    else body.linearVelocity = Vector2.zero;
                    if (stateTimer <= 0f) BeginPattern(NextPattern());
                    break;

                case State.Telegraph:
                    TickTelegraph();
                    break;

                case State.Attack:
                    TickAttack();
                    break;

                case State.Recover:
                    if (stateTimer <= 0f) Enter(State.Approach, walkTime);
                    break;
            }
        }

        // ---------------- 패턴 고르기 ----------------

        private static readonly Pattern[] Phase1 = { Pattern.Slam, Pattern.Charge };
        private static readonly Pattern[] Phase2 = { Pattern.Slam, Pattern.Charge, Pattern.Wave };
        private static readonly Pattern[] Phase3 = { Pattern.Slam, Pattern.Wave, Pattern.Charge };

        private Pattern NextPattern()
        {
            Pattern[] cycle = _phase == 1 ? Phase1 : _phase == 2 ? Phase2 : Phase3;
            Pattern next = cycle[_step % cycle.Length];
            _step++;
            return next;
        }

        private void BeginPattern(Pattern pattern)
        {
            _pattern = pattern;
            switch (pattern)
            {
                case Pattern.Slam:
                    _slamsLeft = _phase >= 3 ? 2 : 1;
                    BeginSlam();
                    break;

                case Pattern.Charge:
                    _chargeDirection = ToPlayer.normalized;
                    Enter(State.Telegraph, chargeTelegraph * PhaseTelegraph);
                    break;

                case Pattern.Wave:
                    if (waveGlow != null) waveGlow.enabled = true;
                    Enter(State.Telegraph, waveTelegraph * PhaseTelegraph);
                    break;
            }
        }

        private void BeginSlam()
        {
            _slamCenter = player.position;
            if (slamMarker != null)
            {
                slamMarker.transform.position = _slamCenter;
                slamMarker.transform.localScale = Vector3.one * (slamRadius * 2f * 0.2f);
                slamMarker.enabled = true;
            }

            Enter(State.Telegraph, slamTelegraph * PhaseTelegraph);
        }

        // ---------------- 예고 ----------------

        private void TickTelegraph()
        {
            switch (_pattern)
            {
                case Pattern.Slam:
                {
                    // 원이 점점 커지며 진해진다 → 다 차면 내려찍기
                    float total = slamTelegraph * PhaseTelegraph;
                    float t = 1f - Mathf.Clamp01(stateTimer / total);
                    if (slamMarker != null)
                    {
                        slamMarker.transform.localScale = Vector3.one * (slamRadius * 2f * Mathf.Lerp(0.2f, 1f, t));
                        slamMarker.color = new Color(1f, 0.25f, 0.2f, Mathf.Lerp(0.2f, 0.55f, t));
                    }

                    if (stateTimer <= 0f) Slam();
                    break;
                }

                case Pattern.Charge:
                    if (stateTimer > chargeAimLock) _chargeDirection = ToPlayer.normalized;
                    DrawTelegraphLine(transform.position, ChargeEnd());
                    if (stateTimer <= 0f)
                    {
                        _chargeHit = false;
                        Enter(State.Attack, chargeMaxTime);
                    }
                    break;

                case Pattern.Wave:
                    if (waveGlow != null)
                    {
                        float pulse = 0.35f + 0.25f * Mathf.Sin(Time.time * 18f);
                        waveGlow.color = new Color(0.75f, 0.45f, 1f, pulse);
                    }

                    if (stateTimer <= 0f) Wave();
                    break;
            }
        }

        // ---------------- 공격 ----------------

        private void Slam()
        {
            if (slamMarker != null) slamMarker.enabled = false;

            Collider2D hit = Physics2D.OverlapCircle(_slamCenter, slamRadius, playerMask);
            if (hit != null && hit.TryGetComponent(out Health target)) target.TryTakeDamage(DamageCalculator.Enemy(Damage * slamDamage, "boss_slam"));
            StrikeFlash(_slamCenter, slamRadius);
            Common.CameraShake.Add(Common.CameraShake.BossImpact);

            _slamsLeft--;
            if (_slamsLeft > 0)
            {
                // 3단계: 곧바로 한 번 더 (짧은 틈)
                _slamCenter = player.position;
                if (slamMarker != null)
                {
                    slamMarker.transform.position = _slamCenter;
                    slamMarker.enabled = true;
                }

                Enter(State.Telegraph, slamTelegraph * PhaseTelegraph * 0.75f + doubleSlamGap);
                return;
            }

            Enter(State.Recover, slamRecover);
        }

        private void TickAttack()
        {
            if (_pattern != Pattern.Charge) return;

            float step = chargeSpeed * Time.deltaTime;
            float radius = chargeHitRadius * transform.localScale.x;
            RaycastHit2D wall = Physics2D.CircleCast(transform.position, radius, _chargeDirection, step + 0.05f, wallMask);
            if (wall.collider != null)
            {
                // 벽에 부딪혀 기절 = 가장 큰 빈틈
                body.linearVelocity = Vector2.zero;
                Enter(State.Recover, stunTime);
                return;
            }

            MoveToward(_chargeDirection, chargeSpeed);

            if (!_chargeHit)
            {
                Collider2D hit = Physics2D.OverlapCircle(transform.position, radius, playerMask);
                if (hit != null && hit.TryGetComponent(out Health target))
                {
                    _chargeHit = target.TryTakeDamage(DamageCalculator.Enemy(Damage * chargeDamage, "boss_charge"));
                }
            }

            if (stateTimer <= 0f) Enter(State.Recover, chargeMissRecover);
        }

        /// <summary>돌진이 멈출 자리 (벽까지). 예고선이 경기장 밖으로 뻗지 않게</summary>
        private Vector2 ChargeEnd()
        {
            Vector2 from = transform.position;
            float distance = chargeSpeed * chargeMaxTime;
            RaycastHit2D wall = Physics2D.CircleCast(from, chargeHitRadius * transform.localScale.x, _chargeDirection, distance, wallMask);
            return wall.collider != null ? wall.centroid : from + _chargeDirection * distance;
        }

        private void Wave()
        {
            if (waveGlow != null) waveGlow.enabled = false;
            FireRing(0f);

            if (_phase >= 3)
            {
                _secondRingPending = true;
                _secondRingTimer = secondRingDelay;
            }

            Enter(State.Recover, waveRecover);
        }

        private void TickSecondRing()
        {
            _secondRingTimer -= Time.deltaTime;
            if (_secondRingTimer > 0f) return;
            _secondRingPending = false;
            FireRing(0.5f); // 첫 고리의 틈 사이로
        }

        private void FireRing(float offset)
        {
            float step = 360f / Mathf.Max(1, waveShards);
            for (int i = 0; i < waveShards; i++)
            {
                float angle = (i + offset) * step * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                EnemyProjectiles.Fire((Vector2)transform.position + direction * 0.9f, direction, waveSpeed, DamageCalculator.Enemy(Damage * waveDamage, "boss_wave"), playerMask, this);
            }
        }

        private static void StrikeFlash(Vector2 center, float radius)
        {
            var go = new GameObject("SlamFlash");
            go.transform.position = center;
            go.transform.localScale = Vector3.one * radius * 2f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprites.Circle;
            sr.color = new Color(1f, 0.85f, 0.6f, 0.5f);
            sr.sortingOrder = -1;
            Destroy(go, 0.15f);
        }

        protected override void OnDied(Health h)
        {
            if (slamMarker != null) slamMarker.enabled = false;
            if (openingRing != null) openingRing.enabled = false;
            if (waveGlow != null) waveGlow.enabled = false;
            _secondRingPending = false;
            Common.HitStop.OnBossKilled(); // 0.2초 멈춤 + 0.5초 0.3배 (조사 A9)
            Common.CameraShake.Add(Common.CameraShake.BossImpact);
            base.OnDied(h);
        }
    }
}
