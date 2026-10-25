using System.Collections.Generic;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Enemy;
using HistoryHeroes.Heroes;
using HistoryHeroes.Modes;
using HistoryHeroes.Player;
using HistoryHeroes.Skills;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    public static partial class SceneBuilder
    {
        private sealed class Data
        {
            public HeroData Khan;
            public EnemyData Charger, Archer, Bomber;
            public RarityTable Rarity;
            public DungeonData Dungeon;

            public readonly List<SkillData> Pool = new List<SkillData>();
            public readonly List<FusionRecipe> Recipes = new List<FusionRecipe>();

            public PassiveSkillData DefUp;
            public ActiveSkillData SpinBlade, FlameField, Lightning, ArrowRain, SteelWhirl, ThunderFire;

            // v0.2
            public SkillDatabase Database;
            public EnemyData WardenEnemy;
            public BossData RiftWarden;
        }

        private static Data CreateData()
        {
            var d = new Data();

            // ----- 영웅: 칭기즈 칸 (Heroes.md 3장: 멈추는 순간 준비 시간 0, 첫 발 +50%) -----
            d.Khan = AssetUtil.LoadOrCreate<HeroData>($"{DataDir}/Heroes/Hero_Khan.asset");
            d.Khan.id = "khan";
            d.Khan.displayName = "칭기즈 칸";
            d.Khan.maxHp = 1000f;
            d.Khan.attack = 100f;
            d.Khan.attackSpeed = 1f;
            d.Khan.moveSpeed = 5f;
            d.Khan.range = 6f;
            d.Khan.firstShotDelay = 0f;
            d.Khan.firstShotBonus = 0.5f;
            d.Khan.projectileSpeed = 14f;
            d.Khan.color = new Color(0.95f, 0.75f, 0.2f);

            // ----- 적 3종 (Content.md 4장, Stats.md 8장 기준값 체력 120 · 공격력 60) -----
            d.Charger = Enemy("charger", "돌진병", 2.6f, 0.8f, 1.2f, new Color(0.85f, 0.25f, 0.25f));
            d.Archer = Enemy("archer", "궁병", 2.2f, 0.6f, 1.0f, new Color(0.7f, 0.3f, 0.75f));
            d.Bomber = Enemy("bomber", "투척병", 2.0f, 1.0f, 2.0f, new Color(0.9f, 0.5f, 0.15f));

            d.Rarity = AssetUtil.LoadOrCreate<RarityTable>($"{DataDir}/Skills/RarityTable.asset");

            // ----- 패시브 6개 (Skills.md 1장 🅥) -----
            d.Pool.Add(Passive("atk_up", "공격력", Rarity.Common, SkillTag.Stat, 5, 1,
                "공격력 +15%\n(레벨마다 +10%)",
                Stat(StatType.Attack, ModifierKind.Percent, 0.15f, 0.10f, StatTarget.Both)));
            d.Pool.Add(Passive("aspd_up", "공격속도", Rarity.Common, SkillTag.Stat, 5, 1,
                "기본 공격 속도 +12%\n(레벨마다 +8%)",
                Stat(StatType.AttackSpeed, ModifierKind.Percent, 0.12f, 0.08f, StatTarget.BasicAttack)));
            d.Pool.Add(Passive("hp_up", "최대체력", Rarity.Common, SkillTag.Survival, 5, 1,
                "최대체력 +15%\n(레벨마다 +10%)\n늘어난 만큼 회복",
                Stat(StatType.MaxHp, ModifierKind.Percent, 0.15f, 0.10f, StatTarget.Both)));
            d.DefUp = Passive("def_up", "방어력", Rarity.Common, SkillTag.Survival | SkillTag.Defense, 5, 1,
                "받는 피해 -6%\n(레벨마다 -4%)",
                Stat(StatType.DamageReduction, ModifierKind.Flat, 0.06f, 0.04f, StatTarget.Both));
            d.Pool.Add(d.DefUp);
            d.Pool.Add(Passive("lifesteal", "흡혈", Rarity.Rare, SkillTag.Survival, 5, 2,
                "기본 공격 피해의 2% 회복\n(레벨마다 +1%)\n초당 최대체력 3%까지",
                Stat(StatType.Lifesteal, ModifierKind.Flat, 0.02f, 0.01f, StatTarget.BasicAttack)));

            PassiveSkillData multishot = Passive("multishot", "멀티샷", Rarity.Rare, SkillTag.Projectile, 3, 2,
                "기본 공격 발사체 +1\n(발당 피해 -10%)");
            multishot.attack = new List<PassiveSkillData.AttackEntry>
            {
                new PassiveSkillData.AttackEntry { kind = AttackModifierKind.ExtraProjectiles, firstLevel = 1, perLevel = 1 },
            };
            d.Pool.Add(multishot);

            // ----- 액티브 4개 (Skills.md 2장 🅥). 동작 프리팹은 LinkData에서 넣는다 -----
            d.SpinBlade = Active("spin_blade", "회전 칼날", Rarity.Common, SkillTag.Melee | SkillTag.Defense, 5, 2,
                "몸 주위를 도는 칼날 2개\n(닿으면 공격력 40%)\n레벨마다 칼날 +1",
                cooldown: 0f, damage: 0.4f, damagePerLevel: 0f, count: 2, countPerLevel: 1, radius: 1.5f);
            d.FlameField = Active("flame_field", "불꽃 장판", Rarity.Common, SkillTag.Fire, 5, 2,
                "4초마다 발밑에 불꽃 장판 3초\n(초당 30%)\n레벨마다 범위 +15%",
                cooldown: 4f, damage: 0.3f, damagePerLevel: 0f, count: 1, countPerLevel: 0, radius: 1.5f, radiusPerLevel: 0.15f, duration: 3f);
            d.Lightning = Active("lightning", "낙뢰", Rarity.Rare, SkillTag.Lightning, 5, 3,
                "3초마다 무작위 적에게 번개\n(120%)\n레벨마다 번개 +1",
                cooldown: 3f, damage: 1.2f, damagePerLevel: 0f, count: 1, countPerLevel: 1);
            // "Lv당 피해 +30%"는 Lv1 피해(150%)의 30% → 레벨마다 +45%p로 해석
            d.ArrowRain = Active("arrow_rain", "화살비", Rarity.Legendary, SkillTag.Projectile, 5, 6,
                "6초마다 화면 안 모든 적에게 화살\n(150%)\n레벨마다 피해 +30%",
                cooldown: 6f, damage: 1.5f, damagePerLevel: 0.45f, count: 1, countPerLevel: 0);
            d.Pool.Add(d.SpinBlade);
            d.Pool.Add(d.FlameField);
            d.Pool.Add(d.Lightning);
            d.Pool.Add(d.ArrowRain);

            // ----- 조합 결과 2개 (Skills.md 4-4). 풀에는 넣지 않는다 (카드로 직접 나오지 않음) -----
            d.SteelWhirl = Active("steel_whirl", "강철 회오리", Rarity.Rare, SkillTag.Melee | SkillTag.Defense, 1, 10,
                "칼날 8개 (닿으면 50%)\n칼날마다 1초에 적 투사체 1발을 막는다",
                cooldown: 0f, damage: 0.5f, damagePerLevel: 0f, count: 8, countPerLevel: 0, radius: 1.5f, saveCostFirst: 10);
            // 합체 결과 Lv2 +25%, Lv3 +50% (Skills.md 4-3)
            d.ThunderFire = Active("thunder_fire", "뇌화 폭풍", Rarity.Rare, SkillTag.Lightning | SkillTag.Fire, 3, 3,
                "3초마다 번개 3개 (각 130%)\n떨어진 자리에 불길 2초 (초당 30%)\nLv2 +25%, Lv3 +50%",
                cooldown: 3f, damage: 1.3f, damagePerLevel: 0.325f, count: 3, countPerLevel: 0, saveCostFirst: 15);

            d.Recipes.Add(Recipe("steel_whirl", FusionType.Evolution, d.SpinBlade, 5, d.DefUp, 1, d.SteelWhirl));
            d.Recipes.Add(Recipe("thunder_fire", FusionType.Merge, d.Lightning, 3, d.FlameField, 3, d.ThunderFire));

            d.Dungeon = AssetUtil.LoadOrCreate<DungeonData>($"{DataDir}/Dungeons/Dungeon_Chapter1_v01.asset");

            // ----- v0.2: 스킬 전체 목록 (기록의 스킬 ID → 데이터) -----
            d.Database = AssetUtil.LoadOrCreate<SkillDatabase>($"{DataDir}/Skills/SkillDatabase.asset");
            d.Database.balanceVersion = 1;
            d.Database.skills = new List<SkillData>(d.Pool) { d.SteelWhirl, d.ThunderFire };
            d.Database.recipes = new List<FusionRecipe>(d.Recipes);
            foreach (string problem in d.Database.FindProblems()) Debug.LogError("[SkillDatabase] " + problem);

            // ----- v0.2: 1구간 보스 균열의 수문장 (Content.md 3장, Modes 2-9) -----
            // 체력: 대표 빌드(기록 80%)가 빈틈마다 멈춰서 쏘면 2~3분 (보스맵 목표, Content 2-1). 15,600은 제자리 38초라 너무 빨랐다
            d.WardenEnemy = Enemy("rift_warden", "균열의 수문장", 2.2f, 1.1f, 1.5f, new Color(0.42f, 0.5f, 0.72f));
            d.WardenEnemy.experience = 0;
            d.RiftWarden = AssetUtil.LoadOrCreate<BossData>($"{DataDir}/Bosses/Boss_RiftWarden.asset");
            d.RiftWarden.id = "rift_warden";
            d.RiftWarden.displayName = "균열의 수문장";
            d.RiftWarden.chapter = 1;
            d.RiftWarden.tests = "공격마다 크게 쉰다 → 빈틈마다 멈춰서 쏘기";
            d.RiftWarden.recommend = "기본 공격이 강한 빌드 (공격속도·멀티샷·흡혈)";
            d.RiftWarden.hpMultiplier = 300f; // 120 × 300 = 36,000. 대표 기록으로 제자리에서 약 1분 30초 → 피하면서 2~3분
            d.RiftWarden.attackMultiplier = 1f;
            d.RiftWarden.star2MaxHits = 8;
            return d;
        }

        private static EnemyData Enemy(string id, string name, float speed, float telegraph, float recover, Color color)
        {
            var e = AssetUtil.LoadOrCreate<EnemyData>($"{DataDir}/Enemies/Enemy_{id}.asset");
            e.id = id;
            e.displayName = name;
            e.maxHp = 120f;
            e.damage = 60f;
            e.moveSpeed = speed;
            e.experience = 3;
            e.telegraphTime = telegraph;
            e.recoverTime = recover;
            e.color = color;
            return e;
        }

        private static PassiveSkillData.StatEntry Stat(StatType stat, ModifierKind kind, float first, float perLevel, StatTarget target)
        {
            return new PassiveSkillData.StatEntry { stat = stat, kind = kind, firstLevel = first, perLevel = perLevel, target = target };
        }

        private static PassiveSkillData Passive(string id, string name, Rarity rarity, SkillTag tags, int maxLevel, int saveCost,
            string description, params PassiveSkillData.StatEntry[] stats)
        {
            var s = AssetUtil.LoadOrCreate<PassiveSkillData>($"{DataDir}/Skills/Passive_{id}.asset");
            FillCommon(s, id, name, rarity, tags, maxLevel, saveCost, saveCost, description);
            s.stats = new List<PassiveSkillData.StatEntry>(stats);
            s.attack = new List<PassiveSkillData.AttackEntry>();
            return s;
        }

        private static ActiveSkillData Active(string id, string name, Rarity rarity, SkillTag tags, int maxLevel, int saveCost,
            string description, float cooldown, float damage, float damagePerLevel, int count, int countPerLevel,
            float radius = 1.5f, float radiusPerLevel = 0f, float duration = 0f, int saveCostFirst = -1)
        {
            var s = AssetUtil.LoadOrCreate<ActiveSkillData>($"{DataDir}/Skills/Active_{id}.asset");
            FillCommon(s, id, name, rarity, tags, maxLevel, saveCostFirst >= 0 ? saveCostFirst : saveCost, saveCost, description);
            s.cooldown = cooldown;
            s.damageFirstLevel = damage;
            s.damagePerLevel = damagePerLevel;
            s.countFirstLevel = count;
            s.countPerLevel = countPerLevel;
            s.radius = radius;
            s.radiusPerLevel = radiusPerLevel;
            s.duration = duration;
            s.durationPerLevel = 0f;
            return s;
        }

        private static void FillCommon(SkillData s, string id, string name, Rarity rarity, SkillTag tags, int maxLevel,
            int saveCostFirst, int saveCostPerLevel, string description)
        {
            s.id = id;
            s.displayName = name;
            s.rarity = rarity;
            s.tags = tags;
            s.maxLevel = maxLevel;
            s.saveCostFirstLevel = saveCostFirst;
            s.saveCostPerLevel = saveCostPerLevel;
            s.recordable = true;
            s.heroOnlyId = string.Empty;
            s.description = description;
        }

        private static FusionRecipe Recipe(string id, FusionType type, ActiveSkillData a, int levelA, SkillData b, int levelB, ActiveSkillData result)
        {
            var r = AssetUtil.LoadOrCreate<FusionRecipe>($"{DataDir}/Fusions/Fusion_{id}.asset");
            r.id = id;
            r.type = type;
            r.materialA = a;
            r.requiredLevelA = levelA;
            r.materialB = b;
            r.requiredLevelB = levelB;
            r.result = result;
            r.heroOnlyId = string.Empty;
            return r;
        }

        /// <summary>프리팹이 생긴 뒤에 넣어야 하는 참조 (스킬 동작, 던전 방 구성).</summary>
        private static void LinkData(Data d, Prefabs p)
        {
            d.RiftWarden.prefab = p.RiftWarden;
            UnityEditor.EditorUtility.SetDirty(d.RiftWarden);
            UnityEditor.EditorUtility.SetDirty(d.Database);

            d.SpinBlade.behaviourPrefab = p.SpinBlade;
            d.FlameField.behaviourPrefab = p.FlameField;
            d.Lightning.behaviourPrefab = p.Lightning;
            d.ArrowRain.behaviourPrefab = p.ArrowRain;
            d.SteelWhirl.behaviourPrefab = p.SteelWhirl;
            d.ThunderFire.behaviourPrefab = p.ThunderFire;

            // v0.1 던전: 방 10개, 5·10번 엘리트 (Content.md 2-2를 줄인 판)
            DungeonData dungeon = d.Dungeon;
            dungeon.id = "chapter1_v01";
            dungeon.displayName = "균열의 초원 (v0.1)";
            dungeon.chapter = 1;
            dungeon.rooms = new List<RoomData>
            {
                Room(RoomKind.Normal, (p.Charger, 3, false)),
                Room(RoomKind.Normal, (p.Charger, 2, false), (p.Archer, 2, false)),
                Room(RoomKind.Normal, (p.Charger, 3, false), (p.Archer, 2, false)),
                Room(RoomKind.Normal, (p.Archer, 3, false), (p.Bomber, 2, false), (p.Charger, 1, false)),
                Room(RoomKind.Elite, (p.Charger, 1, true), (p.Charger, 3, false)),
                Room(RoomKind.Normal, (p.Charger, 4, false), (p.Bomber, 2, false)),
                Room(RoomKind.Normal, (p.Archer, 3, false), (p.Charger, 3, false), (p.Bomber, 1, false)),
                Room(RoomKind.Normal, (p.Charger, 4, false), (p.Archer, 2, false), (p.Bomber, 2, false)),
                Room(RoomKind.Normal, (p.Archer, 4, false), (p.Bomber, 3, false), (p.Charger, 2, false)),
                Room(RoomKind.Elite, (p.Archer, 1, true), (p.Bomber, 1, true), (p.Charger, 2, false)),
            };

            foreach (SkillData s in d.Pool) UnityEditor.EditorUtility.SetDirty(s);
            UnityEditor.EditorUtility.SetDirty(d.SteelWhirl);
            UnityEditor.EditorUtility.SetDirty(d.ThunderFire);
            UnityEditor.EditorUtility.SetDirty(dungeon);
        }

        private static RoomData Room(RoomKind kind, params (EnemyBase prefab, int count, bool elite)[] spawns)
        {
            var room = new RoomData { kind = kind };
            foreach (var s in spawns) room.spawns.Add(new SpawnEntry { prefab = s.prefab, count = s.count, elite = s.elite });
            return room;
        }
    }
}
