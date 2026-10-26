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

            if (EnemyBase.Alive.Count > 0)
            {
                Dev.DevCheats.KillAll();
                return "처치 | " + State();
            }

            var door = Object.FindAnyObjectByType<Door>();
            if (door != null && door.IsOpen)
            {
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
        public static string BossBench(string build = "balanced", float speed = 3f, string modes = "dps,play", string heroes = "khan,guan,napoleon,tesla")
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
            foreach (string mode in modes.Split(','))
            {
                foreach (string hero in heroes.Split(',')) runs.Add(new Dev.BossBench.Run { HeroId = hero.Trim(), Mode = mode.Trim() });
            }

            Dev.BossBench.ChargerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyBase>("Assets/Prefabs/Enemies/Enemy_Charger.prefab");
            Dev.BossBench.ArcherPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyBase>("Assets/Prefabs/Enemies/Enemy_Archer.prefab");
            Dev.BossBench.BomberPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyBase>("Assets/Prefabs/Enemies/Enemy_Bomber.prefab");
            return Dev.BossBench.Begin(runs, skills, speed, db) + $" | 빌드 {build} {cost}pt (클리어 기록 용량 27pt)";
        }

        public static string BossBenchStatus() => Dev.BossBench.Report();

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

        // ---------------------------------------------------------------- v0.4 판 밖 성장

        /// <summary>
        /// 판 밖 성장 스냅샷 예시: "none" 판 밖 0 / "week" 무과금 1주 (Stats 4장 ×1.2 근처: 영웅 Lv8, 1구간 보스 희귀 무기 Lv10, 일반 갑옷 Lv5)
        /// / "month" 1개월 (×1.4 근처) / "max" 전부 최대 (영웅 Lv30 ★5, 전설 3칸 Lv40, 수련 최대, 유물 20개 ★3)
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
                    break;
                case "month":
                    s.heroLevel = 15;
                    s.heroStars = 2;
                    Equip(0, "weapon_general_r", 20);
                    Equip(1, "armor_general_r", 20);
                    Equip(2, "acc_signet_r", 15);
                    s.training = new List<int> { 5, 5, 3, 2, 2, 0 };
                    break;
                case "max":
                    s.heroLevel = 30;
                    s.heroStars = 5;
                    Equip(0, "weapon_conqueror_l", 40);
                    Equip(1, "armor_timeguard_l", 40);
                    Equip(2, "acc_hourglass_l", 40);
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
                   $"멈춤={GamePause.IsPaused} 끝={run.IsFinished} [{string.Join(",", owned)}]";
        }
    }
}
