using System;

namespace HistoryHeroes.Player
{
    /// <summary>
    /// 멈춤 판정 (Docs/Design/Stats.md 1-1).
    /// 입력이 0.1초 이상 없으면 "멈춤". StoppedTime은 멈춤이 된 뒤부터 센다.
    /// 일시정지 중엔 deltaTime이 0이라 시간이 흐르지 않는다.
    /// </summary>
    public sealed class StopDetector
    {
        public const float StopThreshold = 0.1f;
        private const float DeadZoneSqr = 0.01f;

        private float _idleTime = StopThreshold; // 시작할 때는 멈춘 상태

        public bool IsStopped => _idleTime >= StopThreshold;

        /// <summary>멈춤이 된 뒤 지난 시간. 움직이는 동안은 0.</summary>
        public float StoppedTime => IsStopped ? _idleTime - StopThreshold : 0f;

        public event Action Stopped;
        public event Action Moved;

        public void Tick(UnityEngine.Vector2 input, float deltaTime)
        {
            bool wasStopped = IsStopped;

            if (input.sqrMagnitude < DeadZoneSqr) _idleTime += deltaTime;
            else _idleTime = 0f;

            if (!wasStopped && IsStopped) Stopped?.Invoke();
            else if (wasStopped && !IsStopped) Moved?.Invoke();
        }
    }
}
