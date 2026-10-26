using System;
using UnityEngine;

namespace HistoryHeroes.Combat
{
    /// <summary>잠깐 보였다가 옅어지며 사라지는 그림 한 장 (베기 부채꼴, 튕김 선, 포탄 폭발). AttackFx가 빌려 쓴다.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FxSprite : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Color _color;
        private float _duration;
        private float _age;
        private Vector3 _grow;
        private Action<FxSprite> _release;
        private bool _held;

        /// <summary>오브젝트를 모아 두고 다시 쓰는 풀 (유성 표시·회오리·코일처럼 스킬이 잡고 있는 그림)</summary>
        public static Common.ComponentPool<FxSprite> CreatePool(Transform root)
        {
            var template = new GameObject("FxSprite", typeof(SpriteRenderer));
            template.SetActive(false);
            template.transform.SetParent(root, false);
            return new Common.ComponentPool<FxSprite>(template.AddComponent<FxSprite>(), root, prewarm: 2);
        }

        /// <summary>사라지지 않고 계속 보이는 그림 (주인이 위치·크기를 직접 바꾸고, 다 쓰면 풀에 돌려준다)</summary>
        public SpriteRenderer Hold(Sprite sprite, Vector2 position, Vector2 scale, Color color, int order)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            _held = true;
            _renderer.sprite = sprite;
            _renderer.color = color;
            _renderer.sortingOrder = order;
            transform.position = position;
            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(scale.x, scale.y, 1f);
            return _renderer;
        }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        public void Play(Sprite sprite, Vector2 position, float angle, Vector2 scale, Color color, float duration, int order,
            Action<FxSprite> release, float growTo = 1f)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            _held = false;
            _renderer.sprite = sprite;
            _renderer.sortingOrder = order;
            _renderer.color = color;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            transform.localScale = new Vector3(scale.x, scale.y, 1f);
            _grow = new Vector3(scale.x * growTo, scale.y * growTo, 1f);
            _color = color;
            _duration = Mathf.Max(0.01f, duration);
            _age = 0f;
            _release = release;
        }

        private void Update()
        {
            if (_held) return;
            // 게임이 멈춰도(카드 화면) 효과는 마저 사라지게 실제 시간으로
            _age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_age / _duration);
            _renderer.color = new Color(_color.r, _color.g, _color.b, _color.a * (1f - t));
            if (_grow != transform.localScale) transform.localScale = Vector3.Lerp(transform.localScale, _grow, t);
            if (t >= 1f) _release?.Invoke(this);
        }
    }
}
