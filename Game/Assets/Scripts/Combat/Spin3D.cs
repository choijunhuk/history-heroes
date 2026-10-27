using UnityEngine;

namespace HistoryHeroes.Combat
{
    /// <summary>
    /// 줍는 것(경험치 보석)이 제자리에서 돌고 살짝 오르내린다 (Art3D-Plan 6번 · 3D 화면 점검 4번: 효과가 아니라 줍는 것으로 보이게).
    /// 위는 -Z라 Z축으로 돈다. 상태는 없고 시간만 본다 → 풀에서 다시 나와도 그대로.
    /// 월드 크기 맞추기(sizeReference > 0): 부모(보석) 배율이 게임 중에 바뀌어도(떨굴 때 0.45, 합치면 커짐, Z는 1)
    /// 모델이 월드 단위로 같은 높이에 뜨고, 부모가 커진 만큼만 고르게 커진다 (Codex 3D 코드 리뷰 2차 1번).
    /// </summary>
    public sealed class Spin3D : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 120f;
        [SerializeField] private float bobHeight = 0.06f;
        [SerializeField] private float bobSpeed = 3f;
        [Tooltip("모델 공간에서 위가 +Y (펫 모델처럼 Visual3D가 세워 주는 것). 끄면 위 = -Z (전투 땅에 바로 놓인 보석)")]
        [SerializeField] private bool yUp;
        [Tooltip("0보다 크면 월드 크기 맞추기: 부모 x 배율이 이 값일 때 배율 1")]
        [SerializeField] private float sizeReference;
        [Tooltip("월드 크기 맞추기에서 부모 기준 자리 (월드 유닛, 크기 배율만큼 같이 커진다)")]
        [SerializeField] private Vector3 worldOffset;

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
            Vector3 bob = up * (bobHeight * Mathf.Sin(t * bobSpeed));

            Transform parent = transform.parent;
            if (sizeReference > 0f && parent != null)
            {
                Vector3 s = parent.lossyScale;
                s = new Vector3(Mathf.Max(0.0001f, Mathf.Abs(s.x)), Mathf.Max(0.0001f, Mathf.Abs(s.y)), Mathf.Max(0.0001f, Mathf.Abs(s.z)));
                float k = s.x / sizeReference; // 부모가 커진 만큼
                transform.localScale = new Vector3(k / s.x, k / s.y, k / s.z);
                Vector3 world = worldOffset * k + bob;
                transform.localPosition = new Vector3(world.x / s.x, world.y / s.y, world.z / s.z);
                return;
            }

            transform.localPosition = _basePosition + bob;
        }
    }
}
