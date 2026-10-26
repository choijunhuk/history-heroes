using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Pets
{
    /// <summary>
    /// 펫 성장 (Pets 4장): Lv1~10, 레벨당 공격력·체력 +0.3%, 인연 영웅과 같이 쓰면 +2% → 최대 +5%/+5% (힘 예산).
    /// 레벨업 골드 = 1,500 × 지금 레벨. 장착한 펫 하나만 적용된다.
    /// </summary>
    [CreateAssetMenu(menuName = "History Heroes/Pet Table", fileName = "PetTable")]
    public sealed class PetTable : ScriptableObject
    {
        public List<PetData> pets = new List<PetData>();
        public int maxLevel = 10;
        [Tooltip("레벨당 공격력·체력 % (Lv1 = 0.3%, Lv10 = 3%)")]
        public float perLevel = 0.003f;
        public float bondBonus = 0.02f;
        public int costPerLevel = 1500;
        [Tooltip("Lv5·Lv10 각성 (새 행동)")]
        public int awakenFirst = 5;
        public int awakenSecond = 10;

        public PetData Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (PetData p in pets)
            {
                if (p != null && p.id == id) return p;
            }

            return null;
        }

        /// <summary>level → level+1 골드 (최대면 -1)</summary>
        public int LevelCost(int level) => level <= 0 || level >= maxLevel ? -1 : costPerLevel * level;

        /// <summary>공격력·체력 % 보너스 (0.05 = 5%)</summary>
        public float Bonus(int level, bool bond)
        {
            if (level <= 0) return 0f;
            return Mathf.Clamp(level, 1, maxLevel) * perLevel + (bond ? bondBonus : 0f);
        }
    }
}
