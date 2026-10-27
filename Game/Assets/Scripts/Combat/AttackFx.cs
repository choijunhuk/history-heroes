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

        /// <summary>몸 높이 (3D: 튕기는 전기는 발이 아니라 몸과 몸을 잇는다, 위 = -Z)</summary>
        public const float BodyHeight = 0.9f;

        /// <summary>두 점을 잇는 선 (튕기는 전기). 몸 높이에 띄운다</summary>
        public static void Line(Vector2 from, Vector2 to, float width, Color color, float duration = 0.12f)
        {
            Vector2 delta = to - from;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            FxSprite fx = Pool.Get(from, Quaternion.identity);
            fx.Play(PlaceholderSprites.Square, (from + to) * 0.5f, angle, new Vector2(delta.magnitude, width), color, duration, 7, Pool.Release);
            Vector3 p = fx.transform.position;
            fx.transform.position = new Vector3(p.x, p.y, -BodyHeight);
        }

        /// <summary>이 반지름 이상 폭발은 화면을 크게 덮는 섬광으로 본다 (번쩍임 줄이기 대상)</summary>
        public const float BigBurstRadius = 2.5f;
        private const int MaxBigBurstsPerSecond = 3;
        private static readonly float[] BigBurstTimes = { float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity }; // 최근에 보여준 큰 폭발 시각 (돌려 쓰는 칸, 크기 = MaxBigBurstsPerSecond)
        private static int _bigBurstNext;

        /// <summary>원 폭발 (포탄, 회전 베기)</summary>
        public static void Burst(Vector2 center, float radius, Color color, float duration = 0.18f)
        {
            if (radius >= BigBurstRadius && Records.SaveSystem.Data.settings.reduceFlashes)
            {
                // 번쩍임 줄이기 (조사 A9, WCAG 2.3.1): 옅게, 어느 1초를 잘라도 3번까지만.
                // 가장 오래된 칸이 1초 안이면 이미 3번 보여준 것 → 이번 것은 건너뛴다
                float now = Time.unscaledTime;
                if (now - BigBurstTimes[_bigBurstNext] < 1f) return;
                BigBurstTimes[_bigBurstNext] = now;
                _bigBurstNext = (_bigBurstNext + 1) % MaxBigBurstsPerSecond;
                color.a *= 0.4f;
            }

            Pool.Get(center, Quaternion.identity).Play(PlaceholderSprites.Circle, center, 0f,
                Vector2.one * radius * 1.6f, color, duration, 5, Pool.Release, 1.25f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _pool = null;
            _root = null;
            for (int i = 0; i < BigBurstTimes.Length; i++) BigBurstTimes[i] = float.NegativeInfinity;
            _bigBurstNext = 0;
        }
    }
}
