using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>기믹 보스 소품 (Resources/GimmickArt, 장면 빌더가 만든다). 없으면 2D 모양으로</summary>
    [CreateAssetMenu(menuName = "History Heroes/Gimmick Art", fileName = "GimmickArt")]
    public sealed class GimmickArt : ScriptableObject
    {
        [Tooltip("수정 기둥 (블렌더 Deco_Crystal)")]
        public GameObject pillar;

        private static GimmickArt _cached;
        private static bool _looked;

        public static GimmickArt Get()
        {
            if (_looked) return _cached;
            _looked = true;
            _cached = Resources.Load<GimmickArt>("GimmickArt");
            return _cached;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _cached = null;
            _looked = false;
            BossGimmicks.Notice = null;
        }
    }
}
