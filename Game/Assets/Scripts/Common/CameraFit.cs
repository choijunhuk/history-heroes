using UnityEngine;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 화면 비율이 달라도(16:9 ~ 20:9) 방 전체가 보이게 카메라 크기를 맞춘다.
    /// 세로 화면에서는 보통 "가로 폭"이 기준이 된다.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class CameraFit : MonoBehaviour
    {
        [Tooltip("꼭 보여야 하는 영역 (벽 포함, 위쪽 HUD 자리 포함)")]
        [SerializeField] private Vector2 area = new Vector2(10f, 18f);

        private Camera _camera;

        private void LateUpdate()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            float aspect = Mathf.Max(0.01f, _camera.aspect);
            _camera.orthographicSize = Mathf.Max(area.y * 0.5f, area.x / (2f * aspect));
        }
    }
}
