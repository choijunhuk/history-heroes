using System.Collections.Generic;
using HistoryHeroes.Enemy;

namespace HistoryHeroes.Dungeon
{
    /// <summary>
    /// 판 시드로 방 구성을 정한다 (Modes 2-4·2-7). 같은 시드 = 같은 적 편성·같은 레이아웃 → 일일 도전 "모두 같은 판".
    /// - 적: 그 방에 나올 수 있는 적(fromRoom) 중 종류 수만큼 고르고, 예산이 남는 동안 한 마리씩 채운다
    /// - 레이아웃: 일반 방만 고른다. 한 판에 같은 레이아웃 두 번 금지, 같은 종류 연속 금지
    /// </summary>
    public static class Encounter
    {
        public static List<SpawnEntry> Build(ChapterData chapter, RoomPlan plan, int room, System.Random random)
        {
            var result = new List<SpawnEntry>();
            if (chapter == null || plan == null) return result;
            if (plan.kind == RoomKind.Reward || plan.kind == RoomKind.Boss) return result;

            var available = new List<EnemyCost>();
            foreach (EnemyCost e in chapter.enemies)
            {
                if (e.prefab != null && e.cost > 0 && e.fromRoom <= room) available.Add(e);
            }

            if (available.Count == 0) return result;

            // 엘리트 (엘리트 방): 2점 이상 적에서
            for (int i = 0; i < plan.elites; i++)
            {
                EnemyCost pick = available[random.Next(available.Count)];
                Add(result, pick.prefab, 1, true);
            }

            // 이번 방의 적 종류
            int types = plan.maxTypes > plan.minTypes ? random.Next(plan.minTypes, plan.maxTypes + 1) : plan.minTypes;
            types = System.Math.Max(1, System.Math.Min(types, available.Count));
            var pool = new List<EnemyCost>(available);
            var chosen = new List<EnemyCost>(types);
            // 새로 나온 적(fromRoom이 가장 큰 것)을 먼저 넣어 "새 적"이 보이게
            pool.Sort((a, b) => b.fromRoom.CompareTo(a.fromRoom));
            if (pool[0].fromRoom > 1 && pool[0].fromRoom >= room - 2)
            {
                chosen.Add(pool[0]);
                pool.RemoveAt(0);
            }

            while (chosen.Count < types && pool.Count > 0)
            {
                int i = random.Next(pool.Count);
                chosen.Add(pool[i]);
                pool.RemoveAt(i);
            }

            // 예산 채우기: 종류마다 한 마리씩 먼저, 그다음 무작위
            int budget = plan.budget;
            foreach (EnemyCost c in chosen)
            {
                if (budget < c.cost) continue;
                Add(result, c.prefab, 1, false);
                budget -= c.cost;
            }

            int guard = 0;
            while (guard++ < 64)
            {
                var fits = chosen.FindAll(c => c.cost <= budget);
                if (fits.Count == 0) break;
                EnemyCost c = fits[random.Next(fits.Count)];
                Add(result, c.prefab, 1, false);
                budget -= c.cost;
            }

            return result;
        }

        private static void Add(List<SpawnEntry> list, EnemyBase prefab, int count, bool elite)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].prefab == prefab && list[i].elite == elite)
                {
                    SpawnEntry e = list[i];
                    e.count += count;
                    list[i] = e;
                    return;
                }
            }

            list.Add(new SpawnEntry { prefab = prefab, count = count, elite = elite });
        }

        /// <summary>방마다 레이아웃 (일반 방만 뽑고, 나머지는 정해진 모양)</summary>
        public static List<RoomLayout> PickLayouts(IReadOnlyList<RoomPlan> plans, IReadOnlyList<RoomLayout> layouts, RoomLayout special, System.Random random)
        {
            var result = new List<RoomLayout>(plans.Count);
            var used = new HashSet<RoomLayout>();
            LayoutKind? last = null;
            foreach (RoomPlan p in plans)
            {
                if (p.kind != RoomKind.Normal || layouts == null || layouts.Count == 0)
                {
                    result.Add(special);
                    last = null;
                    continue;
                }

                var options = new List<RoomLayout>();
                foreach (RoomLayout l in layouts)
                {
                    if (l != null && !used.Contains(l) && l.kind != last) options.Add(l);
                }

                if (options.Count == 0)
                {
                    // 다 썼으면 (긴 판·무한) 종류 연속만 피한다
                    used.Clear();
                    foreach (RoomLayout l in layouts)
                    {
                        if (l != null && l.kind != last) options.Add(l);
                    }
                }

                RoomLayout pick = options.Count > 0 ? options[random.Next(options.Count)] : special;
                used.Add(pick);
                last = pick != null ? pick.kind : (LayoutKind?)null;
                result.Add(pick);
            }

            return result;
        }
    }
}
