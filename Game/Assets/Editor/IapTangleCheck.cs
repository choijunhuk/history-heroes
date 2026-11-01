using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 안드로이드 빌드 전에 영수증 검사 키(GooglePlayTangle)가 있는지 본다. 없으면 경고만 한다 — 빌드는 그대로 (플레이 콘솔 전에 만드는 GitHub APK).
    /// 키가 없는 빌드는 상점 카드가 "결제 준비 중"이 되고 아무것도 팔지 않는다 (IapService).
    /// </summary>
    public sealed class IapTangleCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android || Populated()) return;
            Debug.LogWarning("[IAP] 영수증 검사 키(GooglePlayTangle)가 없어요 → 이 빌드는 실제 결제를 받지 않아요 (상점 카드 \"결제 준비 중\"). " +
                             "실제 판매 전에 Services → In-App Purchasing → Receipt Validation Obfuscator로 만들고 다시 빌드하세요 (출시 점검표 3장 5번)");
        }

        private static bool Populated()
        {
            foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type tangle = asm.GetType("UnityEngine.Purchasing.Security.GooglePlayTangle", false);
                if (tangle == null) continue;
                object populated = tangle.GetField("IsPopulated")?.GetValue(null) ?? tangle.GetProperty("IsPopulated")?.GetValue(null);
                return populated is bool ok && ok;
            }

            return false;
        }
    }
}
