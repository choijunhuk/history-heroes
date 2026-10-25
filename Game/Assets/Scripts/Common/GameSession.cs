using HistoryHeroes.Economy;
using HistoryHeroes.Records;
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

        /// <summary>이번 판에 하트를 실제로 썼는가 (첫날 무료 판이면 false → 보스 패배 환급 대상 아님)</summary>
        public static bool HeartSpent { get; private set; }

        /// <summary>보스 연습: 하트를 안 쓰고 보상·별·해금도 없다 (Economy 3-4 "하트가 0이어도 보스맵 연습")</summary>
        public static bool Practice { get; private set; }

        public static void StartBossPractice(int chapter, string recordId)
        {
            HeartSpent = false;
            Practice = true;
            Mode = BattleMode.Boss;
            Chapter = Mathf.Max(1, chapter);
            BossRecordId = recordId;
            Load(BattleScene);
        }

        /// <summary>하트 1개를 쓰고 던전 시작. 모자라면 false (로비가 충전 방법을 보여준다)</summary>
        public static bool TryStartDungeon(int chapter)
        {
            if (!TrySpendHeart()) return false;
            StartDungeon(chapter);
            return true;
        }

        /// <summary>하트 1개를 쓰고 보스 도전 (Modes 8장)</summary>
        public static bool TryStartBoss(int chapter, string recordId)
        {
            if (!TrySpendHeart()) return false;
            StartBoss(chapter, recordId);
            return true;
        }

        /// <summary>결과 화면의 "다시 도전": 같은 설정, 하트 1개</summary>
        public static bool TryRetry()
        {
            if (Practice)
            {
                Retry(); // 연습은 하트 없이 또
                return true;
            }

            if (!TrySpendHeart()) return false;
            Retry();
            return true;
        }

        private static bool TrySpendHeart()
        {
            SaveData data = SaveSystem.Data;
            if (!Hearts.TrySpend(data, 1, Clock.UnixSeconds, Clock.Today, out bool free)) return false;
            HeartSpent = !free;
            SaveSystem.Save();
            return true;
        }

        public static void StartDungeon(int chapter)
        {
            Practice = false;
            Mode = BattleMode.Dungeon;
            Chapter = Mathf.Max(1, chapter);
            BossRecordId = null;
            Load(BattleScene);
        }

        public static void StartBoss(int chapter, string recordId)
        {
            Practice = false;
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
            HeartSpent = false;
            Practice = false;
        }
    }
}
