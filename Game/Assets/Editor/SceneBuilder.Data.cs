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
            public HeroData Khan, Guan, Napoleon, Tesla, YiSunsin, YiSeonggye, Davinci, Zhuge, Jeanne, Seondeok, Boudica, Thor, Cleopatra, Anubis, Susanoo, Athena, Tomoe, Wuzetian, Curie;
            public HeroRoster Roster;
            public ActiveSkillData KhanVolley, KhanStorm, GuanSpin, GuanFive, NapoCannon, NapoAusterlitz, TeslaCoil, TeslaTower, YiTurtle, YiHakIkJin, YsgPyeonjeon, YsgShingung, DavTank, DavSpiral, ZhugeGate, ZhugeSevenStar, JeanneWave, JeanneOrleans, SeondeokStarlight, SeondeokConstellation, BoudicaRally, BoudicaUprising, ThorHammer, ThorReturn, CleopatraPharos, CleopatraLibrary, SandStatue, SandGate, WindSlash, StormPath, OwlEye, GuardianGaze, TomoeVolley, TomoeCrosswind, WuzetianDecree, WuzetianOrder, CuriePrism, CurieSpectrum;
            // v0.3 스킬 전체 목록
            public ActiveSkillData IceShard, PoisonCloud, GuardShield, Boomerang, Whirlwind, Meteor;
            public ActiveSkillData FlameTrail, BlizzardShard, ToxicBloom, IronWall, ThunderGod, TwinBoomerang, Blizzard, HeavenBow;
            public EnemyData Charger, Archer, Bomber;
            // v0.6 (Content 4장): 방패병 · 분열체(+작은 것) · 소환사(+졸개)
            public EnemyData Shield, Splitter, SplitterSmall, Summoner, Minion;
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

            // v0.4 판 밖 성장
            public HistoryHeroes.Meta.MetaTable Meta;
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
            d.Khan.model3D = Art3DCharacters.HeroModel("khan");
            d.Khan.modelHeight = HeroModelHeight;
            d.Khan.firstShotDelay = 0f;
            d.Khan.firstShotBonus = 0.5f;
            d.Khan.projectileSpeed = 14f;
            d.Khan.color = new Color(0.95f, 0.75f, 0.2f);
            d.Khan.heroClass = HeroClass.Archer;
            d.Khan.attackKind = BasicAttackKind.Projectile;
            d.Khan.playStyle = "\"멈칫멈칫\" 짧게 서면서 계속 쏘는 리듬";
            d.Khan.signatureName = "기마 속사";
            d.Khan.signatureDescription = "멈추는 순간 공격 준비 시간 0. 첫 발 피해 +50%";
            d.Khan.unlockAfterBossChapter = 0;
            d.Khan.star3FirstShotBonus = 0.75f; // Stats 6-1
            d.Khan.star5FirstShotBonus = 1.0f;
            d.Khan.star5FirstShotPierce = true;

            // ----- 관우 (Heroes.md 4장): 근접 부채꼴 베기, 받는 피해 -15%, 체력 50% 아래 공격력 +20% -----
            d.Guan = Hero("guan", "관우", HeroClass.Warrior, BasicAttackKind.MeleeArc, 1400f, 130f, 0.8f, 5.2f, 2f, new Color(0.25f, 0.7f, 0.4f));
            d.Guan.playStyle = "다가가서 멈추는 위험한 플레이. 근접이라 긴장감이 가장 크다";
            d.Guan.signatureName = "의리의 투지";
            d.Guan.signatureDescription = "받는 피해 -15%. 체력이 50% 아래면 공격력 +20%";
            d.Guan.damageReduction = 0.15f;
            d.Guan.lowHpThreshold = 0.5f;
            d.Guan.lowHpAttackBonus = 0.2f;
            d.Guan.meleeArcDegrees = 120f;
            d.Guan.meleeWaveCoefficient = 0.4f;
            d.Guan.unlockAfterBossChapter = 1;
            d.Guan.star3DamageReduction = 0.20f;
            d.Guan.star5LowHpAttackBonus = 0.35f;

            // ----- 나폴레옹 (5장): 총알 연사·밀어냄, 3발마다 작은 포탄 -----
            d.Napoleon = Hero("napoleon", "나폴레옹", HeroClass.Gunner, BasicAttackKind.Projectile, 900f, 72f, 1.4f, 4.8f, 5f, new Color(0.3f, 0.45f, 0.95f));
            d.Napoleon.playStyle = "연사로 밀어내면서 몰린 적을 포탄으로 정리";
            d.Napoleon.signatureName = "포병의 눈";
            d.Napoleon.signatureDescription = "기본 공격 3발마다 포탄 (범위 피해 100%). 총알이 적을 살짝 밀어냄";
            d.Napoleon.projectileSpeed = 18f;
            d.Napoleon.knockback = 0.25f;
            d.Napoleon.cannonEvery = 3;
            d.Napoleon.cannonCoefficient = 1.0f; // v0.3 벤치: 보스에 너무 강하고 잡몹엔 보통이라 총알 공격력 90→72, 포탄 80%→100%·반지름 1.2→1.5
            d.Napoleon.cannonRadius = 1.5f;
            d.Napoleon.unlockAfterBossChapter = 2;
            d.Napoleon.star3CannonRadiusScale = 1.3f;
            d.Napoleon.star5CannonEvery = 2;

            // ----- 니콜라 테슬라 (6장): 전기 구체가 근처 적에게 2번 튕김 -----
            d.Tesla = Hero("tesla", "니콜라 테슬라", HeroClass.Mage, BasicAttackKind.Projectile, 850f, 95f, 0.9f, 5f, 5.5f, new Color(0.55f, 0.85f, 1f));
            d.Tesla.playStyle = "코일을 깔아두고 그 주변에서 싸우는 자리 잡기";
            d.Tesla.signatureName = "교류";
            d.Tesla.signatureDescription = "기본 공격이 맞으면 근처 적에게 2번 튕김 (튕길 때마다 -30%)";
            d.Tesla.projectileSpeed = 11f;
            d.Tesla.chainBounces = 2;
            d.Tesla.chainRadius = 3f;
            d.Tesla.chainFalloff = 0.3f;
            d.Tesla.unlockAfterBossChapter = 3;
            d.Tesla.star3ExtraBounces = 1;  // Stats 6-1은 "튕김 +2"(1 → 2 기준). 지금 테슬라는 처음부터 2번이라 ★3에 1번 더
            d.Tesla.star5NoFalloff = true;

            // ----- 이순신 (확장 결정 12: 새 영웅 첫째, 수호 포수 — 방어 반격형) -----
            d.YiSunsin = Hero("yisunsin", "이순신", HeroClass.Gunner, BasicAttackKind.Projectile, 1250f, 115f, 0.75f, 4.8f, 5.5f, new Color(0.35f, 0.45f, 0.85f));
            d.YiSunsin.playStyle = "멈춰 서서 맞으며 버티다, 쌓인 힘을 포탄 한 방에";
            d.YiSunsin.signatureName = "철갑";
            d.YiSunsin.signatureDescription = "멈추면 받는 피해 -25%. 막은 피해의 절반을 다음 포탄에 싣는다 (최대 공격력 200%, 움직이면 2초에 사라짐)";
            d.YiSunsin.projectileSpeed = 11f; // 느리고 무거운 총통 포탄
            d.YiSunsin.knockback = 0.2f;
            d.YiSunsin.stopGuard = 0.25f;
            d.YiSunsin.stopGuardStore = 0.5f;
            d.YiSunsin.stopGuardCap = 2f;
            d.YiSunsin.stopGuardDecay = 2f;
            d.YiSunsin.unlockAfterBossChapter = 4;
            d.YiSunsin.star3StopGuard = 0.3f;
            d.YiSunsin.star5StopGuardCap = 3f;

            // ----- 이성계 (확장 결정 12: 새 영웅, 명궁 — 모으기 저격형). 칭기즈 칸 = 짧게 자주, 이성계 = 길게 한 방 -----
            d.YiSeonggye = Hero("yiseonggye", "이성계", HeroClass.Archer, BasicAttackKind.Projectile, 950f, 100f, 1.0f, 5f, 7f, new Color(0.8f, 0.35f, 0.25f));
            d.YiSeonggye.playStyle = "오래 멈춰 활을 끝까지 당겨, 크게 한 방";
            d.YiSeonggye.signatureName = "백발백중";
            d.YiSeonggye.signatureDescription = "멈춘 시간만큼 화살을 모은다: 1단 → 2단 관통 → 3단 관통 + 치명타 확정 + 피해 ×1.6 (1.8초, 바로 발사). 움직이면 모은 화살을 놓는다";
            d.YiSeonggye.projectileSpeed = 17f;
            d.YiSeonggye.chargeShot = true;
            // 밸런스 (영웅 8명 보스 벤치 2026-10-28: 3단 1.5초 ×2.5 + 치명타 확정이 다른 영웅의 2.4배) → 1.8초 ×1.6
            d.YiSeonggye.chargeTimes = new Vector3(0.5f, 1.1f, 1.8f);
            d.YiSeonggye.chargeMidMultiplier = 1.2f;
            d.YiSeonggye.chargeFullMultiplier = 1.6f;
            d.YiSeonggye.unlockAfterBossChapter = 5;
            d.YiSeonggye.star3ChargeFull = 1.9f;
            d.YiSeonggye.star5ChargeFullTime = 1.45f;

            // ----- 레오나르도 다빈치 (확장 결정 12: 새 영웅, 소환사 · 발명가) -----
            d.Davinci = Hero("davinci", "레오나르도 다빈치", HeroClass.Summoner, BasicAttackKind.Projectile, 900f, 80f, 1.0f, 5f, 5f, new Color(0.85f, 0.65f, 0.35f));
            d.Davinci.playStyle = "멈춰서 비행 기계를 조립하고, 같이 쏘는 공방";
            d.Davinci.signatureName = "작업대";
            d.Davinci.signatureDescription = "멈춰서 부품 3개(1초에 1개)를 모으면 비행 기계 (최대 2대, 20초). 기계는 멈춰 있을 때 같은 적을 쏜다 (45%)";
            d.Davinci.projectileSpeed = 15f;
            d.Davinci.workbench = true;
            d.Davinci.partEvery = 1f;
            d.Davinci.partsPerMachine = 3;
            d.Davinci.maxMachines = 2;
            d.Davinci.machineLife = 20f;
            d.Davinci.machineDamage = 0.45f; // 밸런스 (보스 벤치 110초, 다른 영웅 73~97초): 35% → 45%, 공격력 70 → 80
            d.Davinci.machineFireRate = 1f;
            d.Davinci.helperModel = Art3DStage.Prefab("Fx_FlyingMachine");
            d.Davinci.gemsOnly = true; // 보석으로만 (구간 해금 영웅은 6명으로 충분)
            d.Davinci.unlockGems = 2000;
            d.Davinci.star3MachineDamage = 0.6f;
            d.Davinci.star5MaxMachines = 3;

            // ----- 제갈량 (확장 결정 12: 새 영웅, 책사 — 조종형 법사). 📜 팔진도·동남풍은 『삼국지연의』 전승 (각색) -----
            d.Zhuge = Hero("zhuge", "제갈량", HeroClass.Mage, BasicAttackKind.Projectile, 900f, 85f, 1.0f, 5f, 4.5f, new Color(0.55f, 0.75f, 0.95f));
            d.Zhuge.playStyle = "멈춰 서서 바람으로 적 탄을 늦추고, 진법으로 판을 짜는 조종";
            d.Zhuge.signatureName = "동남풍";
            d.Zhuge.signatureDescription = "멈추면 둘레 반경 3의 적 탄이 40% 느려진다. 기본 공격은 깃털 부채 바람 3갈래 (각 52%)";
            d.Zhuge.projectileSpeed = 12f;
            d.Zhuge.knockback = 0.15f;
            d.Zhuge.fanShots = 3;
            d.Zhuge.fanSpread = 18f;
            d.Zhuge.fanCoefficient = 0.52f; // 밸런스 (보스 벤치 109초): 45% → 52%
            d.Zhuge.windRadius = 3f;
            d.Zhuge.windSlow = 0.4f;
            d.Zhuge.gemsOnly = true;
            d.Zhuge.unlockGems = 2000;
            d.Zhuge.star3WindRadius = 4f;
            d.Zhuge.star5WindSlow = 0.55f;

            // ----- 잔 다르크 (Heroes 6-5): 멈춰 세운 깃발 안에서 검을 휘두르는 수호 전사 -----
            d.Jeanne = Hero("jeanne", "잔 다르크", HeroClass.Warrior, BasicAttackKind.MeleeArc, 1200f, 105f, 0.9f, 5f, 2.2f, new Color(0.65f, 0.78f, 1f));
            d.Jeanne.playStyle = "깃발 곁에서 검을 휘두르다가 피하고, 돌아와 다시 멈추는 수호 전사";
            d.Jeanne.signatureName = "성녀의 깃발";
            d.Jeanne.signatureDescription = "1초 멈추면 깃발을 6초 세운다. 깃발 안에 멈추면 기본 공격력 +20%, 받는 피해 -10%";
            d.Jeanne.meleeArcDegrees = 120f;
            d.Jeanne.meleeWaveCoefficient = 0.4f;
            d.Jeanne.bannerDeploySeconds = 1f;
            d.Jeanne.bannerRadius = 2.5f;
            d.Jeanne.bannerDuration = 6f;
            d.Jeanne.bannerAttackBonus = 0.2f;
            d.Jeanne.bannerDamageReduction = 0.1f;
            d.Jeanne.star3BannerDuration = 8f;
            d.Jeanne.star5BannerAttackBonus = 0.3f;
            d.Jeanne.gemsOnly = true;
            d.Jeanne.unlockGems = 2000;

            // 선덕여왕: 관측은 다음 기본 공격 한 번에만 쓰고, 이동하면 준비를 버린다.
            d.Seondeok = Hero("seondeok", "선덕여왕", HeroClass.Mage, BasicAttackKind.Projectile, 900f, 90f, 1f, 5f, 6f, new Color(0.68f, 0.4f, 0.9f));
            d.Seondeok.playStyle = "멈춰 별빛 한 발을 준비하고, 일직선으로 모인 적을 관통하는 법사";
            d.Seondeok.signatureName = "별자리 관측";
            d.Seondeok.signatureDescription = "1초 멈추면 다음 기본 공격 1회가 적 1명을 관통한다. 이동하면 준비 취소";
            d.Seondeok.observationSeconds = 1f;
            d.Seondeok.observationAttackBonus = 0f;
            d.Seondeok.star3ObservationAttackBonus = 0.15f;
            d.Seondeok.star5ObservationAttackBonus = 0.3f;
            d.Seondeok.gemsOnly = true;
            d.Seondeok.unlockGems = 2000;

            // 부디카: 멈춰 만든 빈틈은 다음 실제 기본 공격 한 번에만 쓰인다.
            d.Boudica = AssetUtil.LoadOrCreate<HeroData>($"{DataDir}/Heroes/Hero_boudica.asset");
            BoudicaDefinition.ConfigureHero(d.Boudica);
            d.Boudica.model3D = Art3DCharacters.HeroModel("boudica");
            d.Boudica.modelHeight = HeroModelHeight;

            d.Thor = AssetUtil.LoadOrCreate<HeroData>($"{DataDir}/Heroes/Hero_thor.asset");
            ThorDefinition.ConfigureHero(d.Thor);
            d.Thor.model3D = Art3DCharacters.HeroModel("thor");
            d.Thor.modelHeight = HeroModelHeight;

            d.Cleopatra = AssetUtil.LoadOrCreate<HeroData>($"{DataDir}/Heroes/Hero_cleopatra.asset");
            CleopatraDefinition.ConfigureHero(d.Cleopatra);
            d.Cleopatra.model3D = Art3DCharacters.HeroModel("cleopatra");
            d.Cleopatra.modelHeight = HeroModelHeight;
            d.Cleopatra.helperModel = Art3DStage.Prefab("Fx_StoneSentinel");

            d.Anubis = AssetUtil.LoadOrCreate<HeroData>($"{DataDir}/Heroes/Hero_anubis.asset");
            AnubisDefinition.ConfigureHero(d.Anubis);
            d.Anubis.model3D = Art3DCharacters.HeroModel("anubis");
            d.Anubis.modelHeight = HeroModelHeight;

            d.Susanoo = AssetUtil.LoadOrCreate<HeroData>($"{DataDir}/Heroes/Hero_susanoo.asset");
            SusanooDefinition.ConfigureHero(d.Susanoo);
            d.Susanoo.model3D = Art3DCharacters.HeroModel("susanoo");
            d.Susanoo.modelHeight = HeroModelHeight;

            d.Athena = AssetUtil.LoadOrCreate<HeroData>($"{DataDir}/Heroes/Hero_athena.asset");
            AthenaDefinition.ConfigureHero(d.Athena);
            d.Athena.model3D = Art3DCharacters.HeroModel("athena");
            d.Athena.modelHeight = HeroModelHeight;

            d.Tomoe = AssetUtil.LoadOrCreate<HeroData>($"{DataDir}/Heroes/Hero_tomoe.asset");
            TomoeDefinition.ConfigureHero(d.Tomoe);
            d.Tomoe.model3D = Art3DCharacters.HeroModel("tomoe");
            d.Tomoe.modelHeight = HeroModelHeight;

            d.Wuzetian = AssetUtil.LoadOrCreate<HeroData>($"{DataDir}/Heroes/Hero_wuzetian.asset");
            WuzetianDefinition.ConfigureHero(d.Wuzetian);
            d.Wuzetian.model3D = Art3DCharacters.HeroModel("wuzetian");
            d.Wuzetian.modelHeight = HeroModelHeight;

            d.Curie = AssetUtil.LoadOrCreate<HeroData>($"{DataDir}/Heroes/Hero_curie.asset");
            CurieDefinition.ConfigureHero(d.Curie);
            d.Curie.model3D = Art3DCharacters.HeroModel("curie");
            d.Curie.modelHeight = HeroModelHeight;

            // ----- 적 3종 (Content.md 4장, Stats.md 8장 기준값 체력 120 · 공격력 60) -----
            d.Charger = Enemy("charger", "돌진병", 2.6f, 0.8f, 1.2f, new Color(0.85f, 0.25f, 0.25f));
            d.Archer = Enemy("archer", "궁병", 2.2f, 0.6f, 1.0f, new Color(0.7f, 0.3f, 0.75f));
            d.Bomber = Enemy("bomber", "투척병", 2.0f, 1.0f, 2.0f, new Color(0.9f, 0.5f, 0.15f));

            // v0.6 적 (Content 4장). 예산 점수와 맞춰 체력을 조금씩 다르게
            d.Shield = Enemy("shield", "방패병", 1.3f, 0.6f, 1.6f, new Color(0.55f, 0.6f, 0.7f));
            d.Shield.maxHp = 160f;
            d.Splitter = Enemy("splitter", "분열체", 2.3f, 0.5f, 1.1f, new Color(0.35f, 0.75f, 0.45f));
            d.Splitter.maxHp = 130f;
            d.SplitterSmall = Enemy("splitter_small", "작은 분열체", 2.9f, 0.45f, 1.0f, new Color(0.45f, 0.85f, 0.55f));
            d.SplitterSmall.scale = 0.62f;
            d.SplitterSmall.damage = 40f;
            d.SplitterSmall.experience = 1;
            d.Summoner = Enemy("summoner", "소환사", 2.1f, 1.0f, 1.5f, new Color(0.6f, 0.35f, 0.85f));
            d.Summoner.maxHp = 110f;
            d.Summoner.experience = 5;
            d.Minion = Enemy("minion", "망각 졸개", 3.0f, 0.7f, 1.0f, new Color(0.45f, 0.4f, 0.55f));
            d.Minion.scale = 0.7f;
            d.Minion.damage = 40f;
            d.Minion.experience = 1;
            foreach (EnemyData e in new[] { d.Shield, d.Splitter, d.SplitterSmall, d.Summoner, d.Minion }) UnityEditor.EditorUtility.SetDirty(e);

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
            multishot.warriorDescription = "베기 1회 추가\n(회당 피해 -10%)";
            d.Pool.Add(multishot);

            // ----- v0.3 패시브 12개 (Skills.md 1장) -----
            PassiveSkillData moveUp = Passive("move_up", "이동속도", Rarity.Common, SkillTag.Stat, 5, 1,
                "이동속도 +8%\n(레벨마다 +5%)",
                Stat(StatType.MoveSpeed, ModifierKind.Percent, 0.08f, 0.05f, StatTarget.Both));
            d.Pool.Add(moveUp);
            PassiveSkillData regen = Passive("regen", "체력 재생", Rarity.Common, SkillTag.Survival, 5, 1,
                "초당 최대체력 0.5% 회복\n(레벨마다 +0.3%)",
                Stat(StatType.HpRegen, ModifierKind.Flat, 0.005f, 0.003f, StatTarget.Both));
            d.Pool.Add(regen);
            PassiveSkillData critRate = Passive("crit_rate", "치명타 확률", Rarity.Common, SkillTag.Stat, 5, 1,
                "치명타 확률 +6%\n(레벨마다 +4%)",
                Stat(StatType.CritRate, ModifierKind.Flat, 0.06f, 0.04f, StatTarget.Both));
            d.Pool.Add(critRate);
            d.Pool.Add(Passive("exp_up", "배움", Rarity.Common, SkillTag.Growth, 3, 1,
                "경험치 +15%\n(레벨마다 +10%)\n초반에 고르면 레벨업이 빨라져요",
                Stat(StatType.ExpGain, ModifierKind.Flat, 0.15f, 0.10f, StatTarget.Both)));

            PassiveSkillData pierce = Passive("pierce", "관통", Rarity.Rare, SkillTag.Projectile, 1, 2,
                "기본 공격이 적을 뚫고 지나감\n(뚫을 때마다 피해 -20%)");
            pierce.attack = new List<PassiveSkillData.AttackEntry> { new PassiveSkillData.AttackEntry { kind = AttackModifierKind.Pierce, firstLevel = 1, perLevel = 0 } };
            pierce.warriorDescription = "베기 각도 +40°\n아테나: 찌르기 길이 +40%";
            d.Pool.Add(pierce);
            PassiveSkillData ricochet = Passive("ricochet", "벽 튕김", Rarity.Rare, SkillTag.Projectile, 2, 2,
                "기본 공격이 벽에 2번 튕김\n(레벨마다 +1번)");
            ricochet.attack = new List<PassiveSkillData.AttackEntry> { new PassiveSkillData.AttackEntry { kind = AttackModifierKind.Ricochet, firstLevel = 2, perLevel = 1 } };
            ricochet.warriorDescription = "베기마다 검기 1발\n(공격력 40%, 벽에 튕김)";
            d.Pool.Add(ricochet);
            d.Pool.Add(Passive("crit_dmg", "치명타 피해", Rarity.Rare, SkillTag.Stat, 5, 2,
                "치명타 피해 +25%\n(레벨마다 +15%)",
                Stat(StatType.CritDamage, ModifierKind.Flat, 0.25f, 0.15f, StatTarget.Both)));
            PassiveSkillData cdr = Passive("cdr", "쿨다운 감소", Rarity.Rare, SkillTag.ActiveBoost, 5, 2,
                "액티브 쿨다운 -8%\n(레벨마다 -6%, 최대 40%)",
                Stat(StatType.CooldownReduction, ModifierKind.Flat, 0.08f, 0.06f, StatTarget.Active));
            d.Pool.Add(cdr);
            PassiveSkillData activeDmg = Passive("active_dmg", "기술 숙련", Rarity.Rare, SkillTag.ActiveBoost, 5, 2,
                "액티브 피해 +15%\n(레벨마다 +10%)",
                Stat(StatType.ActiveDamage, ModifierKind.Flat, 0.15f, 0.10f, StatTarget.Active));
            d.Pool.Add(activeDmg);
            d.Pool.Add(Passive("dodge", "회피", Rarity.Rare, SkillTag.Survival, 3, 2,
                "8% 확률로 피해를 피함\n(레벨마다 +5%, 최대 25%)",
                Stat(StatType.Dodge, ModifierKind.Flat, 0.08f, 0.05f, StatTarget.Both)));
            PassiveSkillData undying = Passive("undying", "불굴", Rarity.Legendary, SkillTag.Survival, 1, 6,
                "판에서 한 번,\n죽는 순간 체력 50%로 부활");
            undying.attack = new List<PassiveSkillData.AttackEntry> { new PassiveSkillData.AttackEntry { kind = AttackModifierKind.Undying, firstLevel = 1, perLevel = 0 } };
            d.Pool.Add(undying);
            PassiveSkillData momentum = Passive("momentum", "영웅의 기세", Rarity.Legendary, SkillTag.Stat, 1, 6,
                "멈춘 뒤 3초간\n기본 공격 속도 +50%\n기본 공격 피해 +30%");
            momentum.attack = new List<PassiveSkillData.AttackEntry> { new PassiveSkillData.AttackEntry { kind = AttackModifierKind.Momentum, firstLevel = 1, perLevel = 0 } };
            d.Pool.Add(momentum);

            // ----- 액티브 4개 (Skills.md 2장 🅥). 동작 프리팹은 LinkData에서 넣는다 -----
            d.SpinBlade = Active("spin_blade", "회전 칼날", Rarity.Common, SkillTag.Melee | SkillTag.Defense, 5, 2,
                "몸 주위를 도는 칼날 2개\n(닿으면 공격력 40%)\n레벨마다 칼날 +1",
                cooldown: 0f, damage: 0.4f, damagePerLevel: 0f, count: 2, countPerLevel: 1, radius: 1.5f);
            d.FlameField = Active("flame_field", "불꽃 장판", Rarity.Common, SkillTag.Fire, 5, 2,
                "4초마다 발밑에 불꽃 장판 3초\n(초당 40%)\n레벨마다 범위 +15%",
                cooldown: 4f, damage: 0.4f, damagePerLevel: 0f, count: 1, countPerLevel: 0, radius: 1.5f, radiusPerLevel: 0.15f, duration: 3f);
            d.Lightning = Active("lightning", "낙뢰", Rarity.Rare, SkillTag.Lightning, 5, 3,
                "3초마다 무작위 적에게 번개\n(150%)\n레벨마다 번개 +1",
                cooldown: 3f, damage: 1.5f, damagePerLevel: 0f, count: 1, countPerLevel: 1);
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
                "3초마다 번개 3개 (각 165%)\n떨어진 자리에 불길 2초 (초당 30%)\nLv2 +25%, Lv3 +50%",
                cooldown: 3f, damage: 1.65f, damagePerLevel: 0.4125f, count: 3, countPerLevel: 0, saveCostFirst: 15);

            d.Recipes.Add(Recipe("steel_whirl", FusionType.Evolution, d.SpinBlade, 5, d.DefUp, 1, d.SteelWhirl));
            d.Recipes.Add(Recipe("thunder_fire", FusionType.Merge, d.Lightning, 3, d.FlameField, 3, d.ThunderFire));

            // ----- v0.3 공용 액티브 6개 (Skills.md 2장) -----
            d.IceShard = Active("ice_shard", "얼음 파편", Rarity.Common, SkillTag.Ice | SkillTag.Projectile, 5, 2,
                "3초마다 가까운 적에게 파편 3개\n(각 50%, 둔화 30%)\n레벨마다 +1개",
                cooldown: 3f, damage: 0.5f, damagePerLevel: 0f, count: 3, countPerLevel: 1);
            d.PoisonCloud = Active("poison_cloud", "독 구름", Rarity.Common, SkillTag.Poison, 5, 2,
                "5초마다 적 자리에 독 구름 4초\n(초당 33%, 겹치면 중첩)\n레벨마다 지속 +1초",
                cooldown: 5f, damage: 0.33f, damagePerLevel: 0f, count: 1, countPerLevel: 0, radius: 1.4f, duration: 4f);
            d.PoisonCloud.durationPerLevel = 1f;
            d.GuardShield = Active("guard_shield", "수호 방패", Rarity.Common, SkillTag.Survival | SkillTag.Defense, 5, 2,
                "8초마다 최대체력 10% 보호막\n(레벨마다 +4%)",
                cooldown: 8f, damage: 0.10f, damagePerLevel: 0.04f, count: 1, countPerLevel: 0);
            d.Boomerang = Active("boomerang", "부메랑", Rarity.Rare, SkillTag.Projectile, 5, 3,
                "4초마다 왕복 부메랑 1개\n(80%, 관통)\n레벨마다 +1개",
                cooldown: 4f, damage: 0.8f, damagePerLevel: 0f, count: 1, countPerLevel: 1);
            d.Whirlwind = Active("whirlwind", "회오리", Rarity.Rare, SkillTag.Wind, 5, 3,
                "6초마다 적이 많은 곳에 회오리\n2초 동안 적을 끌어모음\n레벨마다 지속 +0.5초",
                cooldown: 6f, damage: 0f, damagePerLevel: 0f, count: 1, countPerLevel: 0, radius: 3f, duration: 2f);
            d.Whirlwind.durationPerLevel = 0.5f;
            // "Lv당 피해 +60%"는 Lv1 피해(400%)의 60% → 레벨마다 +240%p (화살비와 같은 해석)
            d.Meteor = Active("meteor", "유성 낙하", Rarity.Legendary, SkillTag.Fire, 5, 6,
                "10초마다 적이 많은 곳에 유성\n(400%, 범위 큼, 0.8초 예고)\n레벨마다 피해 +60%",
                cooldown: 10f, damage: 4f, damagePerLevel: 2.4f, count: 1, countPerLevel: 0, radius: 2f);
            foreach (ActiveSkillData s in new[] { d.IceShard, d.PoisonCloud, d.GuardShield, d.Boomerang, d.Whirlwind, d.Meteor }) d.Pool.Add(s);

            // ----- v0.3 공용 조합 8개 (Skills.md 4-1·4-2·4-4). 결과는 풀에 넣지 않는다 -----
            d.FlameTrail = Active("flame_trail", "불꽃 발자국", Rarity.Rare, SkillTag.Fire, 1, 10,
                "움직인 자리에 불길 2초\n(초당 35%)\n원래 불꽃 장판도 계속 깔림",
                cooldown: 4f, damage: 0.3f, damagePerLevel: 0f, count: 1, countPerLevel: 0, radius: 1.5f * 1.6f, duration: 3f, saveCostFirst: 10);
            d.BlizzardShard = Active("blizzard_shard", "서리 창", Rarity.Rare, SkillTag.Ice | SkillTag.Projectile, 1, 10,
                "3초마다 파편 7개 (각 60%, 둔화 40%)\n둔화 3번 → 1초 빙결\n빙결한 적에게 치명타 확정",
                cooldown: 3f, damage: 0.6f, damagePerLevel: 0f, count: 7, countPerLevel: 0, saveCostFirst: 10);
            d.ToxicBloom = Active("toxic_bloom", "맹독 개화", Rarity.Rare, SkillTag.Poison, 1, 10,
                "5초마다 독 구름 8초 (초당 30%)\n구름 안에서 죽은 적 자리에\n작은 구름이 번짐 (3번까지)",
                cooldown: 5f, damage: 0.3f, damagePerLevel: 0f, count: 1, countPerLevel: 0, radius: 1.4f, duration: 8f, saveCostFirst: 10);
            d.IronWall = Active("iron_wall", "철벽", Rarity.Rare, SkillTag.Survival | SkillTag.Defense, 1, 10,
                "8초마다 최대체력 26% 보호막\n깨질 때 주변 폭발 200%",
                cooldown: 8f, damage: 0.26f, damagePerLevel: 0f, count: 1, countPerLevel: 0, saveCostFirst: 10);
            d.ThunderGod = Active("thunder_god", "뇌신의 분노", Rarity.Rare, SkillTag.Lightning, 1, 12,
                "3초마다 번개 5개 (각 120%)\n번개마다 근처 3명에게 연쇄\n(연쇄마다 -20%)",
                cooldown: 3f, damage: 1.2f, damagePerLevel: 0f, count: 5, countPerLevel: 0, saveCostFirst: 12);
            d.TwinBoomerang = Active("twin_boomerang", "쌍 부메랑", Rarity.Rare, SkillTag.Projectile, 1, 12,
                "4초마다 부메랑 5개 (각 80%, 관통)\n돌아올 때 2개로 갈라짐 (각 50%)",
                cooldown: 4f, damage: 0.8f, damagePerLevel: 0f, count: 5, countPerLevel: 0, saveCostFirst: 12);
            d.Blizzard = Active("blizzard", "눈보라", Rarity.Rare, SkillTag.Ice | SkillTag.Wind, 3, 3,
                "6초마다 2.5초 동안 끌어모으며\n초당 60%, 끝날 때 1.5초 빙결\nLv2 +25%, Lv3 +50%",
                cooldown: 6f, damage: 0.6f, damagePerLevel: 0.15f, count: 1, countPerLevel: 0, radius: 3f, duration: 2.5f, saveCostFirst: 15);
            d.HeavenBow = Active("heaven_bow", "천궁", Rarity.Rare, SkillTag.Projectile, 3, 5,
                "6초마다 화면 전체 화살 (150%)\n되돌아오며 한 번 더 75%\nLv2 +25%, Lv3 +50%",
                cooldown: 6f, damage: 1.5f, damagePerLevel: 0.375f, count: 1, countPerLevel: 0, saveCostFirst: 22);
            foreach (ActiveSkillData s in new[] { d.FlameTrail, d.BlizzardShard, d.ToxicBloom, d.IronWall, d.ThunderGod, d.TwinBoomerang })
            {
                s.saveCostPerLevel = 0;
                UnityEditor.EditorUtility.SetDirty(s);
            }

            d.Recipes.Add(Recipe("flame_trail", FusionType.Evolution, d.FlameField, 5, moveUp, 1, d.FlameTrail));
            d.Recipes.Add(Recipe("blizzard_shard", FusionType.Evolution, d.IceShard, 5, critRate, 1, d.BlizzardShard));
            d.Recipes.Add(Recipe("toxic_bloom", FusionType.Evolution, d.PoisonCloud, 5, regen, 1, d.ToxicBloom));
            PassiveSkillData hpUp = (PassiveSkillData)d.Pool.Find(s => s.id == "hp_up");
            d.Recipes.Add(Recipe("iron_wall", FusionType.Evolution, d.GuardShield, 5, hpUp, 1, d.IronWall));
            d.Recipes.Add(Recipe("thunder_god", FusionType.Evolution, d.Lightning, 5, cdr, 1, d.ThunderGod));
            d.Recipes.Add(Recipe("twin_boomerang", FusionType.Evolution, d.Boomerang, 5, activeDmg, 1, d.TwinBoomerang));
            d.Recipes.Add(Recipe("blizzard", FusionType.Merge, d.Whirlwind, 3, d.IceShard, 3, d.Blizzard));
            d.Recipes.Add(Recipe("heaven_bow", FusionType.Merge, d.ArrowRain, 3, d.Boomerang, 3, d.HeavenBow));

            // ----- 영웅 전용 액티브 4개 (Skills.md 3장, 희귀 비용 레벨당 3) -----
            d.KhanVolley = Active("khan_volley", "만궁 연사", Rarity.Rare, SkillTag.Projectile, 5, 3,
                "5초마다 부채꼴 화살 5발\n(각 60%)\n레벨마다 +1발",
                cooldown: 5f, damage: 0.6f, damagePerLevel: 0f, count: 5, countPerLevel: 1);
            d.GuanSpin = Active("guan_spin", "청룡 회전 베기", Rarity.Rare, SkillTag.Melee, 5, 3,
                "6초마다 제자리 회전 베기\n(주변 180%)\n레벨마다 피해 +25%",
                cooldown: 6f, damage: 1.8f, damagePerLevel: 0.45f, count: 1, countPerLevel: 0, radius: 2.5f);
            d.NapoCannon = Active("napo_cannon", "대포 포격", Rarity.Rare, SkillTag.Fire, 5, 3,
                "7초마다 적이 많은 곳에 포탄 3발\n(각 120%)\n레벨마다 +1발",
                cooldown: 7f, damage: 1.2f, damagePerLevel: 0f, count: 3, countPerLevel: 1, radius: 1.2f);
            d.TeslaCoil = Active("tesla_coil", "테슬라 코일", Rarity.Rare, SkillTag.Lightning, 5, 3,
                "8초마다 코일 설치 5초\n(0.5초마다 근처 적 번개 50%)\n레벨마다 지속 +1초",
                cooldown: 8f, damage: 0.5f, damagePerLevel: 0f, count: 1, countPerLevel: 0, radius: 3f, duration: 5f);
            d.TeslaCoil.durationPerLevel = 1f;
            d.KhanVolley.heroOnlyId = "khan";
            d.GuanSpin.heroOnlyId = "guan";
            d.NapoCannon.heroOnlyId = "napoleon";
            d.TeslaCoil.heroOnlyId = "tesla";
            d.Pool.Add(d.KhanVolley);
            d.Pool.Add(d.GuanSpin);
            d.Pool.Add(d.NapoCannon);
            d.Pool.Add(d.TeslaCoil);
            d.YiTurtle = Active("yi_turtle", "거북선 돌격", Rarity.Rare, SkillTag.Fire, 5, 3,
                "8초마다 거북선이 가까운 적 쪽으로 돌진\n(지나간 적 150% + 밀어냄)\n레벨마다 피해 +30%",
                cooldown: 8f, damage: 1.5f, damagePerLevel: 0.45f, count: 1, countPerLevel: 0);
            d.YiTurtle.heroOnlyId = "yisunsin";
            d.Pool.Add(d.YiTurtle);
            d.YiSunsin.exclusiveActive = d.YiTurtle;
            d.YsgPyeonjeon = Active("ysg_pyeonjeon", "편전", Rarity.Rare, SkillTag.Projectile, 5, 3,
                "5초마다 아주 빠른 작은 화살 3발이\n체력이 가장 많은 적에게 (각 60%)\n레벨마다 +1발",
                cooldown: 5f, damage: 0.6f, damagePerLevel: 0f, count: 3, countPerLevel: 1);
            d.YsgPyeonjeon.heroOnlyId = "yiseonggye";
            d.Pool.Add(d.YsgPyeonjeon);
            d.YiSeonggye.exclusiveActive = d.YsgPyeonjeon;
            d.DavTank = Active("dav_tank", "장갑 전차", Rarity.Rare, SkillTag.Projectile, 5, 3,
                "9초마다 원뿔 전차가 6초 동안\n둘레를 돌며 사방 8갈래로 쏜다 (각 30%)\n레벨마다 지속 +1초",
                cooldown: 9f, damage: 0.3f, damagePerLevel: 0f, count: 8, countPerLevel: 0, duration: 6f);
            d.DavTank.durationPerLevel = 1f;
            d.DavTank.heroOnlyId = "davinci";
            d.Pool.Add(d.DavTank);
            d.Davinci.exclusiveActive = d.DavTank;
            d.ZhugeGate = Active("zhuge_gate", "팔진도", Rarity.Rare, SkillTag.Wind | SkillTag.Defense, 5, 3,
                "10초마다 돌기둥 8개로 진을 4초\n적 탄을 막고, 안에 든 적은 1초 헤맴\n레벨마다 지속 +0.5초",
                cooldown: 10f, damage: 0f, damagePerLevel: 0f, count: 8, countPerLevel: 0, radius: 2.6f, duration: 4f);
            d.ZhugeGate.durationPerLevel = 0.5f;
            d.ZhugeGate.heroOnlyId = "zhuge";
            d.Pool.Add(d.ZhugeGate);
            d.Zhuge.exclusiveActive = d.ZhugeGate;
            d.JeanneWave = Active("jeanne_wave", "깃발의 파동", Rarity.Rare, SkillTag.Melee | SkillTag.Defense, 5, 3,
                "8초마다 깃발 자리에서 빛의 파동\n(범위 피해 120%, 밀어냄)\n레벨마다 피해 +20%",
                cooldown: 8f, damage: 1.2f, damagePerLevel: 0.2f, count: 1, countPerLevel: 0, radius: 3f);
            d.JeanneWave.icon = Icon("sword");
            d.JeanneWave.heroOnlyId = "jeanne";
            d.Pool.Add(d.JeanneWave);
            d.Jeanne.exclusiveActive = d.JeanneWave;
            d.SeondeokStarlight = Active("seondeok_starlight", "첨성대 별빛", Rarity.Rare, SkillTag.Lightning, 5, 3,
                "8초마다 가까운 적 자리에 별빛\n(반경 2, 피해 140%)\n레벨마다 피해 +25%",
                cooldown: 8f, damage: 1.4f, damagePerLevel: 0.25f, count: 1, countPerLevel: 0, radius: 2f);
            d.SeondeokStarlight.icon = Icon("star");
            d.SeondeokStarlight.heroOnlyId = "seondeok";
            d.Pool.Add(d.SeondeokStarlight);
            d.Seondeok.exclusiveActive = d.SeondeokStarlight;
            d.BoudicaRally = AssetUtil.LoadOrCreate<ActiveSkillData>($"{DataDir}/Skills/Active_boudica_rally.asset");
            BoudicaDefinition.ConfigureRally(d.BoudicaRally);
            d.BoudicaRally.icon = Icon("sword");
            d.Pool.Add(d.BoudicaRally);
            d.Boudica.exclusiveActive = d.BoudicaRally;
            d.CleopatraPharos = AssetUtil.LoadOrCreate<ActiveSkillData>($"{DataDir}/Skills/Active_cleopatra_pharos.asset");
            CleopatraDefinition.ConfigurePharos(d.CleopatraPharos);
            d.CleopatraPharos.icon = Icon("star");
            d.Pool.Add(d.CleopatraPharos);
            d.Cleopatra.exclusiveActive = d.CleopatraPharos;
            d.ThorHammer = AssetUtil.LoadOrCreate<ActiveSkillData>($"{DataDir}/Skills/Active_thor_hammer.asset");
            ThorDefinition.ConfigureHammer(d.ThorHammer);
            d.ThorHammer.icon = Icon("lightning");
            d.Pool.Add(d.ThorHammer);
            d.Thor.exclusiveActive = d.ThorHammer;
            d.SandStatue = AssetUtil.LoadOrCreate<ActiveSkillData>($"{DataDir}/Skills/Active_sand_statue.asset");
            AnubisDefinition.ConfigureStatue(d.SandStatue);
            d.SandStatue.icon = Icon("shield");
            d.Pool.Add(d.SandStatue);
            d.Anubis.exclusiveActive = d.SandStatue;
            d.WindSlash = AssetUtil.LoadOrCreate<ActiveSkillData>($"{DataDir}/Skills/Active_wind_slash.asset");
            SusanooDefinition.ConfigureWindSlash(d.WindSlash);
            d.WindSlash.icon = Icon("sword");
            d.Pool.Add(d.WindSlash);
            d.Susanoo.exclusiveActive = d.WindSlash;
            d.OwlEye = AssetUtil.LoadOrCreate<ActiveSkillData>($"{DataDir}/Skills/Active_owl_eye.asset");
            AthenaDefinition.ConfigureOwlEye(d.OwlEye);
            d.OwlEye.icon = Icon("shield");
            d.Pool.Add(d.OwlEye);
            d.Athena.exclusiveActive = d.OwlEye;
            d.TomoeVolley = AssetUtil.LoadOrCreate<ActiveSkillData>($"{DataDir}/Skills/Active_tomoe_volley.asset");
            TomoeDefinition.ConfigureVolley(d.TomoeVolley);
            d.TomoeVolley.icon = Icon("khan_volley");
            d.Pool.Add(d.TomoeVolley);
            d.Tomoe.exclusiveActive = d.TomoeVolley;
            d.WuzetianDecree = AssetUtil.LoadOrCreate<ActiveSkillData>($"{DataDir}/Skills/Active_wuzetian_decree.asset");
            WuzetianDefinition.ConfigureDecree(d.WuzetianDecree);
            d.WuzetianDecree.icon = Icon("star");
            d.Pool.Add(d.WuzetianDecree);
            d.Wuzetian.exclusiveActive = d.WuzetianDecree;
            d.CuriePrism = AssetUtil.LoadOrCreate<ActiveSkillData>($"{DataDir}/Skills/Active_curie_prism.asset");
            CurieDefinition.ConfigurePrism(d.CuriePrism);
            d.CuriePrism.icon = Icon("star");
            d.Pool.Add(d.CuriePrism);
            d.Curie.exclusiveActive = d.CuriePrism;
            d.Khan.exclusiveActive = d.KhanVolley;
            d.Guan.exclusiveActive = d.GuanSpin;
            d.Napoleon.exclusiveActive = d.NapoCannon;
            d.Tesla.exclusiveActive = d.TeslaCoil;

            // ----- 영웅 전용 조합 4개 (Skills.md 4-3·4-4) -----
            d.KhanStorm = Active("khan_storm", "천호의 화살비", Rarity.Rare, SkillTag.Projectile, 1, 12,
                "부채꼴 화살 9발 (각 60%)\n2번 연속",
                cooldown: 5f, damage: 0.6f, damagePerLevel: 0f, count: 9, countPerLevel: 0, saveCostFirst: 12);
            d.KhanStorm.saveCostPerLevel = 0;
            d.GuanFive = Active("guan_five", "오관참장", Rarity.Rare, SkillTag.Melee | SkillTag.Defense, 1, 12,
                "회전 베기 5번 연속 (회당 120%)\n도는 동안 받는 피해 -50%",
                cooldown: 6f, damage: 1.2f, damagePerLevel: 0f, count: 1, countPerLevel: 0, radius: 2.5f, saveCostFirst: 12);
            d.GuanFive.saveCostPerLevel = 0;
            d.NapoAusterlitz = Active("napo_austerlitz", "아우스터리츠의 포화", Rarity.Rare, SkillTag.Fire, 3, 4,
                "7초마다 포탄 3발 (각 130%)\n떨어진 자리에 불꽃 장판 2초\nLv2 +25%, Lv3 +50%",
                cooldown: 7f, damage: 1.3f, damagePerLevel: 0.325f, count: 3, countPerLevel: 0, radius: 1.2f, saveCostFirst: 18);
            d.TeslaTower = Active("tesla_tower", "워든클리프 탑", Rarity.Rare, SkillTag.Lightning, 3, 4,
                "12초마다 탑 8초\n1초마다 화면 안 적 5명에게 번개 60%\nLv2 +25%, Lv3 +50%",
                cooldown: 12f, damage: 0.6f, damagePerLevel: 0.15f, count: 1, countPerLevel: 0, radius: 3f, duration: 8f, saveCostFirst: 18);
            d.YiHakIkJin = Active("yi_hakikjin", "학익진", Rarity.Rare, SkillTag.Fire, 1, 12,
                "거북선 3척이 학의 날개(V자)로 돌격\n(각 180%, 밀어냄)\n지나간 자리에 불길 2초",
                cooldown: 8f, damage: 1.8f, damagePerLevel: 0f, count: 3, countPerLevel: 0, saveCostFirst: 12);
            d.YiHakIkJin.saveCostPerLevel = 0;
            d.YiHakIkJin.heroOnlyId = "yisunsin";
            d.YsgShingung = Active("ysg_shingung", "신궁", Rarity.Rare, SkillTag.Projectile, 1, 12,
                "편전 7발 (각 60%)\n맞은 적에게 표식 6초:\n다음 3단 화살 피해 +100%",
                cooldown: 5f, damage: 0.6f, damagePerLevel: 0f, count: 7, countPerLevel: 0, saveCostFirst: 12);
            d.YsgShingung.saveCostPerLevel = 0;
            d.YsgShingung.heroOnlyId = "yiseonggye";
            d.DavSpiral = Active("dav_spiral", "나선 비행기", Rarity.Rare, SkillTag.Projectile | SkillTag.Wind, 3, 4,
                "전차가 떠올라 회오리를 끌고 8초\n사방 8갈래 (각 35%) + 둘레 적을 끌어 초당 50%\nLv2 +25%, Lv3 +50%",
                cooldown: 9f, damage: 0.35f, damagePerLevel: 0.09f, count: 8, countPerLevel: 0, duration: 8f, saveCostFirst: 18);
            d.DavSpiral.heroOnlyId = "davinci";
            d.ZhugeSevenStar = Active("zhuge_sevenstar", "칠성단의 바람", Rarity.Rare, SkillTag.Wind, 3, 4,
                "팔진도 안에 회오리 3개 (5초)\n적을 가운데로 모으고 초당 40%\nLv2 +25%, Lv3 +50%",
                cooldown: 10f, damage: 1f, damagePerLevel: 0.25f, count: 8, countPerLevel: 0, radius: 2.6f, duration: 5f, saveCostFirst: 18);
            d.ZhugeSevenStar.heroOnlyId = "zhuge";
            d.JeanneOrleans = Active("jeanne_orleans", "오를레앙의 빛", Rarity.Rare, SkillTag.Melee | SkillTag.Defense, 3, 4,
                "8초마다 깃발 자리에서 빛의 파동 3번\n(각 90%, 0.4초 간격, 밀어냄)\n레벨마다 피해 +15%",
                cooldown: 8f, damage: 0.9f, damagePerLevel: 0.15f, count: 3, countPerLevel: 0, radius: 3.5f, saveCostFirst: 12);
            d.JeanneOrleans.icon = Icon("shield");
            d.JeanneOrleans.heroOnlyId = "jeanne";
            d.SeondeokConstellation = Active("seondeok_constellation", "별자리의 길", Rarity.Rare, SkillTag.Lightning, 3, 4,
                "8초마다 적 자리를 중심으로 별빛 3개\n(일직선, 각 반경 1.4, 피해 90%)\n0.25초 간격, 레벨마다 피해 +22.5%",
                cooldown: 8f, damage: 0.9f, damagePerLevel: 0.225f, count: 3, countPerLevel: 0, radius: 1.4f, saveCostFirst: 12);
            d.SeondeokConstellation.icon = Icon("star");
            d.SeondeokConstellation.heroOnlyId = "seondeok";
            d.BoudicaUprising = AssetUtil.LoadOrCreate<ActiveSkillData>($"{DataDir}/Skills/Active_boudica_uprising.asset");
            BoudicaDefinition.ConfigureUprising(d.BoudicaUprising);
            d.BoudicaUprising.icon = Icon("sword");
            d.ThorReturn = AssetUtil.LoadOrCreate<ActiveSkillData>($"{DataDir}/Skills/Active_thor_return.asset");
            ThorDefinition.ConfigureReturn(d.ThorReturn);
            d.ThorReturn.icon = Icon("lightning");
            d.SandGate = AssetUtil.LoadOrCreate<ActiveSkillData>($"{DataDir}/Skills/Active_sand_gate.asset");
            AnubisDefinition.ConfigureGate(d.SandGate);
            d.SandGate.icon = Icon("shield");
            d.StormPath = AssetUtil.LoadOrCreate<ActiveSkillData>($"{DataDir}/Skills/Active_storm_path.asset");
            SusanooDefinition.ConfigureStormPath(d.StormPath);
            d.StormPath.icon = Icon("sword");
            d.GuardianGaze = AssetUtil.LoadOrCreate<ActiveSkillData>($"{DataDir}/Skills/Active_guardian_gaze.asset");
            AthenaDefinition.ConfigureGuardianGaze(d.GuardianGaze);
            d.GuardianGaze.icon = Icon("shield");
            d.CleopatraLibrary = AssetUtil.LoadOrCreate<ActiveSkillData>($"{DataDir}/Skills/Active_cleopatra_library.asset");
            CleopatraDefinition.ConfigureLibrary(d.CleopatraLibrary);
            d.CleopatraLibrary.icon = Icon("star");
            d.TomoeCrosswind = AssetUtil.LoadOrCreate<ActiveSkillData>($"{DataDir}/Skills/Active_tomoe_crosswind.asset");
            TomoeDefinition.ConfigureCrosswind(d.TomoeCrosswind);
            d.TomoeCrosswind.icon = Icon("khan_volley");
            d.WuzetianOrder = AssetUtil.LoadOrCreate<ActiveSkillData>($"{DataDir}/Skills/Active_wuzetian_order.asset");
            WuzetianDefinition.ConfigureOrder(d.WuzetianOrder);
            d.WuzetianOrder.icon = Icon("star");
            d.CurieSpectrum = AssetUtil.LoadOrCreate<ActiveSkillData>($"{DataDir}/Skills/Active_curie_spectrum.asset");
            CurieDefinition.ConfigureSpectrum(d.CurieSpectrum);
            d.CurieSpectrum.icon = Icon("star");
            foreach (ActiveSkillData s in new[] { d.KhanStorm, d.GuanFive, d.NapoAusterlitz, d.TeslaTower, d.YiHakIkJin, d.YsgShingung, d.DavSpiral, d.ZhugeSevenStar, d.JeanneOrleans, d.SeondeokConstellation, d.BoudicaUprising, d.ThorReturn, d.CleopatraLibrary, d.SandGate, d.StormPath, d.GuardianGaze, d.TomoeCrosswind, d.WuzetianOrder, d.CurieSpectrum }) UnityEditor.EditorUtility.SetDirty(s);
            d.KhanStorm.heroOnlyId = "khan";
            d.GuanFive.heroOnlyId = "guan";
            d.NapoAusterlitz.heroOnlyId = "napoleon";
            d.TeslaTower.heroOnlyId = "tesla";

            PassiveSkillData aspd = (PassiveSkillData)d.Pool.Find(s => s.id == "aspd_up");
            AddHeroRecipe(d, "khan_storm", FusionType.Evolution, d.KhanVolley, 5, aspd, 1, d.KhanStorm, "khan");
            AddHeroRecipe(d, "guan_five", FusionType.Evolution, d.GuanSpin, 5, d.DefUp, 1, d.GuanFive, "guan");
            AddHeroRecipe(d, "napo_austerlitz", FusionType.Merge, d.NapoCannon, 3, d.FlameField, 3, d.NapoAusterlitz, "napoleon");
            AddHeroRecipe(d, "tesla_tower", FusionType.Merge, d.TeslaCoil, 3, d.Lightning, 3, d.TeslaTower, "tesla");
            AddHeroRecipe(d, "yi_hakikjin", FusionType.Evolution, d.YiTurtle, 5, (PassiveSkillData)d.Pool.Find(s => s.id == "hp_up"), 1, d.YiHakIkJin, "yisunsin");
            AddHeroRecipe(d, "zhuge_sevenstar", FusionType.Merge, d.ZhugeGate, 3, d.Whirlwind, 3, d.ZhugeSevenStar, "zhuge");
            AddHeroRecipe(d, "dav_spiral", FusionType.Merge, d.DavTank, 3, d.Whirlwind, 3, d.DavSpiral, "davinci");
            AddHeroRecipe(d, "ysg_shingung", FusionType.Evolution, d.YsgPyeonjeon, 5, (PassiveSkillData)d.Pool.Find(s => s.id == "crit_dmg"), 1, d.YsgShingung, "yiseonggye");
            AddHeroRecipe(d, "jeanne_orleans", FusionType.Evolution, d.JeanneWave, 5, d.DefUp, 1, d.JeanneOrleans, "jeanne");
            AddHeroRecipe(d, "seondeok_constellation", FusionType.Evolution, d.SeondeokStarlight, 5, d.Pool.Find(s => s.id == "pierce"), 1, d.SeondeokConstellation, "seondeok");
            SkillData boudicaAttack = d.Pool.Find(s => s.id == "atk_up");
            FusionRecipe boudicaRecipe = AddHeroRecipe(d, "boudica_uprising", FusionType.Evolution, d.BoudicaRally, 5, boudicaAttack, 1, d.BoudicaUprising, "boudica");
            BoudicaDefinition.ConfigureRecipe(boudicaRecipe, d.BoudicaRally, boudicaAttack, d.BoudicaUprising);
            UnityEditor.EditorUtility.SetDirty(boudicaRecipe);
            FusionRecipe thorRecipe = AddHeroRecipe(d, "thor_return", FusionType.Evolution, d.ThorHammer, 5, aspd, 1, d.ThorReturn, "thor");
            ThorDefinition.ConfigureRecipe(thorRecipe, d.ThorHammer, aspd, d.ThorReturn);
            UnityEditor.EditorUtility.SetDirty(thorRecipe);
            SkillData health = d.Pool.Find(s => s.id == "hp_up");
            FusionRecipe sandRecipe = AddHeroRecipe(d, "sand_gate", FusionType.Evolution, d.SandStatue, 5, health, 1, d.SandGate, "anubis");
            AnubisDefinition.ConfigureRecipe(sandRecipe, d.SandStatue, health, d.SandGate);
            UnityEditor.EditorUtility.SetDirty(sandRecipe);
            FusionRecipe stormRecipe = AddHeroRecipe(d, "storm_path", FusionType.Evolution, d.WindSlash, 5, pierce, 1, d.StormPath, "susanoo");
            SusanooDefinition.ConfigureRecipe(stormRecipe, d.WindSlash, pierce, d.StormPath);
            UnityEditor.EditorUtility.SetDirty(stormRecipe);
            FusionRecipe gazeRecipe = AddHeroRecipe(d, "guardian_gaze", FusionType.Evolution, d.OwlEye, 5, d.DefUp, 1, d.GuardianGaze, "athena");
            AthenaDefinition.ConfigureRecipe(gazeRecipe, d.OwlEye, d.DefUp, d.GuardianGaze);
            UnityEditor.EditorUtility.SetDirty(gazeRecipe);
            SkillData activeDamage = d.Pool.Find(s => s.id == "active_dmg");
            FusionRecipe cleopatraRecipe = AddHeroRecipe(d, "cleopatra_library", FusionType.Evolution, d.CleopatraPharos, 5, activeDamage, 1, d.CleopatraLibrary, "cleopatra");
            CleopatraDefinition.ConfigureRecipe(cleopatraRecipe, d.CleopatraPharos, activeDamage, d.CleopatraLibrary);
            UnityEditor.EditorUtility.SetDirty(cleopatraRecipe);
            FusionRecipe tomoeRecipe = AddHeroRecipe(d, "tomoe_crosswind", FusionType.Evolution, d.TomoeVolley, 5, aspd, 1, d.TomoeCrosswind, "tomoe");
            TomoeDefinition.ConfigureRecipe(tomoeRecipe, d.TomoeVolley, aspd, d.TomoeCrosswind);
            UnityEditor.EditorUtility.SetDirty(tomoeRecipe);
            FusionRecipe wuzetianRecipe = AddHeroRecipe(d, "wuzetian_order", FusionType.Evolution, d.WuzetianDecree, 5, d.DefUp, 1, d.WuzetianOrder, "wuzetian");
            WuzetianDefinition.ConfigureRecipe(wuzetianRecipe, d.WuzetianDecree, d.DefUp, d.WuzetianOrder);
            UnityEditor.EditorUtility.SetDirty(wuzetianRecipe);
            FusionRecipe curieRecipe = AddHeroRecipe(d, "curie_spectrum", FusionType.Evolution, d.CuriePrism, 5, activeDamage, 1, d.CurieSpectrum, "curie");
            CurieDefinition.ConfigureRecipe(curieRecipe, d.CuriePrism, activeDamage, d.CurieSpectrum);
            UnityEditor.EditorUtility.SetDirty(curieRecipe);

            d.Roster = AssetUtil.LoadOrCreate<HeroRoster>($"{DataDir}/Heroes/HeroRoster.asset");
            d.Roster.heroes = new List<HeroData> { d.Khan, d.Guan, d.Napoleon, d.Tesla, d.YiSunsin, d.YiSeonggye, d.Davinci, d.Zhuge, d.Jeanne, d.Seondeok, d.Boudica, d.Thor, d.Cleopatra, d.Anubis, d.Susanoo, d.Athena, d.Tomoe, d.Wuzetian, d.Curie };

            d.Dungeon = AssetUtil.LoadOrCreate<DungeonData>($"{DataDir}/Dungeons/Dungeon_Chapter1_v01.asset");

            // ----- v0.2: 스킬 전체 목록 (기록의 스킬 ID → 데이터) -----
            d.Database = AssetUtil.LoadOrCreate<SkillDatabase>($"{DataDir}/Skills/SkillDatabase.asset");
            d.Database.balanceVersion = 1;
            d.Database.skills = new List<SkillData>(d.Pool)
            {
                d.SteelWhirl, d.ThunderFire, d.KhanStorm, d.GuanFive, d.NapoAusterlitz, d.TeslaTower, d.YiHakIkJin, d.YsgShingung, d.DavSpiral, d.ZhugeSevenStar, d.JeanneOrleans, d.SeondeokConstellation, d.BoudicaUprising, d.ThorReturn, d.CleopatraLibrary, d.SandGate, d.StormPath, d.GuardianGaze, d.TomoeCrosswind, d.WuzetianOrder, d.CurieSpectrum,
                d.FlameTrail, d.BlizzardShard, d.ToxicBloom, d.IronWall, d.ThunderGod, d.TwinBoomerang, d.Blizzard, d.HeavenBow,
            };
            d.Database.recipes = new List<FusionRecipe>(d.Recipes);
            foreach (SkillData s in d.Database.skills) AssignRole(s);
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
            d.RiftWarden.recommendedRole = SkillRole.Single; // 멈춤 타이밍 보스 (조사 A8)
            UnityEditor.EditorUtility.SetDirty(d.RiftWarden);
            d.RiftWarden.hpMultiplier = 300f; // 120 × 300 = 36,000. 대표 기록으로 제자리에서 약 1분 30초 → 피하면서 2~3분
            d.RiftWarden.attackMultiplier = 1.5f; // Stats.md 8장 (패턴 배수는 보스 쪽)
            d.RiftWarden.star2MaxHits = 8;
            CreateBossData(); // v0.6 중간 보스 5 · 보스맵 보스 4 (SceneBuilder.Bosses)
            d.Meta = CreateMetaData();
            return d;
        }

        // ----- 스킬 역할 (조사 A8, Skills.md 표 "역할" 열). 여기 없는 스킬은 "없음" -----
        private static readonly Dictionary<string, SkillRole> Roles = new Dictionary<string, SkillRole>
        {
            // 단일: 기본 공격·보스 화력
            ["aspd_up"] = SkillRole.Single, ["multishot"] = SkillRole.Single, ["lifesteal"] = SkillRole.Single,
            ["pierce"] = SkillRole.Single, ["ricochet"] = SkillRole.Single, ["crit_rate"] = SkillRole.Single,
            ["crit_dmg"] = SkillRole.Single, ["momentum"] = SkillRole.Single,
            // 광역: 잡몹 떼
            ["lightning"] = SkillRole.Area, ["flame_field"] = SkillRole.Area, ["arrow_rain"] = SkillRole.Area,
            ["meteor"] = SkillRole.Area, ["poison_cloud"] = SkillRole.Area, ["whirlwind"] = SkillRole.Area,
            ["ice_shard"] = SkillRole.Area, ["boomerang"] = SkillRole.Area,
            ["khan_volley"] = SkillRole.Area, ["guan_spin"] = SkillRole.Area, ["napo_cannon"] = SkillRole.Area, ["tesla_coil"] = SkillRole.Area,
            ["thunder_fire"] = SkillRole.Area, ["flame_trail"] = SkillRole.Area, ["blizzard_shard"] = SkillRole.Area,
            ["toxic_bloom"] = SkillRole.Area, ["thunder_god"] = SkillRole.Area, ["twin_boomerang"] = SkillRole.Area,
            ["blizzard"] = SkillRole.Area, ["heaven_bow"] = SkillRole.Area,
            ["khan_storm"] = SkillRole.Area, ["guan_five"] = SkillRole.Area, ["napo_austerlitz"] = SkillRole.Area, ["tesla_tower"] = SkillRole.Area,
            // 새 영웅 전용 (전체 검토 12번: 역할 표에서 빠져 기록 추천에 안 들어가던 것)
            ["yi_turtle"] = SkillRole.Area, ["yi_hakikjin"] = SkillRole.Area, ["dav_tank"] = SkillRole.Area, ["dav_spiral"] = SkillRole.Area,
            ["zhuge_sevenstar"] = SkillRole.Area,
            ["jeanne_wave"] = SkillRole.Area, ["jeanne_orleans"] = SkillRole.Area,
            ["seondeok_starlight"] = SkillRole.Area, ["seondeok_constellation"] = SkillRole.Area,
            ["boudica_rally"] = SkillRole.Area, ["boudica_uprising"] = SkillRole.Area,
            ["thor_hammer"] = SkillRole.Single, ["thor_return"] = SkillRole.Single,
            ["sand_statue"] = SkillRole.Defense, ["sand_gate"] = SkillRole.Defense,
            ["wind_slash"] = SkillRole.Single, ["storm_path"] = SkillRole.Single,
            ["owl_eye"] = SkillRole.Single, ["guardian_gaze"] = SkillRole.Single,
            ["cleopatra_pharos"] = SkillRole.Area, ["cleopatra_library"] = SkillRole.Area,
            ["tomoe_volley"] = SkillRole.Area, ["tomoe_crosswind"] = SkillRole.Area,
            ["wuzetian_decree"] = SkillRole.Area, ["wuzetian_order"] = SkillRole.Area,
            ["curie_prism"] = SkillRole.Area, ["curie_spectrum"] = SkillRole.Area,
            ["ysg_pyeonjeon"] = SkillRole.Single, ["ysg_shingung"] = SkillRole.Single,
            ["zhuge_gate"] = SkillRole.Defense,
            // 방어: 버티기·막기
            ["spin_blade"] = SkillRole.Defense, ["steel_whirl"] = SkillRole.Defense, ["def_up"] = SkillRole.Defense,
            ["hp_up"] = SkillRole.Defense, ["guard_shield"] = SkillRole.Defense, ["iron_wall"] = SkillRole.Defense,
            ["dodge"] = SkillRole.Defense, ["regen"] = SkillRole.Defense, ["undying"] = SkillRole.Defense,
            // 없음: atk_up, move_up, exp_up, cdr, active_dmg
        };

        private static void AssignRole(SkillData skill)
        {
            if (skill == null) return;
            skill.role = Roles.TryGetValue(skill.id, out SkillRole role) ? role : SkillRole.None;
            UnityEditor.EditorUtility.SetDirty(skill);
        }

        /// <summary>영웅 3D 모델 키 (비스듬한 카메라에서 무리 속에서도 보이게 — Codex 화면 점검 1번)</summary>
        private const float HeroModelHeight = 2.1f; // 새 영웅 화면 점검 1번: 1.75 → 2.1 (+20%, 적 무리 속에서 먼저 보이게. 판정은 그대로)

        private static HeroData Hero(string id, string name, HeroClass heroClass, BasicAttackKind attack, float hp, float atk, float aspd,
            float move, float range, Color color)
        {
            var h = AssetUtil.LoadOrCreate<HeroData>($"{DataDir}/Heroes/Hero_{id}.asset");
            h.id = id;
            h.displayName = name;
            h.heroClass = heroClass;
            h.attackKind = attack;
            h.maxHp = hp;
            h.attack = atk;
            h.attackSpeed = aspd;
            h.moveSpeed = move;
            h.range = range;
            h.critRate = 0.05f;
            h.critDamage = 1.5f;
            h.model3D = Art3DCharacters.HeroModel(id); // 3D 모델 (없으면 임시 도형)
            h.modelHeight = HeroModelHeight;
            h.firstShotDelay = 0.2f;
            h.firstShotBonus = 0f;
            h.damageReduction = 0f;
            h.lowHpAttackBonus = 0f;
            h.cannonEvery = 0;
            h.knockback = 0f;
            h.chainBounces = 0;
            h.projectileSpeed = 14f;
            h.unlockGems = 1500;
            h.color = color;
            return h;
        }

        private static FusionRecipe AddHeroRecipe(Data d, string id, FusionType type, ActiveSkillData a, int levelA, SkillData b, int levelB,
            ActiveSkillData result, string heroId)
        {
            FusionRecipe r = Recipe(id, type, a, levelA, b, levelB, result);
            r.heroOnlyId = heroId;
            UnityEditor.EditorUtility.SetDirty(r);
            d.Recipes.Add(r);
            return r;
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
            e.model3D = Art3DCharacters.EnemyModel(id, out float height); // 3D 모델 (없으면 임시 도형)
            e.modelHeight = height;
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
            s.icon = Icon(id); // 그래픽 평가 3번: 카드·HUD 아이콘 (Assets/Art/UI/Icons)
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
            LinkBosses(p);
            d.Guan.projectilePrefab = p.SwordWave;
            d.Napoleon.projectilePrefab = p.Bullet;
            d.Tesla.projectilePrefab = p.Orb;
            d.YiSunsin.projectilePrefab = p.Cannonball;
            d.YiTurtle.behaviourPrefab = p.YiTurtle;
            d.YiHakIkJin.behaviourPrefab = p.YiHakIkJin;
            d.YiSeonggye.projectilePrefab = p.BigArrow;
            d.YsgPyeonjeon.behaviourPrefab = p.YsgPyeonjeon;
            d.YsgShingung.behaviourPrefab = p.YsgShingung;
            d.Davinci.projectilePrefab = p.Bolt;
            d.DavTank.behaviourPrefab = p.DavTank;
            d.DavSpiral.behaviourPrefab = p.DavSpiral;
            d.Zhuge.projectilePrefab = p.Gust;
            d.ZhugeGate.behaviourPrefab = p.ZhugeGate;
            d.ZhugeSevenStar.behaviourPrefab = p.ZhugeSevenStar;
            d.Jeanne.projectilePrefab = p.SwordWave;
            d.JeanneWave.behaviourPrefab = p.JeanneWave;
            d.JeanneOrleans.behaviourPrefab = p.JeanneOrleans;
            d.Seondeok.projectilePrefab = p.Starlight;
            d.SeondeokStarlight.behaviourPrefab = p.SeondeokStarlight;
            d.SeondeokConstellation.behaviourPrefab = p.SeondeokConstellation;
            d.Boudica.projectilePrefab = p.SwordWave;
            d.BoudicaRally.behaviourPrefab = p.BoudicaRally;
            d.BoudicaUprising.behaviourPrefab = p.BoudicaUprising;
            d.Thor.projectilePrefab = p.SwordWave;
            d.ThorHammer.behaviourPrefab = p.ThorHammer;
            d.ThorReturn.behaviourPrefab = p.ThorReturn;
            d.Anubis.projectilePrefab = p.SandOrb;
            d.SandStatue.behaviourPrefab = p.SandStatue;
            d.SandGate.behaviourPrefab = p.SandGate;
            d.Susanoo.projectilePrefab = p.SwordWave;
            d.WindSlash.behaviourPrefab = p.WindSlash;
            d.StormPath.behaviourPrefab = p.StormPath;
            d.Athena.projectilePrefab = p.SwordWave;
            d.OwlEye.behaviourPrefab = p.OwlEye;
            d.GuardianGaze.behaviourPrefab = p.GuardianGaze;
            d.Cleopatra.projectilePrefab = p.Starlight;
            d.CleopatraPharos.behaviourPrefab = p.CleopatraPharos;
            d.CleopatraLibrary.behaviourPrefab = p.CleopatraLibrary;
            d.Tomoe.projectilePrefab = p.PlayerArrow;
            d.TomoeVolley.behaviourPrefab = p.TomoeVolley;
            d.TomoeCrosswind.behaviourPrefab = p.TomoeCrosswind;
            d.Wuzetian.projectilePrefab = p.Orb;
            d.WuzetianDecree.behaviourPrefab = p.WuzetianDecree;
            d.WuzetianOrder.behaviourPrefab = p.WuzetianOrder;
            d.Curie.projectilePrefab = p.Starlight;
            d.CuriePrism.behaviourPrefab = p.CuriePrism;
            d.CurieSpectrum.behaviourPrefab = p.CurieSpectrum;
            d.KhanVolley.behaviourPrefab = p.KhanVolley;
            d.KhanStorm.behaviourPrefab = p.KhanStorm;
            d.GuanSpin.behaviourPrefab = p.GuanSpin;
            d.GuanFive.behaviourPrefab = p.GuanFive;
            d.NapoCannon.behaviourPrefab = p.NapoCannon;
            d.NapoAusterlitz.behaviourPrefab = p.NapoAusterlitz;
            d.TeslaCoil.behaviourPrefab = p.TeslaCoil;
            d.TeslaTower.behaviourPrefab = p.TeslaTower;
            d.IceShard.behaviourPrefab = p.IceShard;
            d.PoisonCloud.behaviourPrefab = p.PoisonCloud;
            d.GuardShield.behaviourPrefab = p.GuardShield;
            d.Boomerang.behaviourPrefab = p.Boomerang;
            d.Whirlwind.behaviourPrefab = p.Whirlwind;
            d.Meteor.behaviourPrefab = p.Meteor;
            d.FlameTrail.behaviourPrefab = p.FlameTrail;
            d.BlizzardShard.behaviourPrefab = p.BlizzardShard;
            d.ToxicBloom.behaviourPrefab = p.ToxicBloom;
            d.IronWall.behaviourPrefab = p.IronWall;
            d.ThunderGod.behaviourPrefab = p.ThunderGod;
            d.TwinBoomerang.behaviourPrefab = p.TwinBoomerang;
            d.Blizzard.behaviourPrefab = p.Blizzard;
            d.HeavenBow.behaviourPrefab = p.HeavenBow;
            foreach (SkillData s in d.Database.skills) UnityEditor.EditorUtility.SetDirty(s);
            foreach (HeroData h in d.Roster.heroes) UnityEditor.EditorUtility.SetDirty(h);
            UnityEditor.EditorUtility.SetDirty(d.Roster);
            UnityEditor.EditorUtility.SetDirty(d.RiftWarden);
            UnityEditor.EditorUtility.SetDirty(d.Database);

            d.SpinBlade.behaviourPrefab = p.SpinBlade;
            d.FlameField.behaviourPrefab = p.FlameField;
            d.Lightning.behaviourPrefab = p.Lightning;
            d.ArrowRain.behaviourPrefab = p.ArrowRain;
            d.SteelWhirl.behaviourPrefab = p.SteelWhirl;
            d.ThunderFire.behaviourPrefab = p.ThunderFire;

            // v0.6 던전: 방 15개 · 구간 5개 · 레이아웃 (SceneBuilder.Dungeon)
            BuildDungeon(d, p);
            DungeonData dungeon = d.Dungeon;

            foreach (SkillData s in d.Pool) UnityEditor.EditorUtility.SetDirty(s);
            UnityEditor.EditorUtility.SetDirty(d.SteelWhirl);
            UnityEditor.EditorUtility.SetDirty(d.ThunderFire);
            UnityEditor.EditorUtility.SetDirty(dungeon);
        }

    }
}
