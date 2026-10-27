using System.Collections.Generic;
using HistoryHeroes.Dungeon;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 확장 1차 묶음 1장 "방 세트" (개발자: "맵도 너무 단조로운거 같은데"): 새 모양 4종(공용) + 구간 전용 방 6개씩 +
    /// 엘리트 원형 투기장 · 보상 받침대 방 · 구간별 중간 보스 방.
    /// 방 판정 크기 9×14 (반폭 4 · 반높이 7). 시작 자리 (0, -5.5) 둘레 2칸 · 문 앞 (0, 6.7) 1.5칸은 비운다.
    /// 모든 방은 시작 자리에서 문과 모든 빈자리에 갈 수 있어야 한다 (LayoutReachabilityTests).
    /// </summary>
    public static partial class SceneBuilder
    {
        // ---------------------------------------------------------------- 새 모양 틀 (구간 맛을 넣어 여러 번 쓴다)

        /// <summary>십자 광장: 네 모서리 덩어리 + 가운데 기둥. crates = 모서리를 상자 무더기로</summary>
        private static List<ObstacleDef> Cross(bool crates)
        {
            var list = new List<ObstacleDef>();
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float y in new[] { 4.2f, -1.8f })
                {
                    if (crates)
                    {
                        list.Add(Crate(sx * 3.4f, y + 0.45f, 4));
                        list.Add(Crate(sx * 2.5f, y + 0.45f, 4));
                        list.Add(Crate(sx * 3.4f, y - 0.45f, 4));
                    }
                    else list.Add(High(sx * 3.05f, y, 1.6f, 1.6f));
                }
            }

            list.Add(High(0f, 1.2f, 1.1f, 1.1f, true));
            return list;
        }

        /// <summary>고리 회랑: 가운데 큰 구덩이(낮은 — 탄은 넘어감) 둘레를 돈다. pillar = 구덩이 가운데 기둥(장식·엄폐)</summary>
        private static List<ObstacleDef> Ring(bool pillar)
        {
            var list = new List<ObstacleDef> { Low(0f, 1.4f, 4f, 4.4f) };
            if (pillar) list.Add(High(0f, 1.4f, 0.9f, 0.9f, true));
            return list;
        }

        /// <summary>지그재그: 엇갈린 긴 벽 3개 → S자 길 (관통·부메랑·근접 몰이). crates = 가운데 벽을 상자 줄로</summary>
        private static List<ObstacleDef> Zigzag(bool crates)
        {
            var list = new List<ObstacleDef> { High(-1f, 3.8f, 6f, 0.7f), High(-1f, -1.4f, 6f, 0.7f) };
            if (crates)
            {
                for (float x = -1.5f; x <= 3.6f; x += 1f) list.Add(Crate(x, 1.2f, 3));
            }
            else list.Add(High(1f, 1.2f, 6f, 0.7f));

            return list;
        }

        /// <summary>다리 방: 구덩이 띠 2줄 + 다리(빈칸) 3곳·2곳 (길목 지키기, 건너편 원거리)</summary>
        private static List<ObstacleDef> Bridges()
        {
            return new List<ObstacleDef>
            {
                Low(-3.65f, 0f, 0.7f, 1f), Low(-1.4f, 0f, 1.8f, 1f), Low(1.4f, 0f, 1.8f, 1f), Low(3.65f, 0f, 0.7f, 1f),
                Low(-3.05f, 3.4f, 1.9f, 1f), Low(0f, 3.4f, 2.2f, 1f), Low(3.05f, 3.4f, 1.9f, 1f),
            };
        }

        /// <summary>기둥 숲: 동그란 기둥 2줄 (구간마다 간격만 다르게)</summary>
        private static List<ObstacleDef> Grove(float gap)
        {
            var list = new List<ObstacleDef>();
            for (int row = 0; row < 3; row++)
            {
                float y = -1.2f + row * 2.6f;
                float shift = row % 2 == 0 ? 0f : gap * 0.5f;
                for (float x = -3f + shift; x <= 3.01f; x += gap) list.Add(High(x, y, 0.75f, 0.75f, true));
            }

            return list;
        }

        /// <summary>복도: 양옆 긴 벽 + 가운데 틈 2곳</summary>
        private static List<ObstacleDef> Halls()
        {
            return new List<ObstacleDef>
            {
                High(-2.4f, 2.6f, 0.7f, 5.2f), High(2.4f, 2.6f, 0.7f, 5.2f),
                High(-2.4f, -2f, 0.7f, 1.6f), High(2.4f, -2f, 0.7f, 1.6f),
            };
        }

        private static List<TrapSpot> Spots(params TrapSpot[] spots) => new List<TrapSpot>(spots);

        // ---------------------------------------------------------------- 구간 장치 (확장 1차 2장)

        private static List<DeviceSpot> Devices(params DeviceSpot[] spots) => new List<DeviceSpot>(spots);

        private static DeviceSpot Berry(float x, float y) => new DeviceSpot { kind = DeviceKind.BerryBush, position = new Vector2(x, y), size = new Vector2(0.85f, 0.85f) };
        private static DeviceSpot Keg(float x, float y) => new DeviceSpot { kind = DeviceKind.PowderKeg, position = new Vector2(x, y), size = new Vector2(0.8f, 0.8f) };
        private static DeviceSpot Lever(float x, float y) => new DeviceSpot { kind = DeviceKind.Lever, position = new Vector2(x, y), size = new Vector2(0.7f, 0.7f) };
        private static DeviceSpot Gate(float x, float y, float length, float angle = 0f) => new DeviceSpot { kind = DeviceKind.IronGate, position = new Vector2(x, y), size = new Vector2(length, 0.5f), angle = angle };
        private static DeviceSpot Saw(float x1, float y1, float x2, float y2) => new DeviceSpot { kind = DeviceKind.SawGear, position = new Vector2(x1, y1), to = new Vector2(x2, y2), size = new Vector2(0.95f, 0.95f) };

        /// <summary>낮은 장애물(구덩이 띠)을 개울로 바꾼다 — 판정은 같고 물이 흐른다</summary>
        private static List<DeviceSpot> Creeks(List<ObstacleDef> lows, params DeviceSpot[] more)
        {
            var list = new List<DeviceSpot>();
            foreach (ObstacleDef o in lows)
            {
                if (o.kind == ObstacleKind.Low) list.Add(new DeviceSpot { kind = DeviceKind.Creek, position = o.position, size = o.size });
            }

            list.AddRange(more);
            return list;
        }

        private static RoomLayout Room(string id, LayoutKind kind, RoomUse use, int chapter, List<ObstacleDef> obstacles, List<TrapSpot> traps, List<DeviceSpot> devices = null)
        {
            RoomLayout l = Layout(id, kind, obstacles, traps ?? new List<TrapSpot>());
            l.use = use;
            l.chapter = chapter;
            l.devices = devices ?? new List<DeviceSpot>();
            UnityEditor.EditorUtility.SetDirty(l);
            return l;
        }

        /// <summary>확장 1차 방 세트 (CreateLayouts 끝에 붙는다)</summary>
        private static List<RoomLayout> CreateExpansionLayouts()
        {
            var list = new List<RoomLayout>();
            var none = new List<TrapSpot>();

            // ----- 공용 새 모양 4종 -----
            list.Add(Room("cross_1", LayoutKind.Pillars, RoomUse.Normal, 0, Cross(false), none));
            list.Add(Room("ring_1", LayoutKind.Split, RoomUse.Normal, 0, Ring(false), none));
            list.Add(Room("zig_1", LayoutKind.Corridor, RoomUse.Normal, 0, Zigzag(false), none));
            list.Add(Room("bridge_1", LayoutKind.Split, RoomUse.Normal, 0, Bridges(), none));

            // ----- 구간 전용 방 (구간마다 6개, 약 40%에 그 구간 함정 1~2칸) -----
            // 1 균열의 초원: 개울(다리 방) · 상자 덤불 · 가시덤불 함정
            list.Add(Room("c1_creek", LayoutKind.Split, RoomUse.Normal, 1, new List<ObstacleDef>(), Spots(Spot(-2.6f, 5f, 1.4f, 1.2f)),
                Creeks(Bridges(), Berry(-3f, 1.7f), Berry(2.9f, -2.8f))));
            list.Add(Room("c1_crates", LayoutKind.Pillars, RoomUse.Normal, 1, Cross(true), none));
            list.Add(Room("c1_zig", LayoutKind.Breakable, RoomUse.Normal, 1, Zigzag(true), none));
            list.Add(Room("c1_ring", LayoutKind.Hazard, RoomUse.Normal, 1, Ring(false), Spots(Spot(-3f, -2.8f, 1.4f, 1.2f), Spot(3f, -2.8f, 1.4f, 1.2f))));
            list.Add(Room("c1_grove", LayoutKind.Pillars, RoomUse.Normal, 1, Grove(2f), none, Devices(Berry(0f, -3.1f), Berry(3.2f, 2.7f))));
            list.Add(Room("c1_halls", LayoutKind.Corridor, RoomUse.Normal, 1, Halls(), none, Devices(Berry(0f, 4.4f), Berry(-3.2f, -3.5f), Berry(3.2f, -3.5f))));
            list.Add(Room("c1_brook", LayoutKind.Split, RoomUse.Normal, 1, new List<ObstacleDef> { High(-2.8f, 4.4f, 0.9f, 0.9f, true), High(2.8f, -2.2f, 0.9f, 0.9f, true) }, none,
                Devices(new DeviceSpot { kind = DeviceKind.Creek, position = new Vector2(-1.15f, 1.2f), size = new Vector2(5.7f, 0.9f) }, Berry(2.9f, 3.4f), Berry(-2.9f, -2.4f))));

            // 2 잊힌 성채: 성문 앞(십자 광장) · 해자(다리 방) · 기둥 회랑 · 화염 분사구
            list.Add(Room("c2_gate", LayoutKind.Pillars, RoomUse.Normal, 2, Cross(false), Spots(Spot(0f, 4.2f, 1.2f, 1.2f)), Devices(Keg(-1.5f, 1.2f), Keg(1.5f, 1.2f))));
            list.Add(Room("c2_moat", LayoutKind.Split, RoomUse.Normal, 2, Bridges(), none));
            list.Add(Room("c2_colonnade", LayoutKind.Pillars, RoomUse.Normal, 2, Grove(2.4f), none));
            list.Add(Room("c2_ring", LayoutKind.Hazard, RoomUse.Normal, 2, Ring(true), Spots(Spot(-3.1f, 1.4f, 1.1f, 1.6f), Spot(3.1f, 1.4f, 1.1f, 1.6f))));
            list.Add(Room("c2_zig", LayoutKind.Corridor, RoomUse.Normal, 2, Zigzag(false), none));
            list.Add(Room("c2_storage", LayoutKind.Breakable, RoomUse.Normal, 2, Cross(true), none, Devices(Keg(-1.6f, 4.65f), Keg(1.6f, -1.35f), Keg(0f, 2.4f))));
            list.Add(Room("c2_portcullis", LayoutKind.Hazard, RoomUse.Normal, 2,
                new List<ObstacleDef> { High(-2.7f, -1.8f, 0.9f, 0.9f, true), High(2.7f, -1.8f, 0.9f, 0.9f, true), Crate(-1.3f, 3.9f, 3), Crate(1.3f, 3.9f, 3) }, none,
                Devices(Gate(0f, 1.4f, 4.6f), Keg(0f, 3.9f))));

            // 3 녹슨 공방: 조립 라인(지그재그 + 컨베이어) · 복도 · 증기
            list.Add(Room("c3_line", LayoutKind.Hazard, RoomUse.Normal, 3, Zigzag(false), Spots(Spot(1.4f, 2.5f, 4f, 1f, 0f), Spot(-1.4f, -0.1f, 4f, 1f, 180f)), Devices(Lever(3.2f, -3.6f))));
            list.Add(Room("c3_halls", LayoutKind.Corridor, RoomUse.Normal, 3, Halls(), Spots(Spot(0f, 1.6f, 1.4f, 3f, 90f), Spot(0f, -2.4f, 1.4f, 1.2f)), Devices(Lever(-3.2f, -3.6f))));
            list.Add(Room("c3_saws", LayoutKind.Hazard, RoomUse.Normal, 3,
                new List<ObstacleDef> { High(-3.05f, 1.2f, 1.2f, 1.2f), High(3.05f, 1.2f, 1.2f, 1.2f) }, none,
                Devices(Saw(-2.5f, 3.9f, 2.5f, 3.9f), Saw(1.9f, -1.6f, -1.9f, -1.6f))));
            list.Add(Room("c3_cross", LayoutKind.Pillars, RoomUse.Normal, 3, Cross(false), none));
            list.Add(Room("c3_crates", LayoutKind.Breakable, RoomUse.Normal, 3, Zigzag(true), none));
            list.Add(Room("c3_ring", LayoutKind.Split, RoomUse.Normal, 3, Ring(true), none));
            list.Add(Room("c3_grove", LayoutKind.Pillars, RoomUse.Normal, 3, Grove(2.2f), none));

            // 4 그림자 협곡: 낭떠러지(다리 방 · 고리) · 낙석
            list.Add(Room("c4_cliffs", LayoutKind.Split, RoomUse.Normal, 4, Bridges(), Spots(Spot(-2.6f, 5f, 1.2f, 1.2f), Spot(2.6f, -2.4f, 1.2f, 1.2f))));
            list.Add(Room("c4_ring", LayoutKind.Split, RoomUse.Normal, 4, Ring(false), none));
            list.Add(Room("c4_zig", LayoutKind.Corridor, RoomUse.Normal, 4, Zigzag(false), Spots(Spot(2.8f, 2.5f, 1.2f, 1.2f))));
            list.Add(Room("c4_cross", LayoutKind.Pillars, RoomUse.Normal, 4, Cross(false), none));
            list.Add(Room("c4_grove", LayoutKind.Pillars, RoomUse.Normal, 4, Grove(2.6f), none));
            list.Add(Room("c4_halls", LayoutKind.Hazard, RoomUse.Normal, 4, Halls(), Spots(Spot(0f, 4f, 1.4f, 1.2f), Spot(0f, -0.6f, 1.4f, 1.2f))));

            // 5 망각의 왕좌: 부서지는 회랑(고리 + 망각 균열) · 기둥 · 다리
            list.Add(Room("c5_ring", LayoutKind.Hazard, RoomUse.Normal, 5, Ring(true), Spots(Spot(-3.1f, 4.6f, 1.2f, 1.2f), Spot(3.1f, -1.8f, 1.2f, 1.2f))));
            list.Add(Room("c5_bridge", LayoutKind.Split, RoomUse.Normal, 5, Bridges(), none));
            list.Add(Room("c5_cross", LayoutKind.Pillars, RoomUse.Normal, 5, Cross(false), Spots(Spot(0f, -1.8f, 1.2f, 1.2f))));
            list.Add(Room("c5_grove", LayoutKind.Pillars, RoomUse.Normal, 5, Grove(2.4f), none));
            list.Add(Room("c5_zig", LayoutKind.Corridor, RoomUse.Normal, 5, Zigzag(false), none));
            list.Add(Room("c5_halls", LayoutKind.Corridor, RoomUse.Normal, 5, Halls(), none));

            // ----- 전용 모양 -----
            // 엘리트 = 원형 투기장 (둘레 기둥 8개, 가운데 비움)
            var arena = new List<ObstacleDef>();
            for (int i = 0; i < 8; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / 8f;
                arena.Add(High(Mathf.Cos(a) * 3.1f, 1.2f + Mathf.Sin(a) * 3.1f, 0.8f, 0.8f, true));
            }

            list.Add(Room("arena", LayoutKind.Special, RoomUse.Elite, 0, arena, none));
            // 보상 = 가운데 받침대 (보상 상자 자리)
            list.Add(Room("reward", LayoutKind.Special, RoomUse.Reward, 0, new List<ObstacleDef> { High(0f, 2f, 1f, 1f, true) }, none));
            // 중간 보스 = 구간마다 (1구간은 넓게 비움, 2~5구간은 그 구간 함정이 네 귀퉁이)
            list.Add(Room("mid_1", LayoutKind.Special, RoomUse.MidBoss, 1, new List<ObstacleDef>(), none));
            for (int c = 2; c <= 5; c++)
            {
                List<TrapSpot> corners = Spots(Spot(-3.1f, 5.3f, 1.2f, 1.2f), Spot(3.1f, 5.3f, 1.2f, 1.2f), Spot(-3.1f, -2.8f, 1.2f, 1.2f), Spot(3.1f, -2.8f, 1.2f, 1.2f));
                var pillars = new List<ObstacleDef> { High(-2.2f, 1.2f, 0.8f, 0.8f, true), High(2.2f, 1.2f, 0.8f, 0.8f, true) };
                list.Add(Room("mid_" + c, LayoutKind.Special, RoomUse.MidBoss, c, c == 3 || c == 5 ? pillars : new List<ObstacleDef>(), corners));
            }

            return list;
        }
    }
}
