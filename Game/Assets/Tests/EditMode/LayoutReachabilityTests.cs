using System.Collections.Generic;
using HistoryHeroes.Dungeon;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>
    /// 확장 1차 1-1장 "근접 도달 규칙" (Modes 2-2): 모든 방에서 시작 자리 → 문, 그리고 걸을 수 있는 모든 자리에 갈 수 있다.
    /// 격자 0.25칸 · 영웅 반지름만큼 장애물을 부풀린다. 높은·낮은 장애물은 막고, 부서지는 상자는 부술 수 있으니 지나간다고 본다.
    /// </summary>
    public sealed class LayoutReachabilityTests
    {
        private const float Cell = 0.25f;
        private const float HalfWidth = 4f, HalfHeight = 7f;
        private const float HeroRadius = 0.35f;
        private static readonly Vector2 Start = new Vector2(0f, -5.5f);
        private static readonly Vector2 DoorFront = new Vector2(0f, 6.3f);

        private static List<RoomLayout> AllLayouts()
        {
            var list = new List<RoomLayout>();
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:RoomLayout", new[] { "Assets/Data/Dungeons/Layouts" }))
            {
                var l = UnityEditor.AssetDatabase.LoadAssetAtPath<RoomLayout>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (l != null) list.Add(l);
            }

            return list;
        }

        private static bool Blocked(RoomLayout layout, Vector2 p)
        {
            if (Mathf.Abs(p.x) > HalfWidth - HeroRadius || Mathf.Abs(p.y) > HalfHeight - HeroRadius) return true;
            foreach (ObstacleDef o in layout.obstacles)
            {
                if (o.kind == ObstacleKind.Breakable) continue;
                Vector2 half = o.size * 0.5f;
                if (o.circle)
                {
                    if (Vector2.Distance(p, o.position) < Mathf.Max(half.x, half.y) + HeroRadius) return true;
                }
                else if (Mathf.Abs(p.x - o.position.x) < half.x + HeroRadius && Mathf.Abs(p.y - o.position.y) < half.y + HeroRadius) return true;
            }

            return false;
        }

        [Test]
        public void EveryRoom_StartReachesDoorAndEveryFreeSpot()
        {
            List<RoomLayout> layouts = AllLayouts();
            Assert.That(layouts.Count, Is.GreaterThan(30), "확장 1차 방 세트가 만들어져 있다 (장면 빌더)");
            int w = Mathf.RoundToInt(HalfWidth * 2f / Cell) + 1, h = Mathf.RoundToInt(HalfHeight * 2f / Cell) + 1;
            Vector2 ToWorld(int x, int y) => new Vector2(-HalfWidth + x * Cell, -HalfHeight + y * Cell);
            (int, int) ToCell(Vector2 p) => (Mathf.RoundToInt((p.x + HalfWidth) / Cell), Mathf.RoundToInt((p.y + HalfHeight) / Cell));

            var failures = new List<string>();
            foreach (RoomLayout layout in layouts)
            {
                var free = new bool[w, h];
                int freeCount = 0;
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        free[x, y] = !Blocked(layout, ToWorld(x, y));
                        if (free[x, y]) freeCount++;
                    }
                }

                (int sx, int sy) = ToCell(Start);
                if (!free[sx, sy])
                {
                    failures.Add($"{layout.id}: 시작 자리가 막힘");
                    continue;
                }

                var seen = new bool[w, h];
                var queue = new Queue<(int, int)>();
                queue.Enqueue((sx, sy));
                seen[sx, sy] = true;
                int reached = 0;
                while (queue.Count > 0)
                {
                    (int x, int y) = queue.Dequeue();
                    reached++;
                    foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h || seen[nx, ny] || !free[nx, ny]) continue;
                        seen[nx, ny] = true;
                        queue.Enqueue((nx, ny));
                    }
                }

                (int dxCell, int dyCell) = ToCell(DoorFront);
                if (!seen[dxCell, dyCell]) failures.Add($"{layout.id}: 문까지 못 감");
                // 확장 1차 3-2장 갈림길 문 2개 (SceneBuilder.ForkX = 2.4): 일반 방(3·9번 자리)에서 양쪽 문 앞까지
                if (layout.use == RoomUse.Normal)
                {
                    foreach (float fx in new[] { -2.4f, 2.4f })
                    {
                        (int fxCell, int fyCell) = ToCell(new Vector2(fx, DoorFront.y));
                        if (!seen[fxCell, fyCell]) failures.Add($"{layout.id}: 갈림길 문({fx})까지 못 감");
                    }
                }
                // 장애물 사이 아주 작은 틈(한두 칸)은 적 생성 자리가 될 수 없으니 3% 안쪽은 봐준다
                if (reached < freeCount * 0.97f) failures.Add($"{layout.id}: 갈 수 없는 빈자리 {freeCount - reached}칸");
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void EveryChapter_HasOwnRooms_AndSpecialShapes()
        {
            List<RoomLayout> layouts = AllLayouts();
            for (int c = 1; c <= 5; c++)
            {
                int own = layouts.FindAll(l => l.chapter == c && l.use == RoomUse.Normal).Count;
                Assert.That(own, Is.GreaterThanOrEqualTo(6), $"{c}구간 전용 방 6개 이상");
                Assert.IsTrue(layouts.Exists(l => l.chapter == c && l.use == RoomUse.MidBoss), $"{c}구간 중간 보스 방");
            }

            Assert.IsTrue(layouts.Exists(l => l.use == RoomUse.Elite), "엘리트 원형 투기장");
            Assert.IsTrue(layouts.Exists(l => l.use == RoomUse.Reward), "보상 받침대 방");
        }

        [Test]
        public void ChapterPick_PrefersOwnRooms_AndSpecialUsesShapes()
        {
            List<RoomLayout> layouts = AllLayouts();
            var special = ScriptableObject.CreateInstance<RoomLayout>();
            var plans = new List<RoomPlan>();
            for (int i = 0; i < 15; i++) plans.Add(new RoomPlan { kind = i == 4 || i == 9 ? RoomKind.Elite : i == 5 || i == 10 ? RoomKind.Reward : i == 14 ? RoomKind.MidBoss : RoomKind.Normal });
            int own = 0, normal = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                List<RoomLayout> picked = Encounter.PickLayouts(plans, layouts, special, new System.Random(seed), 3);
                for (int i = 0; i < picked.Count; i++)
                {
                    RoomPlan p = plans[i];
                    if (p.kind == RoomKind.Normal)
                    {
                        normal++;
                        Assert.That(picked[i].chapter, Is.EqualTo(0).Or.EqualTo(3), "다른 구간 전용 방은 안 나온다");
                        if (picked[i].chapter == 3) own++;
                    }
                    else if (p.kind == RoomKind.Elite) Assert.AreEqual(RoomUse.Elite, picked[i].use);
                    else if (p.kind == RoomKind.Reward) Assert.AreEqual(RoomUse.Reward, picked[i].use);
                    else if (p.kind == RoomKind.MidBoss) Assert.AreEqual(3, picked[i].chapter);
                }
            }

            // 구간 전용 60% (6개뿐이라 한 판 안에서 다 쓰면 공용으로 넘어간다) → 넉넉히 35% 이상
            Assert.That(own / (float)normal, Is.GreaterThan(0.35f));
        }
    }
}
