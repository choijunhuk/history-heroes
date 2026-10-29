using UnityEngine;

namespace HistoryHeroes.Heroes
{
    /// <summary>
    /// 별(각성)까지 반영한 대표 효과 값 (Stats 6-1). HeroData는 공유 에셋이라 고치지 않고, 판마다 이 값을 만들어 쓴다.
    /// ★3·★5 강화는 4-3 "켜진 동안 +30% 이하" 안이다.
    /// </summary>
    public readonly struct HeroSignature
    {
        public readonly float FirstShotBonus;
        public readonly bool FirstShotPierce;
        public readonly float DamageReduction;
        public readonly float LowHpAttackBonus;
        public readonly float CannonRadius;
        public readonly int CannonEvery;
        public readonly int ChainBounces;
        public readonly float ChainFalloff;
        /// <summary>이순신 철갑: 멈춰 있는 동안 받는 피해 감소 · 쌓기 상한(공격력 배수)</summary>
        public readonly float StopGuard;
        public readonly float StopGuardCap;
        /// <summary>이성계 백발백중: 3단 피해 배수 · 3단까지 시간 (0이면 모으지 않는 영웅)</summary>
        public readonly float ChargeFull;
        public readonly float ChargeFullTime;
        /// <summary>다빈치 작업대: 비행 기계 한 발 · 최대 수 (0이면 없음)</summary>
        public readonly float MachineDamage;
        public readonly int MaxMachines;
        /// <summary>제갈량 동남풍: 반경 · 느려짐 (0이면 없음)</summary>
        public readonly float WindRadius;
        public readonly float WindSlow;
        /// <summary>잔 다르크 성녀의 깃발: 지속 시간 · 기본 공격력 보너스 (0이면 없음)</summary>
        public readonly float BannerDuration;
        public readonly float BannerAttackBonus;
        /// <summary>선덕여왕 별자리 관측: 준비한 기본 공격 한 번의 피해 보너스</summary>
        public readonly float ObservationAttackBonus;

        private HeroSignature(float firstShotBonus, bool firstShotPierce, float damageReduction, float lowHpAttackBonus,
            float cannonRadius, int cannonEvery, int chainBounces, float chainFalloff, float stopGuard = 0f, float stopGuardCap = 0f,
            float chargeFull = 0f, float chargeFullTime = 0f, float machineDamage = 0f, int maxMachines = 0, float windRadius = 0f, float windSlow = 0f,
            float bannerDuration = 0f, float bannerAttackBonus = 0f, float observationAttackBonus = 0f)
        {
            ObservationAttackBonus = observationAttackBonus;
            BannerDuration = bannerDuration;
            BannerAttackBonus = bannerAttackBonus;
            WindRadius = windRadius;
            WindSlow = windSlow;
            MachineDamage = machineDamage;
            MaxMachines = maxMachines;
            ChargeFull = chargeFull;
            ChargeFullTime = chargeFullTime;
            StopGuard = stopGuard;
            StopGuardCap = stopGuardCap;
            FirstShotBonus = firstShotBonus;
            FirstShotPierce = firstShotPierce;
            DamageReduction = damageReduction;
            LowHpAttackBonus = lowHpAttackBonus;
            CannonRadius = cannonRadius;
            CannonEvery = cannonEvery;
            ChainBounces = chainBounces;
            ChainFalloff = chainFalloff;
        }

        public static HeroSignature For(HeroData hero, int stars)
        {
            if (hero == null) return default;
            bool s3 = stars >= 3, s5 = stars >= 5;

            float firstShot = hero.firstShotBonus;
            if (s3 && hero.star3FirstShotBonus >= 0f) firstShot = hero.star3FirstShotBonus;
            if (s5 && hero.star5FirstShotBonus >= 0f) firstShot = hero.star5FirstShotBonus;

            float reduction = s3 && hero.star3DamageReduction >= 0f ? hero.star3DamageReduction : hero.damageReduction;
            float lowHp = s5 && hero.star5LowHpAttackBonus >= 0f ? hero.star5LowHpAttackBonus : hero.lowHpAttackBonus;
            float radius = s3 && hero.star3CannonRadiusScale > 0f ? hero.cannonRadius * hero.star3CannonRadiusScale : hero.cannonRadius;
            int every = s5 && hero.star5CannonEvery > 0 && hero.cannonEvery > 0 ? hero.star5CannonEvery : hero.cannonEvery;
            int bounces = hero.chainBounces + (s3 && hero.chainBounces > 0 ? Mathf.Max(0, hero.star3ExtraBounces) : 0);
            float falloff = s5 && hero.star5NoFalloff ? 0f : hero.chainFalloff;

            float guard = s3 && hero.star3StopGuard >= 0f ? hero.star3StopGuard : hero.stopGuard;
            float guardCap = s5 && hero.star5StopGuardCap > 0f ? hero.star5StopGuardCap : hero.stopGuardCap;
            float chargeFull = hero.chargeShot ? (s3 && hero.star3ChargeFull > 0f ? hero.star3ChargeFull : hero.chargeFullMultiplier) : 0f;
            float chargeTime = hero.chargeShot ? (s5 && hero.star5ChargeFullTime > 0f ? hero.star5ChargeFullTime : hero.chargeTimes.z) : 0f;
            float machine = hero.workbench ? (s3 && hero.star3MachineDamage > 0f ? hero.star3MachineDamage : hero.machineDamage) : 0f;
            int machines = hero.workbench ? (s5 && hero.star5MaxMachines > 0 ? hero.star5MaxMachines : hero.maxMachines) : 0;
            float observationBonus = hero.observationAttackBonus;
            if (s3 && hero.star3ObservationAttackBonus >= 0f) observationBonus = hero.star3ObservationAttackBonus;
            if (s5 && hero.star5ObservationAttackBonus >= 0f) observationBonus = hero.star5ObservationAttackBonus;
            return new HeroSignature(firstShot, s5 && hero.star5FirstShotPierce, reduction, lowHp, radius, every, bounces, falloff, guard, guardCap, chargeFull, chargeTime, machine, machines,
                s3 && hero.star3WindRadius > 0f ? hero.star3WindRadius : hero.windRadius, s5 && hero.star5WindSlow > 0f ? hero.star5WindSlow : hero.windSlow,
                hero.bannerDeploySeconds > 0f ? (s3 && hero.star3BannerDuration > 0f ? hero.star3BannerDuration : hero.bannerDuration) : 0f,
                hero.bannerDeploySeconds > 0f ? (s5 && hero.star5BannerAttackBonus >= 0f ? hero.star5BannerAttackBonus : hero.bannerAttackBonus) : 0f,
                hero.observationSeconds > 0f ? observationBonus : 0f);
        }

        /// <summary>별 강화 설명 ("★3 첫 발 피해 +75% · ★5 첫 발 +100%, 관통")</summary>
        public static string StarText(HeroData hero)
        {
            if (hero == null) return string.Empty;
            string s3 = null, s5 = null;
            if (hero.star3FirstShotBonus >= 0f) s3 = $"첫 발 피해 +{hero.star3FirstShotBonus * 100f:0}%";
            if (hero.star3DamageReduction >= 0f) s3 = $"받는 피해 -{hero.star3DamageReduction * 100f:0}%";
            if (hero.star3CannonRadiusScale > 0f) s3 = $"포탄 범위 +{(hero.star3CannonRadiusScale - 1f) * 100f:0}%";
            if (hero.star3ExtraBounces > 0) s3 = $"튕김 +{hero.star3ExtraBounces}";
            if (hero.star3StopGuard >= 0f) s3 = $"멈춤 받는 피해 -{hero.star3StopGuard * 100f:0}%";
            if (hero.star3ChargeFull > 0f) s3 = $"3단 화살 피해 ×{hero.star3ChargeFull:0.#}";
            if (hero.star3MachineDamage > 0f) s3 = $"비행 기계 한 발 {hero.star3MachineDamage * 100f:0}%";
            if (hero.star3WindRadius > 0f) s3 = $"동남풍 반경 {hero.star3WindRadius:0.#}";
            if (hero.star3BannerDuration > 0f) s3 = $"깃발 지속 {hero.star3BannerDuration:0.#}초";
            if (hero.observationSeconds > 0f && hero.star3ObservationAttackBonus >= 0f) s3 = $"관측 공격 피해 +{hero.star3ObservationAttackBonus * 100f:0}%";
            if (hero.star5FirstShotBonus >= 0f) s5 = $"첫 발 피해 +{hero.star5FirstShotBonus * 100f:0}%" + (hero.star5FirstShotPierce ? ", 첫 발 관통" : string.Empty);
            if (hero.star5LowHpAttackBonus >= 0f) s5 = $"체력 50% 아래 공격력 +{hero.star5LowHpAttackBonus * 100f:0}%";
            if (hero.star5CannonEvery > 0) s5 = $"{hero.star5CannonEvery}발마다 포탄";
            if (hero.star5NoFalloff) s5 = "튕겨도 피해가 줄지 않음";
            if (hero.star5StopGuardCap > 0f) s5 = $"쌓기 상한 공격력 {hero.star5StopGuardCap * 100f:0}%";
            if (hero.star5ChargeFullTime > 0f) s5 = $"3단까지 {hero.star5ChargeFullTime:0.#}초";
            if (hero.star5MaxMachines > 0) s5 = $"비행 기계 최대 {hero.star5MaxMachines}대";
            if (hero.star5WindSlow > 0f) s5 = $"적 탄 {hero.star5WindSlow * 100f:0}% 느리게";
            if (hero.star5BannerAttackBonus >= 0f) s5 = $"깃발 안 기본 공격력 +{hero.star5BannerAttackBonus * 100f:0}%";
            if (hero.observationSeconds > 0f && hero.star5ObservationAttackBonus >= 0f) s5 = $"관측 공격 피해 +{hero.star5ObservationAttackBonus * 100f:0}%";
            return $"★3 {s3 ?? "—"} · ★5 {s5 ?? "—"}";
        }
    }
}
