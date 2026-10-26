using System;
using System.Collections.Generic;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Dungeon
{
    public enum RoomKind
    {
        Normal,
        Elite,   // 처치 시 확정 레벨업 (Content 2-2)
        Reward,  // 전투 없음: 체력 회복 / 카드 1장 더 / 골드
        MidBoss, // 15번 방 중간 보스 (Modes 2-8)
        Boss,    // 무한의 균열 10층마다 보스맵 보스
    }

    [Serializable]
    public struct SpawnEntry
    {
        public EnemyBase prefab;
        [Min(1)] public int count;
        public bool elite;
    }

    /// <summary>방 한 자리의 규칙 (Modes 2-7): 종류, 적 예산, 나오는 적 종류 수, 엘리트 수</summary>
    [Serializable]
    public sealed class RoomPlan
    {
        public RoomKind kind = RoomKind.Normal;
        [Tooltip("적 예산 (돌진병·궁병 2, 방패병·투척병·분열체 3, 소환사 4)")]
        public int budget = 6;
        public int minTypes = 1;
        public int maxTypes = 1;
        public int elites;
        [Tooltip("이 적만 (적 데이터 id, 튜토리얼: \"archer\" · \"charger\"). 비우면 구간 적 전부")]
        public string only;

        public RoomPlan Copy() => (RoomPlan)MemberwiseClone();
    }

    /// <summary>
    /// 던전 한 판의 틀 (Content 2-2: 방 15개) + 구간 데이터 + 방 레이아웃.
    /// 방마다 실제 적은 판 시드로 예산 안에서 뽑는다 (Encounter). 적 강화: 방 k마다 체력 × (1 + 0.04k), 공격력 × (1 + 0.02k),
    /// 구간 n은 × 1.35^(n−1), × 1.2^(n−1) (Stats 8장).
    /// </summary>
    [CreateAssetMenu(menuName = "History Heroes/Dungeon", fileName = "Dungeon_")]
    public sealed class DungeonData : ScriptableObject
    {
        [Header("구간 (Content 3장: 구간마다 적 체력 ×1.35, 공격력 ×1.2, 임시)")]
        public float chapterHpGrowth = 1.35f;
        public float chapterAttackGrowth = 1.2f;

        public float ChapterHpMultiplier(int chapter) => Mathf.Pow(chapterHpGrowth, Mathf.Max(0, chapter - 1));
        public float ChapterAttackMultiplier(int chapter) => Mathf.Pow(chapterAttackGrowth, Mathf.Max(0, chapter - 1));

        [Tooltip("방 15개 (Modes 2-7)")]
        public List<RoomPlan> plan = new List<RoomPlan>();
        public List<ChapterData> chapters = new List<ChapterData>();
        [Tooltip("일반 방 레이아웃 (6종 × 4)")]
        public List<RoomLayout> layouts = new List<RoomLayout>();
        [Tooltip("엘리트·보상·중간 보스 방 (정해진 모양)")]
        public RoomLayout specialLayout;

        [Header("강화 (Stats 8장)")]
        public float hpPerRoom = 0.04f;
        public float attackPerRoom = 0.02f;

        /// <param name="room">1부터</param>
        public float RoomHp(int room) => 1f + hpPerRoom * room;
        public float RoomAttack(int room) => 1f + attackPerRoom * room;

        public ChapterData Chapter(int chapter)
        {
            ChapterData best = null;
            foreach (ChapterData c in chapters)
            {
                if (c == null) continue;
                if (c.chapter == chapter) return c;
                if (best == null || (c.chapter < chapter && c.chapter > best.chapter)) best = c;
            }

            return best;
        }
    }
}
