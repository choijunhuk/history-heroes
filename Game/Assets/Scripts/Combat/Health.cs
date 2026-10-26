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

        /// <summary>실제로 깎인 양과 함께 알린다.</summary>
        public event Action<DamageInfo, float> Damaged;
        public event Action<Health> Died;

        /// <summary>누군가 피해를 받았을 때 (피해 숫자·화면 흔들림, 조사 A9). 장면마다 듣는 쪽이 붙고 뗀다</summary>
        public static event Action<Health, DamageInfo, float> AnyDamaged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => AnyDamaged = null;

        /// <param name="stats">피해 감소·회피를 읽을 능력치. 적은 null이어도 된다.</param>
        /// <param name="invulnerableDuration">맞은 뒤 무적 시간. 플레이어 0.5초.</param>
        public void Init(Team owner, float maxHp, StatSheet stats = null, float invulnerableDuration = 0f)
        {
            team = owner;
            _stats = stats;
            _invulnerableDuration = invulnerableDuration;
            _invulnerableUntil = 0f;
            Max = maxHp;
            Current = maxHp;
        }

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
            if (Guard != null)
            {
                info = Guard(info);
                if (info.Amount <= 0f) return false; // 막았다
            }

            if (IncomingModifier != null) info = IncomingModifier(info); // 사냥매 표식: 기본 공격 치명타 확률 +25%
            if (Invulnerable || Time.time < _invulnerableUntil) return false;

            float reduction = 0f;
            if (_stats != null)
            {
                if (UnityEngine.Random.value < _stats.Get(StatType.Dodge)) return false;
                reduction = _stats.Get(StatType.DamageReduction);
            }

            float incoming = info.Amount * (1f - reduction);
            if (Shield > 0f)
            {
                float absorbed = Mathf.Min(Shield, incoming);
                Shield -= absorbed;
                incoming -= absorbed;
                if (Shield <= 0f)
                {
                    Shield = 0f;
                    ShieldBroken?.Invoke();
                }
            }

            dealt = Mathf.Min(Current, incoming);
            if (Current - dealt <= 0f && PreventDeath != null && PreventDeath())
            {
                // 불굴: 죽지 않았다 (체력은 PreventDeath가 정함)
                if (_invulnerableDuration > 0f) _invulnerableUntil = Time.time + _invulnerableDuration;
                Damaged?.Invoke(info, dealt);
                AnyDamaged?.Invoke(this, info, dealt);
                return true;
            }

            Current -= dealt;
            if (_invulnerableDuration > 0f) _invulnerableUntil = Time.time + _invulnerableDuration;

            if (team == Team.Enemy) DamageLog.Record(info, dealt);
            Damaged?.Invoke(info, dealt);
            AnyDamaged?.Invoke(this, info, dealt);
            if (IsDead) Died?.Invoke(this);
            return true;
        }

        /// <summary>켜면 어떤 피해도 받지 않는다 (판이 끝난 뒤 남은 탄환 등)</summary>
        public bool Invulnerable { get; set; }

        /// <summary>보호막: 체력보다 먼저 깎인다 (수호 방패·철벽)</summary>
        public float Shield { get; private set; }

        /// <summary>보호막이 다 깎였을 때 (철벽: 깨질 때 폭발)</summary>
        public event Action ShieldBroken;

        /// <summary>죽을 만큼 맞았을 때 한 번 막을 기회 (불굴). true를 돌려주면 죽지 않고, 체력은 그쪽이 정한다</summary>
        public Func<bool> PreventDeath;

        /// <summary>보호막을 채운다. 이미 있는 것보다 크면 그만큼으로 (겹쳐서 무한히 쌓이지 않게)</summary>
        public void SetShieldAtLeast(float amount) => Shield = Mathf.Max(Shield, amount);

        public void ClearShield() => Shield = 0f;

        /// <summary>죽지 않고 체력을 정한다 (불굴)</summary>
        public void Revive(float hp) => Current = Mathf.Clamp(hp, 1f, Max);

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Min(Max, Current + amount);
        }
    }
}
