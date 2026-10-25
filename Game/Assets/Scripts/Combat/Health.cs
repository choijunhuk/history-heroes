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

        /// <summary>실제로 깎인 양과 함께 알린다.</summary>
        public event Action<DamageInfo, float> Damaged;
        public event Action<Health> Died;

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
            if (Time.time < _invulnerableUntil) return false;

            float reduction = 0f;
            if (_stats != null)
            {
                if (UnityEngine.Random.value < _stats.Get(StatType.Dodge)) return false;
                reduction = _stats.Get(StatType.DamageReduction);
            }

            dealt = Mathf.Min(Current, info.Amount * (1f - reduction));
            Current -= dealt;
            if (_invulnerableDuration > 0f) _invulnerableUntil = Time.time + _invulnerableDuration;

            if (team == Team.Enemy) DamageLog.Record(info.Kind, dealt);
            Damaged?.Invoke(info, dealt);
            if (IsDead) Died?.Invoke(this);
            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Min(Max, Current + amount);
        }
    }
}
