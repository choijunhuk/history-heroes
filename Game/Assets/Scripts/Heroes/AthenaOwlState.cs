using System.Collections.Generic;
using HistoryHeroes.Combat;

namespace HistoryHeroes.Heroes
{
    /// <summary>풀에서 다시 나온 적의 옛 표식을 쓰지 않도록 대상과 생명을 함께 기억한다.</summary>
    public sealed class AthenaOwlState
    {
        private readonly List<Mark> _marks = new List<Mark>(3);
        private object _owner;

        private struct Mark
        {
            public object Target;
            public int Life;
            public float Until;
        }

        public int Count => _marks.Count;

        public void Begin(object owner)
        {
            Clear();
            _owner = owner;
        }

        public void ClearFor(object owner)
        {
            if (ReferenceEquals(_owner, owner)) Clear();
        }

        public void Clear()
        {
            _marks.Clear();
            _owner = null;
        }

        public void Add(object target, int life, float now, float duration, int maxTargets)
        {
            if (target == null || maxTargets <= 0) return;
            for (int i = _marks.Count - 1; i >= 0; i--)
                if (_marks[i].Until <= now || ReferenceEquals(_marks[i].Target, target)) _marks.RemoveAt(i);
            if (_marks.Count >= maxTargets) return;
            _marks.Add(new Mark { Target = target, Life = life, Until = now + duration });
        }

        public bool IsMarked(object target, int life, float now)
        {
            foreach (Mark mark in _marks)
                if (ReferenceEquals(mark.Target, target) && mark.Life == life && mark.Until > now) return true;
            return false;
        }

        public float BonusFor(object target, int life, float now, DamageKind kind, float coefficient)
            => kind == DamageKind.BasicAttack && IsMarked(target, life, now) ? coefficient : 0f;
    }
}
