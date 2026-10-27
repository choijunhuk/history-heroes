using System;
using TMPro;
using UnityEngine;

namespace HistoryHeroes.Dungeon
{
    /// <summary>
    /// 갈림길 문 하나 (확장 1차 3-2장): 위 벽에 새긴 빛나는 아치 + 표지판 + 떠 있는 그림판(아이콘 + 짧은 글).
    /// 갈림길이 아닐 때는 숨어 있다. 열리면 튀어나오며 빛나고, 들어가면 Entered.
    /// </summary>
    [RequireComponent(typeof(Door))]
    public sealed class ForkGate : MonoBehaviour
    {
        [SerializeField] private GameObject visuals;
        [SerializeField] private Transform board;
        [SerializeField] private TextMeshPro label;
        [SerializeField] private SpriteRenderer icon;
        [SerializeField] private SpriteRenderer plate;
        [Tooltip("아이콘 너비 (월드)")]
        [SerializeField] private float iconSize = 0.7f;

        private Door _door;
        private float _shownAt = -1f;
        private Vector3 _boardBase;

        public event Action Entered;

        private void Awake()
        {
            _door = GetComponent<Door>();
            if (board != null) _boardBase = board.localPosition;
            _door.Entered += () => Entered?.Invoke();
            Hide();
        }

        public void Show(string text, Sprite sprite, Color color)
        {
            if (visuals != null) visuals.SetActive(true);
            if (label != null) label.text = text;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
                if (sprite != null)
                {
                    Vector3 size = sprite.bounds.size;
                    icon.transform.localScale = Vector3.one * (iconSize / Mathf.Max(0.01f, Mathf.Max(size.x, size.y)));
                }
            }

            if (plate != null) plate.color = new Color(color.r * 0.35f, color.g * 0.35f, color.b * 0.35f, 0.9f);
            if (label != null) label.color = Color.Lerp(color, Color.white, 0.55f);
            _shownAt = Time.unscaledTime;
            Face();
            _door.SetOpen(true);
        }

        public void Hide()
        {
            _shownAt = -1f;
            if (_door != null) _door.SetOpen(false);
            if (visuals != null) visuals.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_shownAt < 0f || board == null) return;
            // 뽕맛: 처음 0.35초 동안 "툭" 튀어나왔다가 제 크기로, 그 뒤 살짝 둥실
            float t = Mathf.Clamp01((Time.unscaledTime - _shownAt) / 0.35f);
            float pop = t < 1f ? Mathf.Sin(t * Mathf.PI * 0.5f) * (1f + 0.25f * Mathf.Sin(t * Mathf.PI)) : 1f;
            board.localScale = Vector3.one * Mathf.Max(0.01f, pop);
            board.localPosition = _boardBase + Vector3.back * (0.08f * Mathf.Sin(Time.unscaledTime * 2.4f)); // 위 = -Z
            Face();
        }

        /// <summary>그림판이 카메라를 보게 (기울어진 카메라라 글이 눕지 않게)</summary>
        private void Face()
        {
            Camera cam = Camera.main;
            if (board != null && cam != null) board.rotation = cam.transform.rotation;
        }
    }
}
