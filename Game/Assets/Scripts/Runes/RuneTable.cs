using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Runes
{
    /// <summary>룬 숫자 표 (Runes 4·5·7장): 레벨별 배율, 강화 비용, 상점 값, 칸이 열리는 구간</summary>
    [CreateAssetMenu(menuName = "History Heroes/Rune Table", fileName = "RuneTable")]
    public sealed class RuneTable : ScriptableObject
    {
        public List<RuneData> runes = new List<RuneData>();

        [Tooltip("Lv1·2·3 가중치 배율")]
        public float[] multipliers = { 1.5f, 2.0f, 2.5f };
        [Tooltip("Lv1→2, Lv2→3 골드")]
        public int[] upgradeCosts = { 3000, 10000 };
        public int shopPrice = 8000;
        [Tooltip("한 스킬에 여러 룬이 겹쳐도 이 배율까지 (Runes 5장)")]
        public float maxMultiplier = 3f;

        [Tooltip("룬 1칸: 2구간 보스 격파 (= 3구간 열림), 2칸: 4구간 보스 격파 (= 5구간 열림)")]
        public int[] slotUnlockChapter = { 3, 5 };

        public int MaxLevel => multipliers.Length;

        public RuneData Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (RuneData r in runes)
            {
                if (r != null && r.id == id) return r;
            }

            return null;
        }

        public float MultiplierAt(int level) => level <= 0 ? 1f : multipliers[Mathf.Clamp(level, 1, multipliers.Length) - 1];

        /// <summary>지금 레벨 → 다음 레벨 비용 (최대면 -1)</summary>
        public int UpgradeCost(int level) => level >= 1 && level < MaxLevel ? upgradeCosts[level - 1] : -1;
    }
}
