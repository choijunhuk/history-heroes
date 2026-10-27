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
            transform.localRotation = Quaternion.AngleAxis(t * degreesPerSecond, Vector3.forward) * _baseRotation;
            transform.localPosition = _basePosition + new Vector3(0f, 0f, -bobHeight * Mathf.Sin(t * bobSpeed));
        }
    }
}
