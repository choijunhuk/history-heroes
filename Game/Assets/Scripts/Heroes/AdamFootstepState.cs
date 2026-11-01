using System;
using UnityEngine;

namespace HistoryHeroes.Heroes
{
    /// <summary>입력 시간 대신 실제로 연속 이동한 시간을 센다. 준비와 재사용을 따로 보관한다.</summary>
    public sealed class AdamFootstepState
    {
        private float _movement;
        private float _cooldownUntil;
        private float _bonusUntil;

        public bool IsReady { get; private set; }
        public float MovementProgress => _movement;
        public float CooldownRemaining(float now) => Math.Max(0f, _cooldownUntil - now);
        public bool BonusActive(float now) => now < _bonusUntil;

        /// <summary>몸이 입력 방향으로 실제 이동한 몫만 센다. 장치가 민 거리는 따로 뺀다.</summary>
        public static bool IsJoystickMovement(Vector2 input, Vector2 displacement, Vector2 externalDisplacement)
        {
            return input.sqrMagnitude > 0f
                && Vector2.Dot(displacement - externalDisplacement, input.normalized) > 0.0001f;
        }

        public void RecordMovement(float delta, bool actualMovement, float now, float requiredSeconds)
        {
            if (requiredSeconds <= 0f || now < _cooldownUntil)
            {
                _movement = 0f;
                return;
            }
            if (IsReady) return;
            if (!actualMovement)
            {
                _movement = 0f;
                return;
            }
            _movement += Math.Max(0f, delta);
            IsReady = _movement + 0.000001f >= requiredSeconds;
        }

        public bool TryBeginAttack(bool stopped, float now, float cooldown, float duration)
        {
            if (!stopped || !IsReady || now < _cooldownUntil) return false;
            IsReady = false;
            _movement = 0f;
            _cooldownUntil = now + Math.Max(0f, cooldown);
            _bonusUntil = now + Math.Max(0f, duration);
            return true;
        }

        public void OnMoved() => _bonusUntil = 0f;

        public void ResetForRoom()
        {
            _movement = 0f;
            IsReady = false;
            _bonusUntil = 0f;
        }
    }
}
