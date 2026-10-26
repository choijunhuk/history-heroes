#if DEVELOPMENT_BUILD && UNITY_ANDROID && !UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Dev
{
    /// <summary>
    /// 폰 성능 시험 (v1.0 계획 23번). **개발 빌드에서만** 있다. 앱을 실행 옵션과 함께 켜면 서바이벌 벤치를 바로 돌린다:
    ///   adb shell am start -S -n com.choijunhuk.historyheroes/com.unity3d.player.UnityPlayerGameActivity --es hh_bench "survival:khan:god"
    /// (영웅 id, "god"이면 체력을 계속 채워 10분 끝까지). 30초마다·끝에 logcat (태그 Unity, "[SurvivalBench]")으로 프레임·적 수를 남긴다.
    /// 진짜 저장은 건드리지 않는다 (벤치가 임시 저장소로 바꿔 돈다).
    /// </summary>
    internal sealed class DeviceBench : MonoBehaviour
    {
        private const string ExtraKey = "hh_bench";
        private string _arg;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string arg = ReadExtra();
            if (string.IsNullOrEmpty(arg)) return;
            var go = new GameObject("[DeviceBench]");
            DontDestroyOnLoad(go);
            go.AddComponent<DeviceBench>()._arg = arg;
        }

        private IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(2f); // 로비가 다 뜬 다음
            string[] parts = _arg.Split(':');
            if (parts[0] != "survival")
            {
                Debug.LogWarning("[DeviceBench] 모르는 벤치: " + _arg);
                yield break;
            }

            string hero = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : "khan";
            bool god = parts.Length > 2 && parts[2] == "god";
            Debug.Log($"[DeviceBench] {SurvivalBench.Begin(new List<string> { hero }, 1f, god, null)} · 목표 {Application.targetFrameRate}fps");
            Destroy(gameObject);
        }

        private static string ReadExtra()
        {
            try
            {
                using AndroidJavaObject intent = UnityEngine.Android.AndroidApplication.currentActivity.Call<AndroidJavaObject>("getIntent");
                return intent?.Call<string>("getStringExtra", ExtraKey);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[DeviceBench] 실행 옵션을 못 읽음: " + e.Message);
                return null;
            }
        }
    }
}
#endif
