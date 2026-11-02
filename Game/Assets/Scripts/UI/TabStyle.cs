using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 탭 단추 모양 하나로 (2026-11-02 개발자: "상점에서 유물 뽑기 혼자 색이 다르다").
    /// 지금 보는 탭 = 금색, 나머지 = 파랑 (단추 기본 파랑과 같은 색). 잠긴 탭도 같은 파랑 + 자물쇠 그림 ("Lock" 자식).
    /// 예전엔 지금 보는 탭을 interactable=false로 둬서 '못 누름' 틴트가 덮여 오히려 어둡게 보였고,
    /// 잠긴 탭은 거의 검정으로 칠해 고장 난 것처럼 보였다 → 탭은 늘 눌리게 두고 색으로만 고른 것을 보여 준다.
    /// </summary>
    public static class TabStyle
    {
        public static readonly Color Active = UiTone.Gold;
        public static readonly Color Idle = new Color(0.26f, 0.5f, 0.9f);

        public static void Apply(Button tab, bool active, bool locked = false)
        {
            if (tab == null) return;
            tab.interactable = true;
            if (tab.image != null) tab.image.color = active ? Active : Idle;
            Transform lockIcon = tab.transform.Find("Lock");
            if (lockIcon != null) lockIcon.gameObject.SetActive(locked);
        }
    }
}
