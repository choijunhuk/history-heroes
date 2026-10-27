using System;
using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 떨어지는 화살·번개 줄기 같은 짧은 연출. 떨어진 순간 onLand를 부르고, 잠깐 남았다가 흐려진다.
    /// follow를 주면 움직이는 적을 따라가서 맞는다 (화살비는 화면의 모든 적에게 꼭 맞는 스킬).
    /// 3D (Art3D-Plan 6번): "위"는 -Z(하늘). 그림은 세워서(카메라 쪽을 보게) 하늘에서 땅으로 떨어진다. visualOffset.y = 땅에서 그림 가운데까지 높이
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class StrikeVisual : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Color _color;
        private Vector2 _offset;
        private Vector3 _from;
        private Vector2 _to;

        /// <summary>그림을 세운다: 스프라이트의 위(+Y)가 하늘(-Z), 보이는 면은 카메라 쪽(-Y)</summary>
        private static readonly Quaternion Standing = Quaternion.Euler(-90f, 0f, 0f);

        /// <summary>그림 키의 절반 (세운 그림의 가운데가 이보다 낮으면 땅에 묻힌다)</summary>
        private float _half;

        /// <summary>땅 위 점 + 높이 → 월드 (위 = -Z). 그림 아래끝이 땅 밑으로 가지 않게 가운데를 최소 반 키만큼 띄운다 (Codex 3D 코드 리뷰 2차 3번)</summary>
        private Vector3 At(Vector2 ground, Vector2 offset, float height) => new Vector3(ground.x + offset.x, ground.y, -(Mathf.Max(offset.y, _half) + height));
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
            transform.rotation = Standing;
            _half = size.y * 0.5f;
            _from = At(target, visualOffset, fallHeight);
            transform.position = _from;
            _renderer.color = color;
            if (_fallTime <= 0f) Land();
        }

        private void Update()
        {
            if (GamePause.IsPaused) return;
            _t += Time.deltaTime;

            if (!_landed)
            {
                if (_follow != null && _follow.gameObject.activeInHierarchy) _to = _follow.position;
                Vector3 end = At(_to, _offset, 0f);
                float k = Mathf.Clamp01(_t / _fallTime);
                transform.position = Vector3.Lerp(_from, end, k);
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
            transform.position = At(_to, _offset, 0f);
            _onLand?.Invoke();
            _onLand = null;
        }
    }
}
