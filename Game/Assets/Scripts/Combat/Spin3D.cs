using UnityEngine;

namespace HistoryHeroes.Combat
{
    /// <summary>
    /// 줍는 것(경험치 보석)이 제자리에서 돌고 살짝 오르내린다 (Art3D-Plan 6번 · 3D 화면 점검 4번: 효과가 아니라 줍는 것으로 보이게).
    /// 위는 -Z라 Z축으로 돈다. 상태는 없고 시간만 본다 → 풀에서 다시 나와도 그대로.
    /// </summary>
    public sealed class Spin3D : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 120f;
        [SerializeField] private float bobHeight = 0.06f;
        [SerializeField] private float bobSpeed = 3f;
        [Tooltip("모델 공간에서 위가 +Y (펫 모델처럼 Visual3D가 세워 주는 것). 끄면 위 = -Z (전투 땅에 바로 놓인 보석)")]
        [SerializeField] private bool yUp;

        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private float _phase;

        private void Awake()
        {
            _basePosition = transform.localPosition;
            _baseRotation = transform.localRotation;
            _phase = (transform.GetInstanceID() & 255) * 0.1f; // 보석마다 박자가 다르게
        }

        private void LateUpdate()
        {
            float t = Time.time + _phase;
            Vector3 up = yUp ? Vector3.up : Vector3.back;
            transform.localRotation = Quaternion.AngleAxis(t * degreesPerSecond, up) * _baseRotation;
            transform.localPosition = _basePosition + up * (bobHeight * Mathf.Sin(t * bobSpeed));
        }
    }
}
