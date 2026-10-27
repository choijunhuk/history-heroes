using UnityEngine;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 비스듬한 원근 카메라 (Art3D-Plan 3번): 꼭 보여야 하는 땅 네모(벽 포함)가 화면에 다 들어오는 가장 가까운 거리를 푼다.
    /// - 땅은 XY 평면(z = 0), 위는 -Z. 카메라는 앞(+Y, 적이 오는 쪽)을 비스듬히 내려다본다 (tilt = 바로 아래에서 기운 각도).
    /// - 이 부품은 "거치대"에 붙는다: 받침(CameraRig, 서바이벌은 따라가기) → 거치대(맞춤: 거리·기울기) → 카메라(흔들림).
    ///   흔들림은 카메라의 local 위치라 맞춤과 안 싸운다.
    /// - 화면 비율(16:9 ~ 20:9)이 바뀌면 다시 푼다. 위쪽 여백은 HUD 자리.
    /// </summary>
    [ExecuteAlways]
    public sealed class CameraFit : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [Tooltip("꼭 보여야 하는 땅 네모 (벽 포함). 가운데 = 받침 자리 + center")]
        [SerializeField] private Vector2 area = new Vector2(9.2f, 15.2f); // 방 안(8×14) + 벽 조금 (옆벽은 살짝 잘린다 → 방이 크게 보인다)
        [SerializeField] private Vector2 center = Vector2.zero;
        [Tooltip("바로 아래에서 기운 각도 (0 = 위에서 내려다봄). 기울기 + 시야/2가 MaxTopAngle을 넘으면 줄인다 (화면 위가 지평선을 넘지 않게)")]
        [SerializeField, Range(0f, 55f)] private float tilt = 32f;
        [SerializeField, Range(15f, 60f)] private float fieldOfView = 34f;
        [Tooltip("화면 위 여백 (HUD 자리, 화면 높이 비율)")]
        [SerializeField, Range(0f, 0.4f)] private float marginTop = 0.1f;
        [SerializeField, Range(0f, 0.4f)] private float marginBottom = 0.01f;
        [SerializeField, Range(0f, 0.2f)] private float marginSide = 0.01f;

        /// <summary>화면 맨 위 광선이 바로 아래에서 기울 수 있는 최대 각도 (90° = 지평선)</summary>
        public const float MaxTopAngle = 80f;

        private Settings _key;
        private bool _solved;
        private Solution _solution;

        /// <summary>다시 풀어야 하는지 볼 값 (하나씩 비교 — 합을 키로 쓰면 다른 설정이 같아질 수 있다, Codex 3D 코드 리뷰 4번)</summary>
        private struct Settings : System.IEquatable<Settings>
        {
            public Vector2 Area;
            public float Tilt, Fov, Top, Bottom, Side, Aspect;

            public bool Equals(Settings o) => Area == o.Area && Tilt == o.Tilt && Fov == o.Fov && Top == o.Top && Bottom == o.Bottom && Side == o.Side
                                              && Mathf.Approximately(Aspect, o.Aspect);
        }

        /// <summary>바로 아래에서 기운 각도 (2D 그림 시험판은 0 = 바로 위에서)</summary>
        public float Tilt
        {
            get => tilt;
            set => tilt = Mathf.Clamp(value, 0f, 55f);
        }

        /// <summary>꼭 보여야 하는 땅 네모 (서바이벌은 넓게 — 몰려오는 무리가 벽처럼 보이지 않게)</summary>
        public Vector2 Area
        {
            get => area;
            set => area = value;
        }

        /// <summary>받침 기준으로 땅에 보이는 범위 (화면 네 모서리를 땅에 비춘 사다리꼴을 감싼 네모)</summary>
        public Rect GroundView
        {
            get
            {
                Apply();
                Rect g = _solution.Ground;
                g.position += center;
                return g;
            }
        }

        /// <summary>받침에서 땅에 보이는 가장 먼 모서리까지 거리 (서바이벌: 이보다 멀리서 적을 낸다)</summary>
        public float GroundRadius
        {
            get
            {
                Apply();
                return _solution.Radius + center.magnitude;
            }
        }

        public Camera ViewCamera => viewCamera;

        public struct Solution
        {
            public float Distance;
            public float FocusY;
            public Vector3 Position;
            public Quaternion Rotation;
            public Rect Ground;
            public float Radius;
            /// <summary>실제로 쓴 기울기 (SafeTilt로 줄였을 수 있다)</summary>
            public float Tilt;
        }

        /// <summary>화면 맨 위 광선(기울기 + 시야/2)이 MaxTopAngle을 넘지 않는 기울기 → 화면 네 모서리가 늘 땅에 닿는다 (Codex 3D 코드 리뷰 3번)</summary>
        public static float SafeTilt(float tiltDegrees, float fovDegrees) => Mathf.Clamp(tiltDegrees, 0f, Mathf.Max(0f, MaxTopAngle - fovDegrees * 0.5f));

        private void LateUpdate() => Apply();

        /// <summary>지금 화면 비율로 풀어서 거치대에 놓는다 (값이 그대로면 다시 풀지 않는다)</summary>
        public void Apply()
        {
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            if (viewCamera == null) return;
            float aspect = Mathf.Max(0.1f, viewCamera.aspect);
            var key = new Settings { Area = area, Tilt = tilt, Fov = fieldOfView, Top = marginTop, Bottom = marginBottom, Side = marginSide, Aspect = aspect };
            if (!_solved || !key.Equals(_key))
            {
                _solved = true;
                _key = key;
                _solution = Solve(area, tilt, fieldOfView, aspect, marginTop, marginBottom, marginSide);
            }

            viewCamera.orthographic = false;
            viewCamera.fieldOfView = fieldOfView;
            viewCamera.nearClipPlane = 0.3f;
            viewCamera.farClipPlane = _solution.Distance * 3f + 20f;
            transform.localPosition = (Vector3)center + _solution.Position;
            transform.localRotation = _solution.Rotation;
        }

        // ── 계산 (순수 함수, 테스트 CameraFitTests) ──

        public static Vector3 Forward(float tiltDegrees)
        {
            float t = tiltDegrees * Mathf.Deg2Rad;
            return new Vector3(0f, Mathf.Sin(t), Mathf.Cos(t));
        }

        public static Vector3 Up(float tiltDegrees)
        {
            float t = tiltDegrees * Mathf.Deg2Rad;
            return new Vector3(0f, Mathf.Cos(t), -Mathf.Sin(t));
        }

        /// <summary>땅 위 점(z = 0) → 화면 비율 좌표 (0~1). 카메라 뒤면 z ≤ 0</summary>
        public static Vector3 Project(Vector2 point, Vector3 cameraPosition, float tiltDegrees, float fovDegrees, float aspect)
        {
            Vector3 f = Forward(tiltDegrees), u = Up(tiltDegrees);
            Vector3 v = (Vector3)point - cameraPosition;
            float z = Vector3.Dot(v, f);
            if (z <= 0.0001f) return new Vector3(0.5f, 0.5f, z);
            float tanHalf = Mathf.Tan(fovDegrees * 0.5f * Mathf.Deg2Rad);
            return new Vector3(0.5f + v.x / (2f * z * tanHalf * aspect), 0.5f + Vector3.Dot(v, u) / (2f * z * tanHalf), z);
        }

        /// <summary>
        /// 땅 네모(가운데 0, 크기 size)가 여백 안에 다 들어오는 가장 가까운 거리.
        /// 카메라의 오른쪽은 +X라 화면 세로 위치는 땅의 y로만 정해진다 → 세로는 여백 사이 가운데에 오게 초점을 옮기고,
        /// 가로·세로가 다 들어오는 거리를 반씩 나눠 찾는다.
        /// </summary>
        public static Solution Solve(Vector2 size, float tiltDegrees, float fovDegrees, float aspect, float top, float bottom, float side)
        {
            tiltDegrees = SafeTilt(tiltDegrees, fovDegrees);
            Vector3 f = Forward(tiltDegrees);
            float halfW = size.x * 0.5f, halfH = size.y * 0.5f;

            Vector3 CameraAt(float distance, float focusY) => new Vector3(0f, focusY, 0f) - f * distance;

            float CenterFocus(float distance)
            {
                // 초점을 앞(+Y)으로 옮기면 네모가 화면 아래로 내려간다 → (가까운 변 - 아래 여백) = (위 여백 - 먼 변)이 되는 자리
                float lo = -size.y * 2f - distance, hi = size.y * 2f + distance;
                for (int i = 0; i < 40; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    Vector3 cam = CameraAt(distance, mid);
                    Vector3 pn = Project(new Vector2(0f, -halfH), cam, tiltDegrees, fovDegrees, aspect);
                    float near = pn.z <= 0f ? -1e6f : pn.y - bottom; // 가까운 변이 카메라 뒤 = 초점이 너무 앞 → 뒤로
                    float far = 1f - top - Project(new Vector2(0f, halfH), cam, tiltDegrees, fovDegrees, aspect).y;
                    if (near > far) lo = mid;
                    else hi = mid;
                }

                return (lo + hi) * 0.5f;
            }

            bool Fits(float distance, out float focusY)
            {
                focusY = CenterFocus(distance);
                Vector3 cam = CameraAt(distance, focusY);
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sy = -1; sy <= 1; sy += 2)
                    {
                        Vector3 p = Project(new Vector2(sx * halfW, sy * halfH), cam, tiltDegrees, fovDegrees, aspect);
                        if (p.z <= 0f || p.x < side || p.x > 1f - side || p.y < bottom - 0.0001f || p.y > 1f - top + 0.0001f) return false;
                    }
                }

                return true;
            }

            float dLo = 0.5f, dHi = 400f;
            for (int i = 0; i < 40; i++)
            {
                float mid = (dLo + dHi) * 0.5f;
                if (Fits(mid, out _)) dHi = mid;
                else dLo = mid;
            }

            Fits(dHi, out float fy);
            var s = new Solution
            {
                Distance = dHi,
                FocusY = fy,
                Position = CameraAt(dHi, fy),
                Rotation = Quaternion.LookRotation(f, Up(tiltDegrees)),
                Tilt = tiltDegrees,
            };
            GroundBounds(s.Position, tiltDegrees, fovDegrees, aspect, out s.Ground, out s.Radius);
            return s;
        }

        /// <summary>화면 네 모서리를 땅에 비춘 점을 감싼 네모 · 가장 먼 점까지 거리 (수평선 위를 보면 거리 3배에서 자른다)</summary>
        public static void GroundBounds(Vector3 cameraPosition, float tiltDegrees, float fovDegrees, float aspect, out Rect ground, out float radius)
        {
            Vector3 f = Forward(tiltDegrees), u = Up(tiltDegrees), r = Vector3.right;
            float tanHalf = Mathf.Tan(fovDegrees * 0.5f * Mathf.Deg2Rad);
            float maxRay = Mathf.Abs(cameraPosition.z) * 3f + 10f;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            radius = 0f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    Vector3 dir = f + r * (sx * tanHalf * aspect) + u * (sy * tanHalf);
                    float t = dir.z > 0.0001f ? -cameraPosition.z / dir.z : maxRay;
                    t = Mathf.Min(t, maxRay);
                    Vector3 hit = cameraPosition + dir * t;
                    var p = new Vector2(hit.x, hit.y);
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                    radius = Mathf.Max(radius, p.magnitude);
                }
            }

            ground = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
