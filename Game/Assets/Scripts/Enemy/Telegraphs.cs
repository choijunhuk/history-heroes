using System.Collections.Generic;
using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 공격 예고 표시 (원·선·네모) 풀. "모든 적은 공격 전에 예고한다" (Content 4장) — 보스·중간 보스·함정이 같이 쓴다.
    /// 장면이 바뀌면 루트가 사라지므로 그때 풀을 비운다.
    /// </summary>
    public static class Telegraphs
    {
        private static readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        private static Transform _root;

        // 테두리 원(RimCircle)이라 안쪽은 이 알파의 55% — 테두리가 진하게 보이게 조금 올렸다 (3D 화면 점검 2번)
        public static readonly Color Danger = new Color(1f, 0.25f, 0.2f, 0.7f); // 화면 점검 2차: 보스·벽에 가려도 테두리가 보이게
        public static readonly Color Summon = new Color(0.7f, 0.4f, 1f, 0.6f);
        public static readonly Color Warning = new Color(1f, 0.85f, 0.3f, 0.55f);

        private static SpriteRenderer Get(Sprite sprite, int order)
        {
            if (_root == null)
            {
                _pool.Clear();
                _root = new GameObject("[Telegraphs]").transform;
            }

            SpriteRenderer sr = null;
            for (int i = 0; i < _pool.Count; i++)
            {
                if (_pool[i] != null && !_pool[i].gameObject.activeSelf)
                {
                    sr = _pool[i];
                    break;
                }
            }

            if (sr == null)
            {
                var go = new GameObject("Telegraph");
                go.transform.SetParent(_root, false);
                sr = go.AddComponent<SpriteRenderer>();
                _pool.Add(sr);
            }

            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.transform.rotation = Quaternion.identity;
            sr.gameObject.SetActive(true);
            return sr;
        }

        public static SpriteRenderer Circle(Vector2 center, float radius, Color color, int order = -2)
        {
            SpriteRenderer sr = Get(PlaceholderSprites.RimCircle, order);
            sr.transform.position = center;
            sr.transform.localScale = Vector3.one * radius * 2f;
            sr.color = color;
            return sr;
        }

        public static SpriteRenderer Line(Vector2 from, Vector2 to, float width, Color color, int order = -2)
        {
            SpriteRenderer sr = Get(PlaceholderSprites.Square, order);
            SetLine(sr, from, to, width);
            sr.color = color;
            return sr;
        }

        public static void SetLine(SpriteRenderer sr, Vector2 from, Vector2 to, float width)
        {
            Vector2 delta = to - from;
            sr.transform.position = (from + to) * 0.5f;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(Mathf.Max(0.01f, delta.magnitude), width, 1f);
        }

        public static SpriteRenderer Box(Vector2 center, Vector2 size, float angle, Color color, int order = -3)
        {
            SpriteRenderer sr = Get(PlaceholderSprites.RimSquare, order);
            sr.transform.position = center;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            sr.transform.localScale = new Vector3(size.x, size.y, 1f);
            sr.color = color;
            return sr;
        }

        /// <summary>지금 보이는 예고 (읽기만: 벤치 조종 AI가 사람처럼 화면 예고를 보고 피한다)</summary>
        public static void CollectActive(List<SpriteRenderer> result)
        {
            result.Clear();
            if (_root == null) return;
            foreach (SpriteRenderer sr in _pool)
            {
                if (sr != null && sr.gameObject.activeSelf) result.Add(sr);
            }
        }

        public static bool IsCircle(SpriteRenderer sr) => sr.sprite == PlaceholderSprites.RimCircle || sr.sprite == PlaceholderSprites.Circle;
        public static bool IsFan(SpriteRenderer sr) => sr.sprite != null && sr.sprite.texture != null && sr.sprite.texture.name.StartsWith("Fan");

        public static void Hide(SpriteRenderer sr)
        {
            if (sr != null) sr.gameObject.SetActive(false);
        }

        /// <summary>예고가 차오르는 느낌: t(0~1)만큼 진해진다</summary>
        public static void Fill(SpriteRenderer sr, Color color, float t)
        {
            if (sr == null) return;
            color.a = Mathf.Lerp(color.a * 0.35f, Mathf.Min(1f, color.a * 1.3f), Mathf.Clamp01(t));
            sr.color = color;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _pool.Clear();
            _root = null;
        }
    }
}
