using System.Collections.Generic;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using UnityEngine;

namespace HistoryHeroes.Survival
{
    /// <summary>서바이벌 맵 임무 (★2, 확장 1차 6-5장)</summary>
    public enum SurvivalMission
    {
        /// <summary>짚더미 N개 부수기</summary>
        Haystacks,
        /// <summary>화로 N개 부수기</summary>
        Braziers,
        /// <summary>정한 시간 전에 엘리트 N마리</summary>
        ElitesBefore,
    }

    /// <summary>
    /// 서바이벌 맵 진행 (확장 1차 6장 + 검토 9장 5번): 맵마다 따로 해금 · 최고 시간 · 별 3칸 · 첫 클리어 보상.
    /// 맵 1은 1구간 보스, 맵 2·3은 앞 맵 클리어로 열린다. 던전 기록·구간 해금·보스 별과 관계없고,
    /// 서바이벌 별은 던전·보스 별 상자와 따로 센다 (별 5개마다 서바이벌 별 상자, 1회성).
    /// </summary>
    public static class SurvivalProgress
    {
        public const int StarClear = 1;
        public const int StarMission = 2;
        public const int StarNoLegendary = 4;
        public const int StarsPerChest = 5;
        public const int ChestGems = 60;
        public const int ChestShards = 2;

        /// <summary>맵 id 순서 (빌더의 맵 순서와 같다)</summary>
        public static readonly string[] MapIds = { "field", "city", "factory" };

        public static int FirstClearGems(int index) => 30 + 10 * Mathf.Clamp(index, 0, 9);

        /// <summary>맵 index가 열렸나 (진행으로만 — 개발 빌드 예외는 GameSession이 더한다)</summary>
        public static bool Unlocked(SaveData d, int index)
        {
            if (d == null || index < 0 || index >= MapIds.Length) return false;
            if (index == 0) return d.unlockedChapter >= 2; // 1구간 보스 격파 (검토 결정: 2구간 → 1구간)
            SurvivalMapSave prev = d.survival.FindMap(MapIds[index - 1]);
            return prev != null && prev.clears > 0;
        }

        public static string LockText(int index) => index <= 0 ? "1구간 보스를 깨면 열려요" : $"맵 {index}을 클리어하면 열려요";

        public static int StarCount(int bits) => (bits & StarClear) / StarClear + (bits & StarMission) / StarMission + (bits & StarNoLegendary) / StarNoLegendary;

        /// <summary>
        /// 판이 끝났을 때: 최고 시간 · 클리어 · 별 · 첫 클리어 보석 · 별 상자. 받은 것을 글로 돌려준다 (저장은 부르는 쪽)
        /// </summary>
        public static List<string> Finish(SaveData d, int index, float seconds, bool victory, bool missionDone, bool choseLegendary, out bool newBest)
        {
            var lines = new List<string>();
            SurvivalMapSave map = d.survival.MapOf(MapIds[Mathf.Clamp(index, 0, MapIds.Length - 1)]);
            newBest = seconds > map.bestSeconds;
            if (newBest) map.bestSeconds = seconds;
            if (!victory) return lines;

            map.clears++;
            int before = StarCount(map.stars);
            map.stars |= StarClear;
            if (missionDone) map.stars |= StarMission;
            if (!choseLegendary) map.stars |= StarNoLegendary;
            int gained = StarCount(map.stars) - before;
            if (gained > 0) lines.Add($"서바이벌 별 +{gained} ({Stars.Display(StarCount(map.stars))})");

            if (!map.firstClearTaken)
            {
                map.firstClearTaken = true;
                int gems = FirstClearGems(index);
                Wallet.Grant(d, new Reward { Gems = gems });
                lines.Add($"첫 클리어 보석 +{gems}");
                if (index + 1 < MapIds.Length) lines.Add($"맵 {index + 2}이 열렸어요");
            }

            // 별 5개마다 서바이벌 별 상자 (1회성)
            int chests = d.survival.TotalStars() / StarsPerChest;
            while (d.survival.starChestsTaken < chests)
            {
                d.survival.starChestsTaken++;
                Wallet.Grant(d, new Reward { Gems = ChestGems });
                d.AddItem(Relics.RelicTable.AncientShard, ChestShards);
                lines.Add($"서바이벌 별 상자: <sprite name=gem>보석 +{ChestGems} · <sprite name=ancient_shard>고대 파편 +{ChestShards}");
            }

            return lines;
        }
    }
}
