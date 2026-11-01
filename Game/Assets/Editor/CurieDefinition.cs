using HistoryHeroes.Heroes;
using HistoryHeroes.Skills;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>관측과 빛 기술은 게임 창작이다. 방사선의 성질이나 안전성을 나타내지 않는다.</summary>
    public static class CurieDefinition
    {
        public static void ConfigureHero(HeroData hero)
        {
            hero.id = "curie";
            hero.displayName = "마리 퀴리";
            hero.heroClass = HeroClass.Mage;
            hero.attackKind = BasicAttackKind.Projectile;
            hero.maxHp = 900f;
            hero.attack = 90f;
            hero.attackSpeed = 1f;
            hero.moveSpeed = 5f;
            hero.range = 5.5f;
            hero.projectileSpeed = 14f;
            hero.critRate = 0.05f;
            hero.critDamage = 1.5f;
            hero.firstShotDelay = 0.2f;
            hero.firstShotBonus = 0f;
            hero.damageReduction = 0f;
            hero.lowHpAttackBonus = 0f;
            hero.cannonEvery = 0;
            hero.knockback = 0f;
            hero.chainBounces = 0;
            hero.observationMark = true;
            hero.observationWaveRadius = 1.2f;
            hero.observationWaveCoefficient = 0.4f;
            hero.star3ObservationWaveCoefficient = 0.55f;
            hero.star5ObservationWaveRadius = 1.6f;
            hero.color = new Color(0.25f, 0.35f, 0.58f);
            hero.playStyle = "한 적을 관측하고 다음 기본 공격에 작은 파동을 더하는 법사";
            hero.signatureName = "관측 표식";
            hero.signatureDescription = "기본 공격 명중으로 적 1명을 표시. 다음 기본 공격 때 그 적 주변에 작은 빛 파동 (40%)";
            hero.unlockAfterBossChapter = 0;
            hero.gemsOnly = true;
            hero.unlockGems = 2000;
            hero.godShardUnlock = false;
        }

        public static void ConfigurePrism(ActiveSkillData skill)
        {
            ConfigureCommon(skill);
            skill.id = "curie_prism";
            skill.displayName = "분광의 빛";
            skill.description = "8초마다 가까운 적 자리에 빛 파동 (반경 1.6, 피해 110%)\n레벨마다 피해 +20%p";
            skill.maxLevel = 5;
            skill.saveCostFirstLevel = skill.saveCostPerLevel = 3;
            skill.damageFirstLevel = 1.1f;
            skill.damagePerLevel = 0.2f;
            skill.countFirstLevel = 1;
        }

        public static void ConfigureSpectrum(ActiveSkillData skill)
        {
            ConfigureCommon(skill);
            skill.id = "curie_spectrum";
            skill.displayName = "관측의 스펙트럼";
            skill.description = "8초마다 적 방향으로 빛 파동 3개 (간격 1, 반경 1.6, 각 70%)\n레벨마다 피해 +15%p";
            skill.maxLevel = 3;
            skill.saveCostFirstLevel = 12;
            skill.saveCostPerLevel = 4;
            skill.damageFirstLevel = 0.7f;
            skill.damagePerLevel = 0.15f;
            skill.countFirstLevel = 3;
        }

        public static void ConfigureRecipe(FusionRecipe recipe, ActiveSkillData prism, SkillData activeDamage, ActiveSkillData spectrum)
        {
            recipe.id = "curie_spectrum";
            recipe.type = FusionType.Evolution;
            recipe.materialA = prism;
            recipe.requiredLevelA = 5;
            recipe.materialB = activeDamage;
            recipe.requiredLevelB = 1;
            recipe.result = spectrum;
            recipe.heroOnlyId = "curie";
        }

        private static void ConfigureCommon(ActiveSkillData skill)
        {
            skill.heroOnlyId = "curie";
            skill.rarity = Rarity.Rare;
            skill.tags = SkillTag.Lightning;
            skill.role = SkillRole.Area;
            skill.recordable = true;
            skill.cooldown = 8f;
            skill.countPerLevel = 0;
            skill.radius = 1.6f;
            skill.radiusPerLevel = skill.duration = skill.durationPerLevel = 0f;
        }
    }
}
