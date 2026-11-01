using System.Collections.Generic;

namespace HistoryHeroes.Heroes
{
    /// <summary>명중 연속 횟수와 대상별 제어 제한을 나눈다. 이동으로 보스 제어 제한을 우회할 수 없다.</summary>
    public sealed class WuzetianSealState
    {
        private struct Control
        {
            public int Life;
            public float Until;
        }

        private readonly Dictionary<object, Control> _controls = new Dictionary<object, Control>();
        private readonly List<object> _expired = new List<object>();
        private object _target;
        private int _life;
        public int Hits { get; private set; }

        public bool RecordHit(object target, int life, bool stopped, bool primary, int hitsNeeded)
        {
            if (!stopped || hitsNeeded <= 0)
            {
                Reset(false);
                return false;
            }
            if (!primary || target == null) return false;
            if (!ReferenceEquals(target, _target) || life != _life)
            {
                _target = target;
                _life = life;
                Hits = 0;
            }
            Hits++;
            if (Hits < hitsNeeded) return false;
            Hits = 0;
            return true;
        }

        public static bool CanRoot(bool boss, bool elite, bool sameTarget, float distanceSquared, float radius) =>
            !boss && !elite && !sameTarget && radius > 0f && distanceSquared <= radius * radius;

        public bool TryControl(object target, int life, float now, bool boss, bool elite, HeroData hero, out float slow)
        {
            slow = 0f;
            if (target == null || elite || hero == null || hero.sealHits <= 0) return false;
            if (_controls.TryGetValue(target, out Control previous) && previous.Life == life && now < previous.Until) return false;
            slow = boss ? System.Math.Min(0.2f, System.Math.Max(0f, hero.sealBossSlow)) : 1f;
            if (slow <= 0f) return false;
            _controls[target] = new Control { Life = life, Until = now + hero.sealCooldown };
            if (_controls.Count >= 32)
            {
                _expired.Clear();
                foreach (KeyValuePair<object, Control> entry in _controls)
                    if (entry.Value.Until <= now) _expired.Add(entry.Key);
                foreach (object key in _expired) _controls.Remove(key);
            }
            return true;
        }

        public void Reset(bool clearControls = true)
        {
            _target = null;
            _life = 0;
            Hits = 0;
            if (!clearControls) return;
            _controls.Clear();
            _expired.Clear();
        }
    }
}
