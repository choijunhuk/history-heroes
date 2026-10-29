using UnityEngine;

namespace HistoryHeroes.Story
{
    /// <summary>시련 입장 대사. 신화 속 대사를 옮기지 않은 게임 창작이다.</summary>
    public static class GodTrialIntro
    {
        public static readonly string[] Scenes =
        {
            "<color=#FFD966>클리오</color>  \"이 페이지를 지울지, 우리가 지킬지 보여 줘요.\"",
            "<color=#9FD8FF>토르</color>  \"너희가 지킨 기록의 무게를 보여 다오. 내 망치로 시험하겠다.\"",
        };

        public static readonly Color[] Tints =
        {
            new Color(0.06f, 0.1f, 0.14f),
            new Color(0.1f, 0.1f, 0.13f),
        };

        private static readonly string[] AnubisScenes =
        {
            "<color=#FFD966>클리오</color>  \"기록은 심판 앞에서도 스스로 말할 수 있어요.\"",
            "<color=#E5C879>아누비스</color>  \"지켜 온 기억의 무게를 보여 다오.\"",
        };

        private static readonly string[] ZeusScenes =
        {
            "<color=#FFD966>클리오</color>  \"번개 사이에서 멈출 자리를 찾으세요.\"",
            "<color=#E8EDF5>제우스</color>  \"흔들리는 길에서도 너희의 답을 보겠다.\"",
        };

        public static string[] For(string trialId) => trialId == Modes.GodTrialRules.AnubisTrialId ? AnubisScenes
            : trialId == Modes.GodTrialRules.ZeusTrialId ? ZeusScenes
            : trialId == Modes.GodTrialRules.ThorTrialId ? Scenes : System.Array.Empty<string>();

        public static Color[] TintsFor(string trialId) => For(trialId).Length > 0 ? Tints : System.Array.Empty<Color>();
    }
}
