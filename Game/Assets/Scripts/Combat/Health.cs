using System;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Combat
{
    /// <summary>체력. 피해 감소·회피·피격 무적은 여기서 처리한다. (Stats.md 1장)</summary>
    public sealed class Health : MonoBehaviour
    {
        [SerializeField] private Team team = Team.Enemy;

        private StatSheet _stats;
        private float _invulnerableDuration;
        private float _invulnerableUntil;

        public Team Team => team;
        public float Max { get; private set; }
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;

        /// <summary>맞기 직전에 피해를 바꾼다 (사냥매 사냥 표식처럼 "이 적에게만" 붙는 효과). 적은 다시 나올 때 비운다</summary>
        public Func<DamageInfo, DamageInfo> IncomingModifier { get; set; }

        /// <summary>몸이 막는 피해 (방패병의 방패). IncomingModifier보다 먼저. 피해를 0으로 돌려주면 "막음"</summary>
        public Func<DamageInfo, DamageInfo> Guard { get; set; }

        /// <summary>
        /// 들어온 피해 → 실제로 깎을 피해 (스사노오 시련의 두 형상 공유 체력). 형상 몫보다 덜 깎게도, 마지막 형상의 마지막 피해는 남은 체력 전부로도 정한다
        /// (형상 체력과 본체 체력을 따로 깎으면 소수 오차로 0.004가 남아 이길 수 없었다, Codex 리뷰 🔴).
        /// </summary>
        public Func<float, float> FinalDamage { get; set; }

        /// <summary>두 시험 형상이 체력을 공유할 때 실제 본체의 피해 결과를 그대로 돌려준다.</summary>
        public delegate bool DamageReceiver(DamageInfo info, out float dealt);
        public DamageReceiver DamageRedirect { get; set; }

        /// <summary>체력은 이 몸에서 깎되, 피해 숫자와 피격 연출은 실제로 맞은 몸에 표시한다.</summary>
        public Health DamageDisplayTarget { get; set; }

        /// <summary>실제로 깎인 양을 체력·단계·처치 상태에 알린다.</summary>
        public event Action<DamageInfo, float> Damaged;
        /// <summary>실제로 맞은 몸의 피격 연출에만 알린다.</summary>
        public event Action<DamageInfo, float> VisualDamaged;
        public event Action<Health> Died;

        /// <summary>누군가 피해를 받았을 때. 처치·기믹은 실제 체력이 깎인 몸체를 받는다.</summary>
        public static event Action<Health, DamageInfo, float> AnyDamaged;
        /// <summary>피해 숫자·타격 불꽃은 실제로 맞은 몸체를 받는다.</summary>
        public static event Action<Health, DamageInfo, float> AnyVisualDamaged;

        /// <summary>적이 받는 피해를 고친다 (엘리트 "지키는" 속성: 둘레 적 -30%). 없으면 그대로</summary>
        public static Func<Health, DamageInfo, DamageInfo> EnemyIncoming;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AnyDamaged = null;
            AnyVisualDamaged = null;
        }

        /// <param name="stats">피해 감소·회피를 읽을 능력치. 적은 null이어도 된다.</param>
        /// <param name="invulnerableDuration">맞은 뒤 무적 시간. 플레이어 0.5초.</param>
        public void Init(Team owner, float maxHp, StatSheet stats = null, float invulnerableDuration = 0f)
        {
            team = owner;
            _stats = stats;
            _invulnerableDuration = invulnerableDuration;
            _invulnerableUntil = 0f;
            ClearShield();
            DamageRedirect = null;
            DamageDisplayTarget = null;
            FinalDamage = null;
            Max = maxHp;
            Current = maxHp;
            DamageScale = 1f;
            Life++;
        }

        /// <summary>몇 번째 삶인가 (Init마다 +1). 풀에서 다시 나온 적을 앞의 적과 헷갈리지 않게 (신궁 표식 · 팔진도)</summary>
        public int Life { get; private set; }

        /// <summary>방금 받은 피해의 감소 전 값 (받는 피해 증가 · DamageScale까지, 피해 감소 · 보호막 전). Damaged 처리 안에서 읽는다 (이순신 철갑)</summary>
        public float LastBeforeReduction { get; private set; }

        /// <summary>받는 피해 배수 (기믹 보스 수정 기둥 보호막: 0.25 = -75%). 다시 Init하면 1</summary>
        public float DamageScale { get; set; } = 1f;

        /// <summary>최대 체력이 늘면 늘어난 만큼 회복한다 (Skills.md 최대체력 패시브).</summary>
        public void SetMax(float newMax)
        {
            float gained = newMax - Max;
            Max = newMax;
            Current = Mathf.Clamp(Current + Mathf.Max(0f, gained), 0f, Max);
        }

        public bool TryTakeDamage(DamageInfo info) => TryTakeDamage(info, out _);

        /// <param name="dealt">실제로 깎인 체력 (남은 체력보다 큰 피해는 남은 만큼만). 흡혈은 이 값으로 계산한다.</param>
        public bool TryTakeDamage(DamageInfo info, out float dealt)
        {
            dealt = 0f;
            if (IsDead || info.AttackerTeam == team) return false;
            if (DamageRedirect != null) return DamageRedirect(info, out dealt);
            if (Invulnerable || Time.time < _invulnerableUntil) return false;
            if (Guard != null)
            {
                info = Guard(info);
                if (info.Amount <= 0f) return false; // 막았다
            }

            if (IncomingModifier != null) info = IncomingModifier(info); // 사냥매 표식: 기본 공격 치명타 확률 +25%
            if (team == Team.Enemy && EnemyIncoming != null) info = EnemyIncoming(this, info);
            float reduction = 0f, taken = 0f;
            if (_stats != null)
            {
                if (UnityEngine.Random.value < _stats.Get(StatType.Dodge)) return false;
                reduction = _stats.Get(StatType.DamageReduction);
                taken = _stats.Get(StatType.DamageTaken); // 피해 감소는 0 밑으로 안 내려가서, "받는 피해 +%"는 따로 곱한다
            }

            LastBeforeReduction = info.Amount * (1f + taken) * DamageScale;
            float incoming = LastBeforeReduction * (1f - reduction);
            if (Shield > 0f)
            {
                bool guardWasPresent = _shields.Remaining(_legacyShieldOwner, Time.time) > 0f;
                float absorbed = _shields.Absorb(incoming, Time.time);
                incoming -= absorbed;
                // 부처 보호막의 만료·소진으로 철벽 폭발을 새로 만들지 않는다.
                if (guardWasPresent && _shields.Remaining(_legacyShieldOwner, Time.time) <= 0f)
                    ShieldBroken?.Invoke();
            }

            if (FinalDamage != null) incoming = Mathf.Max(0f, FinalDamage(incoming));
            dealt = Mathf.Min(Current, incoming);
            if (Current - dealt <= 0f && PreventDeath != null && PreventDeath())
            {
                // 불굴: 죽지 않았다 (체력은 PreventDeath가 정함)
                if (_invulnerableDuration > 0f) _invulnerableUntil = Time.time + _invulnerableDuration;
                Damaged?.Invoke(info, dealt);
                ReportVisualDamage(info, dealt);
                return true;
            }

            Current -= dealt;
            if (_invulnerableDuration > 0f) _invulnerableUntil = Time.time + _invulnerableDuration;

            if (team == Team.Enemy) DamageLog.Record(info, dealt);
            Damaged?.Invoke(info, dealt);
            ReportVisualDamage(info, dealt);
            if (IsDead) Died?.Invoke(this);
            return true;
        }

        private void ReportVisualDamage(DamageInfo info, float dealt)
        {
            Health target = DamageDisplayTarget != null ? DamageDisplayTarget : this;
            target.VisualDamaged?.Invoke(info, dealt);
            AnyDamaged?.Invoke(this, info, dealt);
            AnyVisualDamaged?.Invoke(target, info, dealt);
        }

        /// <summary>켜면 어떤 피해도 받지 않는다 (판이 끝난 뒤 남은 탄환 등)</summary>
        public bool Invulnerable { get; set; }

        /// <summary>보호막: 체력보다 먼저 깎인다 (수호 방패·철벽)</summary>
        public float Shield => _shields.Maximum(Time.time);

        private readonly ShieldSources _shields = new ShieldSources();
        private readonly object _legacyShieldOwner = new object();

        /// <summary>보호막이 다 깎였을 때 (철벽: 깨질 때 폭발)</summary>
        public event Action ShieldBroken;

        /// <summary>죽을 만큼 맞았을 때 한 번 막을 기회 (불굴). true를 돌려주면 죽지 않고, 체력은 그쪽이 정한다</summary>
        public Func<bool> PreventDeath;

        /// <summary>보호막을 채운다. 이미 있는 것보다 크면 그만큼으로 (겹쳐서 무한히 쌓이지 않게)</summary>
        public void SetShieldAtLeast(float amount) => _shields.SetAtLeast(_legacyShieldOwner, amount, float.PositiveInfinity);

        public void ClearShield() => _shields.Clear();

        /// <summary>같은 보호를 덧씌워도 합산하지 않는다. 만료 시 이 출처만 지운다.</summary>
        public void SetShieldFrom(object source, float amount, float duration, float now)
        {
            _shields.Expire(now);
            _shields.SetAtLeast(source, amount, now + Mathf.Max(0f, duration));
        }

        public void ClearShieldFrom(object source) => _shields.Remove(source);
        public float ShieldFrom(object source, float now) => _shields.Remaining(source, now);
        public void ExpireShields(float now) => _shields.Expire(now);

        /// <summary>판 포기: 막기·보호막·무적·불굴을 건너뛰고 바로 쓰러진다 (일시정지 메뉴, v1.0)</summary>
        public void Kill()
        {
            if (IsDead) return;
            ClearShield();
            Current = 0f;
            Died?.Invoke(this);
        }

        /// <summary>죽지 않고 체력을 정한다 (불굴)</summary>
        public void Revive(float hp) => Current = Mathf.Clamp(hp, 1f, Max);

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Min(Max, Current + amount);
        }
    }
}
