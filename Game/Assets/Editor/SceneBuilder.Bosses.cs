using HistoryHeroes.Enemy;
using HistoryHeroes.Modes;
using HistoryHeroes.Skills;
using UnityEditor;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// v0.6 중간 보스 5 (Modes 2-8) · 보스맵 보스 4 (Content 3장). 숫자는 [제안] 시작값 — 보스 벤치로 맞춘다.
    /// 보스맵 보스 체력 = 적 데이터 120 × 배수 (1구간 300 = 36,000에서 구간마다 기록이 강해지는 만큼 올린다).
    /// </summary>
    public static partial class SceneBuilder
    {
        private sealed class BossDataSet
        {
            public EnemyData Bison, Torch, Golem, Hunter, HunterClone, Executor;
            public EnemyData Wraith, Colossus, Twins, King;
            public BossData WraithLord, SteelColossus, ShadowTwins, OblivionKing;
        }

        private static BossDataSet _bosses;

        private static void CreateBossData()
        {
            var b = new BossDataSet
            {
                Bison = Enemy("mid_bison", "들소 망령", 2.2f, 0.75f, 1.2f, new Color(0.6f, 0.45f, 0.35f)),
                Torch = Enemy("mid_torch", "횃불 기사", 1.8f, 0.8f, 1.4f, new Color(0.85f, 0.4f, 0.2f)),
                Golem = Enemy("mid_golem", "증기 골렘", 1.1f, 1f, 1.6f, new Color(0.55f, 0.55f, 0.6f)),
                Hunter = Enemy("mid_hunter", "그림자 사냥꾼", 2.6f, 0.7f, 1.2f, new Color(0.3f, 0.25f, 0.4f)),
                HunterClone = Enemy("hunter_clone", "사냥꾼의 분신", 3.2f, 0.7f, 1.0f, new Color(0.3f, 0.25f, 0.4f)),
                Executor = Enemy("mid_executor", "망각의 집행자", 2f, 0.8f, 1.2f, new Color(0.45f, 0.25f, 0.6f)),
                Wraith = Enemy("boss_wraith", "망령 군주", 1.6f, 1f, 1.5f, new Color(0.55f, 0.45f, 0.85f)),
                Colossus = Enemy("boss_colossus", "강철 거상", 0.8f, 1f, 1.6f, new Color(0.6f, 0.62f, 0.68f)),
                Twins = Enemy("boss_twins", "그림자 쌍둥이", 2.2f, 0.85f, 1.4f, new Color(0.22f, 0.2f, 0.32f)),
                King = Enemy("boss_king", "망각의 왕", 1.6f, 1f, 1.4f, new Color(0.55f, 0.3f, 0.75f)),
            };
            foreach (EnemyData e in new[] { b.Bison, b.Torch, b.Golem, b.Hunter, b.Executor, b.Wraith, b.Colossus, b.Twins, b.King })
            {
                e.experience = 0; // 보스는 경험치 대신 방 클리어로
                EditorUtility.SetDirty(e);
            }

            b.HunterClone.maxHp = 100f;
            b.HunterClone.experience = 1;
            EditorUtility.SetDirty(b.HunterClone);

            b.WraithLord = Boss("wraith_lord", "망령 군주", 2, "잡몹을 계속 부른다 → 광역 빌드", "광역 빌드 (번개·불꽃 장판·독 구름)", SkillRole.Area, 420f, 1.7f, 7);
            b.SteelColossus = Boss("steel_colossus", "강철 거상", 3, "탄막을 쏟아붓는다 → 칼날 탱커형, 피하기", "칼날 탱커형 (강철 회오리·방어력·최대체력)", SkillRole.Defense, 560f, 2f, 6);
            b.ShadowTwins = Boss("shadow_twins", "그림자 쌍둥이", 4, "둘이 따로 움직이고 한 번에 하나만 맞는다 → 단일 순간 화력", "연사형 (공격속도·멀티샷·공격력)", SkillRole.Single, 680f, 2.3f, 6);
            b.OblivionKing = Boss("oblivion_king", "망각의 왕", 5, "앞 보스의 시험을 전부 섞는다 → 완성된 빌드", "강점이 고른 빌드 (조합 1~2개 + 기본 공격)", SkillRole.None, 900f, 2.7f, 5);
            _bosses = b;
        }

        private static BossData Boss(string id, string name, int chapter, string tests, string recommend, SkillRole role, float hp, float attack, int star2)
        {
            var b = AssetUtil.LoadOrCreate<BossData>($"{DataDir}/Bosses/Boss_{id}.asset");
            b.id = id;
            b.displayName = name;
            b.chapter = chapter;
            b.tests = tests;
            b.recommend = recommend;
            b.recommendedRole = role;
            b.hpMultiplier = hp;
            b.attackMultiplier = attack;
            b.star2MaxHits = star2;
            EditorUtility.SetDirty(b);
            return b;
        }

        private static void CreateBossPrefabs(Prefabs p, Material line)
        {
            BossDataSet b = _bosses;
            System.Action<GameObject, Props> Common(bool mid, float scale) => (go, props) =>
            {
                props.Ref("openingRing", ChildSprite(go, "OpeningRing", _circle, new Color(1f, 0.9f, 0.25f, 0.35f), 1.6f, -1))
                    .Mask("wallMask", Mask(WallLayer)).Bool("midBoss", mid).Float("bossScale", scale);
            };

            p.MidBison = EnemyPrefab<BisonWraithBoss>("Mid_Bison", b.Bison, _circle, line, false, Common(true, 1.6f));
            p.MidTorch = EnemyPrefab<TorchKnightBoss>("Mid_Torch", b.Torch, _square, line, false, Common(true, 1.5f));
            p.MidGolem = EnemyPrefab<SteamGolemBoss>("Mid_Golem", b.Golem, _square, line, false, Common(true, 1.8f));
            var clone = EnemyPrefab<ChargerEnemy>("Mid_HunterClone", b.HunterClone, _circle, line, false);
            p.MidHunter = EnemyPrefab<ShadowHunterBoss>("Mid_Hunter", b.Hunter, _circle, line, false, (go, props) =>
            {
                Common(true, 1.4f)(go, props);
                // 진짜만 발밑에 그림자 (분신에는 없다)
                SpriteRenderer shadow = ChildSprite(go, "Shadow", _circle, new Color(0f, 0f, 0f, 0.55f), 1.15f, 1);
                shadow.transform.localPosition = new Vector3(0f, -0.38f, 0f);
                shadow.transform.localScale = new Vector3(1.2f, 0.45f, 1f);
                props.Ref("clonePrefab", clone).Ref("shadow", shadow).Float("cloneHp", 1f);
            });
            p.MidExecutor = EnemyPrefab<OblivionExecutorBoss>("Mid_Executor", b.Executor, _circle, line, false, (go, props) =>
            {
                Common(true, 1.6f)(go, props);
                props.Ref("clonePrefab", clone);
            });

            p.WraithLord = EnemyPrefab<WraithLordBoss>("Boss_WraithLord", b.Wraith, _circle, line, false, (go, props) =>
            {
                Common(false, 2.1f)(go, props);
                props.Ref("minion", p.Minion);
            });
            p.SteelColossus = EnemyPrefab<SteelColossusBoss>("Boss_SteelColossus", b.Colossus, _square, line, false, Common(false, 2.4f));
            p.ShadowTwins = EnemyPrefab<ShadowTwinsBoss>("Boss_ShadowTwins", b.Twins, _circle, line, false, (go, props) =>
            {
                Common(false, 1.9f)(go, props);
                // 그림자 쪽 (맞지 않는다): 흐린 같은 모양
                SpriteRenderer twin = ChildSprite(go, "Twin", _circle, new Color(0.22f, 0.2f, 0.32f, 0.45f), 1f, 2);
                props.Ref("twin", twin);
            });
            p.OblivionKing = EnemyPrefab<OblivionKingBoss>("Boss_OblivionKing", b.King, _circle, line, false, (go, props) =>
            {
                Common(false, 2.3f)(go, props);
                props.Ref("minion", p.Minion);
            });
        }

        private static void LinkBosses(Prefabs p)
        {
            BossDataSet b = _bosses;
            b.WraithLord.prefab = p.WraithLord;
            b.SteelColossus.prefab = p.SteelColossus;
            b.ShadowTwins.prefab = p.ShadowTwins;
            b.OblivionKing.prefab = p.OblivionKing;
            foreach (BossData boss in new[] { b.WraithLord, b.SteelColossus, b.ShadowTwins, b.OblivionKing }) EditorUtility.SetDirty(boss);
        }

        /// <summary>구간 1~5 보스맵 보스 (로비·보스맵이 같이 쓴다)</summary>
        private static BossData[] AllBosses(BossData warden)
        {
            return new[] { warden, _bosses.WraithLord, _bosses.SteelColossus, _bosses.ShadowTwins, _bosses.OblivionKing };
        }

        private static BossData[] LoadAllBosses()
        {
            string[] ids = { "RiftWarden", "wraith_lord", "steel_colossus", "shadow_twins", "oblivion_king" };
            var list = new BossData[ids.Length];
            for (int i = 0; i < ids.Length; i++) list[i] = AssetDatabase.LoadAssetAtPath<BossData>($"{DataDir}/Bosses/Boss_{ids[i]}.asset");
            return list;
        }
    }
}
