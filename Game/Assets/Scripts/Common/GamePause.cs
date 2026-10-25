using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 게임 일시정지. 레벨업 카드 화면처럼 여러 곳이 동시에 멈출 수 있어서 "누가 멈췄는지"를 센다.
    /// 멈춘 동안에는 Time.deltaTime이 0이라 멈춤 시간·쿨다운도 흐르지 않는다. (Stats.md 1-1)
    /// </summary>
    public static class GamePause
    {
        private static readonly HashSet<object> Requesters = new HashSet<object>();

        public static bool IsPaused => Requesters.Count > 0;

        public static void Pause(object requester)
        {
            Requesters.Add(requester);
            Time.timeScale = 0f;
        }

        public static void Resume(object requester)
        {
            Requesters.Remove(requester);
            if (Requesters.Count == 0) Time.timeScale = 1f;
        }

        /// <summary>장면이 바뀔 때 남은 요청을 비운다.</summary>
        public static void ResetAll()
        {
            Requesters.Clear();
            Time.timeScale = 1f;
        }
    }
}
