using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 아래 탭 5개(상점 · 영웅 · 전투 · 미션 · 기록)를 옆으로 이어진 화면으로 (2026-11-03 개발자:
    /// "팝업처럼 띄우는 느낌 말고 각각 창 분리돼서 넘길 수 있게", "왼쪽으로 슥 넘기면").
    /// · 탭을 누르거나 화면을 옆으로 밀면 넘어간다. 미는 동안 화면이 손가락을 따라온다.
    /// · 아래 탭 줄은 늘 보인다. 지금 화면의 탭이 금색으로 크게 올라온다.
    /// · 창 여닫기는 각 창의 원래 열기 · 뒤로 단추를 그대로 쓴다 (정리 · 저장 · 로비 갱신이 그대로 돈다).
    /// · 다른 곳에서 탭 창이 열리면 (도감 → 유물 뽑기 등) 그 화면으로 옮겨 간다. 탭 창이 스스로 닫히면 홈으로.
    /// </summary>
    public sealed class LobbyPager : MonoBehaviour
    {
        public const int Home = 2;

        [Tooltip("0 상점 · 1 영웅 · 2 홈(Main) · 3 미션 · 4 기록. 탭 창은 holder(Panel의 부모)")]
        [SerializeField] private RectTransform[] pages;
        [SerializeField] private Button[] tabs;
        [Tooltip("각 창의 원래 뒤로 단추 (숨겨 두고 닫을 때 누른다). 홈 자리는 비운다")]
        [SerializeField] private Button[] backs;
        [Tooltip("고른 탭: 이만큼 커지고 올라온다")]
        [SerializeField] private Vector2 activeScale = new Vector2(1.05f, 1.16f); // 가로는 조금만: 양 끝 탭이 화면 밖으로 안 나가게
        [SerializeField] private float activeLift = 26f;

        private const float SlideTime = 0.24f;
        private const float StartDrag = 0.035f;   // 화면 폭 비율: 이만큼 옆으로 움직여야 밀기 시작
        private const float Commit = 0.25f;       // 화면 폭 비율: 이만큼 넘게 밀면 넘어간다
        private const float FlickSpeed = 1.2f;    // 화면 폭 / 초: 이보다 빠르게 튕기면 짧아도 넘어간다

        /// <summary>LobbyScreen이 채운다: index 창을 연다 (내용 새로 채우기)</summary>
        public Action<int> OpenPage;

        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private GameObject[] _roots;
        private float[] _tabBaseY;
        private Color _activeColor, _idleColor;
        private CanvasGroup _group;
        private Canvas _canvas;
        private int _current = Home;
        private bool _tracking, _dragging, _sliding;
        private int _neighbor = -1;
        private Vector2 _start;
        private float _startTime, _offset;

        public int Current => _current;

        private void Awake()
        {
            _roots = new GameObject[pages.Length];
            for (int i = 0; i < pages.Length; i++)
            {
                if (i == Home || pages[i] == null) continue;
                Transform panel = pages[i].Find("Panel");
                _roots[i] = panel != null ? panel.gameObject : pages[i].gameObject;
            }

            _tabBaseY = new float[tabs.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                _tabBaseY[i] = ((RectTransform)tabs[i].transform).anchoredPosition.y;
                tabs[i].onClick.AddListener(() => GoTo(index));
            }

            _activeColor = tabs[Home].image.color;
            _idleColor = tabs[0].image.color;
            Transform parent = transform.parent;
            if (!parent.TryGetComponent(out _group)) _group = parent.gameObject.AddComponent<CanvasGroup>();
            _canvas = GetComponentInParent<Canvas>().rootCanvas;
            UpdateTabs();
        }

        /// <summary>미끄러지는 도중에 꺼져도 누름 막기 · 상태가 남지 않게</summary>
        private void OnDisable()
        {
            StopAllCoroutines();
            if (_group != null) _group.blocksRaycasts = true;
            _sliding = _dragging = _tracking = false;
            if (pages != null && _current >= 0 && _current < pages.Length && pages[_current] != null) Place(_current, 0f);
        }

        private float Width => ((RectTransform)transform.parent).rect.width;

        private bool IsOpen(int i) => i == Home || (_roots[i] != null && _roots[i].activeSelf);

        /// <summary>탭을 누를 때 · 홈의 미션 띠처럼 바로 갈 때</summary>
        public void GoTo(int target)
        {
            if (OpenPage == null || _sliding || _dragging || target == _current || target < 0 || target >= pages.Length) return;
            int from = _current;
            if (!Prepare(target)) return;
            StartCoroutine(Slide(from, target, 0f, true));
        }

        /// <summary>목표 창을 열고 옆에 세워 둔다. 열리지 않았으면 false</summary>
        private bool Prepare(int target)
        {
            if (target != Home && !IsOpen(target)) OpenPage(target);
            if (!IsOpen(target)) return false;
            KeepBelowNav(target);
            Place(target, Mathf.Sign(target - _current) * Width);
            return true;
        }

        private void Place(int i, float x)
        {
            Vector2 p = pages[i].anchoredPosition;
            pages[i].anchoredPosition = new Vector2(x, p.y);
        }

        /// <summary>상점처럼 열 때 맨 위로 올라오는 창을 탭 줄 아래로 되돌린다</summary>
        private void KeepBelowNav(int i)
        {
            int nav = transform.GetSiblingIndex();
            if (pages[i].GetSiblingIndex() > nav) pages[i].SetSiblingIndex(nav);
        }

        /// <summary>from → to로 미끄러진다. offset = 지금 from이 밀려 있는 거리 (손가락으로 민 만큼). commit=false면 제자리로</summary>
        private IEnumerator Slide(int from, int to, float offset, bool commit)
        {
            _sliding = true;
            _group.blocksRaycasts = false;
            float w = Width;
            float dir = Mathf.Sign(to - from); // 오른쪽 탭이면 +1: 지금 화면은 왼쪽으로 나간다
            float endFrom = commit ? -dir * w : 0f;
            float duration = Mathf.Max(0.08f, SlideTime * Mathf.Abs(endFrom - offset) / Mathf.Max(1f, w));
            if (commit)
            {
                _current = to;
                UpdateTabs();
            }

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float x = Mathf.Lerp(offset, endFrom, 1f - Mathf.Pow(1f - t / duration, 3f));
                Place(from, x);
                Place(to, x + dir * w);
                yield return null;
            }

            int shown = commit ? to : from, hidden = commit ? from : to;
            Place(shown, 0f);
            if (hidden != Home) Close(hidden);
            else Place(Home, shown < Home ? w : -w); // 홈은 닫지 않고 화면 밖에 둔다
            _group.blocksRaycasts = true;
            _sliding = false;
        }

        private void Close(int i)
        {
            if (IsOpen(i))
            {
                if (backs[i] != null) backs[i].onClick.Invoke();
                else _roots[i].SetActive(false);
            }

            Place(i, 0f);
        }

        private void Update()
        {
            if (OpenPage == null) return;
            if (!_sliding && !_dragging) SyncWithPanels();
            if (!_sliding) TrackSwipe();

            // 안드로이드 뒤로 = Escape. 탭 화면 위에 다른 창이 없을 때만 홈으로
            Keyboard keys = Keyboard.current;
            if (keys != null && keys.escapeKey.wasPressedThisFrame && !_sliding && !_dragging && _current != Home && !OverlayOpen()) GoTo(Home);
        }

        /// <summary>다른 곳에서 탭 창을 열었거나 탭 창이 스스로 닫혔을 때 맞춘다</summary>
        private void SyncWithPanels()
        {
            for (int i = 0; i < pages.Length; i++)
            {
                if (i == Home || i == _current || !IsOpen(i)) continue;
                // 예: 도감 → 유물 → [뽑기]가 상점을 연다 → 상점(맨 위로 올라와 있음)을 먼저 탭 줄 아래로 내리고,
                // 그 위에 덮여 있던 도감 · 유물 창을 닫은 뒤 상점 화면으로
                KeepBelowNav(i);
                CloseOverlays();
                if (_current != Home) Close(_current);
                _current = i;
                Place(i, 0f);
                Place(Home, i < Home ? Width : -Width);
                UpdateTabs();
                return;
            }

            if (_current != Home && !IsOpen(_current))
            {
                // 창이 스스로 닫혔다 → 홈이 기다리던 쪽에서 들어온다 (왼쪽 탭이면 홈은 오른쪽에 있다)
                float side = _current < Home ? 1f : -1f;
                Place(_current, 0f);
                _current = Home;
                UpdateTabs();
                StartCoroutine(SlideHomeIn(side));
            }
        }

        private IEnumerator SlideHomeIn(float side)
        {
            _sliding = true;
            float w = Width;
            for (float t = 0f; t < SlideTime; t += Time.unscaledDeltaTime)
            {
                Place(Home, Mathf.Lerp(side * w, 0f, 1f - Mathf.Pow(1f - t / SlideTime, 3f)));
                yield return null;
            }

            Place(Home, 0f);
            _sliding = false;
        }

        /// <summary>탭 줄 위(나중 형제)에 켜진 창이 있나 — 있으면 밀기 · 뒤로를 그 창에 맡긴다</summary>
        private bool OverlayOpen()
        {
            Transform parent = transform.parent;
            for (int i = transform.GetSiblingIndex() + 1; i < parent.childCount; i++)
            {
                Transform panel = parent.GetChild(i).Find("Panel");
                if (panel != null && panel.gameObject.activeInHierarchy) return true;
            }

            return false;
        }

        /// <summary>
        /// 탭 화면으로 옮길 때 그 위를 덮던 보기 창만 닫는다 (도감 · 유물). 고르기 · 동의 · 설정 · 보상 연출처럼
        /// 대답을 기다리거나 자기 닫기 절차가 있는 창은 건드리지 않는다.
        /// </summary>
        private void CloseOverlays()
        {
            Transform parent = transform.parent;
            for (int i = transform.GetSiblingIndex() + 1; i < parent.childCount; i++)
            {
                Transform holder = parent.GetChild(i);
                if (holder.GetComponent<CodexPanel>() == null && holder.GetComponent<RelicPanel>() == null) continue;
                Transform panel = holder.Find("Panel");
                if (panel != null && panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
            }
        }

        private void TrackSwipe()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null) return;
            Vector2 pos = pointer.position.ReadValue();
            float screenW = Mathf.Max(1f, Screen.width);

            if (pointer.press.wasPressedThisFrame)
            {
                _start = pos;
                _startTime = Time.unscaledTime;
                _tracking = CanSwipeFrom(pos);
                _dragging = false;
                return;
            }

            if (!_tracking) return;
            Vector2 d = pos - _start;
            if (pointer.press.isPressed)
            {
                if (!_dragging)
                {
                    if (Mathf.Abs(d.x) > StartDrag * screenW && Mathf.Abs(d.x) > Mathf.Abs(d.y) * 1.3f)
                    {
                        BeginDrag(d.x < 0f ? _current + 1 : _current - 1);
                        if (_dragging) _start.x += Mathf.Sign(d.x) * StartDrag * screenW; // 시작 문턱만큼 화면이 한 번에 튀지 않게
                    }
                    else if (Mathf.Abs(d.y) > StartDrag * screenW) _tracking = false; // 세로 = 목록 넘기기
                    return;
                }

                // 손가락을 따라온다 (넘어갈 쪽으로만, 반대로는 막힌다)
                float w = Width;
                float dir = Mathf.Sign(_neighbor - _current);
                _offset = Mathf.Clamp(d.x / _canvas.scaleFactor, dir > 0f ? -w : 0f, dir > 0f ? 0f : w);
                Place(_current, _offset);
                Place(_neighbor, _offset + dir * w);
                return;
            }

            // 손을 뗐다
            _tracking = false;
            if (!_dragging) return;
            _dragging = false;
            float width = Width;
            float speed = d.x / screenW / Mathf.Max(0.016f, Time.unscaledTime - _startTime);
            float toward = -Mathf.Sign(_neighbor - _current); // 넘어가려면 손가락이 움직여야 하는 방향
            bool commit = Mathf.Abs(_offset) > Commit * width || (speed * toward > FlickSpeed && Mathf.Abs(_offset) > 0.06f * width);
            StartCoroutine(Slide(_current, _neighbor, _offset, commit));
        }

        private void BeginDrag(int neighbor)
        {
            if (neighbor < 0 || neighbor >= pages.Length || !Prepare(neighbor))
            {
                _tracking = false;
                return;
            }

            _neighbor = neighbor;
            _dragging = true;
            _offset = 0f;
            _group.blocksRaycasts = false; // 민 뒤 손을 뗄 때 아래 단추가 눌리지 않게
            EventSystem events = EventSystem.current;
            if (events != null) events.SetSelectedGameObject(null);
        }

        /// <summary>지금 화면 위에서 누른 것만 밀기로 본다 (위에 덮인 창 · 탭 줄 · 가로 목록에서 시작하면 아님)</summary>
        private bool CanSwipeFrom(Vector2 pos)
        {
            EventSystem events = EventSystem.current;
            if (events == null || OverlayOpen()) return false;
            _hits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = pos }, _hits);
            if (_hits.Count == 0) return _current == Home; // 홈 무대(3D)
            Transform hit = _hits[0].gameObject.transform;
            if (!hit.IsChildOf(pages[_current])) return false;
            foreach (ScrollRect scroll in hit.GetComponentsInParent<ScrollRect>())
            {
                if (scroll.horizontal) return false;
            }

            return true;
        }

        private void UpdateTabs()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                bool active = i == _current;
                tabs[i].image.color = active ? _activeColor : _idleColor;
                var rt = (RectTransform)tabs[i].transform;
                rt.localScale = active ? new Vector3(activeScale.x, activeScale.y, 1f) : Vector3.one;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, _tabBaseY[i] + (active ? activeLift : 0f));
                if (active) rt.SetAsLastSibling(); // 커진 탭이 옆 탭 위에
            }
        }
    }
}
