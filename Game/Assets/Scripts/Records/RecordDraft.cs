using System;
using System.Collections.Generic;
using HistoryHeroes.Skills;
using UnityEngine;

namespace HistoryHeroes.Records
{
    /// <summary>
    /// 기록 저장 화면의 상태: 스킬마다 "몇 레벨까지 남길지" (GameDesign 7-2 "저장 화면 규칙").
    /// - 레벨 일부만 남길 수 있다 (회전 칼날 Lv5 중 Lv3만). 0 = 뺌
    /// - 진화 스킬은 통째로 남기거나 통째로 뺀다
    /// - 합체 스킬은 레벨을 낮춰서 남길 수 있다
    /// - 진화를 남겼는데 필요한 패시브를 빼면 진화 전 액티브 최대 레벨(Lv5)로 돌아간다 → 화면에 미리 보여준다
    /// - 확률로 실패하는 일은 없다
    /// </summary>
    public sealed class RecordDraft
    {
        public sealed class Entry
        {
            public SkillData Skill { get; }

            /// <summary>판이 끝날 때 가진 레벨 = 남길 수 있는 최대 레벨</summary>
            public int OwnedLevel { get; }

            public bool IsFusion { get; }

            /// <summary>이 스킬을 만든 레시피 (조합 결과가 아니면 null)</summary>
            public FusionRecipe Recipe { get; }

            /// <summary>남길 레벨. 0이면 뺀다.</summary>
            public int Level { get; internal set; }

            public bool Kept => Level > 0;

            /// <summary>
            /// 이미 저장된 기록의 조합 결과인가. 레시피가 바뀌어도 이미 만든 결과는 빼앗지 않는다 (GameDesign 7-5)
            /// → 다시 고르기 화면에서는 재료 패시브를 빼도 진화가 풀리지 않는다.
            /// </summary>
            public bool FromSavedRecord { get; set; }
            public bool IsEvolution => IsFusion && Recipe != null && Recipe.type == FusionType.Evolution;
            public bool IsMerge => IsFusion && Recipe != null && Recipe.type == FusionType.Merge;

            public Entry(SkillData skill, int ownedLevel, bool isFusion, FusionRecipe recipe)
            {
                Skill = skill;
                OwnedLevel = Mathf.Max(1, ownedLevel);
                IsFusion = isFusion;
                Recipe = recipe;
                Level = OwnedLevel;
            }
        }

        private readonly List<Entry> _entries;

        public IReadOnlyList<Entry> Entries => _entries;
        public int Capacity { get; }

        public RecordDraft(IEnumerable<Entry> entries, int capacity)
        {
            _entries = new List<Entry>(entries);
            _entries.RemoveAll(e => e == null || e.Skill == null || !e.Skill.recordable);
            Capacity = Mathf.Max(0, capacity);
        }

        /// <summary>판 끝의 스킬 칸 그대로 (처음에는 전부 가진 레벨로 남긴 상태)</summary>
        public static RecordDraft FromLoadout(SkillLoadout loadout, SkillDatabase database, int capacity)
        {
            var entries = new List<Entry>();
            foreach (SkillInstance s in loadout.Actives)
            {
                FusionRecipe recipe = s.IsFusionResult && database != null ? database.RecipeProducing(s.Data) : null;
                entries.Add(new Entry(s.Data, s.Level, s.IsFusionResult, recipe));
            }

            foreach (SkillInstance s in loadout.Passives) entries.Add(new Entry(s.Data, s.Level, false, null));
            return new RecordDraft(entries, capacity);
        }

        public void SetLevel(Entry entry, int level)
        {
            if (entry == null) return;
            entry.Level = entry.IsEvolution
                ? (level > 0 ? entry.OwnedLevel : 0) // 진화는 통째로
                : Mathf.Clamp(level, 0, entry.OwnedLevel);
        }

        /// <summary>화면의 −/+ 버튼. 진화는 한 번 누르면 통째로 빼거나 넣는다.</summary>
        public void Step(Entry entry, int delta)
        {
            if (entry == null || delta == 0) return;
            if (entry.IsEvolution) SetLevel(entry, delta > 0 ? entry.OwnedLevel : 0);
            else SetLevel(entry, entry.Level + delta);
        }

        public bool IsKept(SkillData skill)
        {
            foreach (Entry e in _entries)
            {
                if (e.Skill == skill && e.Kept) return true;
            }

            return false;
        }

