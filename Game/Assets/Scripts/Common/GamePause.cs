using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 게임 일시정지. 레벨업 카드 화면처럼 여러 곳이 동시에 멈출 수 있어서 "누가 멈췄는지"를 센다.
    /// 멈춘 동안에는 Time.deltaTime이 0이라 멈춤 시간·쿨다운도 흐르지 않는다. (Stats.md 1-1)
    ///
    /// 느리게(히트스톱·슬로모션, 조사 A9)도 여기서 같이 정한다. 규칙:
    /// - 멈춤 요청이 하나라도 있으면 언제나 0 (레벨업 창·결과 화면이 이긴다)
    /// - 아니면 살아 있는 느리게 요청 중 가장 작은 값, 없으면 1
    /// - 느리게 시간은 실제 시간(unscaled)으로 센다 → 멈춘 동안 끝나 버리면 창을 닫은 뒤 원래 속도로 돌아온다
    /// </summary>
    public static class GamePause
    {
        private static readonly HashSet<object> Requesters = new HashSet<object>();
        private static readonly Dictionary<object, Slow> Slows = new Dictionary<object, Slow>();
        private static readonly List<object> Expired = new List<object>();
        private static Runner _runner;

        private struct Slow
        {
            public float Scale;
            public float Until; // Time.unscaledTime 기준
        }

        public static bool IsPaused => Requesters.Count > 0;

        public static void Pause(object requester)
        {
            Requesters.Add(requester);
            Apply();
        }

        public static void Resume(object requester)
        {
            Requesters.Remove(requester);
            Apply();
        }

        /// <summary>
        /// realSeconds 동안 시간을 scale배로 (0 = 잠깐 멈춤). 같은 owner로 다시 부르면 값을 바꾸고 끝나는 때를 늘린다.
        /// </summary>
        public static void RequestSlow(object owner, float scale, float realSeconds)
        {
            if (owner == null || realSeconds <= 0f) return;
            float until = Time.unscaledTime + realSeconds;
            if (Slows.TryGetValue(owner, out Slow old)) until = Mathf.Max(until, old.Until);
            Slows[owner] = new Slow { Scale = Mathf.Clamp01(scale), Until = until };
            EnsureRunner();
            Apply();
        }

        /// <summary>장면이 바뀔 때 남은 요청을 비운다.</summary>
        public static void ResetAll()
        {
            Requesters.Clear();
            Slows.Clear();
            Time.timeScale = 1f;
        }

        private static void Tick()
        {
            if (Slows.Count == 0) return;
            float now = Time.unscaledTime;
            Expired.Clear();
            foreach (KeyValuePair<object, Slow> pair in Slows)
            {
                if (pair.Value.Until <= now) Expired.Add(pair.Key);
            }

            if (Expired.Count == 0) return;
            foreach (object key in Expired) Slows.Remove(key);
            Apply();
        }

        private static void Apply()
        {
            if (Requesters.Count > 0)
            {
                Time.timeScale = 0f;
                return;
            }

            float scale = 1f;
            foreach (Slow slow in Slows.Values) scale = Mathf.Min(scale, slow.Scale);
            Time.timeScale = scale;
        }

        private static void EnsureRunner()
        {
            if (_runner != null || !Application.isPlaying) return;
            // DontSave 플래그는 쓰지 않는다: 에디터에서 Play를 끄면 같이 지워져야 한다
            var go = new GameObject("[GamePause]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
        }

        /// <summary>느리게 요청이 끝나는 때를 실제 시간으로 보는 작은 일꾼</summary>
        private sealed class Runner : MonoBehaviour
        {
            private void Update() => Tick();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Requesters.Clear();
            Slows.Clear();
            _runner = null;
            Time.timeScale = 1f;
        }
    }
}
