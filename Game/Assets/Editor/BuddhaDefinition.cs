using HistoryHeroes.Heroes;
using HistoryHeroes.Skills;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>역사적 스승과 후대 전승을 구분한다. 전투 기술은 게임 창작이다.</summary>
    public static class BuddhaDefinition
    {
        public static void ConfigureHero(HeroData hero)
        {
            hero.id = "buddha";
            hero.displayName = "부처";
            hero.heroClass = HeroClass.Mage;
            hero.attackKind = BasicAttackKind.Projectile;
            hero.maxHp = 950f;
            hero.attack = 85f;
            hero.attackSpeed = 1f;
            hero.moveSpeed = 5f;
            hero.range = 5.5f;
            hero.projectileSpeed = 13f;
            hero.critRate = 0.05f;
            hero.critDamage = 1.5f;
            hero.firstShotDelay = 0.2f;
            hero.firstShotBonus = 0f;
            hero.damageReduction = 0f;
            hero.lowHpAttackBonus = 0f;
            hero.cannonEvery = 0;
            hero.knockback = 0f;
            hero.chainBounces = 0;
            hero.breathAttacks = 3;
            hero.breathShieldFraction = 0.05f;
            hero.breathShieldDuration = 2f;
            hero.breathCooldown = 8f;
            hero.star3BreathShieldDuration = 2.5f;
            hero.star5BreathShieldFraction = 0.06f;
            hero.color = new Color(0.78f, 0.53f, 0.24f);
            hero.playStyle = "멈춰 세 번 공격해 짧은 보호막을 얻고 자신을 회복하는 법사";
            hero.signatureName = "고요한 호흡";
            hero.signatureDescription = "연속 정지 기본 공격 세 번째에 최대체력 5% 보호막을 2초 얻음. 이동하면 사라지고 8초 뒤 다시 사용";
            hero.unlockAfterBossChapter = 0;
            hero.gemsOnly = true;
            hero.unlockGems = 2000;
            hero.godShardUnlock = false;
        }

        public static void ConfigureRest(ActiveSkillData skill)
        {
            ConfigureCommon(skill);
            skill.id = "buddha_rest";
            skill.displayName = "쉬어 가는 빛";
            skill.description = "12초마다 반경 6 안에 적이 있으면 자신을 최대체력 2% 회복\n레벨마다 회복 +0.5%p";
            skill.maxLevel = 5;
            skill.saveCostFirstLevel = skill.saveCostPerLevel = 3;
            skill.healFractionFirstLevel = 0.02f;
            skill.healFractionPerLevel = 0.005f;
            skill.countFirstLevel = 1;
        }

        public static void ConfigureBreath(ActiveSkillData skill)
        {
            ConfigureCommon(skill);
            skill.id = "buddha_breath";
            skill.displayName = "다시 잇는 숨";
            skill.description = "12초마다 자신을 즉시·0.6초 뒤 두 번 회복 (회당 최대체력 2.5%)\n레벨마다 회당 회복 +0.5%p";
            skill.maxLevel = 3;
            skill.saveCostFirstLevel = 12;
            skill.saveCostPerLevel = 4;
            skill.healFractionFirstLevel = 0.025f;
            skill.healFractionPerLevel = 0.005f;
            skill.countFirstLevel = 2;
            skill.duration = 0.6f;
        }

        public static void ConfigureRecipe(FusionRecipe recipe, ActiveSkillData rest, SkillData regen, ActiveSkillData breath)
        {
            recipe.id = "buddha_breath";
            recipe.type = FusionType.Evolution;
            recipe.materialA = rest;
            recipe.requiredLevelA = 5;
            recipe.materialB = regen;
            recipe.requiredLevelB = 1;
            recipe.result = breath;
            recipe.heroOnlyId = "buddha";
        }

        private static void ConfigureCommon(ActiveSkillData skill)
        {
            skill.heroOnlyId = "buddha";
            skill.rarity = Rarity.Rare;
            skill.tags = SkillTag.Defense;
            skill.role = SkillRole.Defense;
            skill.recordable = true;
            skill.cooldown = 12f;
            skill.damageFirstLevel = skill.damagePerLevel = 0f;
            skill.countPerLevel = 0;
            skill.radius = 6f;
            skill.radiusPerLevel = 0f;
            skill.duration = skill.durationPerLevel = 0f;
            skill.pushDistance = 0f;
        }
    }
}
