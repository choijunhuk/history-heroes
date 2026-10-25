using HistoryHeroes.Combat;
using HistoryHeroes.Skills;
using UnityEngine;

namespace HistoryHeroes.Heroes
{
    public enum HeroClass
    {
        Archer,  // 궁수
        Warrior, // 전사 (근접)
        Gunner,  // 총잡이
        Mage,    // 법사
    }

    public enum BasicAttackKind
    {
        /// <summary>날아가는 공격 (화살, 총알, 전기 구체)</summary>
        Projectile,
        /// <summary>근접 부채꼴 베기: 맞은 적 전부 (관우)</summary>
        MeleeArc,
    }

    /// <summary>
    /// 영웅 정의 (변하지 않는 값만). 기준값은 Docs/Design/Heroes.md 2장 표.
    /// 영웅마다 대표 효과 1개 + 전용 액티브 1개 + 전용 조합 1개 (Heroes 1장).
    /// </summary>
    [CreateAssetMenu(menuName = "History Heroes/Hero", fileName = "Hero_")]
    public sealed class HeroData : ScriptableObject
    {
        [Header("이름")]
        [Tooltip("저장용 ID. 한 번 정하면 바꾸지 않는다")]
        public string id = "khan";
        public string displayName = "칭기즈 칸";
        public HeroClass heroClass = HeroClass.Archer;
        [TextArea] public string playStyle;

        [Header("기본 능력치")]
        public float maxHp = 1000f;
        public float attack = 100f;
        public float attackSpeed = 1.0f;
        public float moveSpeed = 5.0f;
        public float range = 6.0f;
        [Range(0f, 1f)] public float critRate = 0.05f;
        public float critDamage = 1.5f;

        [Header("기본 공격")]
        public BasicAttackKind attackKind = BasicAttackKind.Projectile;
        [Tooltip("비어 있으면 장면의 기본 화살")]
        public Projectile projectilePrefab;
        public float projectileSpeed = 14f;
        [Tooltip("근접 베기 각도 (관통 패시브가 있으면 +40°)")]
        public float meleeArcDegrees = 120f;
        [Tooltip("근접 + 벽 튕김: 베기마다 날아가는 검기의 피해 (공격력 배수)")]
        public float meleeWaveCoefficient = 0.4f;

        [Header("대표 효과")]
        public string signatureName = "기마 속사";
        [TextArea] public string signatureDescription;

        [Tooltip("멈춘 뒤 첫 공격까지 기다리는 시간. 기본 0.2초, 칭기즈 칸은 0초 (Stats.md 1-1)")]
        public float firstShotDelay = 0.2f;
        [Tooltip("멈춘 뒤 첫 발 피해 보너스. 칭기즈 칸 +50%")]
        public float firstShotBonus = 0f;

        [Tooltip("받는 피해 감소. 관우 15%")]
        [Range(0f, 0.6f)] public float damageReduction = 0f;
        [Tooltip("체력이 이 비율 아래면 공격력 보너스. 관우 50% 아래 +20%")]
        public float lowHpThreshold = 0.5f;
        public float lowHpAttackBonus = 0f;

        [Tooltip("기본 공격 몇 번마다 작은 포탄. 나폴레옹 3, 0이면 없음")]
        public int cannonEvery = 0;
        public float cannonCoefficient = 0.8f;
        public float cannonRadius = 1.2f;
        [Tooltip("기본 공격이 맞은 적을 밀어내는 힘. 나폴레옹")]
        public float knockback = 0f;

        [Tooltip("기본 공격이 맞으면 근처 적에게 튕기는 횟수. 테슬라 2 (기본 1 + 교류 +1)")]
        public int chainBounces = 0;
        public float chainRadius = 3f;
        [Tooltip("튕길 때마다 피해가 줄어드는 비율")]
        public float chainFalloff = 0.3f;

        [Header("전용")]
        [Tooltip("이 영웅만 쓰는 액티브. 첫 레벨업에 조금 더 잘 나온다 (Skills.md 3장)")]
        public ActiveSkillData exclusiveActive;

        [Header("해금 (Heroes.md 2장)")]
        [Tooltip("이 구간 보스를 처음 깨면 무료 해금. 0이면 처음부터")]
        public int unlockAfterBossChapter = 0;
        [Tooltip("바로 해금하는 보석 값")]
        public int unlockGems = 1500;

        [Header("화면 (임시 도형)")]
        public Color color = new Color(0.95f, 0.75f, 0.2f);

        public string ClassLabel
        {
            get
            {
                switch (heroClass)
                {
                    case HeroClass.Warrior: return "전사";
                    case HeroClass.Gunner: return "총잡이";
                    case HeroClass.Mage: return "법사";
                    default: return "궁수";
                }
            }
        }
    }
}
