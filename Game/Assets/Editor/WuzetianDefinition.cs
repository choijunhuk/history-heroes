using HistoryHeroes.Heroes;
using HistoryHeroes.Skills;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>통치의 역사와 구분한 게임의 인장 수치. 생성 에셋 없이 검사할 수 있다.</summary>
    public static class WuzetianDefinition
    {
        public static void ConfigureHero(HeroData hero)
        {
            hero.id = "wuzetian";
            hero.displayName = "무측천";
            hero.heroClass = HeroClass.Mage;
            hero.attackKind = BasicAttackKind.Projectile;
            hero.maxHp = 900f;
            hero.attack = 90f;
            hero.attackSpeed = 1f;
            hero.moveSpeed = 5f;
            hero.range = 5.5f;
            hero.projectileSpeed = 12f;
            hero.critRate = 0.05f;
            hero.critDamage = 1.5f;
            hero.firstShotDelay = 0.2f;
            hero.firstShotBonus = 0f;
            hero.damageReduction = 0f;
            hero.lowHpAttackBonus = 0f;
            hero.cannonEvery = 0;
            hero.knockback = 0f;
            hero.chainBounces = 0;
            hero.sealHits = 3;
            hero.sealRadius = 2.5f;
            hero.sealRootDuration = 0.6f;
            hero.sealBossSlow = 0.15f;
            hero.sealCooldown = 6f;
            hero.star3SealRootDuration = 0.8f;
            hero.star5SealRadius = 3f;
            hero.color = new Color(0.65f, 0.18f, 0.16f);
            hero.playStyle = "같은 적을 겨눠 주변 한 적의 발을 잠시 묶는 법사";
            hero.signatureName = "질서의 인장";
            hero.signatureDescription = "같은 적에 정지 기본 공격 3회 명중하면 주변 다른 일반 적 1명을 잠깐 묶음. 보스만 남으면 약한 둔화, 대상마다 6초 제한";
            hero.unlockAfterBossChapter = 0;
            hero.gemsOnly = true;
            hero.unlockGems = 2000;
            hero.godShardUnlock = false;
        }

        public static void ConfigureDecree(ActiveSkillData skill)
        {
            ConfigureCommon(skill);
            skill.id = "wuzetian_decree";
            skill.displayName = "기록의 칙령";
            skill.description = "8초마다 가까운 적 자리에 인장의 파동 (반경 1.5, 피해 100%)\n레벨마다 피해 +20%p";
            skill.maxLevel = 5;
            skill.saveCostFirstLevel = skill.saveCostPerLevel = 3;
            skill.damageFirstLevel = 1f;
            skill.damagePerLevel = 0.2f;
            skill.countFirstLevel = 1;
        }

        public static void ConfigureOrder(ActiveSkillData skill)
        {
            ConfigureCommon(skill);
            skill.id = "wuzetian_order";
            skill.displayName = "정연한 서책";
            skill.description = "8초마다 가까운 적 자리에 파동 3번 (0.25초 간격, 반경 1.5, 각 65%)\n레벨마다 피해 +15%p";
            skill.maxLevel = 3;
            skill.saveCostFirstLevel = 12;
            skill.saveCostPerLevel = 4;
            skill.damageFirstLevel = 0.65f;
            skill.damagePerLevel = 0.15f;
            skill.countFirstLevel = 3;
        }

        public static void ConfigureRecipe(FusionRecipe recipe, ActiveSkillData decree, SkillData defense, ActiveSkillData order)
        {
            recipe.id = "wuzetian_order";
            recipe.type = FusionType.Evolution;
            recipe.materialA = decree;
            recipe.requiredLevelA = 5;
            recipe.materialB = defense;
            recipe.requiredLevelB = 1;
            recipe.result = order;
            recipe.heroOnlyId = "wuzetian";
        }

        private static void ConfigureCommon(ActiveSkillData skill)
        {
            skill.heroOnlyId = "wuzetian";
            skill.rarity = Rarity.Rare;
            skill.tags = SkillTag.Lightning;
            skill.role = SkillRole.Area;
            skill.recordable = true;
            skill.cooldown = 8f;
            skill.countPerLevel = 0;
            skill.radius = 1.5f;
            skill.radiusPerLevel = skill.duration = skill.durationPerLevel = 0f;
        }
    }
}
