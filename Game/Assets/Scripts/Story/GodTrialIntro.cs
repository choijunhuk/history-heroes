using UnityEngine;

namespace HistoryHeroes.Story
{
    /// <summary>토르 시련 입장 대사. 신화 속 대사를 옮기지 않은 게임 창작이다.</summary>
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
    }
}
