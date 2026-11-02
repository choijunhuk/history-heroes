using UnityEngine;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 화면 공통 색 (2026-11-02 개발자: "칙칙하다 · 모바일 게임스럽지 않다", Docs/Design/UI-Research-2026-11-02.md).
    /// 예전 줄 · 칸 · 탭 색은 거의 검정(밝기 0.1~0.16)이라 화면 전체가 어두운 회색으로 보였다.
    /// 이제 바탕보다 밝고 채도 있는 파랑 계열 카드, 역할 색 (완료 초록 · 고르기 금색), 못 누름만 어둡게.
    /// </summary>
    public static class UiTone
    {
        /// <summary>보통 줄 · 칸 (선명한 파랑)</summary>
        public static readonly Color Row = new Color(0.24f, 0.4f, 0.74f);
        /// <summary>안 고른 탭 · 칸 (조금 어둡게 — 고른 쪽이 튀게)</summary>
        public static readonly Color Idle = new Color(0.2f, 0.3f, 0.54f);
        /// <summary>못 누름 · 잠김 · 안내 줄 (같은 파랑을 흐리게 — 거의 검정이면 고장 난 것처럼 보였다)</summary>
        public static readonly Color Muted = new Color(0.2f, 0.27f, 0.45f);
        /// <summary>고른 줄 · 칸: 보통 줄보다 밝게 (예전엔 더 어두워 '못 누름'처럼 보였다, 2026-11-02 화면 점검)</summary>
        public static readonly Color Selected = new Color(0.36f, 0.62f, 1f);
        /// <summary>받기 · 준비됨 · 지금 보는 탭</summary>
        public static readonly Color Gold = new Color(1f, 0.72f, 0.2f);
        /// <summary>받음 · 끝남 (초록을 조금 흐리게)</summary>
        public static readonly Color Done = new Color(0.2f, 0.52f, 0.34f);

        /// <summary>
        /// 화면 스크립트가 옛 어두운 색을 주면 같은 뜻의 밝은 색으로 바꾼다 (목록 줄은 ButtonList가 여기를 지난다).
        /// 어둡고 색이 거의 없으면 보통 · 못 누름 파랑, 색이 있으면(초록 · 갈색 · 빨강) 그 색을 밝고 진하게.
        /// </summary>
        public static Color Lift(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (c.a < 0.01f || v >= 0.4f || c == Row || c == Idle || c == Muted) return c; // 투명 (글만 있는 안내 줄) · 이미 밝은 색 · 바꾼 색은 그대로 (두 번 지나도 같다)
            Color o;
            if (s < 0.35f) o = v < 0.13f ? Muted : v < 0.22f ? Row : Idle;
            else o = Color.HSVToRGB(h, Mathf.Max(s, 0.6f), Mathf.Max(v * 2.4f, 0.55f));
            o.a = c.a;
            return o;
        }
    }
}
