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

    /// <summary>어디에 쓰는 방인가 (확장 1차 1-3장): 일반 · 엘리트 원형 투기장 · 보상 받침대 · 중간 보스</summary>
    public enum RoomUse
    {
        Normal,
        Elite,
        Reward,
        MidBoss,
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

    /// <summary>구간 장치 (확장 1차 2장). 모두 예고가 있고 적에게도 똑같이 작용한다</summary>
    public enum DeviceKind
    {
        /// <summary>1구간 열매 덤불: 2번 맞으면 부서지며 회복 구슬 (최대체력 5%, 방마다 2개까지)</summary>
        BerryBush,
        /// <summary>1구간 개울: 낮은 장애물 띠 (걸어서는 못 건너고 탄은 넘어간다), 물이 흐른다</summary>
        Creek,
        /// <summary>2구간 폭약 통: 3번 맞으면 1초 깜빡 → 반경 1.5 폭발 (연쇄 3개까지)</summary>
        PowderKeg,
        /// <summary>2구간 쇠창살: 8초마다 3초 솟아올라 높은 장애물 (1초 전 그림자 + 열린 통로 표시)</summary>
        IronGate,
        /// <summary>3구간 회전 톱니: 두 점 사이를 왕복하는 높은 장애물, 닿으면 밀림 + 최대체력 10%</summary>
        SawGear,
        /// <summary>3구간 레버: 쏘면 3초간 이 방 컨베이어 반대 · 증기 멈춤 (쿨다운 6초)</summary>
        Lever,
    }

    [Serializable]
    public struct DeviceSpot
    {
        public DeviceKind kind;
        public Vector2 position;
        public Vector2 size;
        [Tooltip("회전 톱니: 왕복 끝 자리")]
        public Vector2 to;
        [Tooltip("개울·쇠창살 방향 (도)")]
        public float angle;
    }

    /// <summary>방 하나의 모양. 장면 빌더가 종류별 규칙으로 만든다 (v0.6-Plan [제안]: 손으로 다듬는 것은 그래픽 단계)</summary>
    [CreateAssetMenu(menuName = "History Heroes/Room Layout", fileName = "Layout_")]
    public sealed class RoomLayout : ScriptableObject
    {
        public string id;
        public LayoutKind kind;
        [Tooltip("어디에 쓰는 방 (확장 1차 1-3장)")]
        public RoomUse use = RoomUse.Normal;
        [Tooltip("0 = 모든 구간, 1~5 = 그 구간 전용 (확장 1차 1-2장)")]
        public int chapter;
        [Tooltip("세로 22칸: 카메라가 따라가고 문은 먼 끝에 둔다")]
        public bool longRoom;
        public Rect Bounds => longRoom ? new Rect(-4f, -7f, 8f, 22f) : new Rect(-4f, -7f, 8f, 14f);
        public float DoorY => Bounds.yMax - 0.3f;
        public Rect FirstWaveArea => longRoom ? new Rect(-3.5f, -1f, 7f, 6f) : new Rect(-3.5f, -1f, 7f, 7.5f);
        public Rect SecondWaveArea => new Rect(-3.5f, 5f, 7f, 7f);
        public bool CanSpawnSecondWave(float playerY) => longRoom && playerY >= 2f;
        public List<ObstacleDef> obstacles = new List<ObstacleDef>();
        public List<TrapSpot> traps = new List<TrapSpot>();
        [Tooltip("구간 장치 (확장 1차 2장)")]
        public List<DeviceSpot> devices = new List<DeviceSpot>();
    }
}
