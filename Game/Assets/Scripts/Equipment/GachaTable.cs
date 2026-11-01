using System;
using UnityEngine;

namespace HistoryHeroes.Equipment
{
    /// <summary>상자 하나의 확률·가격 (등급 순서: 일반·고급·희귀·전설)</summary>
    [Serializable]
    public sealed class BoxRates
    {
        public string id;
        public string displayName;
        [Tooltip("등급 확률 (합 1). 화면 확률표와 실제 추첨이 둘 다 이걸 읽는다")]
        public float[] rarity = { 0.70f, 0.27f, 0.03f, 0f };
        public int goldCost;
        public int gemCost;
        [Tooltip("10연 가격 (0이면 10연 없음)")]
        public int gem10Cost;
        [Tooltip("이 회차에 전설 확정 (0이면 천장 없음)")]
        public int pity;
    }

    /// <summary>
    /// 상자 표 (Gacha 2장). **표시와 실제가 같은 데이터**: 확률 보기 화면과 추첨이 이 에셋 하나만 읽는다.
    /// 뽑기는 장비만 판다 (판 안 스킬·기록·조합·영웅은 뽑기로 팔지 않는다).
    /// </summary>
    [CreateAssetMenu(menuName = "History Heroes/Gacha Table", fileName = "GachaTable")]
    public sealed class GachaTable : ScriptableObject
    {
        public EquipmentTable equipment;

        public BoxRates normal = new BoxRates
        {
            id = "normal", displayName = "일반 상자", rarity = new[] { 0.70f, 0.27f, 0.03f, 0f }, goldCost = 5000,
        };

        public BoxRates premium = new BoxRates
        {
            id = "premium", displayName = "고급 상자", rarity = new[] { 0.50f, 0.35f, 0.13f, 0.02f }, gemCost = 250, gem10Cost = 2250, pity = 50,
        };

        [Tooltip("고급 상자를 희귀 이상 없이 9회 연속 열면 다음 회차에서 희귀 미만 결과를 이 표로 다시 뽑는다 (희귀 86.7% · 전설 13.3%)")]
        public float[] tenPullGuarantee = { 0f, 0f, 0.867f, 0.133f };

        [Tooltip("뽑기 기록을 몇 개까지 남길지")]
        public int logLimit = 100;
    }
}
