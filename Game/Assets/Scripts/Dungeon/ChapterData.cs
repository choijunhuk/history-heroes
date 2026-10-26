using System;
using System.Collections.Generic;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Dungeon
{
    /// <summary>적 하나의 예산 점수와 처음 나오는 방 (Modes 2-7)</summary>
    [Serializable]
    public struct EnemyCost
    {
        public EnemyBase prefab;
        public int cost;
        [Tooltip("이 방부터 나온다 (새 적은 조금 늦게 섞이게)")]
        public int fromRoom;
    }

    public enum TrapKind
    {
        None,
        Thorns,      // 1 가시덤불: 밟는 동안 이동속도 -40%
        FireVent,    // 2 화염 분사구: 3초마다 1초 불길 (0.8초 전 빨갛게)
        Conveyor,    // 3 컨베이어(밀림) + 증기 분출 (1초 전 흰 연기)
        Rockfall,    // 4 낙석(1초 전 원) + 그림자 덩굴(밟으면 0.5초 묶임)
        Oblivion,    // 5 망각 균열: 바닥이 3초 사라짐 (1.2초 전 금)
    }

    /// <summary>구간 하나 (Content 3장): 나오는 적, 함정, 중간 보스, 5구간 강화형</summary>
    [CreateAssetMenu(menuName = "History Heroes/Chapter", fileName = "Chapter_")]
    public sealed class ChapterData : ScriptableObject
    {
        [Min(1)] public int chapter = 1;
        public string displayName = "균열의 초원";
        public List<EnemyCost> enemies = new List<EnemyCost>();
        public TrapKind trap;

        [Header("중간 보스 (15번 방)")]
        public EnemyBase midBoss;
        public string midBossName;
        [Tooltip("적 데이터 체력 × 이 값 (구간·방 배수 위에)")]
        public float midBossHp = 100f;
        public float midBossAttack = 1.2f;

        [Header("5구간: 전부 강화형 (Content 3장)")]
        public bool enhanced;
        [Tooltip("강화형 체력 배수")]
        public float enhancedHp = 1.25f;
        [Tooltip("강화형 예고 시간 배수 (빨라진다)")]
        public float enhancedTelegraph = 0.85f;
    }
}
