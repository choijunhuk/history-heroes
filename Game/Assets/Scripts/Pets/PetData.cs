using UnityEngine;

namespace HistoryHeroes.Pets
{
    /// <summary>펫마다 반응하는 행동이 다르다 (Pets 3장): 멈춤 유지 / 이동 지속 / 투사체 접근 / 항상</summary>
    public enum PetKind
    {
        Hawk,     // 🦅 사냥매: 2초 멈추면 급강하 + 사냥 표식
        RedHare,  // 🐎 적토마: 1.5초 움직이면 질주·들이받기, 멈추면 기세
        Marengo,  // 🐴 마렝고: 날아오는 투사체 막아서기
        Dove,     // 🕊️ 흰 비둘기: 보석 물어오기, 보물 찾기, 멈추면 약한 전기
    }

    public enum PetSource
    {
        FirstDungeonClear,  // 첫 던전 클리어 (튜토리얼 보스가 생기기 전 임시, v0.5-Plan)
        BossFirstClear,     // sourceChapter 구간 보스 최초 격파
        SurvivalFirstClear, // 서바이벌 첫 클리어
    }

    /// <summary>펫 하나 (Pets 3·7장). 숫자는 PetController 쪽 행동과 PetTable 성장에 있다</summary>
    [CreateAssetMenu(menuName = "History Heroes/Pet", fileName = "Pet_")]
    public sealed class PetData : ScriptableObject
    {
        public string id;
        public string displayName;
        public PetKind kind;
        [Tooltip("인연 영웅 (같이 쓰면 공격력·체력 +2%)")]
        public string bondHeroId;
        public string bondHeroName;
        public PetSource source;
        public int sourceChapter;
        public Color color = Color.white;
        [Header("3D (Art3D-Plan 5번)")]
        [Tooltip("3D 모델 프리팹 (Assets/Art/3D/Pets). 비어 있으면 임시 도형")]
        public GameObject model3D;
        [Tooltip("게임 속 키 (유닛)")]
        public float modelHeight = 0.7f;

        [Tooltip("반응하는 행동 한 줄 (\"멈춰서 버티기\")")]
        public string trigger;
        [TextArea] public string effect;
        public string awaken5;
        public string awaken10;
        [Tooltip("📜 사실")]
        [TextArea] public string fact;
        [Tooltip("📖 전승 (없으면 비움)")]
        [TextArea] public string legend;

        /// <summary>얻는 방법 (없는 펫 목록에 보여 준다)</summary>
        public string SourceText
        {
            get
            {
                switch (source)
                {
                    case PetSource.FirstDungeonClear: return "튜토리얼 보스를 깨면";
                    case PetSource.BossFirstClear: return $"{sourceChapter}구간 보스를 처음 깨면";
                    case PetSource.SurvivalFirstClear: return "서바이벌을 처음 클리어하면";
                    default: return "";
                }
            }
        }
    }
}
