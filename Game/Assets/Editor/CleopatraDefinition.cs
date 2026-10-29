using HistoryHeroes.Heroes;
using HistoryHeroes.Skills;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>생성 에셋 없이 장면 빌더와 검사가 같은 영웅·기술 수치를 읽게 한다.</summary>
    public static class CleopatraDefinition
    {
        public static void ConfigureHero(HeroData hero)
        {
            hero.id = "cleopatra";
            hero.displayName = "클레오파트라";
            hero.heroClass = HeroClass.Summoner;
            hero.attackKind = BasicAttackKind.Projectile;
            hero.maxHp = 900f;
            hero.attack = 80f;
            hero.attackSpeed = 1f;
            hero.moveSpeed = 5f;
            hero.range = 5f;
            hero.projectileSpeed = 15f;
            hero.critRate = 0.05f;
            hero.critDamage = 1.5f;
            hero.firstShotDelay = 0.2f;
            hero.firstShotBonus = 0f;
            hero.damageReduction = 0f;
            hero.lowHpAttackBonus = 0f;
            hero.cannonEvery = 0;
            hero.knockback = 0f;
            hero.chainBounces = 0;
            hero.workbench = false;
            hero.guardianAttacks = 3;
            hero.guardianCoefficient = 0.6f;
            hero.guardianNearbyRadius = 2f;
            hero.star3GuardianCoefficient = 0.9f;
            hero.star5GuardianTargets = 2;
            hero.color = new Color(0.25f, 0.72f, 0.68f);
            hero.playStyle = "멈춰 수호상을 준비하고 다음 공격을 함께 쏘는 소환사";
            hero.signatureName = "기록의 수호상";
            hero.signatureDescription = "정지 기본 공격 3회로 수호상을 준비한다. 다음 정지 공격에 같은 적을 한 번 더 공격 (60%). 이동하면 준비 취소";
            hero.unlockAfterBossChapter = 0;
            hero.gemsOnly = true;
            hero.unlockGems = 2000;
            hero.godShardUnlock = false;
        }

        public static void ConfigurePharos(ActiveSkillData skill)
        {
            ConfigureCommonSkill(skill);
            skill.id = "cleopatra_pharos";
            skill.displayName = "파로스의 빛";
            skill.description = "8초마다 제자리에 작은 등대 4초 소환\n가까운 적 자리에 빛 4회 (각 반경 0.6, 피해 45%)\n레벨마다 피해 +10%";
            skill.maxLevel = 5;
            skill.saveCostFirstLevel = 3;
            skill.saveCostPerLevel = 3;
            skill.damageFirstLevel = 0.45f;
            skill.damagePerLevel = 0.1f;
            skill.radius = 0.6f;
        }

        public static void ConfigureLibrary(ActiveSkillData skill)
        {
            ConfigureCommonSkill(skill);
            skill.id = "cleopatra_library";
            skill.displayName = "알렉산드리아의 서고";
            skill.description = "8초마다 제자리에 기록의 빛 4초 소환\n가까운 적 자리에 빛 4회 (각 반경 1.6, 피해 70%)\n레벨마다 피해 +15%";
            skill.maxLevel = 3;
            skill.saveCostFirstLevel = 12;
            skill.saveCostPerLevel = 4;
            skill.damageFirstLevel = 0.7f;
            skill.damagePerLevel = 0.15f;
            skill.radius = 1.6f;
        }

        public static void ConfigureRecipe(FusionRecipe recipe, ActiveSkillData pharos, SkillData activeDamage, ActiveSkillData library)
        {
            recipe.id = "cleopatra_library";
            recipe.type = FusionType.Evolution;
            recipe.materialA = pharos;
            recipe.requiredLevelA = 5;
            recipe.materialB = activeDamage;
            recipe.requiredLevelB = 1;
            recipe.result = library;
            recipe.heroOnlyId = "cleopatra";
        }

        private static void ConfigureCommonSkill(ActiveSkillData skill)
        {
            skill.rarity = Rarity.Rare;
            skill.tags = SkillTag.Lightning;
            skill.role = SkillRole.Area;
            skill.recordable = true;
            skill.heroOnlyId = "cleopatra";
            skill.cooldown = 8f;
            skill.countFirstLevel = 4;
            skill.countPerLevel = 0;
            skill.radiusPerLevel = 0f;
            skill.duration = 4f;
            skill.durationPerLevel = 0f;
        }
    }
}
