using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Survival
{
    /// <summary>
    /// 서바이벌 한 판의 숫자 (GameDesign 8장, Modes 2-10). 전부 [제안] 시작값 — 벤치·폰으로 맞춘다.
    /// 10분 동안 적이 점점 많고 세지고, 2·4·6·8분에 엘리트, 10분에 마지막 보스. 보스를 잡으면 클리어.
    /// </summary>
    [CreateAssetMenu(menuName = "History Heroes/Survival", fileName = "Survival_")]
    public sealed class SurvivalData : ScriptableObject
    {
        [Header("판")]
        [Tooltip("마지막 보스가 나오는 시간 (초)")]
        public float duration = 600f;
        [Tooltip("맵 절반 크기 (80×80 → 40)")]
        public float mapHalfSize = 40f;
        [Tooltip("동시에 살아 있는 적 상한 (GameDesign 8-5)")]
        public int maxAlive = 300;

        [Header("적 생성")]
        public EnemyBase charger;
        public EnemyBase archer;
        public EnemyBase bomber;
        [Tooltip("궁병이 나오기 시작하는 시간 (초)")]
        public float archerFrom = 60f;
        public float bomberFrom = 180f;
        [Tooltip("궁병·폭탄병 비율 (나머지는 돌진병). 원거리가 많으면 화살 비가 되어 '멈추기'가 사라진다 (벤치: 궁병 45%면 2~3분에 쓰러짐)")]
        [Range(0f, 1f)] public float archerShare = 0.25f;
        [Range(0f, 1f)] public float bomberShare = 0.1f;
        [Tooltip("분 → 유지할 적 수 (뱀서식: 모자라면 채운다. 빨리 잡을수록 더 많이 잡는다). x = 분, y = 마리")]
        public Vector2[] aliveCurve =
        {
            new Vector2(0f, 8f), new Vector2(1f, 22f), new Vector2(2f, 40f), new Vector2(4f, 80f),
            new Vector2(6f, 130f), new Vector2(8f, 190f), new Vector2(10f, 250f),
        };
        [Tooltip("채우는 간격 (초)")]
        public float spawnInterval = 0.25f;
        [Tooltip("한 번에 최대 몇 마리 채우나 (한꺼번에 몰려나오지 않게)")]
        public int spawnBatchMax = 5;
        [Tooltip("이보다 멀리 뒤처진 적은 화면 바로 밖으로 옮긴다 (생성 거리 × 이 값). 플레이어가 달려도 압박이 따라온다")]
        public float recycleDistanceRatio = 1.8f;
        [Tooltip("체력 배수 = 처음 + 분 × 증가 (처음엔 한두 대에 죽게)")]
        public float hpStart = 0.6f;
        public float hpPerMinute = 0.25f;
        [Tooltip("공격력 배수 = 처음 + 분 × 증가")]
        public float attackStart = 1f;
        public float attackPerMinute = 0.08f;
        [Tooltip("화면 밖 얼마나 떨어져서 나오나 (유닛)")]
        public float spawnMargin = 1.5f;

        [Header("엘리트 (초)")]
        public float[] eliteTimes = { 120f, 240f, 360f, 480f };

        [Header("마지막 보스")]
        public EnemyBase bossPrefab;
        public string bossName = "균열의 수문장 (서바이벌)";
        public float bossHpMultiplier = 160f;
        public float bossAttackMultiplier = 1.5f;
        [Tooltip("보스가 나오기 전 경고 (초)")]
        public float bossWarning = 3f;

        [Header("레벨업 카드")]
        [Tooltip("전설 확률 곡선용 '층' = 1 + 분 × 이 값 (던전 16층 ≈ 10분)")]
        public float floorPerMinute = 1.5f;
        public int activeSlots = 6;
        public int passiveSlots = 6;
        [Tooltip("살아 있는 경험치 보석이 이 수를 넘으면 가까운 보석끼리 합친다")]
        public int maxGems = 150;
        [Tooltip("보석 경험치 배수: 적이 던전보다 훨씬 많아서 (벤치: ×1이면 10분에 Lv50 → 슬롯이 다 차고 기본 공격 비중 7%)")]
        public float expMultiplier = 0.4f;

        [Header("보상 (8-4: 같은 진행도에서 분당 재화 ≤ 던전)")]
        public int goldPerMinute = 50;
        public int accountExpPerMinute = 10;
        [Tooltip("고대 파편: 생존 2분당 1, 판당 최대 5 (Relics 5장)")]
        public int shardsPerTwoMinutes = 1;
        public int shardsMax = 5;
        [Tooltip("하트 (Modes 8장)")]
        public int heartCost = 2;

        /// <summary>지금 유지할 적 수 (aliveCurve를 선으로 잇는다, maxAlive 넘지 않게)</summary>
        public int TargetAlive(float seconds)
        {
            if (aliveCurve == null || aliveCurve.Length == 0) return 0;
            float minutes = seconds / 60f;
            Vector2 prev = aliveCurve[0];
            if (minutes <= prev.x) return Mathf.Min(maxAlive, Mathf.RoundToInt(prev.y));
            for (int i = 1; i < aliveCurve.Length; i++)
            {
                Vector2 next = aliveCurve[i];
                if (minutes <= next.x)
                {
                    float t = (minutes - prev.x) / Mathf.Max(0.0001f, next.x - prev.x);
                    return Mathf.Min(maxAlive, Mathf.RoundToInt(Mathf.Lerp(prev.y, next.y, t)));
                }

                prev = next;
            }

            return Mathf.Min(maxAlive, Mathf.RoundToInt(prev.y));
        }

        public float HpMultiplier(float seconds) => hpStart + hpPerMinute * seconds / 60f;
        public float AttackMultiplier(float seconds) => attackStart + attackPerMinute * seconds / 60f;
    }
}
