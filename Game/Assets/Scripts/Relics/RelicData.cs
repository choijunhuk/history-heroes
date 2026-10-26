using UnityEngine;

namespace HistoryHeroes.Relics
{
    /// <summary>어떻게 얻나 (Relics 4장). 유물마다 획득처는 딱 하나</summary>
    public enum RelicSource
    {
        HeroFirstClear = 0, // 그 영웅으로 던전 첫 클리어 (sourceValue = 영웅 ID)
        BossFirstClear = 1, // N구간 보스 최초 격파 (sourceChapter)
        ChapterStars = 2,   // N구간 별 6개 (던전 3 + 보스 3)
        Survival = 3,       // 서바이벌 마일스톤 (v0.5)
        Achievement = 4,    // 업적 (sourceValue = 업적 키)
    }

    /// <summary>
    /// 유물 하나 (Relics 3장, 출시 20개 = 세트 5 × 4). 끼우는 게 아니라 모으면 바로 적용되는 컬렉션.
    /// 사실 글은 Heroes.md의 사실/전승/각색 표시 규칙을 따른다.
    /// </summary>
    [CreateAssetMenu(menuName = "History Heroes/Relic", fileName = "Relic_")]
    public sealed class RelicData : ScriptableObject
    {
        [Tooltip("저장용 ID. 한 번 정하면 바꾸지 않는다")]
        public string id;
        public string displayName;
        [Tooltip("세트 번호 0~4")]
        public int set;
        [TextArea] public string fact;
        public RelicSource source;
        public int sourceChapter;
        public string sourceValue;
        [Tooltip("얻는 방법 (없을 때 보여줌)")]
        public string howToGet;
    }
}
