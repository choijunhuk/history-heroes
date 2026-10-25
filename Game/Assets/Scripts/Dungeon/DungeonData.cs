using System;
using System.Collections.Generic;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Dungeon
{
    public enum RoomKind
    {
        Normal,
        Elite, // 처치 시 확정 레벨업 (Content.md 2-2)
    }

    [Serializable]
    public struct SpawnEntry
    {
        public EnemyBase prefab;
        [Min(1)] public int count;
        public bool elite;
    }

    [Serializable]
    public sealed class RoomData
    {
        public RoomKind kind = RoomKind.Normal;
        public List<SpawnEntry> spawns = new List<SpawnEntry>();
    }

    /// <summary>
    /// 던전 한 판의 방 구성. 정식은 방 15개(Content.md 2-2), v0.1은 10개로 줄여서 검증한다.
    /// 적 강화: 방 k마다 체력 × (1 + 0.04k), 공격력 × (1 + 0.02k). 구간 n은 × 1.35^(n−1), × 1.2^(n−1). (Stats.md 8장)
    /// </summary>
    [CreateAssetMenu(menuName = "History Heroes/Dungeon", fileName = "Dungeon_")]
    public sealed class DungeonData : ScriptableObject
    {
        public string id = "chapter1";
        public string displayName = "균열의 초원";
        [Min(1)] public int chapter = 1;
        public List<RoomData> rooms = new List<RoomData>();

        [Header("강화 (Stats.md 8장)")]
        public float hpPerRoom = 0.04f;
        public float attackPerRoom = 0.02f;
        public float hpPerChapter = 1.35f;
        public float attackPerChapter = 1.2f;

        /// <param name="room">1부터</param>
        public float HpMultiplier(int room) => Mathf.Pow(hpPerChapter, chapter - 1) * (1f + hpPerRoom * room);
        public float AttackMultiplier(int room) => Mathf.Pow(attackPerChapter, chapter - 1) * (1f + attackPerRoom * room);
    }
}
