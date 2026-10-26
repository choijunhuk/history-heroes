using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Relics
{
    /// <summary>
    /// 유물 숫자 표 (Relics 2·5장): ★ 하나당 공격력·체력 +0.1%, 세트 완성(4개 모두 ★1 이상) 공격력·체력 +0.8% · 피해 감소 +0.8%,
    /// ★2 🏺30 · ★3 🏺80, 교환소 🏺120. 20개 전부 ★3 + 세트 5개 = 공격력·체력 +10%, 피해 감소 +4% (유물 몫 상한).
    /// </summary>
    [CreateAssetMenu(menuName = "History Heroes/Relic Table", fileName = "RelicTable")]
    public sealed class RelicTable : ScriptableObject
    {
        public List<RelicData> relics = new List<RelicData>();
        public string[] setNames = { "발명의 시대", "기록과 문자", "바다와 항해", "장수의 유품", "하늘과 시간" };

        public float perStar = 0.001f;
        public float setBonus = 0.008f;
        public float setDamageReduction = 0.008f;
        [Tooltip("★1→2, ★2→3 🏺 고대 파편")]
        public int[] starCosts = { 30, 80 };
        public int exchangeCost = 120;
        [Tooltip("보스 반복 격파 🏺 (하루 3회 보상 안에서)")]
        public int bossRepeatShards = 5;

        public const int MaxStars = 3;
        public const string AncientShard = "ancient_shard"; // 🏺 고대 파편 (유물 전용, 팔지 않음)

        public RelicData Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (RelicData r in relics)
            {
                if (r != null && r.id == id) return r;
            }

            return null;
        }

        public int StarCost(int stars) => stars >= 1 && stars < MaxStars ? starCosts[stars - 1] : -1;
    }
}
