using System;
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
            IsOpen = open;
            if (sprite != null) sprite.color = open ? openColor : closedColor;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsOpen || other.GetComponentInParent<PlayerController>() == null) return;
            SetOpen(false); // 두 번 들어가지 않게
            Entered?.Invoke();
        }
    }
}
