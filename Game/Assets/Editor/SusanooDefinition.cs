using HistoryHeroes.Heroes;
using HistoryHeroes.Skills;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>스사노오의 수치는 생성 코드와 검사에서 함께 쓴다.</summary>
    public static class SusanooDefinition
    {
        public static void ConfigureHero(HeroData hero)
        {
            hero.id = "susanoo";
            hero.displayName = "스사노오";
            hero.heroClass = HeroClass.Warrior;
            hero.attackKind = BasicAttackKind.MeleeArc;
            hero.maxHp = 1200f;
            hero.attack = 110f;
            hero.attackSpeed = 1f;
            hero.moveSpeed = 5f;
            hero.range = 2.2f;
            hero.critRate = 0.05f;
            hero.critDamage = 1.5f;
            hero.firstShotDelay = 0.2f;
            hero.color = new Color(0.28f, 0.72f, 0.7f);
            hero.playStyle = "첫 베기로 자리를 만들고 멈춘 채 좁은 연속 베기를 맞힌다";
            hero.signatureName = "바람의 간격";
            hero.signatureDescription = "정지 첫 베기는 넓고 약하게, 두 번째부터 좁고 강하게. 이동하면 다시 첫 베기";
            hero.meleeArcDegrees = 70f;
            hero.meleeWaveCoefficient = 0.4f;
            hero.windFirstArc = 120f;
            hero.windFirstCoefficient = 0.7f;
            hero.windFollowArc = 70f;
            hero.windFollowCoefficient = 1.15f;
            hero.star3WindFirstCoefficient = 0.85f;
            hero.star5WindFollowCoefficient = 1.25f;
            hero.godShardUnlock = true;
            hero.unlockAfterBossChapter = 0;
            hero.gemsOnly = false;
            hero.unlockGems = 0;
        }

        public static void ConfigureWindSlash(ActiveSkillData skill)
        {
            Common(skill);
            skill.id = "wind_slash";
            skill.displayName = "바람 베기";
            skill.description = "7초마다 앞쪽 직선 검기 한 줄, 피해 75%와 작은 밀침\n레벨마다 피해 +10%";
            skill.maxLevel = 5;
            skill.saveCostFirstLevel = 3;
            skill.saveCostPerLevel = 3;
            skill.damageFirstLevel = 0.75f;
            skill.damagePerLevel = 0.1f;
            skill.countFirstLevel = 1;
        }

        public static void ConfigureStormPath(ActiveSkillData skill)
        {
            Common(skill);
            skill.id = "storm_path";
            skill.displayName = "폭풍의 길";
            skill.description = "같은 방향의 직선 검기 3번, 각 피해 60%\n길 밖의 적은 맞지 않는다";
            skill.maxLevel = 1;
            skill.saveCostFirstLevel = 10;
            skill.saveCostPerLevel = 0;
            skill.damageFirstLevel = 0.6f;
            skill.damagePerLevel = 0f;
            skill.countFirstLevel = 3;
        }

        public static void ConfigureRecipe(FusionRecipe recipe, ActiveSkillData slash, SkillData pierce, ActiveSkillData result)
        {
            recipe.id = "storm_path";
            recipe.type = FusionType.Evolution;
            recipe.materialA = slash;
            recipe.requiredLevelA = 5;
            recipe.materialB = pierce;
            recipe.requiredLevelB = 1;
            recipe.result = result;
            recipe.heroOnlyId = "susanoo";
        }

        private static void Common(ActiveSkillData skill)
        {
            skill.rarity = Rarity.Rare;
            skill.tags = SkillTag.Melee | SkillTag.Wind | SkillTag.Projectile;
            skill.role = SkillRole.Single;
            skill.recordable = true;
            skill.heroOnlyId = "susanoo";
            skill.cooldown = 7f;
            skill.waveLength = 5.5f;
            skill.waveSpeed = 11f;
            skill.waveInterval = 0.22f;
            skill.pushDistance = 0.35f;
            skill.radius = 0f;
            skill.radiusPerLevel = 0f;
            skill.duration = 0f;
            skill.durationPerLevel = 0f;
            skill.countPerLevel = 0;
        }
    }
}
