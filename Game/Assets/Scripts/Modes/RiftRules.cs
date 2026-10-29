using System;
using System.Collections.Generic;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using UnityEngine;

namespace HistoryHeroes.Modes
{
    /// <summary>
    /// 무한의 균열 규칙 (Modes 4장): 층 = 방 1개. 5의 배수 층 엘리트 (깨면 보상 방 선택), 10의 배수 층 보스맵 보스 (균열 버전) → 체크포인트.
    /// 적: 체력 1.06^층, 공격력 1.04^층. 10층마다 나오는 적이 다음 구간 것으로 (1~10층 1구간 … 41층부터 5구간).
    /// 주간 시즌: 월요일 05:00에 이번 주 최고 층·체크포인트 초기화, 주간 변형 규칙 1개. 이번 주 최고 층 보상 (10·20·30·40·50층, 주 1회씩).
    /// 하트 0개, 판 밖 성장 반영, 기록은 남기지 않는다 (빌드는 1층부터 새로).
    /// </summary>
    public static class RiftRules
    {
        public const int ResetHour = 5;

        public static float HpScale(int floor) => Mathf.Pow(1.06f, Mathf.Max(0, floor));
        public static float AttackScale(int floor) => Mathf.Pow(1.04f, Mathf.Max(0, floor));

        /// <summary>균열 보스 체력 = 보스맵 배수 × 이 값 × 1.06^층 (보스맵은 완성된 기록, 균열은 그 층의 빌드라서)</summary>
        public const float BossHpFactor = 0.22f;
        public const float BossAttackFactor = 0.7f;

        /// <summary>균열 보스 졸개 = 그 층 일반 적 배수 × 이 값 (균열 벤치: 20층 망령 군주에서 셋 중 셋이 졸개에 쓰러짐)</summary>
        public const float BossMinionHpFactor = 0.8f;
        public const float BossMinionAttackFactor = 0.75f;

        public static bool IsBossFloor(int floor) => floor > 0 && floor % 10 == 0;
        public static bool IsEliteFloor(int floor) => floor > 0 && floor % 5 == 0 && !IsBossFloor(floor);

        /// <summary>그 층의 적이 나오는 구간 (1~10층 1구간, 11~20층 2구간 …, 41층부터 5구간)</summary>
        public static int ChapterFor(int floor) => Mathf.Clamp(1 + (Mathf.Max(1, floor) - 1) / 10, 1, 5);

        /// <summary>10층마다 보스맵 보스 5명이 차례로 (50층 다음은 처음부터)</summary>
        public static int BossIndex(int floor) => ((Mathf.Max(10, floor) / 10) - 1) % 5;

        public static RoomPlan PlanFor(int floor)
        {
            if (IsBossFloor(floor)) return new RoomPlan { kind = RoomKind.Boss, budget = 0 };
            if (IsEliteFloor(floor)) return new RoomPlan { kind = RoomKind.Elite, budget = 6, minTypes = 1, maxTypes = 2, elites = 1 + floor / 25 };
            int budget = Mathf.Min(24, 6 + Mathf.FloorToInt(floor * 0.5f));
            int types = 1 + Mathf.Min(3, floor / 6);
            return new RoomPlan { kind = RoomKind.Normal, budget = budget, minTypes = Mathf.Max(1, types - 1), maxTypes = types };
        }

        // ---------------------------------------------------------------- 주간 시즌

        public static string WeekKey(DateTimeOffset now) => Missions.MissionService.WeekKey(now, ResetHour);

        /// <summary>주가 바뀌었으면 이번 주 기록·체크포인트·받은 보상을 비운다 (역대 최고는 남긴다)</summary>
        public static bool EnsureWeek(SaveData save, DateTimeOffset now)
        {
            string week = WeekKey(now);
            if (save.rift.weekKey == week) return false;
            save.rift.weekKey = week;
            save.rift.weeklyBest = 0;
            save.rift.rewardsClaimed.Clear();
            save.rift.checkpoint = new RiftCheckpoint();
            return true;
        }

        public enum Variant
        {
            ToughEnemiesMoreExp,   // 적 체력 +30%, 대신 경험치 +30%
            FierceEnemiesStronger, // 적 공격력 +25%, 대신 공격력 +15%
            FastArrowsFastFeet,    // 적 투사체 빠르기 +20%, 대신 이동속도 +10%
            QuickSkillsWeakShots,  // 액티브 쿨다운 -20%, 대신 기본 공격 피해 -15%
        }

        public static Variant VariantFor(string weekKey)
        {
            int n = Enum.GetValues(typeof(Variant)).Length;
            int h = Missions.MissionService.StableHash(weekKey ?? string.Empty);
            return (Variant)(((h % n) + n) % n);
        }

        public static string VariantText(Variant v)
        {
            switch (v)
            {
                case Variant.ToughEnemiesMoreExp: return "이번 주: 적 체력 +30%, 대신 경험치 +30%";
                case Variant.FierceEnemiesStronger: return "이번 주: 적 공격력 +25%, 대신 내 공격력 +15%";
                case Variant.FastArrowsFastFeet: return "이번 주: 적 투사체 빠르기 +20%, 대신 이동속도 +10%";
                default: return "이번 주: 액티브 쿨다운 -20%, 대신 기본 공격 피해 -15%";
            }
        }

        // ---------------------------------------------------------------- 보상 (Modes 4-5, 이번 주 최고 층 기준 주 1회씩)

        public static readonly int[] Milestones = { 10, 20, 30, 40, 50 };

        public static Reward RewardFor(int milestone, out int ancientShards)
        {
            ancientShards = 0;
            switch (milestone)
            {
                case 10: return new Reward { Gold = 2000 };
                case 20: ancientShards = 10; return default;
                case 30: return new Reward { Gold = 4000, GearShards = 5 };
                case 40: ancientShards = 10; return default;
                case 50: return new Reward { Tickets = 1 };
                default: return default;
            }
        }

        /// <summary>도달한 층으로 이번 주 최고 층을 올리고, 새로 넘은 보상 층의 보상을 준다. 받은 줄을 돌려준다</summary>
        public static List<string> ReachFloor(SaveData save, int floor)
        {
            var lines = new List<string>();
            RiftSave rift = save.rift;
            rift.weeklyBest = Mathf.Max(rift.weeklyBest, floor);
            rift.bestEver = Mathf.Max(rift.bestEver, floor);
            foreach (int m in Milestones)
            {
                if (floor < m || rift.rewardsClaimed.Contains(m)) continue;
                rift.rewardsClaimed.Add(m);
                Reward r = RewardFor(m, out int shards);
                Wallet.Grant(save, r);
                if (shards > 0) save.AddItem(Relics.RelicTable.AncientShard, shards);
                string text = r.ToString();
                if (shards > 0) text += (text.Length > 0 ? " · " : "") + $"<sprite name=ancient_shard>고대 파편 +{shards}";
                lines.Add($"균열 {m}층 보상  {text}");
            }

            return lines;
        }

        public static bool Unlocked(SaveData save) => save.unlockedChapter >= 4; // 3구간 보스 격파 (Systems 3장)
    }
}
