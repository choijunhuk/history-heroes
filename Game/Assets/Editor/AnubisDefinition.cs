using HistoryHeroes.Heroes;
using HistoryHeroes.Skills;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>아누비스 영웅과 전용 기술의 시작 수치를 장면 생성과 검사에서 함께 쓴다.</summary>
    public static class AnubisDefinition
    {
        public static void ConfigureHero(HeroData hero)
        {
            hero.id = "anubis";
            hero.displayName = "아누비스";
            hero.heroClass = HeroClass.Mage;
            hero.attackKind = BasicAttackKind.Projectile;
            hero.maxHp = 950f;
            hero.attack = 90f;
            hero.attackSpeed = 1f;
            hero.moveSpeed = 5f;
            hero.range = 5.5f;
            hero.critRate = 0.05f;
            hero.critDamage = 1.5f;
            hero.projectileSpeed = 14f;
            hero.color = new Color(0.78f, 0.66f, 0.33f);
            hero.playStyle = "멈춰 모래 구체를 맞히고 같은 적에게 표식을 쌓아 공격할 틈을 만든다";
            hero.signatureName = "저울의 표식";
            hero.signatureDescription = "같은 적에 기본 공격 3회 명중 뒤 다음 기본 공격이 짧게 둔화시킨다";
            hero.markHits = 3;
            hero.markSlow = 0.25f;
            hero.markDuration = 0.6f;
            hero.markCooldown = 6f;
            hero.markBossScale = 0.3f;
            hero.star3MarkDuration = 0.8f;
            hero.star5MarkSlow = 0.35f;
            hero.godShardUnlock = true;
            hero.unlockAfterBossChapter = 0;
            hero.gemsOnly = false;
            hero.unlockGems = 0;
        }

        public static void ConfigureStatue(ActiveSkillData skill)
        {
            ConfigureCommon(skill);
            skill.id = "sand_statue";
            skill.displayName = "모래 수호상";
            skill.description = "9초마다 수호상 1기, 4초간 주변 적 20% 둔화\n직접 피해 없음 · 레벨마다 둔화 +2.5%p, 반경 +0.1";
            skill.maxLevel = 5;
            skill.saveCostFirstLevel = 3;
            skill.saveCostPerLevel = 3;
            skill.radius = 1.6f;
            skill.radiusPerLevel = 0.0625f;
            skill.damageFirstLevel = 0f;
            skill.damagePerLevel = 0f;
            skill.slowFirstLevel = 0.2f;
            skill.slowPerLevel = 0.025f;
            skill.waveRadius = 0f;
        }

        public static void ConfigureGate(ActiveSkillData skill)
        {
            ConfigureCommon(skill);
            skill.id = "sand_gate";
            skill.displayName = "모래의 문";
            skill.description = "넓은 모래 수호상이 주변 적을 둔화\n적이 들어오면 작은 모래 파동 1회";
            skill.maxLevel = 1;
            skill.saveCostFirstLevel = 10;
            skill.saveCostPerLevel = 0;
            skill.radius = 2.2f;
            skill.radiusPerLevel = 0f;
            skill.damageFirstLevel = 0.35f;
            skill.damagePerLevel = 0f;
            skill.slowFirstLevel = 0.3f;
            skill.slowPerLevel = 0f;
            skill.waveRadius = 0.6f;
        }

        public static void ConfigureRecipe(FusionRecipe recipe, ActiveSkillData statue, SkillData health, ActiveSkillData gate)
        {
            recipe.id = "sand_gate";
            recipe.type = FusionType.Evolution;
            recipe.materialA = statue;
            recipe.requiredLevelA = 5;
            recipe.materialB = health;
            recipe.requiredLevelB = 1;
            recipe.result = gate;
            recipe.heroOnlyId = "anubis";
        }

        private static void ConfigureCommon(ActiveSkillData skill)
        {
            skill.rarity = Rarity.Rare;
            skill.tags = SkillTag.Defense | SkillTag.Survival;
            skill.role = SkillRole.Defense;
            skill.recordable = true;
            skill.heroOnlyId = "anubis";
            skill.cooldown = 9f;
            skill.countFirstLevel = 1;
            skill.countPerLevel = 0;
            skill.duration = 4f;
            skill.durationPerLevel = 0f;
            skill.slowFirstLevel = 0f;
            skill.slowPerLevel = 0f;
            skill.slowBossScale = 0.3f;
        }
    }
}
