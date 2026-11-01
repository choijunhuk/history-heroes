using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Enemy;
using HistoryHeroes.Equipment;
using HistoryHeroes.Records;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>v0.6 던전 구조 (Content 2-2, Modes 2-4·2-5·2-7)</summary>
    public sealed class DungeonTests
    {
        private ChapterData _chapter;
        private EnemyBase _charger, _archer, _shield;
        private readonly List<GameObject> _made = new List<GameObject>();

        private EnemyBase Prefab(string name)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            _made.Add(go);
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<Combat.Health>();
            return go.AddComponent<ChargerEnemy>();
        }

        [SetUp]
        public void SetUp()
        {
            _charger = Prefab("charger");
            _archer = Prefab("archer");
            _shield = Prefab("shield");
            _chapter = ScriptableObject.CreateInstance<ChapterData>();
            _chapter.enemies = new List<EnemyCost>
            {
                new EnemyCost { prefab = _charger, cost = 2, fromRoom = 1 },
                new EnemyCost { prefab = _archer, cost = 2, fromRoom = 1 },
                new EnemyCost { prefab = _shield, cost = 3, fromRoom = 4 },
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) Object.DestroyImmediate(go);
            _made.Clear();
        }

        private static int Cost(List<SpawnEntry> spawns, ChapterData chapter)
        {
            int total = 0;
            foreach (SpawnEntry s in spawns)
            {
                if (s.elite) continue;
                total += s.count * chapter.enemies.Find(e => e.prefab == s.prefab).cost;
            }

            return total;
        }

        [Test]
        public void Encounter_StaysInBudgetAndRespectsFromRoom()
        {
            var plan = new RoomPlan { kind = RoomKind.Normal, budget = 12, minTypes = 2, maxTypes = 3 };
            for (int seed = 0; seed < 30; seed++)
            {
                List<SpawnEntry> early = Encounter.Build(_chapter, plan, 2, new System.Random(seed));
                Assert.LessOrEqual(Cost(early, _chapter), 12);
                Assert.GreaterOrEqual(Cost(early, _chapter), 11, "남는 예산은 가장 싼 적 1마리보다 작다");
                Assert.IsFalse(early.Exists(s => s.prefab == _shield), "방패병은 4번 방부터");

                List<SpawnEntry> later = Encounter.Build(_chapter, plan, 4, new System.Random(seed));
                Assert.IsTrue(later.Exists(s => s.prefab == _shield), "막 나온 새 적은 먼저 보여 준다");
            }
        }

        [Test]
        public void Encounter_SameSeedSameRoom()
        {
            var plan = new RoomPlan { kind = RoomKind.Elite, budget = 6, minTypes = 1, maxTypes = 2, elites = 2 };
            List<SpawnEntry> a = Encounter.Build(_chapter, plan, 10, new System.Random(42));
            List<SpawnEntry> b = Encounter.Build(_chapter, plan, 10, new System.Random(42));
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].prefab, b[i].prefab);
                Assert.AreEqual(a[i].count, b[i].count);
                Assert.AreEqual(a[i].elite, b[i].elite);
            }

            int elites = 0;
            foreach (SpawnEntry s in a) if (s.elite) elites += s.count;
            Assert.AreEqual(2, elites);
            Assert.AreEqual(0, Encounter.Build(_chapter, new RoomPlan { kind = RoomKind.Reward }, 6, new System.Random(1)).Count, "보상 방은 전투 없음");
        }

        [Test]
        public void Layouts_NoRepeatNoSameKindInARow()
        {
            var layouts = new List<RoomLayout>();
            foreach (LayoutKind kind in new[] { LayoutKind.Open, LayoutKind.Pillars, LayoutKind.Corridor, LayoutKind.Hazard, LayoutKind.Breakable, LayoutKind.Split })
            {
                for (int i = 0; i < 4; i++)
                {
                    var l = ScriptableObject.CreateInstance<RoomLayout>();
                    l.id = kind + "_" + i;
                    l.kind = kind;
                    layouts.Add(l);
                }
            }

            var special = ScriptableObject.CreateInstance<RoomLayout>();
            special.kind = LayoutKind.Special;
            var plans = new List<RoomPlan>();
            for (int i = 0; i < 15; i++) plans.Add(new RoomPlan { kind = i == 5 || i == 10 ? RoomKind.Reward : i == 14 ? RoomKind.MidBoss : RoomKind.Normal });

            for (int seed = 0; seed < 20; seed++)
            {
                List<RoomLayout> picked = Encounter.PickLayouts(plans, layouts, special, new System.Random(seed));
                var seen = new HashSet<RoomLayout>();
                for (int i = 0; i < picked.Count; i++)
                {
                    if (plans[i].kind != RoomKind.Normal)
                    {
                        Assert.AreSame(special, picked[i], "보상·중간 보스 방은 정해진 모양");
                        continue;
                    }

                    Assert.IsTrue(seen.Add(picked[i]), "한 판에 같은 레이아웃 두 번 금지");
                    if (i > 0 && plans[i - 1].kind == RoomKind.Normal) Assert.AreNotEqual(picked[i - 1].kind, picked[i].kind, "같은 종류 연속 금지");
                }
            }
        }

        [Test]
        public void LongRooms_SelectOneOfSevenOrTwelve_InEachChapter()
        {
            var layouts = new List<RoomLayout>();
            for (int chapter = 1; chapter <= 5; chapter++)
            {
                for (int i = 0; i < 2; i++)
                {
                    var layout = ScriptableObject.CreateInstance<RoomLayout>();
                    layout.id = $"c{chapter}_long_{i}";
                    layout.chapter = chapter;
                    layout.longRoom = true;
                    layout.kind = i == 0 ? LayoutKind.Pillars : LayoutKind.Corridor;
                    layouts.Add(layout);
                }
            }
            for (int i = 0; i < 12; i++)
            {
                var layout = ScriptableObject.CreateInstance<RoomLayout>();
                layout.id = $"short_{i}";
                layout.kind = (LayoutKind)(i % 6);
                layouts.Add(layout);
            }
            try
            {
                for (int chapter = 1; chapter <= 5; chapter++)
                {
                    var random = new System.Random(100 + chapter);
                    int selected = Encounter.PickLongRoom(random);
                    Assert.That(selected, Is.EqualTo(7).Or.EqualTo(12));
                    var plans = new List<RoomPlan>();
                    for (int room = 1; room <= 15; room++)
                    {
                        RoomKind kind = room == 5 || room == 10 ? RoomKind.Elite : room == 6 || room == 11 ? RoomKind.Reward : room == 15 ? RoomKind.MidBoss : RoomKind.Normal;
                        bool longRoom = Encounter.UsesLongRoom(room, selected, 15, kind, false, false, false);
                        plans.Add(new RoomPlan { kind = kind, longRoom = longRoom });
                    }
                    Assert.AreEqual(1, plans.FindAll(p => p.longRoom).Count);
                    List<RoomLayout> picked = Encounter.PickLayouts(plans, layouts, null, random, chapter);
                    for (int i = 0; i < picked.Count; i++)
                    {
                        if (plans[i].kind != RoomKind.Normal) continue;
                        Assert.IsNotNull(picked[i], $"{chapter}구간 {i + 1}번 방");
                        Assert.AreEqual(plans[i].longRoom, picked[i].longRoom, $"{chapter}구간 {i + 1}번 방 크기");
                        if (plans[i].longRoom) Assert.AreEqual(chapter, picked[i].chapter);
                    }
                }
                Assert.IsFalse(Encounter.UsesLongRoom(7, 7, 6, RoomKind.Normal, true, false, false));
                Assert.IsFalse(Encounter.UsesLongRoom(7, 7, 7, RoomKind.Normal, false, true, false));
                Assert.IsFalse(Encounter.UsesLongRoom(7, 7, 15, RoomKind.Normal, false, false, true));
                Assert.IsFalse(Encounter.NormalOptions(layouts, 3, null, null).Exists(l => l.longRoom), "무한의 균열도 기본 후보에서 긴 방을 빼야 한다");
            }
            finally { foreach (RoomLayout layout in layouts) Object.DestroyImmediate(layout); }
        }

        [Test]
        public void LongRoomWaves_KeepEveryEnemyAndSplitAcrossLength()
        {
            var spawns = new List<SpawnEntry>
            {
                new SpawnEntry { prefab = _charger, count = 1 },
                new SpawnEntry { prefab = _archer, count = 1 },
                new SpawnEntry { prefab = _shield, count = 3 }
            };
            List<SpawnEntry>[] waves = Encounter.SplitWaves(spawns);
            int Count(List<SpawnEntry> entries) { int total = 0; foreach (SpawnEntry entry in entries) total += entry.count; return total; }
            Assert.AreEqual(3, Count(waves[0]));
            Assert.AreEqual(2, Count(waves[1]));
            Assert.AreEqual(5, Count(waves[0]) + Count(waves[1]));
            Assert.AreEqual(3, spawns[2].count, "원래 적 편성은 바뀌지 않는다");

            var layout = ScriptableObject.CreateInstance<RoomLayout>();
            try
            {
                layout.longRoom = true;
                Assert.IsFalse(layout.CanSpawnSecondWave(1.99f));
                Assert.IsTrue(layout.CanSpawnSecondWave(2f));
                layout.longRoom = false;
                Assert.IsFalse(layout.CanSpawnSecondWave(10f));
            }
            finally { Object.DestroyImmediate(layout); }
        }

        [Test]
        public void RunRandom_SameSeedSameStreams_AndStreamsIndependent()
        {
            RunRandom.Begin(1234);
            int room = RunRandom.Rooms.Next();
            float card = RunRandom.Card01();
            RunRandom.Begin(1234);
            for (int i = 0; i < 10; i++) RunRandom.Card01(); // 카드를 더 많이 뽑아도
            RunRandom.Begin(1234);
            Assert.AreEqual(room, RunRandom.Rooms.Next());
            Assert.AreEqual(card, RunRandom.Card01());

            RunRandom.Begin(1234);
            for (int i = 0; i < 10; i++) RunRandom.Card01();
            Assert.AreEqual(room, RunRandom.Rooms.Next(), "카드 갈래를 많이 써도 방 갈래는 그대로");
            Assert.AreEqual(RunRandom.Hash("2026-10-26"), RunRandom.Hash("2026-10-26"));
            Assert.AreNotEqual(RunRandom.Hash("2026-10-26"), RunRandom.Hash("2026-10-27"));
        }

        [Test]
        public void Traps_ChapterThreeAndFourAlternate()
        {
            Assert.AreEqual(TrapZone.Effect.Thorns, TrapZone.EffectFor(TrapKind.Thorns, 1));
            Assert.AreEqual(TrapZone.Effect.Conveyor, TrapZone.EffectFor(TrapKind.Conveyor, 0));
            Assert.AreEqual(TrapZone.Effect.Steam, TrapZone.EffectFor(TrapKind.Conveyor, 1));
            Assert.AreEqual(TrapZone.Effect.Rockfall, TrapZone.EffectFor(TrapKind.Rockfall, 2));
            Assert.AreEqual(TrapZone.Effect.Vines, TrapZone.EffectFor(TrapKind.Rockfall, 3));
            Assert.AreEqual(TrapZone.Effect.Oblivion, TrapZone.EffectFor(TrapKind.Oblivion, 0));
        }

        [Test]
        public void LegendaryChoice_OnceAfterChapterFiveBoss()
        {
            var table = ScriptableObject.CreateInstance<EquipmentTable>();
            var legend = ScriptableObject.CreateInstance<EquipmentData>();
            legend.id = "weapon_timeguard_l";
            legend.displayName = "시간 수호자의 무기";
            legend.rarity = EquipRarity.Legendary;
            legend.slot = EquipSlot.Weapon;
            var common = ScriptableObject.CreateInstance<EquipmentData>();
            common.id = "weapon_c";
            common.rarity = EquipRarity.Common;
            table.items.Add(legend);
            table.items.Add(common);

            var save = new SaveData { unlockedChapter = 5 };
            Assert.IsFalse(EquipmentInventory.LegendaryChoicePending(save), "5구간 보스 전");
            save.unlockedChapter = 6;
            Assert.IsTrue(EquipmentInventory.LegendaryChoicePending(save));
            Assert.AreEqual(1, EquipmentInventory.LegendaryOptions(table).Count);
            Assert.IsNull(EquipmentInventory.TakeLegendaryChoice(save, table, "weapon_c"), "전설만 고를 수 있다");
            Assert.IsNotNull(EquipmentInventory.TakeLegendaryChoice(save, table, "weapon_timeguard_l"));
            Assert.IsFalse(EquipmentInventory.LegendaryChoicePending(save));
            Assert.IsNull(EquipmentInventory.TakeLegendaryChoice(save, table, "weapon_timeguard_l"), "한 번만");
            Assert.AreEqual(1, save.equipment.items.Count);
        }

        [Test]
        public void TutorialPlan_ShootsFromFirstRoom()
        {
            // 첫 공격 20초 · 첫 카드 45초 (전체 기획 검토 1번): 빈 방 없이 첫 방부터 궁병, 방 6개, 마지막은 엘리트
            List<RoomPlan> plan = Progress.Tutorial.Plan();
            Assert.AreEqual(6, plan.Count);
            Assert.AreEqual("archer", plan[0].only);
            Assert.IsTrue(plan.TrueForAll(r => r.budget > 0), "빈 방이 없다");
            Assert.AreEqual(RoomKind.Elite, plan[5].kind);

            // 실제 1구간 데이터: 궁병은 "2번 방부터"지만 튜토리얼이 고른 적이라 첫 방에 1마리 나온다 (Codex 검토 1번)
            var chapter1 = UnityEditor.AssetDatabase.LoadAssetAtPath<ChapterData>("Assets/Data/Dungeons/Chapter_1.asset");
            Assert.IsNotNull(chapter1);
            List<SpawnEntry> first = Encounter.Build(chapter1, plan[0], 1, new System.Random(1));
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(1, first[0].count);
            Assert.AreEqual("archer", first[0].prefab.Data.id);
        }
    }
}
