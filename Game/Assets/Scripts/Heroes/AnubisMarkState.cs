using System.Collections.Generic;

namespace HistoryHeroes.Heroes
{
    /// <summary>실제 기본 공격의 첫 명중만 센다. 대상이 바뀌면 쌓은 표식은 사라지고 재사용 시간은 남는다.</summary>
    public sealed class AnubisMarkState
    {
        private struct Cooldown
        {
            public int Life;
            public float Until;
        }

        private readonly Dictionary<object, Cooldown> _cooldowns = new Dictionary<object, Cooldown>();
        private readonly List<object> _expired = new List<object>();
        private object _target;
        private int _life;
        private int _hits;

        public int Hits => _hits;

        public bool IsReady(float now, int hitsNeeded)
        {
            if (_target == null || hitsNeeded <= 0 || _hits < hitsNeeded) return false;
            return !_cooldowns.TryGetValue(_target, out Cooldown previous) || previous.Life != _life || now >= previous.Until;
        }

        public static float EffectiveSlow(HeroSignature signature, float bossScale, bool isBoss) =>
            signature.MarkSlow * (isBoss ? bossScale : 1f);

        public static float EffectiveDuration(HeroSignature signature) =>
            signature.MarkDuration < 1f ? signature.MarkDuration : 1f;

        public bool RecordHit(object target, int life, float now, bool primary, int hitsNeeded, float cooldown)
        {
            if (!primary || target == null || hitsNeeded <= 0) return false;
            if (!ReferenceEquals(_target, target) || _life != life)
            {
                _target = target;
                _life = life;
                _hits = 0;
            }

            _hits++;
            if (_hits <= hitsNeeded) return false;
            _hits = 0;
            if (_cooldowns.TryGetValue(target, out Cooldown previous) && previous.Life == life && now < previous.Until) return false;
            _cooldowns[target] = new Cooldown { Life = life, Until = now + cooldown };

            // 오래 끝난 적을 주기적으로 비워 긴 판에서도 대상 참조가 쌓이지 않게 한다.
            if (_cooldowns.Count >= 32)
            {
                _expired.Clear();
                foreach (KeyValuePair<object, Cooldown> entry in _cooldowns)
                    if (entry.Value.Until <= now) _expired.Add(entry.Key);
                foreach (object key in _expired) _cooldowns.Remove(key);
            }

            return true;
        }

        public void Reset()
        {
            _target = null;
            _life = 0;
            _hits = 0;
            _cooldowns.Clear();
            _expired.Clear();
        }
    }
}
