using HistoryHeroes.Heroes;
using HistoryHeroes.Skills;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>아테나의 수치는 생성 코드와 검사에서 함께 쓴다.</summary>
    public static class AthenaDefinition
    {
        public static void ConfigureHero(HeroData hero)
        {
            hero.id = "athena";
            hero.displayName = "아테나";
            hero.heroClass = HeroClass.Warrior;
            hero.attackKind = BasicAttackKind.MeleeThrust;
            hero.maxHp = 1250f;
            hero.attack = 108f;
            hero.attackSpeed = 0.9f;
            hero.moveSpeed = 5f;
            hero.range = 2.5f;
            hero.critRate = 0.05f;
            hero.critDamage = 1.5f;
            hero.firstShotDelay = 0.2f;
            hero.color = new Color(0.78f, 0.69f, 0.47f);
            hero.playStyle = "멈춰 적 탄 한 발을 읽고 막은 뒤 짧은 창 찌르기로 반격한다";
            hero.signatureName = "판단의 방패";
            hero.signatureDescription = "1초 정지하면 다음 적 탄 한 발 차단. 이동하면 준비 취소, 성공부터 8초 재사용";
            hero.spearWidth = 0.6f;
            hero.shieldPrepareSeconds = 1f;
            hero.shieldReuseSeconds = 8f;
            hero.owlBonusCoefficient = 0.2f;
            hero.star3ShieldPrepareSeconds = 0.8f;
            hero.star5OwlBonusCoefficient = 0.25f;
            hero.meleeWaveCoefficient = 0.4f;
            hero.godShardUnlock = true;
            hero.unlockAfterBossChapter = 0;
            hero.gemsOnly = false;
            hero.unlockGems = 0;
        }

        public static void ConfigureOwlEye(ActiveSkillData skill)
        {
            Common(skill);
            skill.id = "owl_eye";
            skill.displayName = "올빼미의 눈";
            skill.description = "10초마다 가까운 적 1체를 3초간 표식\n그 적을 기본 공격으로 맞히면 작은 추가 피해\n레벨마다 표적 탐색 범위 +10%";
            skill.maxLevel = 5;
            skill.saveCostFirstLevel = 3;
            skill.saveCostPerLevel = 3;
            skill.countFirstLevel = 1;
            skill.radius = 5f;
            skill.radiusPerLevel = 0.1f;
        }

        public static void ConfigureGuardianGaze(ActiveSkillData skill)
        {
            Common(skill);
            skill.id = "guardian_gaze";
            skill.displayName = "수호자의 시선";
            skill.description = "10초마다 가까운 적 최대 3체를 3초간 표식\n기본 공격 명중에만 작은 추가 피해";
            skill.maxLevel = 1;
            skill.saveCostFirstLevel = 10;
            skill.saveCostPerLevel = 0;
            skill.countFirstLevel = 3;
            skill.radius = 7f;
            skill.radiusPerLevel = 0f;
        }

        public static void ConfigureRecipe(FusionRecipe recipe, ActiveSkillData eye, SkillData defense, ActiveSkillData result)
        {
            recipe.id = "guardian_gaze";
            recipe.type = FusionType.Evolution;
            recipe.materialA = eye;
            recipe.requiredLevelA = 5;
            recipe.materialB = defense;
            recipe.requiredLevelB = 1;
            recipe.result = result;
            recipe.heroOnlyId = "athena";
        }

        private static void Common(ActiveSkillData skill)
        {
            skill.rarity = Rarity.Rare;
            skill.tags = SkillTag.Defense | SkillTag.Melee;
            skill.role = SkillRole.Single;
            skill.recordable = true;
            skill.heroOnlyId = "athena";
            skill.cooldown = 10f;
            skill.damageFirstLevel = 0f;
            skill.damagePerLevel = 0f;
            skill.countPerLevel = 0;
            skill.duration = 3f;
            skill.durationPerLevel = 0f;
        }
    }
}
