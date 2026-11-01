using System.Collections.Generic;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 판 안에서 가진 스킬 목록과 슬롯 (GameDesign 6-1: 던전 모드 액티브 4칸 · 패시브 4칸).
    /// 조합 규칙(재료 재획득 금지, 같은 결과 1개)을 지키기 위한 기록도 여기 둔다. (GameDesign 6-2)
    /// </summary>
    public sealed class SkillLoadout
    {
        private readonly List<SkillInstance> _actives = new List<SkillInstance>();
        private readonly List<SkillInstance> _passives = new List<SkillInstance>();
        private readonly HashSet<SkillData> _consumedMaterials = new HashSet<SkillData>();
        private readonly HashSet<FusionRecipe> _completedFusions = new HashSet<FusionRecipe>();
        private readonly HashSet<FusionRecipe> _hiddenThisRun = new HashSet<FusionRecipe>();
        private readonly HashSet<string> _bannedSkillIds = new HashSet<string>();

        public const int BansPerRun = 2;
        public int BansLeft { get; private set; } = BansPerRun;
        public IEnumerable<string> BannedSkillIds => _bannedSkillIds;

        public int ActiveSlots { get; }
        public int PassiveSlots { get; }

        public IReadOnlyList<SkillInstance> Actives => _actives;
        public IReadOnlyList<SkillInstance> Passives => _passives;

        public SkillLoadout(int activeSlots = 4, int passiveSlots = 4)
        {
            ActiveSlots = activeSlots;
            PassiveSlots = passiveSlots;
        }

        public bool HasFreeSlot(SkillKind kind)
        {
            return kind == SkillKind.Passive ? _passives.Count < PassiveSlots : _actives.Count < ActiveSlots;
        }

        public SkillInstance Find(SkillData data)
        {
            foreach (SkillInstance s in _actives) if (s.Data == data) return s;
            foreach (SkillInstance s in _passives) if (s.Data == data) return s;
            return null;
        }

        public int LevelOf(SkillData data) => Find(data)?.Level ?? 0;

        public SkillInstance AddNew(SkillData data, int level = 1)
        {
            var instance = new SkillInstance(data, level);
            if (data.Kind == SkillKind.Passive) _passives.Add(instance);
            else _actives.Add(instance);
            return instance;
        }

        public void Remove(SkillInstance instance)
        {
            if (!_actives.Remove(instance)) _passives.Remove(instance);
        }

        /// <summary>조합 재료는 그 판에서 다시 후보에 나오지 않는다.</summary>
        public IEnumerable<SkillData> ConsumedMaterials => _consumedMaterials;
        public IEnumerable<FusionRecipe> CompletedFusions => _completedFusions;
        public IEnumerable<FusionRecipe> HiddenFusions => _hiddenThisRun;

        public void MarkConsumed(SkillData material) => _consumedMaterials.Add(material);
        public bool IsConsumed(SkillData data) => _consumedMaterials.Contains(data);

        public void MarkCompleted(FusionRecipe recipe) => _completedFusions.Add(recipe);
        public bool IsCompleted(FusionRecipe recipe) => _completedFusions.Contains(recipe);

        /// <summary>조합 카드의 "이번 판에서 숨기기"</summary>
        public void Hide(FusionRecipe recipe) => _hiddenThisRun.Add(recipe);
        public bool IsHidden(FusionRecipe recipe) => _hiddenThisRun.Contains(recipe);

        /// <summary>가진 스킬의 효과는 남기고, 이번 판의 신규·레벨업 후보에서만 뺀다.</summary>
        public bool TryBan(SkillCard card)
        {
            if (card == null || (card.Type != CardType.NewSkill && card.Type != CardType.LevelUp)
                || card.Skill == null || string.IsNullOrEmpty(card.Skill.id) || BansLeft <= 0) return false;
            if (!_bannedSkillIds.Add(card.Skill.id)) return false;
            BansLeft--;
            return true;
        }

        public bool IsBanned(SkillData skill) => skill != null && _bannedSkillIds.Contains(skill.id);

        public void RestoreBans(IEnumerable<string> ids, int left)
        {
            _bannedSkillIds.Clear();
            if (ids != null)
            {
                foreach (string id in ids)
                {
                    if (!string.IsNullOrEmpty(id)) _bannedSkillIds.Add(id);
                }
            }

            BansLeft = System.Math.Clamp(left, 0, BansPerRun);
        }
    }
}
