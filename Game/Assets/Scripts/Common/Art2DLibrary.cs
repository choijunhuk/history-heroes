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
            [Tooltip("비침 (그림자 쌍둥이의 맞지 않는 쪽 0.45)")]
            public float alpha = 1f;
        }

        public List<Character> characters = new List<Character>();

        [Header("방 (1구간 · 구간마다 아래 배열이 있으면 그것)")]
        public Sprite floor;
        public Sprite wallTop;
        public Sprite wallSide;
        [Tooltip("구간 1~5 바닥 · 위아래 벽 · 옆 벽 (비면 1구간 것)")]
        public Sprite[] floors = new Sprite[5];
        public Sprite[] wallTops = new Sprite[5];
        public Sprite[] wallSides = new Sprite[5];

        [Header("보스 경기장 · 서바이벌 맵")]
        public Sprite bossFloor;
        public Sprite bossRing;
        [Tooltip("서바이벌 맵 id(field · city · factory) 순서")]
        public Sprite[] grounds = new Sprite[3];

        [Header("함정 바닥 (TrapZone 효과마다)")]
        public Sprite fireVent;
        public Sprite conveyor;
        public Sprite steam;
        public Sprite rockfall;
        public Sprite vines;
        public Sprite oblivion;
        [Tooltip("서바이벌 들판 바위 무리 · 낮은 장애물")]
        public Sprite rockCluster;
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

        public Sprite FloorFor(int chapter) => Pick(floors, chapter, floor);
        public Sprite WallTopFor(int chapter) => Pick(wallTops, chapter, wallTop);
        public Sprite WallSideFor(int chapter) => Pick(wallSides, chapter, wallSide);

        private static Sprite Pick(Sprite[] list, int chapter, Sprite fallback)
        {
            int i = chapter - 1;
            return list != null && i >= 0 && i < list.Length && list[i] != null ? list[i] : fallback;
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
