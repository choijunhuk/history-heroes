using System;
using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Dungeon
{
    /// <summary>방 레이아웃 6종 (Modes 2-2) + 정해진 모양(엘리트·보상·중간 보스)</summary>
    public enum LayoutKind
    {
        Open,      // A 열린 광장
        Pillars,   // B 기둥 방
        Corridor,  // C 좁은 복도
        Hazard,    // D 위험 지형 (구간 함정)
        Breakable, // E 부서지는 벽
        Split,     // F 이중 구역 (가운데 구덩이 + 통로)
        Special,   // 엘리트·보상·중간 보스
    }

    /// <summary>장애물 (Modes 2-3): 높은 = 이동·투사체 막음, 낮은 = 이동만 막음, 부서지는 = 막음 + 맞으면 부서짐</summary>
    public enum ObstacleKind
    {
        High,
        Low,
        Breakable,
    }

    [Serializable]
    public struct ObstacleDef
    {
        public ObstacleKind kind;
        public bool circle;
        public Vector2 position;
        public Vector2 size;
        [Tooltip("부서지는 장애물: 몇 번 맞으면 부서지나 (3~5)")]
        public int hits;
    }

    /// <summary>함정 자리 (종류는 구간이 정한다 — 같은 레이아웃을 구간마다 다른 함정으로)</summary>
    [Serializable]
    public struct TrapSpot
    {
        public Vector2 position;
        public Vector2 size;
        [Tooltip("컨베이어 방향 (도)")]
        public float angle;
    }

    /// <summary>방 하나의 모양. 장면 빌더가 종류별 규칙으로 만든다 (v0.6-Plan [제안]: 손으로 다듬는 것은 그래픽 단계)</summary>
    [CreateAssetMenu(menuName = "History Heroes/Room Layout", fileName = "Layout_")]
    public sealed class RoomLayout : ScriptableObject
    {
        public string id;
        public LayoutKind kind;
        public List<ObstacleDef> obstacles = new List<ObstacleDef>();
        public List<TrapSpot> traps = new List<TrapSpot>();
    }
}
