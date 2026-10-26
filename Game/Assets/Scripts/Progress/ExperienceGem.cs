using System;
using UnityEngine;

namespace HistoryHeroes.Progress
{
    /// <summary>경험치 보석. 가까이 가면 끌려와서 먹힌다.</summary>
    public sealed class ExperienceGem : MonoBehaviour
    {
        [SerializeField] private float magnetRadius = 2.2f;
        [SerializeField] private float pickupRadius = 0.45f;
        [SerializeField] private float flySpeed = 11f;

        private Transform _player;
        private float _value;
        private bool _attracted;
        private Action<ExperienceGem, float> _collected;

        public void Drop(Transform player, float value, Action<ExperienceGem, float> collected)
        {
            _player = player;
            _value = value;
            transform.localScale = new Vector3(0.45f, 0.45f, 1f);
            _collected = collected;
            _attracted = false;
        }

        public float Value => _value;

        /// <summary>다른 보석을 합친다 (값을 더하고 조금 커진다, 서바이벌에서 보석이 너무 많을 때)</summary>
        public void Absorb(float value)
        {
            _value += value;
            float scale = Mathf.Clamp(0.45f + Mathf.Sqrt(_value) * 0.08f, 0.45f, 1.4f);
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>방을 다 깨면 남은 보석을 한꺼번에 끌어온다.</summary>
        public void Attract() => _attracted = true;

        private void Update()
        {
            if (_player == null || Time.deltaTime <= 0f) return;

            Vector2 to = _player.position - transform.position;
            float distance = to.magnitude;

            if (distance <= pickupRadius)
            {
                _collected?.Invoke(this, _value);
                return;
            }

            if (_attracted || distance <= magnetRadius)
            {
                _attracted = true;
                transform.position += (Vector3)(to / distance * flySpeed * Time.deltaTime);
            }
        }
    }
}
