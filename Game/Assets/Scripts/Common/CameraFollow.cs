using UnityEngine;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 서바이벌 카메라: 플레이어를 가운데 두고 부드럽게 따라가되, 맵 경계 밖은 보이지 않게 자른다 (Modes 2-10).
    /// 카메라를 감싼 "받침(rig)"에 붙는다 → 화면 흔들림(CameraShake)은 카메라의 local 위치라 서로 안 싸운다.
    /// 원근 카메라라 화면에 보이는 땅은 사다리꼴 → 맞춤(CameraFit)이 푼 "땅에 보이는 범위"로 자른다.
    /// 보스맵과 일반 던전 방에서는 꺼 두고, 긴 던전 방에서 켠다.
    /// </summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private CameraFit fit;
        [Tooltip("맵 절반 크기 (경계, 가로·세로)")]
        [SerializeField] private Vector2 boundsHalf = new Vector2(40f, 40f);
        [SerializeField] private Vector2 boundsCenter;
        private Rect? _trackingView;
        [Tooltip("따라가는 빠르기 (클수록 딱 붙음)")]
        [SerializeField] private float sharpness = 10f;

        public Vector2 BoundsHalf
        {
            get => boundsHalf;
            set => boundsHalf = value;
        }

        public Vector2 BoundsCenter
        {
            get => boundsCenter;
            set => boundsCenter = value;
        }

        /// <summary>긴 던전 방의 카메라 이동 범위 계산에만 쓰는 화면 영역. 비우면 실제 지면 시야를 쓴다.</summary>
        public Rect? TrackingView
        {
            get => _trackingView;
            set => _trackingView = value;
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

        /// <summary>받침 위치 = 플레이어 자리. 땅에 보이는 범위(받침 기준)가 맵 경계를 넘지 않게 자른다</summary>
        private Vector2 Goal()
        {
            Vector2 p = target.position;
            Rect view = _trackingView ?? (fit != null ? fit.GroundView : new Rect(-5f, -8f, 10f, 18f));
            return ClampToBounds(p, view, boundsCenter, boundsHalf);
        }

        public static Vector2 ClampToBounds(Vector2 target, Rect view, Vector2 center, Vector2 half)
            => new Vector2(Clamp(target.x, center.x - half.x - view.xMin, center.x + half.x - view.xMax),
                Clamp(target.y, center.y - half.y - view.yMin, center.y + half.y - view.yMax));

        /// <summary>보이는 범위가 맵보다 넓으면 가운데에 둔다</summary>
        private static float Clamp(float v, float min, float max) => min <= max ? Mathf.Clamp(v, min, max) : (min + max) * 0.5f;
    }
}
