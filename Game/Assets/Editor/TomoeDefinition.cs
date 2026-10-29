using HistoryHeroes.Heroes;
using HistoryHeroes.Skills;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>군기문학의 인물을 바탕으로 한 게임 수치다. 실제 궁술을 복원하지 않는다.</summary>
    public static class TomoeDefinition
    {
        public static void ConfigureHero(HeroData hero)
        {
            hero.id = "tomoe";
            hero.displayName = "토모에 고젠";
            hero.heroClass = HeroClass.Archer;
            hero.attackKind = BasicAttackKind.Projectile;
            hero.maxHp = 950f;
            hero.attack = 100f;
            hero.attackSpeed = 1f;
            hero.moveSpeed = 5f;
            hero.range = 6f;
            hero.projectileSpeed = 16f;
            hero.critRate = 0.05f;
            hero.critDamage = 1.5f;
            hero.firstShotDelay = 0.2f;
            hero.firstShotBonus = 0f;
            hero.damageReduction = 0f;
            hero.lowHpAttackBonus = 0f;
            hero.cannonEvery = 0;
            hero.knockback = 0f;
            hero.chainBounces = 0;
            hero.crossShotEvery = 2;
            hero.crossShotHalfAngle = 8f;
            hero.crossShotCoefficient = 0.6f;
            hero.star3CrossShotCoefficient = 0.7f;
            hero.star5CrossShotCoefficient = 0.8f;
            hero.color = new Color(0.8f, 0.3f, 0.2f);
            hero.playStyle = "멈춰 두 번째 화살을 좌우로 나누는 궁수";
            hero.signatureName = "교차 사격";
            hero.signatureDescription = "연속 정지 기본 공격 두 번째의 주화살이 좌우 두 갈래 (각 60%). 이동하면 횟수 초기화";
            hero.unlockAfterBossChapter = 0;
            hero.gemsOnly = true;
            hero.unlockGems = 2000;
            hero.godShardUnlock = false;
        }

        public static void ConfigureVolley(ActiveSkillData skill)
        {
            ConfigureCommon(skill);
            skill.id = "tomoe_volley";
            skill.displayName = "겹시위";
            skill.description = "7초마다 부채꼴 화살 3발 (각 55%)\n레벨마다 피해 +10%p";
            skill.maxLevel = 5;
            skill.saveCostFirstLevel = skill.saveCostPerLevel = 3;
            skill.damageFirstLevel = 0.55f;
            skill.damagePerLevel = 0.1f;
            skill.countFirstLevel = 3;
        }

        public static void ConfigureCrosswind(ActiveSkillData skill)
        {
            ConfigureCommon(skill);
            skill.id = "tomoe_crosswind";
            skill.displayName = "교차하는 바람";
            skill.description = "7초마다 부채꼴 화살 5발을 두 번 (0.3초 간격, 각 60%)\n레벨마다 피해 +12%p";
            skill.maxLevel = 3;
            skill.saveCostFirstLevel = 12;
            skill.saveCostPerLevel = 4;
            skill.damageFirstLevel = 0.6f;
            skill.damagePerLevel = 0.12f;
            skill.countFirstLevel = 5;
        }

        public static void ConfigureRecipe(FusionRecipe recipe, ActiveSkillData volley, SkillData attackSpeed, ActiveSkillData crosswind)
        {
            recipe.id = "tomoe_crosswind";
            recipe.type = FusionType.Evolution;
            recipe.materialA = volley;
            recipe.requiredLevelA = 5;
            recipe.materialB = attackSpeed;
            recipe.requiredLevelB = 1;
            recipe.result = crosswind;
            recipe.heroOnlyId = "tomoe";
        }

        private static void ConfigureCommon(ActiveSkillData skill)
        {
            skill.heroOnlyId = "tomoe";
            skill.rarity = Rarity.Rare;
            skill.tags = SkillTag.Projectile;
            skill.role = SkillRole.Area;
            skill.recordable = true;
            skill.cooldown = 7f;
            skill.countPerLevel = 0;
            skill.radius = skill.radiusPerLevel = 0f;
            skill.duration = skill.durationPerLevel = 0f;
        }
    }
}
