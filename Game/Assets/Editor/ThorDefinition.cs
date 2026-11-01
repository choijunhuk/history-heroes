using HistoryHeroes.Heroes;
using HistoryHeroes.Skills;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>토르의 수치를 한곳에서 채워 장면 생성과 EditMode 검사가 같은 값을 본다.</summary>
    public static class ThorDefinition
    {
        public static void ConfigureHero(HeroData hero)
        {
            hero.id = "thor";
            hero.displayName = "토르";
            hero.heroClass = HeroClass.Warrior;
            hero.attackKind = BasicAttackKind.MeleeArc;
            hero.maxHp = 1250f;
            hero.attack = 115f;
            hero.attackSpeed = 0.85f;
            hero.moveSpeed = 5f;
            hero.range = 2.1f;
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
            hero.color = new Color(0.64f, 0.73f, 0.83f);
            hero.playStyle = "가까이 다가가 멈춰 망치를 휘두르고, 세 번째 타격으로 작은 충격파를 낸다";
            hero.signatureName = "천둥의 무게";
            hero.signatureDescription = "연속 정지 기본 공격 세 번째에 작은 충격파. 이동하면 횟수가 사라진다";
            hero.meleeArcDegrees = 100f;
            hero.meleeWaveCoefficient = 0.4f;
            hero.thunderEvery = 3;
            hero.thunderRadius = 1.15f;
            hero.thunderCoefficient = 0.4f;
            hero.star3ThunderRadius = 1.4f;
            hero.star5ThunderCoefficient = 0.5f;
            hero.godShardUnlock = true;
            hero.unlockAfterBossChapter = 0;
            hero.gemsOnly = false;
            hero.unlockGems = 0;
        }

        public static void ConfigureHammer(ActiveSkillData skill)
        {
            ConfigureCommon(skill);
            skill.id = "thor_hammer";
            skill.displayName = "귀환 망치";
            skill.description = "8초마다 앞쪽으로 망치를 보내고 되돌린다\n가는 길과 오는 길 각각 피해 65%\n레벨마다 피해 +10%";
            skill.maxLevel = 5;
            skill.saveCostFirstLevel = 3;
            skill.saveCostPerLevel = 3;
            skill.damageFirstLevel = 0.65f;
            skill.damagePerLevel = 0.1f;
            skill.returnImpactCoefficient = 0f;
        }

        public static void ConfigureReturn(ActiveSkillData skill)
        {
            ConfigureCommon(skill);
            skill.id = "thor_return";
            skill.displayName = "천둥의 귀환";
            skill.description = "귀환 망치가 돌아온 자리에 좁은 낙뢰 1회\n망치 왕복 각각 80%, 낙뢰 75%";
            skill.maxLevel = 1;
            skill.saveCostFirstLevel = 10;
            skill.saveCostPerLevel = 0;
            skill.damageFirstLevel = 0.8f;
            skill.damagePerLevel = 0f;
            skill.radius = 0.8f;
            skill.returnImpactCoefficient = 0.75f;
        }

        public static void ConfigureRecipe(FusionRecipe recipe, ActiveSkillData hammer, SkillData speed, ActiveSkillData result)
        {
            recipe.id = "thor_return";
            recipe.type = FusionType.Evolution;
            recipe.materialA = hammer;
            recipe.requiredLevelA = 5;
            recipe.materialB = speed;
            recipe.requiredLevelB = 1;
            recipe.result = result;
            recipe.heroOnlyId = "thor";
        }

        private static void ConfigureCommon(ActiveSkillData skill)
        {
            skill.rarity = Rarity.Rare;
            skill.tags = SkillTag.Melee | SkillTag.Lightning;
            skill.role = SkillRole.Single;
            skill.recordable = true;
            skill.heroOnlyId = "thor";
            skill.cooldown = 8f;
            skill.countFirstLevel = 1;
            skill.countPerLevel = 0;
            skill.radius = 0.8f;
            skill.radiusPerLevel = 0f;
            skill.duration = 0f;
            skill.durationPerLevel = 0f;
        }
    }
}
