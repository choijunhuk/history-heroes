using HistoryHeroes.Records;
using UnityEngine;

namespace HistoryHeroes.Story
{
    /// <summary>
    /// 망각의 왕 첫 격파 뒤 클리오의 예고 (GodsVsHumanity 2장 "이야기 노출"): 되찾은 역사를 심판하려는 존재가 있다.
    /// 로비에 돌아왔을 때 한 번만. 대사는 전부 게임 창작이고, 신 이름 · 신앙을 직접 말하지 않는다.
    /// </summary>
    public static class GodsTeaser
    {
        public const string KingBossId = "oblivion_king";

        public static readonly string[] Scenes =
        {
            "망각의 왕이 쓰러지고,\n되찾은 기록서가 다시 빛났다.\n\n<size=80%>그 빛을 하늘 위에서\n조용히 내려다보는 눈들이 있었다.</size>",
            "<color=#FFD966>???</color>  \"전쟁과 파괴를 되풀이한 역사가\n시간의 틈을 넓혔다.\n\n망각을 막으려면 — 그 역사를 봉인해야 한다.\"",
            "<color=#FFD966>클리오</color>  \"신들이… 우리 기록을 심판하려 해요.\n실패도, 서로 지킨 기억도 전부 우리 역사예요.\n기록 수호자님, 다음 페이지를 지켜 주세요.\"\n\n<size=70%><color=#9AA4B2>— 이야기는 계속됩니다</color></size>",
        };

        public static readonly Color[] Tints =
        {
            new Color(0.07f, 0.06f, 0.12f), new Color(0.12f, 0.1f, 0.04f), new Color(0.06f, 0.1f, 0.14f),
        };

        /// <summary>망각의 왕을 깼고 아직 예고를 안 봤다</summary>
        public static bool Pending(SaveData d) => d != null && !d.godsTeaserSeen && d.defeatedBosses != null && d.defeatedBosses.Contains(KingBossId);
    }
}
