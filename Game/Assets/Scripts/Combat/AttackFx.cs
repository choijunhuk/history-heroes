using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Combat
{
    /// <summary>
    /// 공격 효과 그림 (임시 도형). 근접 베기 부채꼴, 튕김 번개 선, 포탄 폭발 원.
    /// 오브젝트를 모아 두고 다시 쓴다 (전투 중 계속 생겨서 매번 만들면 끊긴다).
    /// </summary>
    public static class AttackFx
    {
        private static ComponentPool<FxSprite> _pool;
        private static Transform _root;

        private static ComponentPool<FxSprite> Pool
        {
            get
            {
                // 장면이 바뀌면 담아 둔 오브젝트가 같이 사라진다 → 그때 새로 만든다
                if (_pool != null && _root != null) return _pool;
                _root = new GameObject("AttackFx").transform;
                var template = new GameObject("FxSprite", typeof(SpriteRenderer));
                template.SetActive(false);
                template.transform.SetParent(_root, false);
                var prefab = template.AddComponent<FxSprite>();
                _pool = new ComponentPool<FxSprite>(prefab, _root, prewarm: 8);
                return _pool;
            }
        }

        /// <summary>부채꼴 베기: center에서 direction 쪽으로 radius, degrees만큼</summary>
        public static void Arc(Vector2 center, Vector2 direction, float radius, float degrees, Color color, float duration = 0.14f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Pool.Get(center, Quaternion.identity).Play(PlaceholderSprites.Fan(Mathf.RoundToInt(degrees)), center, angle,
                Vector2.one * radius * 2f, color, duration, 6, Pool.Release, 1.08f);
        }

        /// <summary>두 점을 잇는 선 (튕기는 전기)</summary>
        public static void Line(Vector2 from, Vector2 to, float width, Color color, float duration = 0.12f)
        {
            Vector2 delta = to - from;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            Pool.Get(from, Quaternion.identity).Play(PlaceholderSprites.Square, (from + to) * 0.5f, angle,
                new Vector2(delta.magnitude, width), color, duration, 7, Pool.Release);
        }

        /// <summary>원 폭발 (포탄, 회전 베기)</summary>
        public static void Burst(Vector2 center, float radius, Color color, float duration = 0.18f)
        {
            Pool.Get(center, Quaternion.identity).Play(PlaceholderSprites.Circle, center, 0f,
                Vector2.one * radius * 1.6f, color, duration, 5, Pool.Release, 1.25f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _pool = null;
            _root = null;
        }
    }
}
