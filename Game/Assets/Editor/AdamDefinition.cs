using HistoryHeroes.Heroes;
using HistoryHeroes.Skills;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>창세기의 전승과 게임 속 전투·옷·목봉은 구분한다.</summary>
    public static class AdamDefinition
    {
        public static void ConfigureHero(HeroData hero)
        {
            hero.id = "adam";
            hero.displayName = "아담";
            hero.heroClass = HeroClass.Warrior;
            hero.attackKind = BasicAttackKind.MeleeArc;
            hero.maxHp = 1200f;
            hero.attack = 105f;
            hero.attackSpeed = 1f;
            hero.moveSpeed = 5.2f;
            hero.range = 2f;
            hero.meleeArcDegrees = 120f;
            hero.meleeWaveCoefficient = 0.4f;
            hero.critRate = 0.05f;
            hero.critDamage = 1.5f;
            hero.firstShotDelay = 0.2f;
            hero.firstShotBonus = 0f;
            hero.damageReduction = 0f;
            hero.lowHpAttackBonus = 0f;
            hero.cannonEvery = 0;
            hero.knockback = 0f;
            hero.chainBounces = 0;
            hero.footstepMoveSeconds = 0.8f;
            hero.footstepDodgeBonus = 0.1f;
            hero.footstepDodgeDuration = 1.5f;
            hero.footstepCooldown = 6f;
            hero.star3FootstepDodgeDuration = 2f;
            hero.star5FootstepDodgeBonus = 0.12f;
            hero.color = new Color(0.62f, 0.49f, 0.32f);
            hero.playStyle = "걸어서 피한 뒤 멈춰 첫 공격으로 잠시 회피를 얻는 전사";
            hero.signatureName = "살피는 발걸음";
            hero.signatureDescription = "0.8초 실제 이동 뒤 첫 정지 기본 공격에 회피 +10%p를 1.5초 얻음. 이동하면 사라지고 6초 뒤 다시 사용";
            hero.unlockAfterBossChapter = 0;
            hero.gemsOnly = true;
            hero.unlockGems = 2000;
            hero.godShardUnlock = false;
        }

        public static void ConfigurePath(ActiveSkillData skill)
        {
            ConfigureCommon(skill);
            skill.id = "adam_path";
            skill.displayName = "앞길 짚기";
            skill.description = "7초마다 가까운 적 방향 140도·반경 2.8 전방 타격 한 번 (피해 100%)\n레벨마다 피해 +20%p";
            skill.maxLevel = 5;
            skill.saveCostFirstLevel = skill.saveCostPerLevel = 3;
            skill.damageFirstLevel = 1f;
            skill.damagePerLevel = 0.2f;
            skill.countFirstLevel = 1;
            skill.radius = 2.8f;
        }

        public static void ConfigureWay(ActiveSkillData skill)
        {
            ConfigureCommon(skill);
            skill.id = "adam_way";
            skill.displayName = "이어 가는 길";
            skill.description = "7초마다 고정된 자리·방향의 160도·반경 3 전방 타격 두 번 (0.3초 간격, 각 110%)\n레벨마다 각 타격 피해 +20%p";
            skill.maxLevel = 3;
            skill.saveCostFirstLevel = 12;
            skill.saveCostPerLevel = 4;
            skill.damageFirstLevel = 1.1f;
            skill.damagePerLevel = 0.2f;
            skill.countFirstLevel = 2;
            skill.radius = 3f;
            skill.duration = 0.3f;
        }

        public static void ConfigureRecipe(FusionRecipe recipe, ActiveSkillData path, SkillData dodge, ActiveSkillData way)
        {
            recipe.id = "adam_way";
            recipe.type = FusionType.Evolution;
            recipe.materialA = path;
            recipe.requiredLevelA = 5;
            recipe.materialB = dodge;
            recipe.requiredLevelB = 1;
            recipe.result = way;
            recipe.heroOnlyId = "adam";
        }

        private static void ConfigureCommon(ActiveSkillData skill)
        {
            skill.heroOnlyId = "adam";
            skill.rarity = Rarity.Rare;
            skill.tags = SkillTag.Melee;
            skill.role = SkillRole.Area;
            skill.recordable = true;
            skill.cooldown = 7f;
            skill.countPerLevel = 0;
            skill.radiusPerLevel = 0f;
            skill.duration = skill.durationPerLevel = 0f;
            skill.healFractionFirstLevel = skill.healFractionPerLevel = 0f;
            skill.pushDistance = 0f;
        }
    }
}
