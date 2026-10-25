using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Enemy;
using HistoryHeroes.Player;
using HistoryHeroes.Progress;
using HistoryHeroes.Skills;
using HistoryHeroes.UI;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// Play 모드에서 한 판을 빠르게 끝까지 돌려보는 점검 도구 (unity command eval로 부른다).
    /// Step()을 반복해서 부르면: 카드가 떠 있으면 고르고 → 적이 있으면 처치 → 문이 열려 있으면 들어간다.
    /// </summary>
    public static class QaTools
    {
        /// <param name="heal">true면 매번 체력을 가득 채운다 (흐름만 확인할 때)</param>
        public static string Step(int cardIndex = 0, bool heal = false)
        {
            var levelUp = Object.FindAnyObjectByType<LevelUpScreen>();
            var run = Object.FindAnyObjectByType<DungeonRun>();
            if (levelUp == null || run == null) return "Play 모드가 아님";
            if (run.IsFinished) return "끝남 | " + State();
            if (heal) Object.FindAnyObjectByType<PlayerController>().Health.Heal(1e9f);

            if (levelUp.IsOpen)
            {
                levelUp.ChooseIndex(cardIndex);
                return "카드 고름 | " + State();
            }

            if (EnemyBase.Alive.Count > 0)
            {
                Dev.DevCheats.KillAll();
                return "처치 | " + State();
            }

            var door = Object.FindAnyObjectByType<Door>();
            if (door != null && door.IsOpen)
            {
                Object.FindAnyObjectByType<PlayerController>().Teleport(door.transform.position);
                return "문 | " + State();
            }

            return "대기 | " + State();
        }

        public static string State()
        {
            var run = Object.FindAnyObjectByType<DungeonRun>();
            var exp = Object.FindAnyObjectByType<ExperienceSystem>();
            var skills = Object.FindAnyObjectByType<SkillManager>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (run == null) return "Play 모드가 아님";

            var owned = new List<string>();
            foreach (SkillInstance s in skills.Loadout.Actives) owned.Add($"{s.Data.id}{s.Level}");
            foreach (SkillInstance s in skills.Loadout.Passives) owned.Add($"{s.Data.id}{s.Level}");

            return $"방 {run.Room}/{run.RoomCount} Lv{exp.Level.Level} 처치{exp.Kills} 적{EnemyBase.Alive.Count} " +
                   $"HP{Mathf.CeilToInt(player.Health.Current)}/{Mathf.CeilToInt(player.Health.Max)} " +
                   $"멈춤={GamePause.IsPaused} 끝={run.IsFinished} [{string.Join(",", owned)}]";
        }
    }
}
