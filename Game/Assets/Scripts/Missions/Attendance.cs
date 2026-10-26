using System;
using System.Collections.Generic;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;

namespace HistoryHeroes.Missions
{
    /// <summary>
    /// 출석부 (Missions 5장): **출석한 날 수**로 센다 (연속 아님, 하루 빠져도 처음으로 안 돌아감).
    /// 7일 주기: 1~6일째 🪙500 + 💎50, 7일째 + 🎫1. 누적 14일 🔩20 · 28일 🔩40 + 칭호 "꾸준한 수호자".
    /// 누적은 28일 묶음마다 다시 (시즌 달력이 생기면 시즌 기준으로 바꾼다). 튜토리얼 보스를 깨야 열린다 (Systems 3장).
    /// </summary>
    public static class Attendance
    {
        public const int ResetHour = 5;
        public const int Cycle = 7;
        public const int Block = 28;
        public const string SteadyTitle = "steady_guardian";

        public static bool Unlocked(SaveData s) => s.tutorialDone;

        public static Reward DayReward(int cycleDay) => cycleDay >= Cycle ? new Reward { Gold = 500, Gems = 50, Tickets = 1 } : new Reward { Gold = 500, Gems = 50 };

        /// <summary>지금 주기에서 몇 일째까지 받았나 (0 = 아직)</summary>
        public static int CycleDay(SaveData s) => s.attendance.days <= 0 ? 0 : (s.attendance.days - 1) % Cycle + 1;

        /// <summary>28일 묶음 안에서 몇 일째</summary>
        public static int BlockDay(SaveData s) => s.attendance.days <= 0 ? 0 : (s.attendance.days - 1) % Block + 1;

        public static bool CheckedInToday(SaveData s, DateTimeOffset now) => s.attendance.lastDay == MissionService.DayKey(now, ResetHour);

        /// <summary>오늘 처음이면 출석하고 보상을 준다. 받은 줄을 돌려준다 (이미 했거나 잠겼으면 빈 목록)</summary>
        public static List<string> CheckIn(SaveData s, DateTimeOffset now)
        {
            var lines = new List<string>();
            if (!Unlocked(s) || CheckedInToday(s, now)) return lines;
            s.attendance.lastDay = MissionService.DayKey(now, ResetHour);
            s.attendance.days++;

            int day = CycleDay(s);
            Reward r = DayReward(day);
            Wallet.Grant(s, r);
            lines.Add($"출석 {day}일째  {r}");

            int block = BlockDay(s);
            if (block == 14)
            {
                var bonus = new Reward { GearShards = 20 };
                Wallet.Grant(s, bonus);
                lines.Add($"누적 출석 14일  {bonus}");
            }
            else if (block == Block)
            {
                var bonus = new Reward { GearShards = 40 };
                Wallet.Grant(s, bonus);
                if (!s.titles.Contains(SteadyTitle)) s.titles.Add(SteadyTitle);
                lines.Add($"누적 출석 28일  {bonus} · 칭호 \"꾸준한 수호자\"");
            }

            return lines;
        }
    }
}
