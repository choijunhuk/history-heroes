using System;
using System.Collections.Generic;

namespace HistoryHeroes.Combat
{
    /// <summary>보호막을 겹쳐 내구도가 늘지 않도록 같은 흡수량을 모든 출처의 잔량에서 뺀다.</summary>
    public sealed class ShieldSources
    {
        private struct Entry
        {
            public float Amount;
            public float Until;
        }

        private readonly Dictionary<object, Entry> _entries = new Dictionary<object, Entry>();
        private readonly List<object> _keys = new List<object>();

        public void SetAtLeast(object source, float amount, float until)
        {
            if (source == null || amount <= 0f) return;
            _entries.TryGetValue(source, out Entry previous);
            _entries[source] = new Entry { Amount = Math.Max(previous.Amount, amount), Until = until };
        }

        public float Remaining(object source, float now)
        {
            Expire(now);
            return source != null && _entries.TryGetValue(source, out Entry entry) ? entry.Amount : 0f;
        }

        public float Maximum(float now)
        {
            Expire(now);
            float largest = 0f;
            foreach (Entry entry in _entries.Values) largest = Math.Max(largest, entry.Amount);
            return largest;
        }

        public float Absorb(float damage, float now)
        {
            float absorbed = Math.Min(Math.Max(0f, damage), Maximum(now));
            if (absorbed <= 0f) return 0f;
            _keys.Clear();
            _keys.AddRange(_entries.Keys);
            foreach (object source in _keys)
            {
                Entry entry = _entries[source];
                entry.Amount = Math.Max(0f, entry.Amount - absorbed);
                if (entry.Amount <= 0f) _entries.Remove(source);
                else _entries[source] = entry;
            }
            return absorbed;
        }

        public void Expire(float now)
        {
            _keys.Clear();
            foreach (KeyValuePair<object, Entry> pair in _entries)
                if (now >= pair.Value.Until) _keys.Add(pair.Key);
            foreach (object source in _keys) _entries.Remove(source);
        }

        public void Remove(object source)
        {
            if (source != null) _entries.Remove(source);
        }

        public void Clear() => _entries.Clear();
    }
}
