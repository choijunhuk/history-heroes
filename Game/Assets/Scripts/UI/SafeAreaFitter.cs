using UnityEngine;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 이 RectTransform을 화면의 안전 영역에 맞춘다.
    /// 카메라 구멍·상태 표시줄·아래 제스처 바에 HUD가 가려지지 않게 한다.
    /// 화면 크기나 방향이 바뀌면 다시 맞춘다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _appliedArea;
        private Vector2Int _appliedScreen;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _appliedArea || Screen.width != _appliedScreen.x || Screen.height != _appliedScreen.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            _appliedArea = Screen.safeArea;
            _appliedScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            Vector2 min = _appliedArea.min;
            Vector2 max = _appliedArea.max;
            _rect.anchorMin = new Vector2(min.x / Screen.width, min.y / Screen.height);
            _rect.anchorMax = new Vector2(max.x / Screen.width, max.y / Screen.height);
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
