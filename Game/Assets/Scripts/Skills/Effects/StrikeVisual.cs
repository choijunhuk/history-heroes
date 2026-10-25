using System;
using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 떨어지는 화살·번개 줄기 같은 짧은 연출. 떨어진 순간 onLand를 부르고, 잠깐 남았다가 흐려진다.
    /// follow를 주면 움직이는 적을 따라가서 맞는다 (화살비는 화면의 모든 적에게 꼭 맞는 스킬).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class StrikeVisual : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Color _color;
        private Vector2 _offset;
        private Vector2 _from;
        private Vector2 _to;
        private Transform _follow;
        private float _fallTime;
        private float _linger;
        private float _t;
        private bool _landed;
        private Action _onLand;
        private Action<StrikeVisual> _release;

        public static ComponentPool<StrikeVisual> CreatePool(Transform root, int sortingOrder = 10)
        {
            var template = new GameObject("StrikeVisual");
            template.SetActive(false);
            template.transform.SetParent(root, false);
            var sr = template.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprites.Square;
            sr.sortingOrder = sortingOrder;
            return new ComponentPool<StrikeVisual>(template.AddComponent<StrikeVisual>(), root, prewarm: 8);
        }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        /// <param name="fallHeight">얼마나 위에서 떨어지나. 0이면 바로 맞음</param>
        /// <param name="visualOffset">맞는 지점에서 그림 중심까지 (번개 줄기는 위로 반 길이)</param>
        public void Play(Vector2 target, Transform follow, float fallHeight, float fallTime, float linger,
            Vector2 size, Vector2 visualOffset, Color color, Action onLand, Action<StrikeVisual> release)
        {
            _to = target;
            _follow = follow;
            _offset = visualOffset;
            _fallTime = fallHeight > 0f ? Mathf.Max(0.01f, fallTime) : 0f;
            _linger = linger;
            _onLand = onLand;
            _release = release;
            _color = color;
            _t = 0f;
            _landed = false;

            transform.localScale = new Vector3(size.x, size.y, 1f);
            _from = target + visualOffset + Vector2.up * fallHeight;
            transform.position = _from;
            _renderer.color = color;
            if (_fallTime <= 0f) Land();
        }

        private void Update()
        {
            _t += Time.deltaTime;

            if (!_landed)
            {
                if (_follow != null && _follow.gameObject.activeInHierarchy) _to = _follow.position;
                Vector2 end = _to + _offset;
                float k = Mathf.Clamp01(_t / _fallTime);
                transform.position = Vector2.Lerp(_from, end, k);
                if (k >= 1f) Land();
                return;
            }

            Color c = _color;
            c.a *= 1f - Mathf.Clamp01(_t / Mathf.Max(0.01f, _linger));
            _renderer.color = c;
            if (_t >= _linger) _release?.Invoke(this);
        }

        private void Land()
        {
            _landed = true;
            _t = 0f;
            transform.position = _to + _offset;
            _onLand?.Invoke();
            _onLand = null;
        }
    }
}
