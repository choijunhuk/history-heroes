using HistoryHeroes.Skills;
using UnityEngine;

namespace HistoryHeroes.Runes
{
    /// <summary>어디서 얻나 (Runes 7장)</summary>
    public enum RuneSource
    {
        BossChoice = 0, // 2·4구간 보스 최초 격파 때 셋 중 고르기 (못 고른 건 상점)
        Shop = 1,       // 룬 칸이 열린 뒤 골드 상점
        Survival = 2,   // 서바이벌 첫 클리어 (인연의 룬, v0.5)
    }

    /// <summary>
    /// 룬 하나 (Runes 3장, 출시 7종). 레벨업 카드에서 **같은 등급 안에서만** 태그가 맞는 스킬이 더 잘 나오게 한다.
    /// 능력치는 주지 않는다 (힘 예산 밖). 인연의 룬은 태그 대신 "가진 스킬과 조합되는 재료"(발견한 레시피만).
    /// </summary>
    [CreateAssetMenu(menuName = "History Heroes/Rune", fileName = "Rune_")]
    public sealed class RuneData : ScriptableObject
    {
        [Tooltip("저장용 ID. 한 번 정하면 바꾸지 않는다")]
        public string id;
        public string displayName;
        public SkillTag tag;
        [Tooltip("인연의 룬: 태그 대신 조합 재료")]
        public bool bond;
        [Tooltip("노리는 빌드 (화면 설명)")]
        public string aim;
        public RuneSource source;
    }
}
