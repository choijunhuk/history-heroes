using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Common
{
    /// <summary>2D 그림 목록 (Resources/Art2DLibrary, 에디터 Art2DImport.BuildAll이 만든다). Art2D 참고</summary>
    [CreateAssetMenu(menuName = "History Heroes/Art 2D Library", fileName = "Art2DLibrary")]
    public sealed class Art2DLibrary : ScriptableObject
    {
        [System.Serializable]
        public sealed class Character
        {
            [Tooltip("3D 프리팹 이름 (Guan3D, E_Charger3D …)")]
            public string modelName;
            public Sprite front;
            public Sprite back;
            [Tooltip("오른쪽을 본 옆모습 (왼쪽은 뒤집는다)")]
            public Sprite side;
            [Tooltip("그림 키 = 3D 모델 키 × 이 배율 (그림에 무기·장식이 위로 솟은 만큼)")]
            public float heightScale = 1.1f;
        }

        public List<Character> characters = new List<Character>();

        [Header("방 (1구간)")]
        public Sprite floor;
        public Sprite wallTop;
        public Sprite wallSide;
        public Sprite doorClosed;
        public Sprite doorOpen;
        public Sprite pillar;
        public Sprite crate;
        public Sprite brazier;
        public Sprite banner;
        public Sprite water;
        public Sprite moss;
        public Sprite bones;
        public Sprite healOrb;
        [Tooltip("1구간 가시덤불 함정 바닥")]
        public Sprite thorns;

        [System.Serializable]
        public sealed class Prop
        {
            [Tooltip("3D 프리팹 이름 (Berry_Bush …)")]
            public string modelName;
            public Sprite sprite;
        }

        [Tooltip("구간 장치 · 소품: 3D 프리팹 이름 → 그림 (RoomDevices.FitModel이 찾는다)")]
        public List<Prop> props = new List<Prop>();

        [Header("UI")]
        public Sprite cardFrame;
        public Sprite cardFrameRare;
        public Sprite cardFrameLegend;
        public Sprite panel;
        public Sprite buttonPrimary;
        public Sprite buttonSecondary;
        public Sprite ribbon;

        public Sprite PropFor(string modelName)
        {
            foreach (Prop p in props)
            {
                if (p != null && p.modelName == modelName && p.sprite != null) return p.sprite;
            }

            return null;
        }

        public Character FindCharacter(string modelName)
        {
            foreach (Character c in characters)
            {
                if (c != null && c.modelName == modelName && c.front != null) return c;
            }

            return null;
        }
    }
}
