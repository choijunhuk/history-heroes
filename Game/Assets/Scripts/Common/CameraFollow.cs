using UnityEngine;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 서바이벌 카메라: 플레이어를 가운데 두고 부드럽게 따라가되, 맵 경계 밖은 보이지 않게 자른다 (Modes 2-10).
    /// 카메라를 감싼 "받침(rig)"에 붙는다 → 화면 흔들림(CameraShake)은 카메라의 local 위치라 서로 안 싸운다.
    /// 던전·보스맵에서는 꺼 둔다 (방이 한 화면).
    /// </summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Camera viewCamera;
        [Tooltip("맵 절반 크기 (경계)")]
        [SerializeField] private float boundsHalfSize = 40f;
        [Tooltip("따라가는 빠르기 (클수록 딱 붙음)")]
        [SerializeField] private float sharpness = 10f;

        public float BoundsHalfSize
        {
            get => boundsHalfSize;
            set => boundsHalfSize = value;
        }

        private bool _snapNext;

        public void SnapToTarget()
        {
            if (target != null) transform.position = Goal();
        }

        /// <summary>켜지면 첫 프레임에 바로 붙는다 (판 시작에 맵 가운데에서 미끄러져 오지 않게)</summary>
        private void OnEnable() => _snapNext = true;

        private void LateUpdate()
        {
            if (target == null) return;
            if (_snapNext)
            {
                _snapNext = false;
                SnapToTarget();
                return;
            }

            Vector2 goal = Goal();
            // 멈춤(레벨업 창)에서도 마저 따라가게 실제 시간으로
            float k = 1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime);
            Vector2 now = Vector2.Lerp(transform.position, goal, k);
            transform.position = new Vector3(now.x, now.y, transform.position.z);
        }

        /// <summary>받침 위치 = 플레이어 자리 (카메라는 받침 안에서 HUD만큼 위로 올라가 있다). 경계 밖이 안 보이게 자른다</summary>
        private Vector2 Goal()
        {
            Vector2 p = target.position;
            float halfH = viewCamera != null ? viewCamera.orthographicSize : 9f;
            float halfW = halfH * (viewCamera != null ? viewCamera.aspect : 0.56f);
            float lift = viewCamera != null ? viewCamera.transform.localPosition.y : 0f;
            float x = Mathf.Max(0f, boundsHalfSize - halfW);
            float yMin = -boundsHalfSize + halfH - lift;
            float yMax = boundsHalfSize - halfH - lift;
            return new Vector2(Mathf.Clamp(p.x, -x, x), yMin < yMax ? Mathf.Clamp(p.y, yMin, yMax) : p.y);
        }
    }
}
