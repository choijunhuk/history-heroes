using System;
using System.Collections.Generic;
using HistoryHeroes.Player;
using UnityEngine;

namespace HistoryHeroes.Dungeon
{
    /// <summary>방 위쪽의 문. 방을 깨면 열리고, 플레이어가 닿으면 다음 방으로. (Content.md 2-2: 로딩 없이 1~2초 연출)</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Door : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer sprite;
        [SerializeField] private Color closedColor = new Color(0.25f, 0.22f, 0.2f);
        [SerializeField] private Color openColor = new Color(0.4f, 0.95f, 0.55f);

        private Collider2D _trigger;
        private static readonly List<Collider2D> _overlaps = new List<Collider2D>();

        public bool IsOpen { get; private set; }
        public event Action Entered;

        private void Awake()
        {
            _trigger = GetComponent<Collider2D>();
            _trigger.isTrigger = true;
            SetOpen(false);
        }

        public void SetOpen(bool open)
        {
            if (open && !IsOpen) Audio.AudioManager.Play("door", 0.7f);
            IsOpen = open;
            if (sprite != null) sprite.color = open ? openColor : closedColor;

            // 닫힌 문 위에 서 있다가 방을 깬 경우: 들어오는 순간(Enter)이 다시 오지 않으므로 바로 확인
            if (open && _trigger != null)
            {
                _trigger.Overlap(ContactFilter2D.noFilter, _overlaps);
                foreach (Collider2D c in _overlaps)
                {
                    if (TryEnter(c)) break;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other) => TryEnter(other);

        private bool TryEnter(Collider2D other)
        {
            if (!IsOpen || other.GetComponentInParent<PlayerController>() == null) return false;
            SetOpen(false); // 두 번 들어가지 않게
            Entered?.Invoke();
            return true;
        }
    }
}
