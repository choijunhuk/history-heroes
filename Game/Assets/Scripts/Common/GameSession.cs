using UnityEngine;
using UnityEngine.SceneManagement;

namespace HistoryHeroes.Common
{
    public enum BattleMode
    {
        Dungeon,
        Boss,
    }

    /// <summary>
    /// 장면을 넘어가도 남는 "이번에 무엇을 하러 가는가" (로비 → 전투 장면).
    /// 전투 장면을 에디터에서 바로 Play하면 기본값(1구간 던전)으로 시작한다.
    /// </summary>
    public static class GameSession
    {
        public const string LobbyScene = "Lobby";
        public const string BattleScene = "Battle";

        public static BattleMode Mode { get; private set; } = BattleMode.Dungeon;
        public static int Chapter { get; private set; } = 1;

        /// <summary>보스전에 쓸 기록 ID</summary>
        public static string BossRecordId { get; private set; }

        public static void StartDungeon(int chapter)
        {
            Mode = BattleMode.Dungeon;
            Chapter = Mathf.Max(1, chapter);
            BossRecordId = null;
            Load(BattleScene);
        }

        public static void StartBoss(int chapter, string recordId)
        {
            Mode = BattleMode.Boss;
            Chapter = Mathf.Max(1, chapter);
            BossRecordId = recordId;
            Load(BattleScene);
        }

        /// <summary>같은 설정으로 한 번 더 (결과 화면의 "다시 도전")</summary>
        public static void Retry() => Load(BattleScene);

        public static void ToLobby() => Load(LobbyScene);

        private static void Load(string scene)
        {
            GamePause.ResetAll();
            SceneManager.LoadScene(scene);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Mode = BattleMode.Dungeon;
            Chapter = 1;
            BossRecordId = null;
        }
    }
}
