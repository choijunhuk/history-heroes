using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Relics
{
    /// <summary>
    /// 유물 숫자 표 (Relics 2·5장). 24개 전부 ★3 + 세트 6개 = 공격력·체력 +10%, 피해 감소 +4%.
    /// 뽑기 화면과 추첨은 이 표의 가격·보장·중복 수치를 함께 읽는다.
    /// </summary>
    [CreateAssetMenu(menuName = "History Heroes/Relic Table", fileName = "RelicTable")]
    public sealed class RelicTable : ScriptableObject
    {
        public List<RelicData> relics = new List<RelicData>();
        public string[] setNames = { "발명의 시대", "기록과 문자", "바다와 항해", "장수의 유품", "하늘과 시간", "준비의 흔적" };

        public float perStar = 0.01f / 12f;
        public float setBonus = 0.02f / 3f;
        public float setDamageReduction = 0.02f / 3f;
        [Tooltip("한 번 / 열 번 뽑기 가격 (파편, 보석)")]
        public int pullShardCost = 100;
        public int tenPullShardCost = 900;
        public int pullGemCost = 100;
        public int tenPullGemCost = 900;
        [Tooltip("새 유물을 못 얻은 연속 횟수 다음에 새 유물 확정")]
        public int newRelicPity = 20;
        [Tooltip("★1→2, ★2→3에 필요한 중복 개수")]
        public int[] duplicateCosts = { 1, 2 };
        public int overflowShards = 5;
        public int sourceShards = 20;
        [Tooltip("보스 반복 격파 🏺 (하루 3회 보상 안에서)")]
        public int bossRepeatShards = 5;

        public const int MaxStars = 3;
        public const string AncientShard = "ancient_shard"; // 고대 파편 (유물 뽑기·기록 용량, 직접 팔지 않음)

        public RelicData Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (RelicData r in relics)
            {
                if (r != null && r.id == id) return r;
            }

            return null;
        }

        public int DuplicatesNeeded(int stars) => stars >= 1 && stars < MaxStars ? duplicateCosts[stars - 1] : 0;
    }
}
