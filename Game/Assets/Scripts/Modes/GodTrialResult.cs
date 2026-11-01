using System.Collections.Generic;
using HistoryHeroes.Economy;

namespace HistoryHeroes.Modes
{
    /// <summary>시련 결과 화면에 필요한 실제 지급값과 첫 격파 여부.</summary>
    public sealed class GodTrialResult
    {
        public bool FirstClear;
        public Reward Reward;
        public int WeeklyShards;
        public bool HeartRefunded;
        public List<string> Lines = new List<string>();
    }
}
