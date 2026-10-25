using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 층(방)별 등급 추첨 가중치 (GameDesign 6-5). 그래프로 두면 게임을 켠 채로 인스펙터에서 끌어서 조절할 수 있다.
    /// 전설: 1~5층 약 1%, 6~15층 1% → 2.5%, 그 뒤 최대 약 4%. 천장(확정 보장)은 없다.
    /// </summary>
    [CreateAssetMenu(menuName = "History Heroes/Skill/Rarity Table", fileName = "RarityTable")]
    public sealed class RarityTable : ScriptableObject
    {
        public AnimationCurve common = new AnimationCurve(new Keyframe(1, 74f), new Keyframe(15, 67.5f), new Keyframe(20, 66f));
        public AnimationCurve rare = new AnimationCurve(new Keyframe(1, 25f), new Keyframe(15, 30f), new Keyframe(20, 30f));
        public AnimationCurve legendary = new AnimationCurve(new Keyframe(1, 1f), new Keyframe(5, 1f), new Keyframe(15, 2.5f), new Keyframe(20, 4f));

        public float WeightOf(Rarity rarity, int floor)
        {
            switch (rarity)
            {
                case Rarity.Common: return Mathf.Max(0f, common.Evaluate(floor));
                case Rarity.Rare: return Mathf.Max(0f, rare.Evaluate(floor));
                default: return Mathf.Max(0f, legendary.Evaluate(floor));
            }
        }
    }
}
