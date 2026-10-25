namespace HistoryHeroes.Common
{
    /// <summary>한국어 조사 고르기 ("방어력이" / "회전 칼날이" / "멀티샷이"…). 받침이 있으면 앞의 것, 없으면 뒤의 것.</summary>
    public static class KoreanText
    {
        public static bool HasFinalConsonant(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            char last = word[word.Length - 1];
            if (last < 0xAC00 || last > 0xD7A3) return false; // 한글 음절이 아니면 받침 없음으로 본다
            return (last - 0xAC00) % 28 != 0;
        }

        /// <summary>예: Josa("방어력", "이", "가") → "방어력이"</summary>
        public static string Josa(string word, string withFinal, string withoutFinal)
        {
            return word + (HasFinalConsonant(word) ? withFinal : withoutFinal);
        }
    }
}
