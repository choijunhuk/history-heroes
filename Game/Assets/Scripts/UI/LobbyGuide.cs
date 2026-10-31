using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>전투의 TutorialGuide와 같은 클리오 안내 띠. 로비에 들어올 때 하나만 보여 준다.</summary>
    public sealed class LobbyGuide : MonoBehaviour
    {
        [SerializeField] private GameObject banner;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Button dismissButton;
        [SerializeField] private RectTransform focus;
        [SerializeField] private GameObject[] blockers;

        private bool _used;
        private float _hideAt;
        private Button _target;

        public bool Used => _used;
        public bool IsOpen => banner != null && banner.activeSelf;
        public bool Blocked
        {
            get
            {
                if (blockers == null) return false;
                foreach (GameObject panel in blockers)
                {
                    if (panel != null && panel.activeInHierarchy) return true;
                }
                return false;
            }
        }

        private void Awake()
        {
            Hide();
            if (dismissButton != null) dismissButton.onClick.AddListener(Hide);
        }

        public bool Show(string text, Button target, System.Func<bool> remember = null)
        {
            if (_used || Blocked || banner == null || label == null || target == null || !target.gameObject.activeInHierarchy) return false;
            // 실패해도 이번 로비의 시도는 끝낸다. 저장 공간 부족 때 0.5초마다 디스크에 재시도하지 않게 한다.
            _used = true;
            if (remember != null && !remember()) return false;
            _target = target;
            _target.onClick.AddListener(Hide);
            label.text = $"<color=#FFD966>클리오</color>  {text}\n<size=70%>누르면 닫혀요</size>";
            banner.SetActive(true);
            if (focus != null)
            {
                focus.SetParent(target.transform, false);
                focus.anchorMin = Vector2.zero;
                focus.anchorMax = Vector2.one;
                focus.offsetMin = new Vector2(-6f, -6f);
                focus.offsetMax = new Vector2(6f, 6f);
                focus.gameObject.SetActive(true);
            }
            _hideAt = Time.unscaledTime + 12f;
            return true;
        }

        public void Hide()
        {
            if (banner != null) banner.SetActive(false);
            if (focus != null) focus.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (IsOpen && (Blocked || Time.unscaledTime >= _hideAt)) Hide();
        }

        private void OnDestroy()
        {
            if (_target != null) _target.onClick.RemoveListener(Hide);
        }
    }
}
