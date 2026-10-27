using HistoryHeroes.Records;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 설정을 기기에 건다 (로비·전투가 열릴 때, 설정을 바꿀 때). 프레임 30/60 · 그래픽 품질 3단.
    /// 흔들림·피해 숫자·번쩍임·조이스틱은 그 기능이 그때그때 설정을 읽는다.
    /// </summary>
    public static class SettingsApplier
    {
        public static void Apply(GameSettings s)
        {
            if (s == null) return;
            QualitySettings.vSyncCount = 0; // 모바일: targetFrameRate가 정한다
            Application.targetFrameRate = s.frame60 ? 60 : 30;
            ApplyGraphics(s.graphics);
        }

        /// <summary>
        /// 그래픽 품질 (v1.0 17번, 3D 성능 Art3D-Plan 8번): 0 낮음 = 그림자 끔 · 해상도 75%, 1 보통 = 그림자 · 90%, 2 높음 = 그림자 · 100% · 계단 줄임(MSAA 2배).
        /// 그림자는 전투 카메라마다 끈다. 해상도·계단 줄임은 렌더 설정 에셋을 바꾸므로 **빌드에서만** (에디터에서 바꾸면 에셋 파일이 바뀐다).
        /// </summary>
        public static void ApplyGraphics(int level)
        {
            level = Mathf.Clamp(level, 0, 2);
            Camera cam = Camera.main;
            if (cam != null && cam.TryGetComponent(out UniversalAdditionalCameraData data)) data.renderShadows = level > 0;
            StageLook.SetPost(level > 0); // 낮음: 화면 후처리(색 보정·빛 번짐) 끔
#if !UNITY_EDITOR
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.supportsHDR = level > 0; // 빛 번짐은 밝은 색(1 넘는 값)이 필요하다. 낮음은 후처리가 없으니 끈다
                urp.renderScale = RenderScale(level);
                urp.msaaSampleCount = level >= 2 ? 2 : 1;
                urp.shadowDistance = level >= 2 ? 40f : 28f;
            }
#endif
        }

        /// <summary>품질별 해상도 배율</summary>
        public static float RenderScale(int level) => level <= 0 ? 0.75f : level == 1 ? 0.9f : 1f;

        /// <summary>설정 화면 글</summary>
        public static string GraphicsName(int level) => level <= 0 ? "낮음 (빠르게)" : level == 1 ? "보통" : "높음 (선명하게)";
    }
}
