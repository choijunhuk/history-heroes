using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Heroes;
using HistoryHeroes.Skills;

namespace HistoryHeroes.Economy
{
    /// <summary>결과 화면에 쓰는 보상 글자</summary>
    public static class RunText
    {
        /// <summary>피해 출처 이름: 기본 공격 / 영웅 대표 효과 / 스킬 이름</summary>
        public static string SourceName(string sourceId, SkillDatabase database, HeroData hero)
        {
            if (sourceId == DamageLog.BasicSource) return "기본 공격";
            if (sourceId != null && sourceId.StartsWith("sig_")) return hero != null ? hero.signatureName : "대표 효과";
            if (sourceId != null && sourceId.StartsWith("pet_")) return Pets.PetService.SourceName(sourceId);
            SkillData data = database != null ? database.Find(sourceId) : null;
            return data != null ? data.displayName : "기타";
        }

        /// <summary>"기본 공격 41% · 낙뢰 32% · 불꽃 장판 12% …" (상위 count개, 조사 A5)</summary>
        public static string TopDamage(SkillDatabase database, HeroData hero, int count = 5)
        {
            List<KeyValuePair<string, float>> shares = DamageLog.Shares();
            var parts = new List<string>();
            for (int i = 0; i < shares.Count && i < count; i++)
            {
                parts.Add($"{SourceName(shares[i].Key, database, hero)} {UnityEngine.Mathf.RoundToInt(shares[i].Value * 100f)}%");
            }

            return parts.Count > 0 ? string.Join(" · ", parts) : "피해 없음";
        }

        /// <summary>예: "계정 경험치 +80" / "계정 경험치 +80 · Lv 2 → 3 (골드 +400 · 보석 +20) · 하트 가득"</summary>
        public static string Level(AccountLevel.Result r)
        {
            if (r.Exp <= 0 && !r.LeveledUp) return r.ToLevel >= AccountLevel.MaxLevel ? "계정 레벨 최대" : "계정 경험치 +0";
            string line = $"계정 경험치 +{r.Exp}";
            if (r.LeveledUp) line += $" · Lv {r.FromLevel} → {r.ToLevel} ({r.Reward}) · 하트 가득";
            return line;
        }
    }
}
