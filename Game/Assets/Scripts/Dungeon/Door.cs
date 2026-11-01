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
        [Header("3D 아치 문 (그래픽 평가 4번): 문짝이 뒤로 열리고, 열리면 문 안이 빛난다")]
        [SerializeField] private Transform leafRight;
        [SerializeField] private Transform leafLeft;
        [SerializeField] private GameObject openGlow;
        [SerializeField] private float openAngle = 100f;

        private Collider2D _trigger;
        private Quaternion _rightBase, _leftBase;
        private float _angle, _targetAngle;
        private static readonly List<Collider2D> _overlaps = new List<Collider2D>();

        public bool IsOpen { get; private set; }
        public event Action Entered;

        private void Awake()
        {
            _trigger = GetComponent<Collider2D>();
            _trigger.isTrigger = true;
            if (leafRight != null) _rightBase = leafRight.localRotation;
            if (leafLeft != null) _leftBase = leafLeft.localRotation;
            SetOpen(false);
        }

        private void Update()
        {
            if (leafRight == null || Mathf.Approximately(_angle, _targetAngle)) return;
            _angle = Mathf.MoveTowards(_angle, _targetAngle, 240f * Time.deltaTime);
            // 경첩(문짝 원점)을 축으로 뒤(벽 너머)로 연다 → 문짝이 방 안을 가리지 않는다
            leafRight.localRotation = _rightBase * Quaternion.Euler(0f, _angle, 0f);
            if (leafLeft != null) leafLeft.localRotation = _leftBase * Quaternion.Euler(0f, -_angle, 0f);
        }

        public void SetOpen(bool open)
        {
            if (open && !IsOpen) Audio.AudioManager.Play("door", 0.7f);
            IsOpen = open;
            if (sprite != null) sprite.color = open ? openColor : closedColor;
            _targetAngle = open ? openAngle : 0f;
            if (openGlow != null) openGlow.SetActive(open);

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
