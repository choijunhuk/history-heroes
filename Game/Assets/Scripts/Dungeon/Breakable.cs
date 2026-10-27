using HistoryHeroes.Combat;
using UnityEngine;

namespace HistoryHeroes.Dungeon
{
    /// <summary>
    /// 부서지는 장애물 (Modes 2-3): 이동·투사체를 막고, 맞으면 깎여서 부서진다 (3~5번). 벽 튕김은 여기서 튕기지 않는다.
    /// 높은 장애물과 같은 Wall 레이어 — 투사체가 벽에 닿을 때 이 부품이 있으면 Hit을 부른다.
    /// </summary>
    public sealed class Breakable : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer sprite;
        private int _hits;
        private int _max;
        private Color _color;

        public bool Broken => _hits <= 0;

        public void Setup(int hits, SpriteRenderer renderer)
        {
            sprite = renderer;
            _max = Mathf.Max(1, hits);
            _hits = _max;
            _color = renderer != null ? renderer.color : Color.white;
            Common.Stage3D.ShowWear(transform, 1f);
            gameObject.SetActive(true);
        }

        public void Hit()
        {
            if (Broken) return;
            _hits--;
            if (sprite != null)
            {
                Color c = _color;
                c.a = Mathf.Lerp(0.35f, 1f, (float)_hits / _max);
                sprite.color = c;
            }

            Common.Stage3D.ShowWear(transform, (float)_hits / _max); // 3D 상자는 어두워진다

            if (_hits > 0) return;
            AttackFx.Burst(transform.position, Mathf.Max(transform.localScale.x, transform.localScale.y) * 0.6f, new Color(0.7f, 0.55f, 0.35f, 0.7f), 0.2f);
            gameObject.SetActive(false);
        }
    }
}
