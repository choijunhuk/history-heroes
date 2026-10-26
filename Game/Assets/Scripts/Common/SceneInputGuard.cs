using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 화면(장면)이 바뀐 직후 잠깐 터치를 받지 않는다. 결과 화면 "로비로"를 두 번 빨리 누르면
    /// 두 번째 터치가 로비의 같은 자리 버튼("영웅")을 눌러 버렸다 (v1.0 폰 시험).
    /// 맨 위에 투명한 막을 깔아 그 사이의 터치를 막이 받아 버린다.
    /// (EventSystem을 꺼 두면 터치가 버려지지 않고 쌓였다가 다시 켤 때 눌린다 — 폰에서 확인)
    /// </summary>
    public sealed class SceneInputGuard : MonoBehaviour
    {
        public const float Seconds = 0.3f;

        private float _until;
        private int _firstFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            var go = new GameObject("[InputGuard]");
            SceneManager.MoveGameObjectToScene(go, scene);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            go.AddComponent<GraphicRaycaster>();

            var blocker = new GameObject("Blocker", typeof(RectTransform));
            blocker.transform.SetParent(go.transform, false);
            var rect = (RectTransform)blocker.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            blocker.AddComponent<Image>().color = Color.clear; // 보이지 않지만 터치는 받는다

            var guard = go.AddComponent<SceneInputGuard>();
            guard._until = Time.realtimeSinceStartup + Seconds; // 읽는 동안 멈춘 시간은 빼고 (unscaledTime은 그 프레임 시작 시각이라 바로 지나 버린다)
            guard._firstFrame = Time.frameCount;
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup >= _until && Time.frameCount > _firstFrame + 2) Destroy(gameObject); // 읽는 동안 쌓인 터치가 처리되는 첫 프레임들도 막는다
        }
    }
}
