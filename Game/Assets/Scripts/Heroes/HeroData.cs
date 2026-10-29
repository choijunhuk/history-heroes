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
        Summoner, // 소환사 (다빈치, 확장: 새 영웅)
    }

    public enum BasicAttackKind
    {
        /// <summary>날아가는 공격 (화살, 총알, 전기 구체)</summary>
        Projectile,
        /// <summary>근접 부채꼴 베기: 맞은 적 전부 (관우)</summary>
        MeleeArc,
        /// <summary>짧고 좁은 직선 찌르기 (아테나)</summary>
        MeleeThrust,
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

        [Header("스사노오 바람의 간격")]
        public float windFirstArc;
        public float windFirstCoefficient;
        public float windFollowArc;
        public float windFollowCoefficient;

        [Header("아테나 판단의 방패·올빼미의 눈")]
        public float spearWidth;
        [Tooltip("관통이 있으면 찌르기 길이를 이 비율만큼 늘린다 (0.4 = +40%)")]
        public float spearPierceReachBonus = 0.4f;
        public float shieldPrepareSeconds;
        public float shieldReuseSeconds;
        public float owlBonusCoefficient;

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

        [Header("이순신 철갑 (확장: 새 영웅)")]
        [Tooltip("멈춰 있는 동안 받는 피해 감소 (0.25 = -25%). 0이면 없음")]
        public float stopGuard = 0f;
        [Tooltip("막은 피해 중 쌓아 두는 비율 → 다음 기본 공격에 더한다")]
        public float stopGuardStore = 0.5f;
        [Tooltip("쌓아 둘 수 있는 상한 (공격력 배수, 2 = 200%)")]
        public float stopGuardCap = 2f;
        [Tooltip("움직이면 쌓인 것이 이 시간에 걸쳐 사라진다 (초)")]
        public float stopGuardDecay = 2f;

        [Header("이성계 백발백중 (확장: 새 영웅)")]
        [Tooltip("멈춰서 활을 모으는 영웅 (공격속도 대신 모으는 시간)")]
        public bool chargeShot;
        [Tooltip("1단 · 2단(관통) · 3단(관통 + 치명타 확정 + 큰 피해)까지 멈춘 시간 (초)")]
        public Vector3 chargeTimes = new Vector3(0.4f, 0.9f, 1.5f);
        [Tooltip("2단 피해 배수")]
        public float chargeMidMultiplier = 1.4f;
        [Tooltip("3단 피해 배수")]
        public float chargeFullMultiplier = 2.5f;

        [Header("제갈량 (확장: 새 영웅)")]
        [Tooltip("기본 공격 갈래 수 (깃털 부채 바람 3갈래). 1이면 보통")]
        public int fanShots = 1;
        public float fanSpread = 20f;
        [Tooltip("갈래 하나 피해 배수")]
        public float fanCoefficient = 1f;
        [Tooltip("동남풍: 멈춰 있는 동안 이 반경 안 적 탄이 느려진다 (0이면 없음)")]
        public float windRadius = 0f;
        [Tooltip("동남풍: 느려지는 비율 (0.4 = 40% 느리게)")]
        public float windSlow = 0.4f;

        [Header("다빈치 작업대 (확장: 새 영웅)")]
        [Tooltip("멈춰 있으면 부품을 만들고, 부품이 모이면 비행 기계를 조립한다")]
        public bool workbench;
        [Tooltip("부품 하나에 멈춘 시간 (초)")]
        public float partEvery = 1f;
        public int partsPerMachine = 3;
        public int maxMachines = 2;
        [Tooltip("비행 기계가 떠 있는 시간 (초)")]
        public float machineLife = 20f;
        [Tooltip("비행 기계 한 발 (공격력 배수). 영웅이 멈춰 있을 때만 쏜다")]
        public float machineDamage = 0.35f;
        [Tooltip("비행 기계가 초당 쏘는 횟수")]
        public float machineFireRate = 1f;
        [Tooltip("대표 효과 소환물 모델 (비행 기계 또는 돌 수호상)")]
        public GameObject helperModel;

        [Header("잔 다르크 성녀의 깃발")]
        [Tooltip("연속으로 멈춰 깃발을 세울 시간. 0이면 깃발을 쓰지 않는다")]
        public float bannerDeploySeconds;
        public float bannerRadius;
        public float bannerDuration;
        [Tooltip("깃발 안에 멈춰 있을 때 기본 공격력 보너스")]
        public float bannerAttackBonus;
        [Tooltip("깃발 안에 멈춰 있을 때 받는 피해 감소")]
        public float bannerDamageReduction;

        [Header("선덕여왕 별자리 관측")]
        [Tooltip("연속으로 멈춰 다음 기본 공격의 관통을 준비하는 시간. 0이면 사용하지 않는다")]
        public float observationSeconds;
        [Tooltip("준비한 기본 공격 한 번의 피해 보너스")]
        public float observationAttackBonus;

        [Header("부디카 되찾는 자리")]
        [Tooltip("연속 정지로 밀침 공격을 준비하는 시간. 0이면 사용하지 않는다")]
        public float reclaimStopSeconds;
        [Tooltip("준비한 기본 공격을 시작한 뒤 재사용까지 걸리는 시간")]
        public float reclaimCooldown;
        public float reclaimKnockback;
        [Tooltip("보스 밀침 거리 비율. 일반 밀침 면역과 별도로 적용한다")]
        public float reclaimBossScale;

        [Header("토르 천둥의 무게")]
        [Tooltip("연속으로 멈춰 쓴 기본 공격 몇 회마다 충격파가 나가는가. 0이면 사용하지 않는다")]
        public int thunderEvery;
        public float thunderRadius;
        public float thunderCoefficient;
        [Tooltip("해금 전 30조각이 필요한 신 영웅")]
        public bool godShardUnlock;

        [Header("아누비스 저울의 표식")]
        [Tooltip("같은 적에게 필요한 기본 공격 명중 횟수. 다음 기본 공격에 둔화를 건다")]
        public int markHits;
        [Range(0f, 1f)] public float markSlow;
        public float markDuration;
        public float markCooldown;
        [Tooltip("보스에게 주는 둔화 세기 비율")]
        public float markBossScale = 0.3f;

        [Header("클레오파트라 기록의 수호상")]
        [Tooltip("수호상을 준비하는 연속 정지 기본 공격 횟수. 0이면 사용하지 않는다")]
        public int guardianAttacks;
        public float guardianCoefficient;
        [Tooltip("★5 수호상이 함께 공격할 다른 적을 찾는 반경")]
        public float guardianNearbyRadius;

        [Header("각성 대표 효과 강화 (Stats 6-1, ★3·★5). 음수 = 그대로")]
        [Tooltip("★3 첫 발 피해 보너스 (칭기즈 칸 +75%)")]
        public float star3FirstShotBonus = -1f;
        [Tooltip("★5 첫 발 피해 보너스 (칭기즈 칸 +100%)")]
        public float star5FirstShotBonus = -1f;
        [Tooltip("★5 첫 발이 적을 하나 관통 (칭기즈 칸)")]
        public bool star5FirstShotPierce;
        [Tooltip("★3 받는 피해 감소 (관우 20%)")]
        public float star3DamageReduction = -1f;
        [Tooltip("★5 체력 50% 아래 공격력 보너스 (관우 +35%)")]
        public float star5LowHpAttackBonus = -1f;
        [Tooltip("★3 포탄 범위 배수 (나폴레옹 ×1.3)")]
        public float star3CannonRadiusScale = -1f;
        [Tooltip("★5 기본 공격 몇 발마다 포탄 (나폴레옹 2)")]
        public int star5CannonEvery = -1;
        [Tooltip("★3 튕김 횟수 더하기 (테슬라 +1)")]
        public int star3ExtraBounces;
        [Tooltip("★5 튕길 때 피해가 줄지 않음 (테슬라)")]
        public bool star5NoFalloff;
        [Tooltip("★3 멈춤 피해 감소 (이순신 30%)")]
        public float star3StopGuard = -1f;
        [Tooltip("★5 쌓기 상한 (이순신 공격력 300%)")]
        public float star5StopGuardCap = -1f;
        [Tooltip("★3 3단 피해 배수 (이성계 ×3)")]
        public float star3ChargeFull = -1f;
        [Tooltip("★5 3단까지 시간 (이성계 1.2초)")]
        public float star5ChargeFullTime = -1f;
        [Tooltip("★3 비행 기계 한 발 (다빈치 50%)")]
        public float star3MachineDamage = -1f;
        [Tooltip("★5 비행 기계 최대 수 (다빈치 3)")]
        public int star5MaxMachines = -1;
        [Tooltip("★3 동남풍 반경 (제갈량 4)")]
        public float star3WindRadius = -1f;
        [Tooltip("★5 동남풍 느려짐 (제갈량 55%)")]
        public float star5WindSlow = -1f;
        [Tooltip("★3 깃발 지속 시간 (잔 다르크 8초)")]
        public float star3BannerDuration = -1f;
        [Tooltip("★5 깃발 안 기본 공격력 보너스 (잔 다르크 30%)")]
        public float star5BannerAttackBonus = -1f;
        [Tooltip("★3 준비한 기본 공격 피해 보너스 (선덕여왕 15%)")]
        public float star3ObservationAttackBonus = -1f;
        [Tooltip("★5 준비한 기본 공격 피해 보너스 (선덕여왕 30%)")]
        public float star5ObservationAttackBonus = -1f;
        [Tooltip("★3 준비 공격 밀침 거리 (부디카 0.9)")]
        public float star3ReclaimKnockback = -1f;
        [Tooltip("★5 준비 공격 피해 보너스 (부디카 20%)")]
        public float star5ReclaimAttackBonus = -1f;
        [Tooltip("★3 토르 충격파 반경")]
        public float star3ThunderRadius = -1f;
        [Tooltip("★5 토르 충격파 피해 배수")]
        public float star5ThunderCoefficient = -1f;
        [Tooltip("★3 표식 둔화 지속 시간")]
        public float star3MarkDuration = -1f;
        [Tooltip("★5 표식 둔화 세기")]
        public float star5MarkSlow = -1f;
        [Tooltip("★3 스사노오 첫 베기 피해 배수")]
        public float star3WindFirstCoefficient = -1f;
        [Tooltip("★5 스사노오 후속 베기 피해 배수")]
        public float star5WindFollowCoefficient = -1f;
        [Tooltip("★3 아테나 방패 준비 시간")]
        public float star3ShieldPrepareSeconds = -1f;
        [Tooltip("★5 아테나 표식 기본 공격 추가 피해 배수")]
        public float star5OwlBonusCoefficient = -1f;
        [Tooltip("★3 수호상 한 타격의 공격력 배수")]
        public float star3GuardianCoefficient = -1f;
        [Tooltip("★5 수호상 한 번이 공격할 최대 적 수 (원래 표적 포함)")]
        public int star5GuardianTargets = -1;

        [Header("전용")]
        [Tooltip("이 영웅만 쓰는 액티브. 첫 레벨업에 조금 더 잘 나온다 (Skills.md 3장)")]
        public ActiveSkillData exclusiveActive;

        [Header("해금 (Heroes.md 2장)")]
        [Tooltip("이 구간 보스를 처음 깨면 무료 해금. 0이면 처음부터")]
        public int unlockAfterBossChapter = 0;
        [Tooltip("보석 또는 10판 기념 무료 선택으로 연다. 켜면 구간 보스 해금은 쓰지 않는다")]
        public bool gemsOnly;
        [Tooltip("바로 해금하는 보석 값")]
        public int unlockGems = 1500;

        [Header("화면 (임시 도형)")]
        public Color color = new Color(0.95f, 0.75f, 0.2f);

        [Header("3D (Art3D-Plan)")]
        [Tooltip("3D 캐릭터 프리팹 (Assets/Art/3D/Characters). 비어 있으면 임시 도형")]
        public GameObject model3D;
        public float modelHeight = 1.5f;

        public string ClassLabel
        {
            get
            {
                switch (heroClass)
                {
                    case HeroClass.Warrior: return "전사";
                    case HeroClass.Gunner: return "총잡이";
                    case HeroClass.Mage: return "법사";
                    case HeroClass.Summoner: return "소환사";
                    default: return "궁수";
                }
            }
        }
    }
}
