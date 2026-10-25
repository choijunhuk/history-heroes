namespace HistoryHeroes.Economy
{
    /// <summary>결과 화면에 쓰는 보상 글자</summary>
    public static class RunText
    {
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
