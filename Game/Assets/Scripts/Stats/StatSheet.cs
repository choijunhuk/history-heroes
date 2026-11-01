using System;
using System.Collections.Generic;

namespace HistoryHeroes.Stats
{
    /// <summary>
    /// 한 캐릭터의 능력치. 보정값을 모아두고 물어볼 때마다 다시 계산한다.
    /// 최종값 = (기본값 + Flat 합) × (1 + Percent 합), 그 뒤 상한에서 자른다. (Stats.md 2장)
    /// 매번 다시 계산하므로 스킬을 얻는 순서와 관계없이 결과가 같다.
    /// </summary>
    public sealed class StatSheet
    {
        private readonly Dictionary<StatType, float> _base = new Dictionary<StatType, float>();
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();

        /// <summary>보정값이나 기본값이 바뀌면 알린다. (UI, 최대 체력 갱신 등)</summary>
        public event Action Changed;

        public void SetBase(StatType stat, float value)
        {
            _base[stat] = value;
            Changed?.Invoke();
        }

        public float GetBase(StatType stat)
        {
            return _base.TryGetValue(stat, out var value) ? value : 0f;
        }

        public void Add(in StatModifier modifier)
        {
            _modifiers.Add(modifier);
            Changed?.Invoke();
        }

        /// <summary>이 주인이 붙인 보정값을 전부 지운다. 스킬 교체·조합·기록 복원 때 쓴다.</summary>
        /// <summary>
        /// owner의 보정을 통째로 바꾼다. 지우고 다시 넣는 동안 알림을 **한 번만** 보낸다.
        /// (따로 하면 최대체력이 1150 → 1000 → 1250으로 두 번 바뀌어 회복이 두 번 일어난다)
        /// </summary>
        public void ReplaceFrom(object owner, System.Collections.Generic.IReadOnlyList<StatModifier> modifiers)
        {
            _modifiers.RemoveAll(m => ReferenceEquals(m.Owner, owner));
            for (int i = 0; i < modifiers.Count; i++) _modifiers.Add(modifiers[i]);
            Changed?.Invoke();
        }

        public int RemoveAllFrom(object owner)
        {
            int removed = _modifiers.RemoveAll(m => ReferenceEquals(m.Owner, owner));
            if (removed > 0) Changed?.Invoke();
            return removed;
        }

        /// <summary>이 출처의 보정값을 전부 지운다. 판이 끝나면 Skill 출처를 지운다.</summary>
        public int RemoveAllBySource(ModifierSource source)
        {
            int removed = _modifiers.RemoveAll(m => m.Source == source);
            if (removed > 0) Changed?.Invoke();
            return removed;
        }

        /// <param name="context">기본 공격을 계산할 땐 BasicAttack, 액티브는 Active, 그 밖에는 Both.</param>
        public float Get(StatType stat, StatTarget context = StatTarget.Both) => GetWithout(stat, null, context);

        /// <summary>owner의 보정을 뺀 값 (상한까지 같이 계산). 이순신 철갑이 "실제로 막은 피해"를 재는 데 쓴다</summary>
        public float GetWithout(StatType stat, object excludedOwner, StatTarget context = StatTarget.Both)
        {
            float flat = 0f;
            float percent = 0f;

            for (int i = 0; i < _modifiers.Count; i++)
            {
                StatModifier m = _modifiers[i];
                if (m.Stat != stat || !m.AppliesTo(context)) continue;
                if (excludedOwner != null && ReferenceEquals(m.Owner, excludedOwner)) continue;

                if (m.Kind == ModifierKind.Flat) flat += m.Value;
                else percent += m.Value;
            }

            float value = (GetBase(stat) + flat) * (1f + percent);
            return StatCaps.Clamp(stat, value);
        }

        public int ModifierCount => _modifiers.Count;
    }
}
