using System.Collections.Generic;

namespace HistoryHeroes.Enemy
{
    /// <summary>둔화마다 끝나는 시각을 따로 둔다. 약한 둔화가 강한 둔화의 시간을 늘리지 않는다.</summary>
    public sealed class SlowChannels
    {
        private struct Slow
        {
            public float Fraction;
            public float Until;
        }

        private Slow _legacy;
        private readonly Dictionary<object, Slow> _sources = new Dictionary<object, Slow>();
        private readonly List<object> _expired = new List<object>();

        public void Apply(float fraction, float duration, float now, object source = null)
        {
            if (source == null)
            {
                // 이전 기술들의 한 채널 동작은 저장하지 않는 상태이므로 그대로 유지한다.
                if (now >= _legacy.Until || fraction >= _legacy.Fraction) _legacy.Fraction = Clamp(fraction);
                if (now + duration > _legacy.Until) _legacy.Until = now + duration;
                return;
            }

            _sources.TryGetValue(source, out Slow slow);
            if (now >= slow.Until || fraction >= slow.Fraction) slow.Fraction = Clamp(fraction);
            if (now + duration > slow.Until) slow.Until = now + duration;
            _sources[source] = slow;

            if (_sources.Count < 8) return;
            _expired.Clear();
            foreach (KeyValuePair<object, Slow> entry in _sources)
                if (now >= entry.Value.Until) _expired.Add(entry.Key);
            foreach (object key in _expired) _sources.Remove(key);
        }

        public float FractionAt(float now)
        {
            float best = now < _legacy.Until ? _legacy.Fraction : 0f;
            foreach (Slow slow in _sources.Values)
                if (now < slow.Until && slow.Fraction > best) best = slow.Fraction;
            return best;
        }

        public void Clear()
        {
            _legacy = default;
            _sources.Clear();
            _expired.Clear();
        }

        private static float Clamp(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
