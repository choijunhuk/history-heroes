using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 2D 그림 시험판 (개발자 2026-10-28: "그래픽이 이정도 밖에 안된다면 3d는 보관하고 2d로 한번", 결정 "코덱스 이미지 2D 시험판").
    /// 코덱스 이미지 생성으로 만든 그림(앞 · 뒤 · 옆 한 장씩)을 3D 모델 대신 쓴다. 판정은 2D 그대로라 그림만 바뀐다.
    /// 켜고 끄기는 기기 설정(PlayerPrefs "art2d") — 시험판이라 설정 화면에는 아직 없다 (QA: Art2D.SetEnabled).
    /// 그림 목록 = Resources/Art2DLibrary (에디터 Art2DImport.BuildAll이 만든다). 그림이 없는 캐릭터는 3D 그대로
    /// </summary>
    public static class Art2D
    {
        private const string Key = "art2d";
        private static int _enabled = -1;
        private static Art2DLibrary _library;
        private static bool _loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _enabled = -1;
            _library = null;
            _loaded = false;
        }

        public static bool Enabled
        {
            get
            {
                if (_enabled < 0) _enabled = PlayerPrefs.GetInt(Key, 0);
                return _enabled == 1 && Library != null;
            }
        }

        public static void SetEnabled(bool on)
        {
            _enabled = on ? 1 : 0;
            PlayerPrefs.SetInt(Key, _enabled);
            PlayerPrefs.Save();
        }

        public static Art2DLibrary Library
        {
            get
            {
                if (_loaded) return _library;
                _loaded = true;
                _library = Resources.Load<Art2DLibrary>("Art2DLibrary");
                return _library;
            }
        }

        /// <summary>3D 프리팹 이름(예: "Guan3D", "E_Charger3D")에 맞는 2D 그림. 없으면 null</summary>
        public static Art2DLibrary.Character Find(string modelName)
        {
            if (!Enabled || string.IsNullOrEmpty(modelName)) return null;
            return Library.FindCharacter(modelName);
        }

        /// <summary>2D에서 그림 정렬: 아래(화면 앞)에 있을수록 위에 그린다</summary>
        public static int SortOrder(float y) => 100 - Mathf.RoundToInt(y * 4f);
    }
}
