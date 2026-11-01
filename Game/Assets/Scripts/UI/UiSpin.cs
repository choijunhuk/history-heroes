using UnityEngine;

namespace HistoryHeroes.UI
{
    /// <summary>천천히 도는 빛살·장식 (실제 시간). pulse > 0이면 크기도 숨 쉬듯 바뀐다</summary>
    public sealed class UiSpin : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 18f;
        [SerializeField] private float pulse;

        private Vector3 _baseScale = Vector3.one;

        private void Awake() => _baseScale = transform.localScale;

        private void Update()
        {
            transform.Rotate(0f, 0f, -degreesPerSecond * Time.unscaledDeltaTime);
            if (pulse > 0f) transform.localScale = _baseScale * (1f + Mathf.Sin(Time.unscaledTime * 2.4f) * pulse);
        }
    }
}
