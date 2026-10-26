using HistoryHeroes.Records;
using UnityEngine;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 설정을 기기에 건다 (로비·전투가 열릴 때, 설정을 바꿀 때). 지금은 프레임 30/60.
    /// 흔들림·피해 숫자·번쩍임·조이스틱은 그 기능이 그때그때 설정을 읽는다.
    /// </summary>
    public static class SettingsApplier
    {
        public static void Apply(GameSettings s)
        {
            if (s == null) return;
            QualitySettings.vSyncCount = 0; // 모바일: targetFrameRate가 정한다
            Application.targetFrameRate = s.frame60 ? 60 : 30;
        }
    }
}
