using System.Collections.Generic;

namespace HistoryHeroes.Player
{
    public enum AttackModifierKind
    {
        ExtraProjectiles, // 멀티샷: 발사체 +1 (근접은 베기 +1)
        Pierce,           // 관통 횟수
        Ricochet,         // 벽 튕김 횟수
    }

    /// <summary>
    /// 기본 공격의 모양을 바꾸는 값 (멀티샷·관통·벽 튕김). 능력치(StatSheet)와 따로 둔다.
    /// 주인(스킬 인스턴스) 단위로 붙이고 지울 수 있다. (Skills.md 1-1)
    /// </summary>
    public sealed class AttackModifiers
    {
        private readonly Dictionary<(object owner, AttackModifierKind kind), int> _values =
            new Dictionary<(object, AttackModifierKind), int>();

        public void Set(object owner, AttackModifierKind kind, int value)
        {
            if (value == 0) _values.Remove((owner, kind));
            else _values[(owner, kind)] = value;
        }

        public void RemoveAllFrom(object owner)
        {
            var toRemove = new List<(object, AttackModifierKind)>();
            foreach (var key in _values.Keys)
            {
                if (ReferenceEquals(key.owner, owner)) toRemove.Add(key);
            }

            foreach (var key in toRemove) _values.Remove(key);
        }

        public int Get(AttackModifierKind kind)
        {
            int sum = 0;
            foreach (var pair in _values)
            {
                if (pair.Key.kind == kind) sum += pair.Value;
            }

            return sum;
        }

        public void Clear() => _values.Clear();
    }
}
