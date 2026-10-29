using System;
using System.Collections.Generic;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;

namespace HistoryHeroes.Modes
{
    /// <summary>시련은 기존 보스 경기장을 쓰지만 별·구간 보상과는 따로 진행한다.</summary>
    public static class GodTrialRules
    {
        public const string ThorTrialId = "thor_trial";
        public const string ThorHeroId = "thor";
        public const int UnlockShards = 30;
        public const int FirstClearShards = 10;
        public const int WeeklyShards = 5;
        public const string ThorTitle = "천둥의 증인";

        public static bool IsGod(string heroId) => heroId == ThorHeroId;
        public static bool IsHeroUnlocked(SaveData save, string heroId) => !IsGod(heroId) || save.unlockedHeroes.Contains(heroId);
        public static bool CanReceiveNormalShards(SaveData save, string heroId) => !IsGod(heroId) || IsHeroUnlocked(save, heroId);

        /// <summary>망각의 왕 본인 기록 첫 격파는 기존 보스의 영구 격파 표식을 쓴다.</summary>
        public static bool IsUnlocked(SaveData save, string trialId) =>
            save != null && trialId == ThorTrialId && save.defeatedBosses.Contains("oblivion_king");

        /// <summary>조각 30개를 소비한 뒤에는 사람 영웅과 같은 Lv1 ★1 성장 규칙을 쓴다.</summary>
        public static bool TryUnlockHero(SaveData save, string heroId)
        {
            if (save == null || heroId != ThorHeroId || !IsUnlocked(save, ThorTrialId) || IsHeroUnlocked(save, heroId)
                || !Wallet.TrySpendItem(save, Wallet.HeroShard(heroId), UnlockShards)) return false;
            save.unlockedHeroes.Add(heroId);
            HeroProgress progress = save.Hero(heroId);
            progress.level = 1;
            progress.stars = 1;
            if (save.godTrials.goalTrialId == ThorTrialId) save.godTrials.goalTrialId = null;
            return true;
        }

        /// <summary>월요일 한국 시간 05:00에 승리 기록과 지급 상태만 초기화한다.</summary>
        public static bool EnsureWeek(SaveData save, DateTimeOffset now)
        {
            string week = Missions.MissionService.WeekKey(now.ToOffset(TimeSpan.FromHours(9)), 5);
            GodTrialSave trials = save.godTrials;
            if (trials.weekKey == week) return false;
            trials.weekKey = week;
            trials.firstRecordId = null;
            trials.firstActives.Clear();
            trials.weeklyWins.Clear();
            trials.goalLocked = false;
            trials.weeklyShards = 0;
            return true;
        }

        /// <summary>첫 승리 전에는 목표를 바꿀 수 있고, 첫 승리부터는 그 주 동안 고정된다.</summary>
        public static bool TryChooseGoal(SaveData save, string trialId, DateTimeOffset now)
        {
            if (!IsUnlocked(save, trialId) || IsHeroUnlocked(save, ThorHeroId)) return false;
            EnsureWeek(save, now);
            if (save.godTrials.goalLocked && save.godTrials.goalTrialId != trialId) return false;
            save.godTrials.goalTrialId = trialId;
            return true;
        }

        /// <summary>결과를 저장 데이터에 적용한다. 화면·저장은 부르는 쪽에서 담당한다.</summary>
        public static GodTrialResult Complete(SaveData save, string trialId, RunRecord record,
            ICollection<string> foughtActives, bool victory, bool practice, bool heartSpent, DateTimeOffset now)
        {
            var result = new GodTrialResult();
            if (!IsUnlocked(save, trialId) || practice || record == null || save.FindRecord(record.id) != record) return result;

            GodTrialSave trials = save.godTrials;
            string today = now.ToString("yyyy-MM-dd");
            if (!victory)
            {
                if (heartSpent && !trials.cleared.Contains(trialId))
                {
                    result.HeartRefunded = Hearts.TryRefundBossLoss(save, trialId, now.ToUnixTimeSeconds(), today);
                    if (result.HeartRefunded) result.Lines.Add("아직 못 깬 시련이라 하트 1개를 돌려받았어요");
                }

                return result;
            }

            result.FirstClear = !trials.cleared.Contains(trialId);
            if (result.FirstClear)
            {
                trials.cleared.Add(trialId);
                if (!save.titles.Contains(ThorTitle)) save.titles.Add(ThorTitle);
                result.Lines.Add("첫 시련 격파 · 칭호 천둥의 증인");
                if (!IsHeroUnlocked(save, ThorHeroId))
                {
                    int earned = Math.Min(FirstClearShards, Math.Max(0, UnlockShards - save.ItemCount(Wallet.HeroShard(ThorHeroId))));
                    if (earned > 0)
                    {
                        save.AddItem(Wallet.HeroShard(ThorHeroId), earned);
                        result.Reward = new Reward { HeroId = ThorHeroId, HeroShards = earned };
                    }
                }
            }
            else
            {
                result.Reward = RunRewards.BossRepeat(save, today);
                Wallet.Grant(save, result.Reward);
            }

            if (!result.Reward.IsEmpty) result.Lines.Add(result.Reward.ToString());
            else if (!result.FirstClear) result.Lines.Add("오늘 반복 격파 보상은 다 받았어요 (하루 3번)");

            EnsureWeek(save, now);
            if (trials.goalTrialId == trialId && !IsHeroUnlocked(save, ThorHeroId) && trials.weeklyShards < WeeklyShards)
            {
                var actives = new List<string>(foughtActives ?? Array.Empty<string>());
                if (string.IsNullOrEmpty(trials.firstRecordId))
                {
                    trials.firstRecordId = record.id;
                    trials.firstActives = new List<string>(actives);
                    trials.goalLocked = true;
                }
                else if (trials.weeklyWins.Exists(w => w.recordId != record.id && DifferentActives(w.actives, actives)))
                {
                    result.WeeklyShards = Math.Min(WeeklyShards, Math.Max(0, UnlockShards - save.ItemCount(Wallet.HeroShard(ThorHeroId))));
                    if (result.WeeklyShards > 0)
                    {
                        save.AddItem(Wallet.HeroShard(ThorHeroId), result.WeeklyShards);
                        result.Lines.Add($"주간 목표 · 토르 조각 +{result.WeeklyShards}");
                    }

                    trials.weeklyShards = WeeklyShards;
                }

                if (!trials.weeklyWins.Exists(w => w.recordId == record.id && new HashSet<string>(w.actives).SetEquals(actives)))
                    trials.weeklyWins.Add(new GodTrialWin { recordId = record.id, actives = actives });
            }

            return result;
        }

        private static bool DifferentActives(ICollection<string> first, ICollection<string> second)
        {
            var difference = new HashSet<string>(first ?? Array.Empty<string>());
            difference.SymmetricExceptWith(second ?? Array.Empty<string>());
            return difference.Count >= 2;
        }
    }

}
