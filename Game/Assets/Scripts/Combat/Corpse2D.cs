using UnityEngine;

namespace HistoryHeroes.Combat
{
    /// <summary>2D 그림 시험판: 쓰러진 적 그림이 옆으로 넘어지며 0.6초에 사라진다 (Visual3D.Died2D가 만든다)</summary>
    public sealed class Corpse2D : MonoBehaviour
    {
        private const float Life = 0.6f;
        private SpriteRenderer _sprite;
        private float _t;
        private float _dir;

        public void Begin(SpriteRenderer sprite, bool flip)
        {
            _sprite = sprite;
            _dir = flip ? -1f : 1f;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / Life);
            transform.rotation = Quaternion.Euler(0f, 0f, _dir * 85f * Mathf.Min(1f, k * 2.2f));
            if (_sprite != null)
            {
                Color c = _sprite.color;
                c.a = 1f - k * k;
                _sprite.color = c;
            }

            if (k >= 1f) Destroy(gameObject);
        }
    }
}
