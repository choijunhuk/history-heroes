using System;
using UnityEngine;

namespace HistoryHeroes.Economy
{
    /// <summary>
    /// 지금 시각 (하트 충전, 하루 한도). 테스트는 Now를 바꿔 끼운다.
    /// 오프라인 게임이라 기기 시계를 믿는다. 시계를 되돌리면 충전 계산을 그 자리에서 다시 시작한다 (Hearts).
    /// </summary>
    public static class Clock
    {
        public static Func<DateTimeOffset> Now = () => DateTimeOffset.Now;

        public static long UnixSeconds => Now().ToUnixTimeSeconds();

        /// <summary>기기 시간대 기준 날짜 (하루 한도를 자정에 새로 셈)</summary>
        public static string Today => Now().ToString("yyyy-MM-dd");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Now = () => DateTimeOffset.Now;
    }
}
