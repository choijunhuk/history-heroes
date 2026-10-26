using System;
using HistoryHeroes.Equipment;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Meta
{
    /// <summary>수련 6종 (Stats 5장). 순서가 저장 칸 번호다 — 바꾸지 않는다</summary>
    public enum TrainingType
    {
        Health = 0,   // 💪 체력 단련
        Weapon = 1,   // ⚔️ 무기 단련
        Agility = 2,  // 🏃 민첩
        Focus = 3,    // 🎯 집중
        Strategy = 4, // 🧠 지략
        Recovery = 5, // 🌿 회복력
    }

    [Serializable]
    public struct TrainingRow
    {
        public string name;
        public StatType stat;
        public ModifierKind kind;
        [Tooltip("레벨당 값 (0.0075 = 0.75%)")]
        public float perLevel;
        public int maxLevel;
    }

    /// <summary>
    /// 판 밖 성장 숫자 표 (Stats 4~6장): 영웅 레벨, 각성(★), 수련, 장비 표. 전부 [제안] 시작값.
    /// 힘 예산: 판 밖 전부 합쳐 공격력·체력 최대 +100% (Stats 4장), 보조 능력치는 시스템별 몫 (4-1). 테스트로 고정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "History Heroes/Meta Table", fileName = "MetaTable")]
    public sealed class MetaTable : ScriptableObject
    {
        public EquipmentTable equipment;

        [Header("영웅 레벨 (Lv1~30, 레벨당 공격력·체력 +1%, Lv1 = +0%)")]
        public int maxHeroLevel = 30;
        public float levelPercent = 0.01f;
        [Tooltip("Lv n → n+1 = 이 값 × n (영웅 하나 Lv30까지 약 87,000)")]
        public int levelCostPerLevel = 200;

        [Header("각성 ★1~5 (Stats 6장)")]
        [Tooltip("★2·★3·★4·★5에 필요한 🧩 영웅 조각")]
        public int[] awakenShards = { 10, 20, 40, 80 };
        [Tooltip("별마다 누적 공격력·체력 % (★1 0, ★2 3, ★3 6, ★4 10, ★5 15)")]
        public float[] awakenBonus = { 0f, 0.03f, 0.06f, 0.10f, 0.15f };

        [Header("수련 (계정 공용, 레벨 ≤ 계정 레벨, 2구간 보스 후)")]
        public TrainingRow[] training =
        {
            new TrainingRow { name = "체력 단련", stat = StatType.MaxHp, kind = ModifierKind.Percent, perLevel = 0.0075f, maxLevel = 20 },
            new TrainingRow { name = "무기 단련", stat = StatType.Attack, kind = ModifierKind.Percent, perLevel = 0.0075f, maxLevel = 20 },
            new TrainingRow { name = "민첩", stat = StatType.AttackSpeed, kind = ModifierKind.Percent, perLevel = 0.002f, maxLevel = 20 },
            new TrainingRow { name = "집중", stat = StatType.CritRate, kind = ModifierKind.Flat, perLevel = 0.0015f, maxLevel = 20 },
            new TrainingRow { name = "지략", stat = StatType.CooldownReduction, kind = ModifierKind.Flat, perLevel = 0.002f, maxLevel = 20 },
            // 문서는 "초당 +1". 우리 체력 재생은 초당 최대체력 비율이라 칭기즈 칸(1,000) 기준으로 바꿈: 레벨당 0.1% = 초당 1
            new TrainingRow { name = "회복력", stat = StatType.HpRegen, kind = ModifierKind.Flat, perLevel = 0.001f, maxLevel = 10 },
        };
        [Tooltip("Lv n → n+1 = 이 값 × n")]
        public int trainingCostPerLevel = 500;

        public int HeroLevelCost(int level) => levelCostPerLevel * Mathf.Max(1, level);

        public float HeroLevelBonus(int level) => Mathf.Clamp(level - 1, 0, maxHeroLevel - 1) * levelPercent;

        public float AwakenBonus(int stars) => awakenBonus[Mathf.Clamp(stars, 1, awakenBonus.Length) - 1];

        /// <summary>지금 별 → 다음 별에 필요한 조각 (★5면 -1)</summary>
        public int AwakenCost(int stars) => stars >= 1 && stars <= awakenShards.Length ? awakenShards[stars - 1] : -1;

        public int TrainingCost(int level) => trainingCostPerLevel * Mathf.Max(1, level);
    }
}
