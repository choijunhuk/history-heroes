using System.Collections;
using UnityEngine;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 창이 켜질 때 살짝 커지며 나타난다 (0.94 → 1.0 + 흐림 → 선명, 0.22초, 끝에 조금 넘쳤다 돌아옴).
    /// 장면 빌더가 모든 창(Overlay)의 판에 붙인다. 시간 멈춤에도 움직이게 unscaled.
    /// 바탕(첫 자식 Bleed)은 그대로 두고 내용만 움직이면 좋지만, 판 전체를 움직여도 0.22초라 눈에 안 띈다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class PanelIntro : MonoBehaviour
    {
        private const float Duration = 0.22f;
        private CanvasGroup _group;

        private void OnEnable()
        {
            if (_group == null && !TryGetComponent(out _group)) _group = gameObject.AddComponent<CanvasGroup>();
            StopAllCoroutines();
            StartCoroutine(Play());
        }

        private void OnDisable()
        {
            transform.localScale = Vector3.one;
            if (_group != null) _group.alpha = 1f;
        }

        private IEnumerator Play()
        {
            for (float t = 0f; t < Duration; t += Time.unscaledDeltaTime)
            {
                float k = t / Duration;
                transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.94f, 1f, OutBack(k));
                _group.alpha = Mathf.Clamp01(k * 2.2f);
                yield return null;
            }

            transform.localScale = Vector3.one;
            _group.alpha = 1f;
        }

        /// <summary>끝에서 조금 넘쳤다 돌아오는 곡선 (0 → 1)</summary>
        public static float OutBack(float k)
        {
            const float c = 1.6f;
            k -= 1f;
            return 1f + (c + 1f) * k * k * k + c * k * k;
        }
    }
}
