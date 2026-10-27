using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Dungeon
{
    /// <summary>
    /// 컨베이어 띠 위를 흐르는 꺾쇠 화살표 (화면 점검 5번의 남은 것: "어느 쪽으로 미는지 안 보인다").
    /// 띠가 미는 빠르기로 미끄러지고, 양 끝에서는 옅어진다. 레버로 거꾸로 돌면 화살표도 뒤집히고 주황색이 된다.
    /// TrapZone의 자식 (TrapZone이 돌아가 있어서 자식의 +X = 미는 쪽)
    /// </summary>
    public sealed class ConveyorArrows : MonoBehaviour
    {
        private const float Spacing = 1.5f;
        private static readonly Color Normal = new Color(1f, 0.92f, 0.45f, 0.75f);
        private static readonly Color Reversed = new Color(1f, 0.55f, 0.2f, 0.85f);

        private SpriteRenderer[] _arrows = new SpriteRenderer[0];
        private int _sortingOrder;
        private float _length;
        private int _count;
        private float _offset;

        public static ConveyorArrows Create(Transform zone, int sortingOrder)
        {
            var go = new GameObject("Arrows");
            go.transform.SetParent(zone, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.03f); // 바닥 띠보다 조금 위 (위 = -Z)
            var arrows = go.AddComponent<ConveyorArrows>();
            arrows._sortingOrder = sortingOrder;
            return arrows;
        }

        public void Setup(Vector2 size)
        {
            gameObject.SetActive(true);
            _length = Mathf.Max(1f, size.x);
            _count = Mathf.Max(1, Mathf.FloorToInt(_length / Spacing));
            float scale = Mathf.Clamp(size.y * 0.55f, 0.5f, 1.3f);
            if (_arrows.Length < _count)
            {
                var grown = new SpriteRenderer[_count];
                for (int i = 0; i < _arrows.Length; i++) grown[i] = _arrows[i];
                for (int i = _arrows.Length; i < _count; i++)
                {
                    var a = new GameObject($"Chevron{i}");
                    a.transform.SetParent(transform, false);
                    var sr = a.AddComponent<SpriteRenderer>();
                    sr.sprite = PlaceholderSprites.Chevron;
                    sr.sortingOrder = _sortingOrder;
                    grown[i] = sr;
                }

                _arrows = grown;
            }

            for (int i = 0; i < _arrows.Length; i++)
            {
                _arrows[i].gameObject.SetActive(i < _count);
                _arrows[i].transform.localScale = new Vector3(scale, scale, 1f);
            }

            Place(false);
        }

        /// <summary>매 프레임 (TrapZone.TickConveyor): speed = 띠가 미는 빠르기</summary>
        public void Tick(bool reversed, float speed, float dt)
        {
            _offset += (reversed ? -1f : 1f) * speed * dt;
            Place(reversed);
        }

        private void Place(bool reversed)
        {
            float span = _count * Spacing;
            float half = _length * 0.5f;
            for (int i = 0; i < _count; i++)
            {
                float x = Mathf.Repeat(i * Spacing + _offset, span) - span * 0.5f;
                Transform t = _arrows[i].transform;
                t.localPosition = new Vector3(x, 0f, 0f);
                t.localRotation = reversed ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;
                // 양 끝 0.8 안쪽에서 옅어진다 (끝에서 툭 나타나고 사라지지 않게)
                float edge = Mathf.Clamp01((half - Mathf.Abs(x)) / 0.8f);
                Color c = reversed ? Reversed : Normal;
                c.a *= edge;
                _arrows[i].color = c;
            }
        }
    }
}
