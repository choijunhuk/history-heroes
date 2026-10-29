using System.Collections;
using HistoryHeroes.Combat;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 4구간 보스 🌑 그림자 쌍둥이 (Content 3장: "둘이 따로 움직임 → 단일 순간 화력").
    /// 둘이 따로 움직이며 같이 공격하지만, **맞는 것은 한 번에 하나**(진한 쪽)다. 잠깐 서는 빈틈 동안 몰아쳐야 하고,
    /// 공격이 끝나면 둘이 자리를 바꾼다 (표적을 다시 잡아야 한다). 체력은 하나 (진한 쪽이 맞는다).
    /// - 엇갈려 돌진: 둘이 양쪽에서 동시에
    /// - 그림자 단검: 둘 다 부채꼴 (3단계: 더 많이)
    /// </summary>
    public sealed class ShadowTwinsBoss : PatternBoss
    {
        [SerializeField] private SpriteRenderer twin;
        [SerializeField] private float twinSpeed = 2.6f;
        [SerializeField] private float knifeSpeed = 6f;
        [SerializeField] private float knifeDamage = 0.7f;
        [SerializeField] private bool twoFormTrial;
        [SerializeField] private ShadowTrialForm trialForm;
        [SerializeField] private SpriteRenderer twinOpeningRing;
        [SerializeField] private float trialRecover = 1.8f;

        private int _step;
        private Vector2 _twinPos;
        private Vector2 _twinDash;
        private float _twinDashUntil;
        private bool _twinHit;
        private bool _twinHold; // 돌진 예고 중 (예고선에서 출발해야 한다)

        private Visual3D _twinVisual;
        private readonly ShadowTwinTrialState _trialState = new ShadowTwinTrialState();
        private Collider2D _mainCollider;
        private Collider2D _twinCollider;
        private SpriteRenderer _mainSprite;
        private bool _openSecond;
        private bool _incomingSecond;
        private bool _promotedSecond;
        private State _lastPromotedState;

        public bool IsTwoFormTrial => twoFormTrial;
        public Vector2 OpenFormPosition => twoFormTrial && _openSecond && !_promotedSecond ? _twinPos : Pos;

        protected override void Awake()
        {
            base.Awake();
            _mainCollider = GetComponent<Collider2D>();
            _mainSprite = GetComponent<SpriteRenderer>();
            if (trialForm != null) _twinCollider = trialForm.GetComponent<Collider2D>();
            health.Damaged += OnTrialDamaged;
        }

        protected override void OnSpawned()
        {
            if (twin == null) return;
            _openSecond = false;
            _incomingSecond = false;
            _promotedSecond = false;
            _lastPromotedState = State.Approach;
            if (twoFormTrial) _step = 0;
            if (twoFormTrial)
            {
                _trialState.Reset(health.Max);
                health.Guard = GuardTrialDamage;
                health.DamageCap = incoming => _trialState.Remaining(_incomingSecond || _promotedSecond);
            }
            twin.gameObject.SetActive(true);
            twin.transform.SetParent(null, true);
            _twinPos = Pos + new Vector2(2.5f, 0f);
            _twinHold = false;
            if (twoFormTrial && trialForm != null)
            {
                trialForm.Owner = this;
                trialForm.Spawn(player, HpMultiplierUsed, AttackMultiplierUsed, false);
                trialForm.Health.DamageRedirect = RedirectTwinDamage;
                if (twinOpeningRing != null) twinOpeningRing.enabled = false;
                UpdateTrialTargets();
            }
            // 3D 유령 (Art3D-Plan 5번): 모델이 있으면 흐린 원 대신 비치는 쌍둥이가 영웅을 본다
            if (_twinVisual == null) _twinVisual = twin.GetComponent<Visual3D>();
            if (_twinVisual != null) _twinVisual.LookTarget = player;
            twin.enabled = _twinVisual == null || !_twinVisual.HasModel;
        }

        private void TwinAttack(Vector2 direction)
        {
            if (_twinVisual != null) _twinVisual.Attack(direction);
        }

        protected override void Tick()
        {
            if (twoFormTrial && _promotedSecond && trialForm != null && trialForm.Status.IsFrozen)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            base.Tick();
            if (!twoFormTrial || !_promotedSecond || trialForm == null) return;
            if (state == State.Attack && _lastPromotedState != State.Attack) TwinAttack(ToPlayer);
            _lastPromotedState = state;
            body.linearVelocity *= trialForm.Status.SpeedMultiplier;
            _twinPos = Pos;
            twin.transform.position = Pos;
            twin.transform.localScale = transform.localScale;
        }

        protected override void OnTick()
        {
            if (twin == null || player == null) return;
            if (twoFormTrial)
            {
                if (openingRing != null) openingRing.enabled = IsOpen && !_openSecond;
                if (twinOpeningRing != null) twinOpeningRing.enabled = IsOpen && _openSecond;
                if (_promotedSecond)
                {
                    _twinPos = Pos;
                    twin.transform.position = Pos;
                    return;
                }
                if (!_trialState.SecondAlive) return;
                if (trialForm != null && trialForm.Status.IsFrozen) return;
            }
            float twinSpeedScale = twoFormTrial && trialForm != null ? trialForm.Status.SpeedMultiplier : 1f;
            if (bossTime < _twinDashUntil)
            {
                _twinPos += _twinDash * 11f * twinSpeedScale * Time.deltaTime;
                if (!_twinHit) _twinHit = HitPlayerIn(_twinPos, 0.6f, 1.2f, "boss_twins_dash");
            }
            else if (!_twinHold)
            {
                Vector2 to = (Vector2)player.position - _twinPos;
                if (to.magnitude > 2.4f) _twinPos += to.normalized * twinSpeed * twinSpeedScale * Time.deltaTime;
            }

            _twinPos = Vector2.ClampMagnitude(_twinPos, 3.6f); // 경기장 안
            twin.transform.position = _twinPos;
            twin.transform.localScale = transform.localScale;
        }

        protected override IEnumerator Cycle()
        {
            _step++;
            if (_step % 2 == 1)
            {
                // 엇갈려 돌진: 그림자 쪽도 예고선을 보여 준 뒤 둘이 동시에
                bool secondAttacks = !twoFormTrial || _trialState.SecondAlive && !_promotedSecond
                    && (trialForm == null || !trialForm.Status.IsFrozen);
                if (secondAttacks)
                {
                    _twinHold = true;
                    Vector2 twinDir = ((Vector2)player.position - _twinPos).normalized;
                    SpriteRenderer line = Show(Telegraphs.Line(_twinPos, _twinPos + twinDir * 9f, 0.5f, Telegraphs.Danger));
                    float telegraph = 0.85f * Tele;
                    float start = bossTime;
                    Enter(State.Telegraph);
                    while (bossTime < start + telegraph)
                    {
                        Telegraphs.Fill(line, Telegraphs.Danger, (bossTime - start) / telegraph);
                        yield return null;
                    }

                    HideShown(line);
                    _twinHold = false;
                    _twinDash = twinDir;
                    _twinDashUntil = bossTime + 0.7f;
                    TwinAttack(twinDir);
                    _twinHit = false;
                }
                yield return Dash(0.25f, 10.5f, 0.9f, 1.2f, "boss_twins_dash",
                    twoFormTrial ? trialRecover : 1.6f, twoFormTrial ? trialRecover : 1.2f);
            }
            else
            {
                yield return Glow(0.7f * Tele, new Color(0.4f, 0.2f, 0.6f, 0.5f));
                int knives = phase >= 3 ? 7 : 5;
                Fan(knives, 60f, knifeSpeed, knifeDamage, "boss_twins_knife");
                if (!twoFormTrial || _trialState.SecondAlive && !_promotedSecond
                    && (trialForm == null || !trialForm.Status.IsFrozen))
                {
                    FanFrom(_twinPos, knives, 60f, knifeSpeed, knifeDamage, "boss_twins_knife");
                    TwinAttack((Vector2)player.position - _twinPos);
                }
                yield return Open(twoFormTrial ? trialRecover : phase >= 2 ? 1.4f : 1.8f);
            }

            Swap();
        }

        /// <summary>자리 바꾸기: 진한 쪽(맞는 쪽)과 그림자가 순간 이동으로 바뀐다</summary>
        private void Swap()
        {
            if (twin == null) return;
            if (twoFormTrial && (_promotedSecond || !_trialState.SecondAlive)) return;
            Vector2 main = Pos;
            Blink(_twinPos);
            _twinPos = main;
            twin.transform.position = main;
            if (twoFormTrial)
            {
                _openSecond = !_openSecond;
                UpdateTrialTargets();
            }
        }

        private DamageInfo GuardTrialDamage(DamageInfo hit)
        {
            bool second = _incomingSecond || _promotedSecond;
            if (_promotedSecond && !_incomingSecond || second != _openSecond || _trialState.Remaining(second) <= 0f) hit.Amount = 0f;
            else hit.Amount = Mathf.Min(hit.Amount, _trialState.Remaining(second));
            return hit;
        }

        private bool RedirectTwinDamage(DamageInfo hit, out float dealt)
        {
            // 두 번째 형상에 붙은 표식을 한 번만 적용하고, 닫힌 첫 형상의 표식은 이 피해에 섞지 않는다.
            if (trialForm.Health.IncomingModifier != null) hit = trialForm.Health.IncomingModifier(hit);
            System.Func<DamageInfo, DamageInfo> mainModifier = health.IncomingModifier;
            health.IncomingModifier = null;
            _incomingSecond = true;
            try { return health.TryTakeDamage(hit, out dealt); }
            finally
            {
                _incomingSecond = false;
                health.IncomingModifier = mainModifier;
            }
        }

        private void OnTrialDamaged(DamageInfo hit, float dealt)
        {
            if (!twoFormTrial || dealt <= 0f) return;
            bool second = _incomingSecond || _promotedSecond;
            _trialState.Apply(second, dealt);
            if (!second && !_trialState.FirstAlive && _trialState.SecondAlive)
            {
                // 본체는 합계 체력·패턴만 맡고, 두 번째 몸체의 Health/Life와 표식을 그대로 보존한다.
                _promotedSecond = true;
                _openSecond = true;
                body.position = _twinPos;
                transform.position = _twinPos;
                if (_mainSprite != null) _mainSprite.enabled = false;
                if (visual != null && visual.Model != null) visual.Model.gameObject.SetActive(false);
                health.IncomingModifier = null;
                status.Clear();
            }
            else if (second && !_trialState.SecondAlive && _trialState.FirstAlive)
            {
                _openSecond = false;
                if (trialForm != null) trialForm.gameObject.SetActive(false);
                if (twinOpeningRing != null) twinOpeningRing.enabled = false;
            }

            UpdateTrialTargets();
        }

        private void UpdateTrialTargets()
        {
            if (!twoFormTrial) return;
            if (_mainCollider != null) _mainCollider.enabled = !_openSecond;
            if (_twinCollider != null) _twinCollider.enabled = _openSecond && _trialState.SecondAlive;
            SetTargetable(!_openSecond);
            if (trialForm != null) trialForm.SetTargetable(_openSecond && _trialState.SecondAlive);
        }

        protected override void OnDied(Health h)
        {
            if (twin != null)
            {
                twin.enabled = false;
                if (_twinVisual != null || twoFormTrial) twin.gameObject.SetActive(false); // 3D 유령도 같이 사라진다
            }

            base.OnDied(h);
        }

        private void OnDestroy()
        {
            if (twin != null && twin.transform.parent == null) Destroy(twin.gameObject);
        }
    }
}
