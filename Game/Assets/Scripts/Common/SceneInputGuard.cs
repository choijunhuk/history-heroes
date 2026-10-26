using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 화면(장면)이 바뀐 직후 잠깐 터치를 받지 않는다. 결과 화면 "로비로"를 두 번 빨리 누르면
    /// 두 번째 터치가 로비의 같은 자리 버튼("영웅")을 눌러 버렸다 (v1.0 폰 시험).
    /// 장면의 EventSystem을 꺼 두었다가 시간이 지나면 다시 켠다 (QA 도구의 onClick.Invoke는 영향 없음).
    /// </summary>
    public sealed class SceneInputGuard : MonoBehaviour
    {
        public const float Seconds = 0.3f;

        private EventSystem _events;
        private float _until;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            EventSystem events = FindAnyObjectByType<EventSystem>();
            if (events == null || !events.enabled) return;
            var guard = events.gameObject.AddComponent<SceneInputGuard>();
            guard._events = events;
            guard._until = Time.unscaledTime + Seconds;
            events.enabled = false;
        }

        private void Update()
        {
            if (Time.unscaledTime < _until) return;
            if (_events != null) _events.enabled = true;
            Destroy(this);
        }
    }
}
