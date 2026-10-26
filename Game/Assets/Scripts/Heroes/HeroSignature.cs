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

        private HeroSignature(float firstShotBonus, bool firstShotPierce, float damageReduction, float lowHpAttackBonus,
            float cannonRadius, int cannonEvery, int chainBounces, float chainFalloff)
        {
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

            return new HeroSignature(firstShot, s5 && hero.star5FirstShotPierce, reduction, lowHp, radius, every, bounces, falloff);
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
            if (hero.star5FirstShotBonus >= 0f) s5 = $"첫 발 피해 +{hero.star5FirstShotBonus * 100f:0}%" + (hero.star5FirstShotPierce ? ", 첫 발 관통" : string.Empty);
            if (hero.star5LowHpAttackBonus >= 0f) s5 = $"체력 50% 아래 공격력 +{hero.star5LowHpAttackBonus * 100f:0}%";
            if (hero.star5CannonEvery > 0) s5 = $"{hero.star5CannonEvery}발마다 포탄";
            if (hero.star5NoFalloff) s5 = "튕겨도 피해가 줄지 않음";
            return $"★3 {s3 ?? "—"} · ★5 {s5 ?? "—"}";
        }
    }
}
