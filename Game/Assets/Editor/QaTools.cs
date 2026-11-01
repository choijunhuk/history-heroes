using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Enemy;
using HistoryHeroes.Player;
using HistoryHeroes.Progress;
using HistoryHeroes.Skills;
using HistoryHeroes.UI;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// Play 모드에서 한 판을 빠르게 끝까지 돌려보는 점검 도구 (unity command eval로 부른다).
    /// Step()을 반복해서 부르면: 카드가 떠 있으면 고르고 → 적이 있으면 처치 → 문이 열려 있으면 들어간다.
    /// </summary>
    public static class QaTools
    {
        /// <summary>
        /// 에디터 창이 뒤에 있으면 Play 모드가 아예 멈춘다 (프레임이 안 넘어감).
        /// 자동 테스트를 창 포커스와 상관없이 돌리려고, QA 도구를 부를 때마다 켠다. 이번 Play에만 적용된다.
        /// </summary>
        private static void KeepRunning()
        {
            Application.runInBackground = true;
        }

        /// <param name="heal">true면 매번 체력을 가득 채운다 (흐름만 확인할 때)</param>
        public static string Step(int cardIndex = 0, bool heal = false)
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            KeepRunning();
            var levelUp = Object.FindAnyObjectByType<LevelUpScreen>();
            var run = Object.FindAnyObjectByType<DungeonRun>();
            if (levelUp == null || run == null) return "Play 모드가 아님";
            if (run.IsFinished) return "끝남 | " + State();
            if (heal) Object.FindAnyObjectByType<PlayerController>().Health.Heal(1e9f);

            if (levelUp.IsOpen)
            {
                levelUp.ChooseIndex(cardIndex);
                return "카드 고름 | " + State();
            }

            // v0.6 보상 방: 카드 번호로 고른다 (0 회복 · 1 카드 · 2 골드)
            if (run.RewardRoom != null && run.RewardRoom.IsOpen)
            {
                run.RewardRoom.Choose((RewardRoomPanel.Choice)Mathf.Clamp(cardIndex, 0, 2));
                return "보상 고름 | " + State();
            }

            if (EnemyBase.Alive.Count > 0)
            {
                Dev.DevCheats.KillAll();
                return "처치 | " + State();
            }

            // 확장 1차 갈림길: 카드 번호로 문을 고른다 (0 왼쪽 평범 · 1 오른쪽 특별)
            if (run.ForkOpen)
            {
                run.DevChooseFork(Mathf.Clamp(cardIndex, 0, 1));
                return "갈림길 | " + State();
            }

            foreach (Door door in Object.FindObjectsByType<Door>(FindObjectsSortMode.None))
            {
                if (!door.IsOpen) continue;
                Object.FindAnyObjectByType<PlayerController>().Teleport(door.transform.position);
                return "문 | " + State();
            }

            return "대기 | " + State();
        }

        // ---------- 빌드 비교 시나리오 (GameDesign 6-8 "상황마다 1등이 다른가", 3-3 "정지 vs 이동") ----------

        /// <summary>
        /// Play 시작 직후에 부른다. 던전 흐름을 끄고, 빌드 프리셋을 준 뒤, 시나리오 적을 세운다.
        /// kind: boss (체력 25배 엘리트 1) / swarm (돌진병 14) / barrage (궁병 6 + 투척병 2)
        /// move: true면 계속 움직임(기본 공격 없음), false면 제자리(멈춤)
        /// 시나리오 적은 경험치를 주지 않아서 도중에 레벨업으로 빌드가 바뀌지 않는다.
        /// </summary>
        public static string Scenario(int preset, string kind, bool move)
        {
            // 에디터 장면(Play 전)을 건드리면 안 된다 → Play 중일 때만
            if (!Application.isPlaying) return "Play 모드가 아님";
            KeepRunning();
            var run = Object.FindAnyObjectByType<DungeonRun>();
            var cheats = Object.FindAnyObjectByType<Dev.DevCheats>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (run == null || cheats == null || player == null) return "Play 모드가 아님";

            // 방 적을 없앨 때 얻는 경험치로 레벨업 창이 뜨면 게임이 멈춰서(timeScale 0) 측정이 끝나지 않는다 → 시나리오 동안 끈다
            var levelUp = Object.FindAnyObjectByType<LevelUpScreen>();
            if (levelUp != null)
            {
                levelUp.DevCancel();
                levelUp.enabled = false;
            }

            run.gameObject.SetActive(false); // 방 흐름·그 방의 적 정지
            Dev.DevCheats.KillAll();
            Object.FindAnyObjectByType<SkillManager>().ClearAll(); // Play 시작 후 몇 초 동안 고른 카드가 섞이지 않게
            string presetName = cheats.ApplyPreset(preset);

            player.Teleport(new Vector2(0f, -5.5f));
            player.Health.Heal(1e9f);

            var root = new GameObject("Scenario").transform;
            switch (kind)
            {
                case "boss":
                    SpawnScenario(root, "Enemy_Charger", 1, hp: 5f, elite: true, player);
                    break;
                case "swarm":
                    SpawnScenario(root, "Enemy_Charger", 14, hp: 1.4f, elite: false, player);
                    break;
                default:
                    SpawnScenario(root, "Enemy_Archer", 6, hp: 1.4f, elite: false, player);
                    SpawnScenario(root, "Enemy_Bomber", 2, hp: 1.4f, elite: false, player);
                    break;
            }

            Combat.DamageLog.Reset();
            string scenarioName = $"{presetName} | {kind} | {(move ? "이동만" : "정지")}";
            root.gameObject.AddComponent<Dev.ScenarioMonitor>().Begin(scenarioName, move, player);
            return "시작: " + scenarioName;
        }

        /// <summary>시나리오 진행 확인. 적이 다 죽으면 걸린 시간과 기본 공격 피해 비중을 돌려준다.</summary>
        public static string ScenarioStatus()
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            KeepRunning();
            var monitor = Object.FindAnyObjectByType<Dev.ScenarioMonitor>();
            if (monitor == null) return "무효: 시나리오 없음";
            if (!monitor.Finished) return $"진행 중 {monitor.Elapsed:0.0}초 남은 적 {EnemyBase.Alive.Count}";

            string result = monitor.Cleared ? $"{monitor.Elapsed:0.0}초" : $"{Dev.ScenarioMonitor.TimeLimit:0}초 안에 못 잡음";
            return $"결과: {monitor.ScenarioName} | {result} | 기본 공격 {Mathf.RoundToInt(Combat.DamageLog.BasicShare * 100f)}%";
        }

        private static void SpawnScenario(Transform root, string prefabName, int count, float hp, bool elite, PlayerController player)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyBase>($"Assets/Prefabs/Enemies/{prefabName}.prefab");
            for (int i = 0; i < count; i++)
            {
                var position = new Vector2(Random.Range(-3.2f, 3.2f), Random.Range(1f, 6f));
                EnemyBase enemy = Object.Instantiate(prefab, position, Quaternion.identity, root);
                enemy.Spawn(player.transform, hp, 1f, elite);
            }
        }

        /// <summary>
        /// 스킬 하나만 들고 적 무리(돌진병 8 + 궁병 3 + 엘리트 1)를 상대해 본다 (새 스킬 점검용).
        /// 방 흐름·레벨업 창은 끈다. 예: TrySkill("meteor", 3) / TrySkill("thunder_god", 1, fusion: true)
        /// </summary>
        public static string TrySkill(string skillId, int level = 3, bool fusion = false)
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            KeepRunning();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var skills = Object.FindAnyObjectByType<SkillManager>();
            var db = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDatabase>("Assets/Data/Skills/SkillDatabase.asset");
            SkillData skill = db.Find(skillId);
            if (player == null || skills == null || skill == null) return "없음: " + skillId;

            var levelUp = Object.FindAnyObjectByType<LevelUpScreen>();
            if (levelUp != null)
            {
                levelUp.DevCancel();
                levelUp.enabled = false;
            }

            var run = Object.FindAnyObjectByType<DungeonRun>();
            if (run != null) run.gameObject.SetActive(false);
            Dev.DevCheats.KillAll();
            skills.ClearAll();
            skills.DevSetLevel(skill, level, fusion);
            player.Teleport(new Vector2(0f, -4f));
            player.Health.Heal(1e9f);
            player.Health.Invulnerable = true; // 스킬만 보려고: 죽어서 결과 화면이 뜨면 점검이 멈춘다

            var root = new GameObject("TrySkill").transform;
            SpawnScenario(root, "Enemy_Charger", 8, hp: 2f, elite: false, player);
            SpawnScenario(root, "Enemy_Archer", 3, hp: 2f, elite: false, player);
            SpawnScenario(root, "Enemy_Charger", 1, hp: 2f, elite: true, player);
            Combat.DamageLog.Reset();
            return $"시작: {skill.displayName} Lv{level} (적 12)";
        }

        /// <summary>
        /// v0.3 8단계 검증 벤치: 영웅 4명 × (dps, play)로 같은 기록을 들고 실제 1구간 보스와 싸운다 (연습 모드).
        /// build: "balanced"(기본) / "rapid"(연사형) / "blade"(칼날 탱커형) / "storm"(번개 광역형). 전설·영웅 전용 스킬 없음.
        /// modes: "dps", "play"(보스) / "swarm", "barrage"(던전 잡몹). 예: BossBench("balanced", 3f, "swarm,barrage")
        /// 끝나면 BossBenchStatus()로 결과. 예: BossBench("balanced", 3f)
        /// </summary>
        public static string BossBench(string build = "balanced", float speed = 3f, string modes = "dps,play", string heroes = "khan,guan,napoleon,tesla", string chapters = "1")
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            KeepRunning();
            var db = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDatabase>("Assets/Data/Skills/SkillDatabase.asset");
            List<Records.RecordSkill> skills = BenchBuild(build);
            int cost = 0;
            foreach (Records.RecordSkill s in skills)
            {
                SkillData data = db.Find(s.id);
                if (data == null) return "없는 스킬: " + s.id;
                if (data.rarity == Rarity.Legendary) return "전설은 넣지 않는다: " + s.id;
                cost += data.SaveCostAt(s.level);
            }

            var runs = new List<Dev.BossBench.Run>();
            foreach (string chapter in chapters.Split(','))
            {
                int ch = int.TryParse(chapter.Trim(), out int c) ? c : 1;
                foreach (string mode in modes.Split(','))
                {
                    foreach (string hero in heroes.Split(',')) runs.Add(new Dev.BossBench.Run { HeroId = hero.Trim(), Mode = mode.Trim(), Chapter = ch });
                }
            }

            Dev.BossBench.ChargerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyBase>("Assets/Prefabs/Enemies/Enemy_Charger.prefab");
            Dev.BossBench.ArcherPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyBase>("Assets/Prefabs/Enemies/Enemy_Archer.prefab");
            Dev.BossBench.BomberPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyBase>("Assets/Prefabs/Enemies/Enemy_Bomber.prefab");
            return Dev.BossBench.Begin(runs, skills, speed, db) + $" | 빌드 {build} {cost}pt (클리어 기록 용량 27pt)";
        }

        public static string BossBenchStatus() => Dev.BossBench.Report();

        /// <summary>
        /// v0.6 역할 벤치 (Content 3장 "보스가 시험하는 것"): 구간마다 빌드 여럿을 같은 판 밖 성장으로 붙인다.
        /// 2구간 망령 군주 = 광역(storm), 3구간 강철 거상 = 칼날 탱커(blade), 4구간 그림자 쌍둥이 = 단일 화력(rapid), 5구간 = 완성 빌드(balanced).
        /// 예: RoleBench(4f, "khan", "month", "2,3,4,5", "storm,blade,rapid,balanced", "play") → BossBenchStatus()
        /// </summary>
        public static string RoleBench(float speed = 4f, string hero = "khan", string meta = "month", string chapters = "2,3,4,5", string builds = "storm,blade,rapid,balanced", string mode = "play")
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            KeepRunning();
            var db = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDatabase>("Assets/Data/Skills/SkillDatabase.asset");
            var runs = new List<Dev.BossBench.Run>();
            foreach (string c in chapters.Split(','))
            {
                int ch = int.TryParse(c.Trim(), out int v) ? v : 1;
                foreach (string b in builds.Split(','))
                {
                    string build = b.Trim();
                    runs.Add(new Dev.BossBench.Run { HeroId = hero, Mode = mode, Chapter = ch, Build = BenchBuild(build), Meta = MetaPreset(meta), Label = $"{build}/{meta}" });
                }
            }

            Dev.BossBench.ChargerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyBase>("Assets/Prefabs/Enemies/Enemy_Charger.prefab");
            Dev.BossBench.ArcherPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyBase>("Assets/Prefabs/Enemies/Enemy_Archer.prefab");
            Dev.BossBench.BomberPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyBase>("Assets/Prefabs/Enemies/Enemy_Bomber.prefab");
            return Dev.BossBench.Begin(runs, BenchBuild("balanced"), speed, db);
        }

        /// <summary>
        /// v0.5 서바이벌 벤치: 영웅마다 10분 판을 자동 조종으로 (카드 무작위, 위험하면 피하고 여유 있으면 멈춰 쏘기).
        /// god = 체력 계속 채움 (끝까지 가서 적 300·성능 확인). 예: SurvivalBench("khan,guan", 4f, false) → SurvivalBenchStatus()
        /// </summary>
        public static string SurvivalBench(string heroes = "khan,guan,napoleon,tesla", float speed = 4f, bool god = false)
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            KeepRunning();
            var db = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDatabase>("Assets/Data/Skills/SkillDatabase.asset");
            var list = new List<string>();
            foreach (string h in heroes.Split(',')) list.Add(h.Trim());
            return Dev.SurvivalBench.Begin(list, speed, god, db);
        }

        public static string SurvivalBenchStatus() => Dev.SurvivalBench.Report();

        /// <summary>
        /// v0.6 무한의 균열 벤치: 영웅마다 1층부터 자동 조종으로 쓰러질 때까지 (Modes 4-3 목표 20~30층).
        /// meta: MetaPreset 이름 (none/week/month/max). 예: RiftBench("khan,guan,napoleon,tesla", "month", 4f, 60) → RiftBenchStatus()
        /// </summary>
        public static string RiftBench(string heroes = "khan,guan,napoleon,tesla", string meta = "month", float speed = 4f, int maxFloor = 60, int dungeonChapter = 0)
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            KeepRunning();
            var db = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDatabase>("Assets/Data/Skills/SkillDatabase.asset");
            var list = new List<string>();
            foreach (string h in heroes.Split(',')) list.Add(h.Trim());
            return Dev.RiftBench.Begin(list, MetaPreset(meta), speed, maxFloor, db, dungeonChapter);
        }

        public static string RiftBenchStatus() => Dev.RiftBench.Report();

        // ---------------------------------------------------------------- v0.4 판 밖 성장

        /// <summary>
        /// 판 밖 성장 스냅샷 예시: "none" 판 밖 0 / "week" 무과금 1주 (Stats 4장 ×1.2 근처: 영웅 Lv8, 1구간 보스 희귀 무기 Lv10, 일반 갑옷 Lv5)
        /// / "month" 1개월 (×1.4 근처) / "max" 전부 최대 (영웅 Lv30 ★5, 전설 6칸 Lv40, 수련 최대, 유물 24개 ★3)
        /// </summary>
        public static Records.MetaSnapshot MetaPreset(string name)
        {
            var s = new Records.MetaSnapshot();
            void Equip(int slot, string id, int level) => s.equipment.Add(new Records.EquippedItem { slot = slot, dataId = id, level = level });
            switch (name)
            {
                case "week":
                    s.heroLevel = 8;
                    Equip(0, "weapon_general_r", 10);
                    Equip(1, "armor_recruit_c", 5);
                    Equip(3, "gloves_general_r", 10);
                    Equip(4, "belt_recruit_c", 5);
                    Equip(5, "boots_recruit_c", 5);
                    break;
                case "month":
                    s.heroLevel = 15;
                    s.heroStars = 2;
                    Equip(0, "weapon_general_r", 20);
                    Equip(1, "armor_general_r", 20);
                    Equip(2, "acc_signet_r", 15);
                    Equip(3, "gloves_general_r", 20);
                    Equip(4, "belt_general_r", 20);
                    Equip(5, "boots_general_r", 20);
                    s.training = new List<int> { 5, 5, 3, 2, 2, 0 };
                    break;
                case "max":
                    s.heroLevel = 30;
                    s.heroStars = 5;
                    Equip(0, "weapon_conqueror_l", 40);
                    Equip(1, "armor_timeguard_l", 40);
                    Equip(2, "acc_hourglass_l", 40);
                    Equip(3, "gloves_conqueror_l", 40);
                    Equip(4, "belt_timeguard_l", 40);
                    Equip(5, "boots_timeguard_l", 40);
                    s.training = new List<int> { 20, 20, 20, 20, 20, 10 };
                    var relics = UnityEditor.AssetDatabase.LoadAssetAtPath<Relics.RelicTable>("Assets/Data/Relics/RelicTable.asset");
                    foreach (Relics.RelicData r in relics.relics) s.relics.Add(new Records.RelicStar { id = r.id, stars = 3 });
                    break;
            }

            return s;
        }

        /// <summary>
        /// v0.4 완료 기준 벤치 (GameDesign 11장 + Stats 4-3). 실제 1구간 보스, 연습 모드, 칭기즈 칸.
        /// ① 같은 빌드(연사형)로 판 밖 0 vs 최대 (dps: 처치 시간 비율 3.0 이하)
        /// ② "기본 성장(1주) + 잘 짠 빌드(연사형)" vs "높은 성장(최대) + 대충 고른 빌드" (play: 피하기 AI)
        /// </summary>
        public static string MetaBench(float speed = 3f, string hero = "khan")
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            KeepRunning();
            var db = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDatabase>("Assets/Data/Skills/SkillDatabase.asset");
            List<Records.RecordSkill> good = BenchBuild("rapid");
            List<Records.RecordSkill> sloppy = BenchBuild("sloppy");
            var runs = new List<Dev.BossBench.Run>
            {
                new Dev.BossBench.Run { HeroId = hero, Mode = "dps", Build = good, Meta = MetaPreset("none"), Label = "판 밖 0 + 연사형" },
                new Dev.BossBench.Run { HeroId = hero, Mode = "dps", Build = good, Meta = MetaPreset("max"), Label = "판 밖 최대 + 연사형" },
                new Dev.BossBench.Run { HeroId = hero, Mode = "play", Build = good, Meta = MetaPreset("week"), Label = "1주 성장 + 연사형" },
                new Dev.BossBench.Run { HeroId = hero, Mode = "play", Build = sloppy, Meta = MetaPreset("max"), Label = "최대 성장 + 대충 빌드" },
                new Dev.BossBench.Run { HeroId = hero, Mode = "play", Build = sloppy, Meta = MetaPreset("month"), Label = "1개월 성장 + 대충 빌드" },
            };

            Dev.BossBench.ChargerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyBase>("Assets/Prefabs/Enemies/Enemy_Charger.prefab");
            Dev.BossBench.ArcherPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyBase>("Assets/Prefabs/Enemies/Enemy_Archer.prefab");
            Dev.BossBench.BomberPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyBase>("Assets/Prefabs/Enemies/Enemy_Bomber.prefab");
            return Dev.BossBench.Begin(runs, good, speed, db);
        }

        /// <summary>
        /// 개발용 (v0.4 4단계, 임시 저장에서 쓸 것): 지금 저장에 판 밖 성장을 채운다 — 구간 해금, 영웅 Lv·★, 전설 장비 3칸 + 칸 강화,
        /// 수련, 유물, 룬. 2구간 보스가 아직 없어서 수련·룬·유물을 시험하려면 필요하다.
        /// </summary>
        /// <summary>
        /// v0.5 펫 시험: 4마리를 모두 그 레벨로 주고, 고른 영웅에게 equip 펫을 끼운다 (SandboxSave(true)와 같이 쓴다).
        /// 예: DevPets("hawk", 10) → 던전·서바이벌에서 각성 행동까지 본다
        /// </summary>
        public static string DevPets(string equip = "hawk", int level = 1)
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            Records.SaveData d = Records.SaveSystem.Data;
            var meta = UnityEditor.AssetDatabase.LoadAssetAtPath<Meta.MetaTable>("Assets/Data/Meta/MetaTable.asset");
            if (meta == null || meta.pets == null) return "펫 표가 없음";
            foreach (Pets.PetData p in meta.pets.pets)
            {
                Records.PetLevel owned = d.pets.owned.Find(x => x.id == p.id);
                if (owned == null) d.pets.owned.Add(new Records.PetLevel { id = p.id, level = Mathf.Clamp(level, 1, meta.pets.maxLevel) });
                else owned.level = Mathf.Clamp(level, 1, meta.pets.maxLevel);
            }

            bool ok = Pets.PetService.Equip(d, meta.pets, d.selectedHero, string.IsNullOrEmpty(equip) ? null : equip);
            Records.SaveSystem.Save();
            return $"펫 {meta.pets.pets.Count}마리 Lv{level}, {d.selectedHero} ← {equip} ({(ok ? "끼움" : "실패")})";
        }

        public static string DevFillMeta(string heroId = "khan", int chapters = 5)
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            Records.SaveData d = Records.SaveSystem.Data;
            var meta = UnityEditor.AssetDatabase.LoadAssetAtPath<Meta.MetaTable>("Assets/Data/Meta/MetaTable.asset");
            d.unlockedChapter = Mathf.Max(d.unlockedChapter, chapters + 1);
            d.accountLevel = Mathf.Max(d.accountLevel, 20);
            d.Hero(heroId).level = 30;
            d.Hero(heroId).stars = 5;
            foreach (string id in new[] { "weapon_conqueror_l", "armor_timeguard_l", "acc_hourglass_l" })
            {
                var r = Equipment.EquipmentInventory.Add(d, meta.equipment, meta.equipment.Find(id));
                Records.EquipmentItem item = r.Item ?? d.equipment.items.Find(i => i.dataId == id);
                if (item != null) Equipment.EquipmentInventory.Equip(d, meta.equipment, heroId, item.uid);
            }

            for (int i = 0; i < 3; i++) d.equipment.slotLevels[i] = 40;
            d.training = new List<int> { 20, 20, 20, 20, 20, 10 };
            foreach (Relics.RelicData r in meta.relics.relics)
            {
                if (Relics.RelicService.Stars(d, r.id) == 0) d.relics.owned.Add(new Records.RelicStar { id = r.id, stars = 3 });
            }

            foreach (Runes.RuneData r in meta.runes.runes)
            {
                if (!Runes.RuneService.Owns(d, r.id)) d.runes.owned.Add(new Records.RuneLevel { id = r.id, level = 3 });
            }

            Records.SaveSystem.Save();
            return $"판 밖 성장 채움: {heroId} (구간 {d.unlockedChapter - 1}까지 깬 것으로)";
        }

        private static Records.SaveStore _realStore;

        /// <summary>
        /// 화면 점검용 임시 저장: on = 지금 데이터를 임시 폴더에 복사해 그쪽으로 바꿔 끼운다 (골드·보석을 넣어 봐도 실제 저장은 그대로).
        /// off = 원래 저장으로 되돌린다. Play를 꺼도 다음 Play는 기기 저장을 읽는다.
        /// </summary>
        public static string SandboxSave(bool on)
        {
            bool sandboxed = Records.SaveSystem.Store.MainPath.Contains("QaSandboxSave");
            if (on)
            {
                if (sandboxed) return "이미 임시 저장";
                _realStore = Records.SaveSystem.Store;
                var sandbox = new Records.SaveStore(System.IO.Path.Combine(Application.temporaryCachePath, "QaSandboxSave"));
                sandbox.Save(Records.SaveSystem.Data);
                Records.SaveSystem.Use(sandbox);
                return "임시 저장으로 바꿈";
            }

            if (!sandboxed) return "임시 저장 아님";
            Records.SaveSystem.Use(_realStore ?? Records.SaveStore.ForDevice());
            _realStore = null;
            return "원래 저장으로 되돌림";
        }

        private static List<Records.RecordSkill> BenchBuild(string build)
        {
            var list = new List<Records.RecordSkill>();
            void Add(string id, int level, bool fusion = false) => list.Add(new Records.RecordSkill(id, level, fusion));
            switch (build)
            {
                case "rapid": // 연사형 (GameDesign 6-8)
                    Add("aspd_up", 4); Add("multishot", 2); Add("atk_up", 3); Add("lifesteal", 2); Add("lightning", 2);
                    break;
                case "blade": // 칼날 탱커형
                    Add("steel_whirl", 1, true); Add("def_up", 2); Add("hp_up", 3); Add("atk_up", 2); Add("aspd_up", 1);
                    break;
                case "storm": // 번개 광역형
                    Add("thunder_fire", 1, true); Add("hp_up", 3); Add("atk_up", 2); Add("aspd_up", 1);
                    break;
                case "aoe": // 광역 여럿 (조합 없이, 21pt): 낙뢰 + 불꽃 장판 + 독 구름
                    Add("lightning", 3); Add("flame_field", 2); Add("poison_cloud", 2); Add("atk_up", 3); Add("aspd_up", 1);
                    break;
                // 완성 빌드 (27pt = 던전 클리어 기록 용량 34 × 0.8): 5구간 보스 확인용 (v0.6 검증 ⏳)
                case "storm27":
                    Add("thunder_fire", 2, true); Add("hp_up", 3); Add("atk_up", 3); Add("aspd_up", 3);
                    break;
                case "blade27":
                    Add("steel_whirl", 1, true); Add("def_up", 3); Add("hp_up", 4); Add("atk_up", 4); Add("aspd_up", 3); Add("lifesteal", 1); Add("crit_rate", 1);
                    break;
                case "rapid27":
                    Add("aspd_up", 5); Add("multishot", 3); Add("atk_up", 4); Add("lifesteal", 2); Add("lightning", 2); Add("crit_rate", 2);
                    break;
                case "balanced27":
                    Add("aspd_up", 4); Add("multishot", 2); Add("atk_up", 4); Add("lifesteal", 2); Add("lightning", 2); Add("flame_field", 2); Add("hp_up", 1);
                    break;
                case "complete27": // 5구간 추천 "강점이 고른 빌드 (조합 1~2개 + 기본 공격)": 강철 회오리 + 기본 공격 패시브
                    Add("steel_whirl", 1, true); Add("aspd_up", 3); Add("atk_up", 4); Add("multishot", 2); Add("lifesteal", 2); Add("hp_up", 2);
                    break;
                case "sloppy": // 대충 고른 빌드: 보스에 별 도움 안 되는 것들 (같은 크기 21pt)
                    Add("exp_up", 3); Add("move_up", 3); Add("regen", 3); Add("ice_shard", 2); Add("poison_cloud", 2); Add("guard_shield", 2);
                    break;
                default: // 섞음: 대표 완성 빌드에 가깝게 (기본 공격 패시브 + 액티브 2개)
                    Add("aspd_up", 3); Add("multishot", 1); Add("atk_up", 3); Add("lifesteal", 2);
                    Add("lightning", 2); Add("flame_field", 2);
                    break;
            }

            return list;
        }

        /// <summary>TrySkill 뒤: 남은 적, 액티브 피해, 플레이어 체력</summary>
        /// <summary>
        /// 새 영웅 실전 점검: 영웅을 바꾸고 그 영웅 전용 액티브 Lv5를 들고 TrySkill과 같은 적 12마리와 싸운다 (가만히 서서 = 멈춤 대표 효과 확인).
        /// 결과는 TrySkillStatus()의 출처별 피해. 예: HeroCheck("thor")
        /// </summary>
        public static string HeroCheck(string heroId)
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            var roster = UnityEditor.AssetDatabase.LoadAssetAtPath<Heroes.HeroRoster>("Assets/Data/Heroes/HeroRoster.asset");
            Heroes.HeroData hero = roster != null ? roster.heroes.Find(h => h != null && h.id == heroId) : null;
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (hero == null || player == null) return "없음: " + heroId;
            player.SetHero(hero);
            if (hero.exclusiveActive == null) return "전용 액티브 없음: " + heroId;
            return TrySkill(hero.exclusiveActive.id, 5) + " · " + hero.displayName;
        }

        public static string TrySkillStatus()
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            var player = Object.FindAnyObjectByType<PlayerController>();
            player.Health.Heal(1e9f);
            var db = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDatabase>("Assets/Data/Skills/SkillDatabase.asset");
            return $"남은 적 {EnemyBase.Alive.Count} · 액티브 피해 {Combat.DamageLog.Active:0} · 기본 공격 {Combat.DamageLog.BasicAttack:0} · 보호막 {player.Health.Shield:0} / "
                + $"출처별: {Economy.RunText.TopDamage(db, player.Hero, 8)}";
        }

        /// <summary>
        /// 화면에 보이는 버튼을 이름으로 누른다 (로비·결과·기록 화면 흐름 점검용).
        /// 예: "Dungeon", "Boss", "Primary"(결과 첫 버튼), "Save", "AutoFit", "Slot1", "Start", "Back"
        /// </summary>
        public static string Press(string buttonName)
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            KeepRunning();
            foreach (UnityEngine.UI.Button b in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
            {
                if (b.name != buttonName || !b.gameObject.activeInHierarchy) continue;
                if (!b.interactable) return $"{buttonName}: 누를 수 없음";
                b.onClick.Invoke();
                return $"{buttonName} 누름 | 장면 {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}";
            }

            return $"{buttonName}: 화면에 없음";
        }

        /// <summary>
        /// 메인 카메라가 보는 전투 화면을 바로 PNG로 (UI 제외). Game 창이 뒤에 있으면 capture가 옛 화면을 줄 때가 있어서.
        /// 예: SnapshotWorld("Assets/Temp/qa/x.png")
        /// </summary>
        public static string SnapshotWorld(string path, int width = 540, int height = 1170)
        {
            Camera camera = Camera.main;
            if (camera == null) return "카메라 없음";
            var rt = new RenderTexture(width, height, 24);
            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            camera.targetTexture = previous;
            RenderTexture.active = null;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(rt);
            return "저장 " + path;
        }

        /// <summary>Play 모드에 들어가서 장면이 다 준비됐는가 (자동 점검 스크립트가 기다릴 때 씀)</summary>
        public static string Ready()
        {
            if (Application.isPlaying) KeepRunning();
            bool ready = Application.isPlaying && Time.time > 0.5f && Object.FindAnyObjectByType<DungeonRun>() != null;
            return ready ? "ready" : "wait";
        }

        /// <summary>
        /// 확장 1차 장치·방 점검: 지금 방에 레이아웃 하나를 바로 깐다 (적은 그대로, 판정·장치·함정 새로).
        /// id = 레이아웃 id (예: "c2_portcullis"). Play 중에만
        /// </summary>
        public static string ShowLayout(string id)
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            KeepRunning();
            var run = Object.FindAnyObjectByType<DungeonRun>();
            if (run == null) return "던전 아님";
            string path = $"Assets/Data/Dungeons/Layouts/Layout_{id}.asset";
            var layout = UnityEditor.AssetDatabase.LoadAssetAtPath<RoomLayout>(path);
            if (layout == null) return "레이아웃 없음: " + path;
            typeof(DungeonRun).GetMethod("ApplyLayout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(run, new object[] { layout });
            int devices = layout.devices != null ? layout.devices.Count : 0;
            return $"{id}: 장애물 {layout.obstacles.Count} · 함정 {layout.traps.Count} · 장치 {devices}";
        }

        // ───── 글자 점검 (TextAudit) ─────

        private static string Path(Transform t)
        {
            string n = t.name;
            for (int i = 0; i < 2 && t.parent != null && t.parent.GetComponent<Canvas>() == null; i++) { t = t.parent; n = t.name + "/" + n; }
            return n;
        }

        // 캔버스 로컬 좌표 사각형 (에디터 Game 창 크기와 상관없이 1080 기준 단위)
        private static Rect ScreenBounds(RectTransform rt, Bounds local, Canvas canvas)
        {
            var root = (RectTransform)canvas.transform;
            Vector3 a = root.InverseTransformPoint(rt.TransformPoint(local.min)), b = root.InverseTransformPoint(rt.TransformPoint(local.max));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>
        /// 화면의 모든 TMP 글자를 훑어 문제 목록을 만든다 (Play 중에만).
        /// 종류: 칸밖(OVF) · 잘림(CLIP) · 화면밖(OFF) · 작음(SMALL &lt;22, <size=%> 글자 포함) · 자동축소 바닥(MIN) · 같은 역할 단추 글자 크기 다름(BTN-SIZE) · 겹침(OVL).
        /// 측정은 1080 기준 해상도 픽셀. 예: TextAudit() 또는 TextAudit("overlay") 로 겹침만 줄이기.
        /// </summary>
        public static string TextAudit(float minSize = 22f)
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            KeepRunning();
            var lines = new List<string>();
            var items = new List<(TMPro.TMP_Text t, Rect r, Canvas c)>();
            int total = 0;
            foreach (TMPro.TMP_Text t in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))
            {
                if (!t.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(t.text)) continue;
                Canvas canvas = t.canvas;
                if (canvas == null || !canvas.isActiveAndEnabled || canvas.renderMode == RenderMode.WorldSpace) continue;
                canvas = canvas.rootCanvas;
                if (t.color.a < 0.02f) continue;
                float cg = 1f;
                foreach (CanvasGroup g in t.GetComponentsInParent<CanvasGroup>()) cg *= g.alpha;
                if (cg < 0.02f) continue;
                t.ForceMeshUpdate();
                total++;
                var rt = t.rectTransform;
                string head = $"{Path(t.transform)} \"{Trim(t.text)}\"";
                Bounds tb = t.textBounds;
                Rect sr = ScreenBounds(rt, tb, canvas);
                if (tb.size.x < 0.01f && tb.size.y < 0.01f) continue;

                float refW = canvas.GetComponent<RectTransform>().rect.width;
                float scaleToRef = rt.lossyScale.x / Mathf.Max(0.0001f, canvas.transform.lossyScale.x);
                float eff = t.fontSize * scaleToRef * 1080f / Mathf.Max(1f, refW);
                if (eff < minSize) lines.Add($"SMALL {eff:0}px | {head}");
                else if (t.text.IndexOf("<size=", System.StringComparison.Ordinal) >= 0)
                {
                    // <size=70%> 같은 줄임 태그가 하한 밑으로 내려가는지 (글자 크기 × 가장 작은 %)
                    float lowest = 100f;
                    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(t.text, @"<size=(\d+(?:\.\d+)?)%>"))
                        lowest = Mathf.Min(lowest, float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
                    if (eff * lowest / 100f < minSize) lines.Add($"SMALL(태그 {lowest:0}%) {eff * lowest / 100f:0}px | {head}");
                }
                if (t.enableAutoSizing && t.fontSize <= t.fontSizeMin + 0.01f && t.preferredHeight > rt.rect.height + 1f)
                    lines.Add($"MIN {t.fontSize:0}px 바닥에서도 넘침 | {head}");

                Rect r = rt.rect;
                float ow = tb.size.x - r.width, oh = tb.size.y - r.height;
                if (ow > 2f || oh > 2f || t.isTextTruncated)
                    lines.Add($"OVF 글 {tb.size.x:0}x{tb.size.y:0} 칸 {r.width:0}x{r.height:0}{(t.isTextTruncated ? " 잘림" : "")} | {head}");

                // 화면 · 안전영역 밖
                Rect screen = ((RectTransform)canvas.transform).rect;
                bool inScroll = t.GetComponentInParent<UnityEngine.UI.ScrollRect>() != null; // 스크롤 목록은 가장자리에서 잘려 보이는 게 정상
                if (!inScroll && screen.Overlaps(sr) && (sr.xMin < screen.xMin - 1f || sr.xMax > screen.xMax + 1f || sr.yMin < screen.yMin - 1f || sr.yMax > screen.yMax + 1f))
                    lines.Add($"OFF 화면밖 {sr.xMin - screen.xMin:0},{sr.yMin - screen.yMin:0}-{sr.xMax - screen.xMin:0},{sr.yMax - screen.yMin:0} | {head}");

                // 마스크 밖으로 잘림 (스크롤 중 완전히 가려진 건 무시)
                bool hidden = false;
                foreach (UnityEngine.UI.RectMask2D m in t.GetComponentsInParent<UnityEngine.UI.RectMask2D>())
                {
                    Rect mr = ScreenBounds(m.rectTransform, new Bounds(m.rectTransform.rect.center, m.rectTransform.rect.size), canvas);
                    if (!mr.Overlaps(sr)) { hidden = true; break; }
                    if (!inScroll && (sr.xMin < mr.xMin - 2f || sr.xMax > mr.xMax + 2f || sr.yMin < mr.yMin - 2f || sr.yMax > mr.yMax + 2f))
                        lines.Add($"CLIP 마스크에 걸림 | {head}");
                    break;
                }
                if (!hidden && screen.Overlaps(sr)) items.Add((t, sr, canvas));
            }

            // 같은 부모 · 같은 높이의 단추(= 같은 역할)는 글자 크기가 같아야 한다 (UX.md). 목록(ButtonList) 줄은 글이 제각각이라 제외
            var sibling = new Dictionary<(Transform, int), List<(string name, float size)>>();
            foreach (UnityEngine.UI.Button b in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
            {
                if (!b.gameObject.activeInHierarchy || b.transform.parent == null) continue;
                if (b.GetComponentInParent<HistoryHeroes.UI.ButtonList>() != null && b.GetComponentInParent<HistoryHeroes.UI.ButtonTextGroup>() == null) continue;
                TMPro.TMP_Text label = b.GetComponentInChildren<TMPro.TMP_Text>(false);
                if (label == null || string.IsNullOrWhiteSpace(label.text) || label.color.a < 0.02f) continue;
                label.ForceMeshUpdate();
                float eff = label.fontSize; // 같은 부모의 단추는 같은 캔버스 · 같은 배율 (팝 애니메이션 중 배율 차이를 빼려고 원래 글자 크기로 비교)
                var key = (b.transform.parent, Mathf.RoundToInt(((RectTransform)b.transform).rect.height));
                if (!sibling.TryGetValue(key, out var group)) sibling[key] = group = new List<(string, float)>();
                group.Add((b.name, eff));
            }

            foreach (var kv in sibling)
            {
                if (kv.Value.Count < 2) continue;
                float lo = float.MaxValue, hi = 0f;
                foreach (var g in kv.Value) { lo = Mathf.Min(lo, g.size); hi = Mathf.Max(hi, g.size); }
                if (hi - lo > 0.6f)
                    lines.Add($"BTN-SIZE 같은 단추 글자 크기가 다름 {lo:0.#}~{hi:0.#}px | {kv.Key.Item1.name}: {string.Join(", ", kv.Value.ConvertAll(g => g.name + " " + g.size.ToString("0.#")))}");
            }

            for (int i = 0; i < items.Count; i++)
                for (int j = i + 1; j < items.Count; j++)
                {
                    var a = items[i]; var b = items[j];
                    if (a.t.transform.IsChildOf(b.t.transform) || b.t.transform.IsChildOf(a.t.transform)) continue;
                    if (a.c != b.c || PanelRoot(a.t.transform) != PanelRoot(b.t.transform)) continue; // 뒤에 깔린 다른 화면과는 비교하지 않는다
                    float ix = Mathf.Min(a.r.xMax, b.r.xMax) - Mathf.Max(a.r.xMin, b.r.xMin);
                    float iy = Mathf.Min(a.r.yMax, b.r.yMax) - Mathf.Max(a.r.yMin, b.r.yMin);
                    if (ix <= 0f || iy <= 0f) continue;
                    float small = Mathf.Min(a.r.width * a.r.height, b.r.width * b.r.height);
                    if (small > 1f && ix * iy / small > 0.25f)
                        lines.Add($"OVL {(ix * iy / small):P0} | {Path(a.t.transform)} \"{Trim(a.t.text)}\" <> {Path(b.t.transform)} \"{Trim(b.t.text)}\"");
                }

            return $"글자 {total}개 · 문제 {lines.Count}\n" + string.Join("\n", lines);
        }

        // 캔버스(또는 Safe) 바로 아래 가지: 같은 화면인지 가르는 기준
        private static Transform PanelRoot(Transform t)
        {
            while (t.parent != null && t.parent.GetComponent<Canvas>() == null && t.parent.name != "Safe") t = t.parent;
            return t;
        }

        private static string Trim(string s)
        {
            s = s.Replace("\n", " ");
            return s.Length > 20 ? s.Substring(0, 20) : s;
        }

        public static string State()
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            KeepRunning();
            var run = Object.FindAnyObjectByType<DungeonRun>();
            var exp = Object.FindAnyObjectByType<ExperienceSystem>();
            var skills = Object.FindAnyObjectByType<SkillManager>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (run == null) return "Play 모드가 아님";

            var owned = new List<string>();
            foreach (SkillInstance s in skills.Loadout.Actives) owned.Add($"{s.Data.id}{s.Level}");
            foreach (SkillInstance s in skills.Loadout.Passives) owned.Add($"{s.Data.id}{s.Level}");

            return $"방 {run.Room}/{run.RoomCount} Lv{exp.Level.Level} 처치{exp.Kills} 적{EnemyBase.Alive.Count} " +
                   $"HP{Mathf.CeilToInt(player.Health.Current)}/{Mathf.CeilToInt(player.Health.Max)} " +
                   $"멈춤={GamePause.IsPaused} 끝={run.IsFinished} 특수={run.CurrentSpecial} 갈림길={run.ForkOpen} [{string.Join(",", owned)}]";
        }

        // ───── 결제 (Unity IAP · 에디터는 가짜 상점, Play 중에만) ─────

        /// <summary>상점 상태 · 상품별 가격 · 지갑(유료/무료) · 장부 한 줄</summary>
        public static string IapStatus()
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            KeepRunning();
            Records.SaveData d = Records.SaveSystem.Data;
            var prices = new List<string>();
            foreach (Economy.IapCatalog.Item item in Economy.IapCatalog.All)
                prices.Add($"{item.Id}={Economy.IapService.Price(item.Id) ?? "-"}{(Economy.IapLedger.FirstBonusLeft(d, item) ? "*" : "")}");
            return $"{Economy.IapService.State} 구매중={Economy.IapService.BuyingId ?? "-"} 보석 유료{d.gemsPaid} 무료{d.gemsFree} 골드{d.gold} 소환권{d.ItemCount(Economy.Wallet.Ticket)} " +
                   $"장부{d.purchases.ledger.Count} 패키지받음={d.purchases.starterClaimed} | {string.Join(" ", prices)} (*=첫 구매 2배 남음)";
        }

        /// <summary>가짜 상점 결제 창(IMGUI)의 [Buy] 또는 [Cancel]을 누른다</summary>
        public static string IapFakeStore(bool buy)
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            KeepRunning();
            GameObject window = GameObject.Find("UIFakeStoreWindow");
            Component dialog = window != null ? window.GetComponent("UIFakeStoreWindow") : null;
            if (dialog == null) return "가짜 상점 창이 없음";
            var method = dialog.GetType().GetMethod(buy ? "OnOkClicked" : "OnCancelClicked",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (method == null) return "가짜 상점 창 단추를 못 찾음 (패키지 버전이 바뀜?)";
            method.Invoke(dialog, null);
            return (buy ? "구매" : "취소") + " 누름";
        }

        /// <summary>
        /// 같은 거래를 상점이 다시 보낸 것처럼 지급 → 저장을 한 번 더 (결제가 올 때와 같은 길). 장부가 막아야 한다.
        /// transactionId를 비우면 장부의 마지막 거래를 쓴다.
        /// </summary>
        public static string IapRedeliver(string productId = null, string transactionId = null)
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            Records.SaveData d = Records.SaveSystem.Data;
            if (string.IsNullOrEmpty(transactionId))
            {
                if (d.purchases.ledger.Count == 0) return "장부가 비어 있음";
                Records.PurchaseEntry last = d.purchases.ledger[d.purchases.ledger.Count - 1];
                transactionId = last.transactionId;
                productId ??= last.productId;
            }
            string before = $"유료{d.gemsPaid} 무료{d.gemsFree} 장부{d.purchases.ledger.Count}";
            Economy.IapLedger.Result result = Economy.IapService.GrantAndSave(productId, transactionId, out _, out Records.SaveSystem.CommitResult saved);
            d = Records.SaveSystem.Data;
            return $"{productId} 다시 받기 → {result} / {saved} | 전 {before} → 후 유료{d.gemsPaid} 무료{d.gemsFree} 장부{d.purchases.ledger.Count}";
        }

        /// <summary>디스크의 저장 파일을 다시 읽어 장부가 들어 있는지 본다</summary>
        public static string IapSaveCheck()
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            string json = System.IO.File.ReadAllText(Records.SaveSystem.Store.MainPath);
            var parsed = Records.SaveStore.Parse(json, out Records.SaveData disk, out string problem);
            if (disk == null) return $"못 읽음: {parsed} {problem}";
            var lines = new List<string>();
            foreach (Records.PurchaseEntry e in disk.purchases.ledger)
                lines.Add($"{e.productId}(유료{e.paidGems}+무료{e.freeGems}{(e.firstBonus ? " 첫구매" : "")}{(e.tickets > 0 ? $" 소환권{e.tickets}" : "")}{(e.gold > 0 ? $" 골드{e.gold}" : "")})");
            return $"{Records.SaveSystem.Store.MainPath} 검사={parsed} 구조={disk.schemaVersion} 장부{disk.purchases.ledger.Count}: {string.Join(", ", lines)} | 첫구매 [{string.Join(",", disk.purchases.firstBought)}] 패키지={disk.purchases.starterClaimed}";
        }

        // ───── 계정 · 클라우드 저장 (진짜 UGS 서버, Play 중에만) ─────

        private static string _accountCheck = "아직 안 함";

        /// <summary>계정 상태 한 줄: 상태 · 연결 이름 · UGS 플레이어 ID · 마지막 안내</summary>
        public static string AccountStatus()
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            KeepRunning();
            return $"{Account.AccountService.State} 연결={Account.AccountService.LinkedName ?? "-"} 플레이어={Account.AccountService.PlayerId ?? "-"} " +
                   $"시험={Account.AccountService.SandboxLinked} 안내={Account.AccountService.Message ?? "-"} | 클라우드 점검: {_accountCheck}";
        }

        /// <summary>에디터 게스트를 연결된 계정처럼 (올리기 · 받기 · 삭제 시험). 끄면 다시 게스트</summary>
        public static string AccountSandbox(bool on)
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            Account.AccountService.SetSandbox(on);
            return AccountStatus();
        }

        /// <summary>설정의 "지금 클라우드에 저장"과 같은 일 (결과는 AccountStatus의 안내)</summary>
        public static string AccountUpload()
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            _ = Account.AccountService.UploadNowAsync();
            return "올리는 중 → AccountStatus()";
        }

        /// <summary>
        /// 클라우드 왕복 점검: "save"를 받아 이 기기 본 저장 파일 글과 같은지, 해시 검사(Parse)가 Ok인지, "meta"가 읽히는지 본다.
        /// replace면 받은 저장으로 SaveSystem.Replace까지 (계정 전환 길과 같다, 백업이 남는다). 결과는 AccountStatus 끝에.
        /// </summary>
        public static string AccountCloudCheck(bool replace = false)
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            _accountCheck = "받는 중";
            CloudCheck(replace);
            return "받는 중 → AccountStatus()";
        }

        private static async void CloudCheck(bool replace)
        {
            try
            {
                var items = await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.LoadAsync(
                    new HashSet<string> { Account.AccountService.SaveKey, Account.AccountService.MetaKey });
                if (!items.TryGetValue(Account.AccountService.SaveKey, out var save))
                {
                    _accountCheck = "클라우드에 save 없음";
                    return;
                }

                string cloudJson = save.Value.GetAs<string>();
                string localJson = System.IO.File.ReadAllText(Records.SaveSystem.Store.MainPath);
                var parsed = Records.SaveStore.Parse(cloudJson, out Records.SaveData data, out string problem);
                string meta = items.TryGetValue(Account.AccountService.MetaKey, out var m) ? m.Value.GetAs<string>() : null;
                var summary = meta != null ? JsonUtility.FromJson<Account.AccountSummary>(meta) : null;
                string result = $"글 {cloudJson.Length:N0}자 파일과같음={cloudJson == localJson} 검사={parsed} {problem} meta={(summary != null ? $"{summary.Text(null)} 번호={summary.uploadId} 플레이어={summary.playerId} 사용자={summary.userId}" : "없음")}";
                if (replace && data != null) result += $" 바꾸기={Records.SaveSystem.Replace(data)} 구간={Records.SaveSystem.Data.unlockedChapter} 사용자ID={Records.SaveSystem.Data.userId}";
                _accountCheck = result;
            }
            catch (System.Exception e)
            {
                _accountCheck = "실패: " + e.Message;
            }
        }

        private static string _serverCheck = "아직 안 함";

        /// <summary>
        /// 서버 결제 검증 모듈(진짜 Cloud Code) 점검: GetPurchases (이 플레이어 장부 · 준비 여부) → 가짜 토큰으로 VerifyPurchase
        /// (비밀값이 없으면 not_configured, 있으면 구글이 모르는 토큰이라 invalid). 로그인 뒤 Play 중에만. 결과는 ServerCheckResult()
        /// </summary>
        public static string ServerCheck()
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            KeepRunning();
            if (Account.AccountService.PlayerId == null) return "로그인 전 (AccountStatus 확인)";
            _serverCheck = "묻는 중";
            AskPurchaseServer();
            return "묻는 중 → ServerCheckResult()";
        }

        public static string ServerCheckResult() => $"{_serverCheck} | 맞춤={Economy.IapService.ServerSynced} 판매가능={Economy.IapService.LedgerSettled}";

        private static async void AskPurchaseServer()
        {
            try
            {
                Economy.PurchaseServer.Ledger ledger = await Economy.PurchaseServer.GetPurchasesAsync();
                string result = ledger == null ? "GetPurchases 빈 답" : $"GetPurchases 준비={ledger.configured} 장부={ledger.purchases?.Count ?? -1}";
                Economy.PurchaseServer.Verdict v = await Economy.PurchaseServer.VerifyAsync("gems_t1", "qa-fake-token-" + System.Guid.NewGuid().ToString("N"));
                _serverCheck = result + $" | VerifyPurchase(가짜 토큰) {v?.status ?? "빈 답"} {v?.reason}";
            }
            catch (System.Exception e)
            {
                _serverCheck = "실패: " + e.Message;
            }
        }

        /// <summary>계정 삭제 (설정의 확인 뒤와 같은 일): 클라우드 · UGS 계정 · 이 기기 저장을 지우고 로비로</summary>
        public static string AccountDelete()
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            _ = Account.AccountService.DeleteAsync();
            return "지우는 중 → AccountStatus()";
        }
    }
}
