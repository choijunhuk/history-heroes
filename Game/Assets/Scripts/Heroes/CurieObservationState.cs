namespace HistoryHeroes.Heroes
{
    /// <summary>이동·소비 전 화살은 무효로 하되, 비행 시간이 공격 간격보다 길어도 정상 명중을 놓치지 않는다.</summary>
    public sealed class CurieObservationState
    {
        private object _target;
        private int _life;
        private int _attack;
        private int _invalidThrough;
        public bool HasMark => _target != null;

        public int BeginAttack(out object markedTarget, out int markedLife)
        {
            markedTarget = _target;
            markedLife = _life;
            int attack = ++_attack;
            if (HasMark) _invalidThrough = attack;
            _target = null;
            _life = 0;
            return attack;
        }

        public bool TryMark(object target, int life, int attack, bool primary)
        {
            if (!primary || target == null || attack <= _invalidThrough || attack > _attack || HasMark) return false;
            _target = target;
            _life = life;
            _invalidThrough = attack;
            return true;
        }

        /// <summary>이동 전 투사체는 무효로 하고 이미 남긴 관측 표식만 유지한다.</summary>
        public void InvalidateAttack()
        {
            _invalidThrough = ++_attack;
        }

        public void Reset()
        {
            InvalidateAttack();
            _target = null;
            _life = 0;
        }

        public static bool CanRelease(object target, int storedLife, int currentLife, bool alive) =>
            target != null && alive && storedLife == currentLife;
    }
}
