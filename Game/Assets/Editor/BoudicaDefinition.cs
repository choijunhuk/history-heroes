using HistoryHeroes.Heroes;
using HistoryHeroes.Skills;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>장면 빌더와 EditMode 검사가 같은 부디카 수치를 쓰도록 에셋 생성 없이 값을 채운다.</summary>
    public static class BoudicaDefinition
    {
        public static void ConfigureHero(HeroData hero)
        {
            hero.id = "boudica";
            hero.displayName = "부디카";
            hero.heroClass = HeroClass.Warrior;
            hero.attackKind = BasicAttackKind.MeleeArc;
            hero.maxHp = 1300f;
            hero.attack = 110f;
            hero.attackSpeed = 0.85f;
            hero.moveSpeed = 5f;
            hero.range = 2.2f;
            hero.critRate = 0.05f;
            hero.critDamage = 1.5f;
            hero.firstShotDelay = 0.2f;
            hero.firstShotBonus = 0f;
            hero.damageReduction = 0f;
            hero.lowHpAttackBonus = 0f;
            hero.cannonEvery = 0;
            hero.knockback = 0f;
            hero.chainBounces = 0;
            hero.projectileSpeed = 14f;
            hero.color = new Color(0.32f, 0.62f, 0.36f);
            hero.playStyle = "피한 뒤 멈춰서 적을 밀어내고, 되찾은 자리에서 다시 싸우는 전사";
            hero.signatureName = "되찾는 자리";
            hero.signatureDescription = "1초 멈춰 다음 기본 공격을 준비한다. 맞은 일반 적을 밀어내며 재사용은 6초. 보스 밀침은 30%";
            hero.meleeArcDegrees = 120f;
            hero.meleeWaveCoefficient = 0.4f;
            hero.reclaimStopSeconds = 1f;
            hero.reclaimCooldown = 6f;
            hero.reclaimKnockback = 0.6f;
            hero.reclaimBossScale = 0.3f;
            hero.star3ReclaimKnockback = 0.9f;
            hero.star5ReclaimAttackBonus = 0.2f;
            hero.unlockAfterBossChapter = 0;
            hero.gemsOnly = true;
            hero.unlockGems = 2000;
        }

        public static void ConfigureRally(ActiveSkillData skill)
        {
            ConfigureCommonSkill(skill);
            skill.id = "boudica_rally";
            skill.displayName = "이케니의 함성";
            skill.description = "8초마다 영웅 자리에서 함성의 파동\n(반경 3, 피해 130%)\n레벨마다 피해 +25%";
            skill.maxLevel = 5;
            skill.saveCostFirstLevel = 3;
            skill.saveCostPerLevel = 3;
            skill.damageFirstLevel = 1.3f;
            skill.damagePerLevel = 0.25f;
            skill.countFirstLevel = 1;
            skill.radius = 3f;
        }

        public static void ConfigureUprising(ActiveSkillData skill)
        {
            ConfigureCommonSkill(skill);
            skill.id = "boudica_uprising";
            skill.displayName = "되찾은 터전";
            skill.description = "8초마다 영웅 자리에서 파동 3번\n(각 반경 3.2, 피해 100%, 0.3초 간격)\n레벨마다 피해 +20%";
            skill.maxLevel = 3;
            skill.saveCostFirstLevel = 12;
            skill.saveCostPerLevel = 4;
            skill.damageFirstLevel = 1f;
            skill.damagePerLevel = 0.2f;
            skill.countFirstLevel = 3;
            skill.radius = 3.2f;
        }

        public static void ConfigureRecipe(FusionRecipe recipe, ActiveSkillData rally, SkillData attack, ActiveSkillData result)
        {
            recipe.id = "boudica_uprising";
            recipe.type = FusionType.Evolution;
            recipe.materialA = rally;
            recipe.requiredLevelA = 5;
            recipe.materialB = attack;
            recipe.requiredLevelB = 1;
            recipe.result = result;
            recipe.heroOnlyId = "boudica";
        }

        private static void ConfigureCommonSkill(ActiveSkillData skill)
        {
            skill.rarity = Rarity.Rare;
            skill.tags = SkillTag.Melee;
            skill.role = SkillRole.Area;
            skill.recordable = true;
            skill.heroOnlyId = "boudica";
            skill.cooldown = 8f;
            skill.countPerLevel = 0;
            skill.radiusPerLevel = 0f;
            skill.duration = 0f;
            skill.durationPerLevel = 0f;
        }
    }
}