        /// <summary>진화를 남겼는데 그 진화에 필요한 패시브를 뺐는가 → 진화 전 액티브로 돌아간다</summary>
        public bool Reverts(Entry entry)
        {
            return entry != null && entry.IsEvolution && entry.Kept && !entry.FromSavedRecord
                   && entry.Recipe.materialA != null && !IsKept(entry.Recipe.materialB);
        }

        /// <summary>진화가 풀리면 돌아갈 레벨 = 진화 전 액티브의 최대 레벨 (진화 조건이 MAX였으므로)</summary>
        public static int RevertLevel(SkillData baseActive) => baseActive.maxLevel;

        /// <summary>이 칸을 지금 설정대로 남길 때의 비용</summary>
        public int CostOf(Entry entry)
        {
            if (entry == null || !entry.Kept) return 0;
            if (Reverts(entry))
            {
                SkillData baseActive = entry.Recipe.materialA;
                return baseActive.SaveCostAt(RevertLevel(baseActive));
            }

            return entry.Skill.SaveCostAt(entry.Level);
        }

        public int TotalCost
        {
            get
            {
                int sum = 0;
                foreach (Entry e in _entries) sum += CostOf(e);
                return sum;
            }
        }

        public bool Fits => TotalCost <= Capacity;
        public bool HasAnything => _entries.Exists(e => e.Kept);

        /// <summary>
        /// 용량에 들어갈 때까지 덜 중요한 것부터 한 레벨씩 낮춘다 (화면을 처음 열 때의 추천값, "자동 맞추기" 버튼).
        /// 순서: 일반 → 희귀 → 전설, 패시브 → 액티브 → 합체 → 진화, 같은 묶음이면 높은 레벨부터.
        /// 남긴 진화의 필수 패시브는 Lv1 아래로 깎지 않는다 (깎으면 진화가 풀리므로 차라리 진화를 뺀다).
        /// </summary>
        public void AutoFit()
        {
            for (int guard = 0; guard < 500 && !Fits; guard++)
            {
                Entry victim = PickToCut();
                if (victim == null) break;
                SetLevel(victim, victim.IsEvolution ? 0 : victim.Level - 1);
            }
        }

        private Entry PickToCut()
        {
            Entry best = null;
            int bestScore = int.MaxValue;
            foreach (Entry e in _entries)
            {
                if (!e.Kept) continue;
                if (e.Level <= 1 && IsRequiredByKeptEvolution(e.Skill)) continue;

                int score = CutPriority(e);
                if (score < bestScore)
                {
                    best = e;
                    bestScore = score;
                }
            }

            return best;
        }

        private bool IsRequiredByKeptEvolution(SkillData passive)
        {
            foreach (Entry e in _entries)
            {
                if (e.IsEvolution && e.Kept && !e.FromSavedRecord && e.Recipe.materialB == passive) return true;
            }

            return false;
        }

        /// <summary>작을수록 먼저 깎는다</summary>
        private static int CutPriority(Entry e)
        {
            int group = e.IsEvolution ? 2 : e.IsMerge ? 1 : 0;
            int rarity = (int)e.Skill.rarity;
            int kind = e.Skill.Kind == SkillKind.Passive ? 0 : 1;
            return group * 10000 + rarity * 1000 + kind * 100 + (99 - e.Level);
        }

        /// <summary>저장할 스킬 목록. 진화가 풀린 칸은 진화 전 액티브로 적는다.</summary>
        public List<RecordSkill> ToSkills()
        {
            var list = new List<RecordSkill>();
            foreach (Entry e in _entries)
            {
                if (!e.Kept) continue;
                if (Reverts(e))
                {
                    SkillData baseActive = e.Recipe.materialA;
                    list.Add(new RecordSkill(baseActive.id, RevertLevel(baseActive), false));
                }
                else
                {
                    list.Add(new RecordSkill(e.Skill.id, e.Level, e.IsFusion));
                }
            }

            return list;
        }

        public RunRecord ToRecord(string heroId, int chapter, int roomReached, int roomCount, bool cleared, int balanceVersion)
        {
            return new RunRecord
            {
                id = Guid.NewGuid().ToString("N"),
                heroId = heroId,
                skills = ToSkills(),
                capacity = Capacity,
                chapter = chapter,
                roomReached = roomReached,
                roomCount = roomCount,
                cleared = cleared,
                createdAt = DateTime.UtcNow.ToString("o"),
                balanceVersion = balanceVersion,
            };
        }
    }
}
