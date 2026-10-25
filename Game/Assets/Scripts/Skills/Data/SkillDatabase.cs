using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 게임에 있는 모든 스킬(조합 결과 포함)과 레시피. 기록의 스킬 ID → 스킬 데이터를 찾을 때 쓴다.
    /// 밸런스 버전도 여기 둔다 (GameDesign 12-2: 저장 구조 버전과 따로).
    /// </summary>
    [CreateAssetMenu(menuName = "History Heroes/Skill/Database", fileName = "SkillDatabase")]
    public sealed class SkillDatabase : ScriptableObject
    {
        [Tooltip("스킬 숫자를 패치할 때마다 올린다")]
        public int balanceVersion = 1;

        [Tooltip("조합 결과까지 전부")]
        public List<SkillData> skills = new List<SkillData>();
        public List<FusionRecipe> recipes = new List<FusionRecipe>();

        private Dictionary<string, SkillData> _byId;

        private void OnEnable() => _byId = null;

        public SkillData Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_byId == null) Rebuild();
            if (_byId.TryGetValue(id, out SkillData skill) && skill != null) return skill;

            // 에디터에서 목록을 고친 직후일 수 있다 → 한 번만 다시 만들어 본다
            Rebuild();
            return _byId.TryGetValue(id, out skill) ? skill : null;
        }

        public FusionRecipe FindRecipe(string id)
        {
            foreach (FusionRecipe r in recipes)
            {
                if (r != null && r.id == id) return r;
            }

            return null;
        }

        /// <summary>이 스킬을 결과로 만드는 레시피 (조합 결과가 아니면 null)</summary>
        public FusionRecipe RecipeProducing(SkillData result)
        {
            if (result == null) return null;
            foreach (FusionRecipe r in recipes)
            {
                if (r != null && r.result == result) return r;
            }

            return null;
        }

        /// <summary>에디터 점검: 빈 ID, 중복 ID, 빈 칸 (DevRules 5장 "ID는 한 번 정하면 바꾸지 않는다")</summary>
        public List<string> FindProblems()
        {
            var problems = new List<string>();
            var ids = new HashSet<string>();
            for (int i = 0; i < skills.Count; i++)
            {
                SkillData s = skills[i];
                if (s == null)
                {
                    problems.Add($"스킬 {i}번 칸이 비어 있음");
                    continue;
                }

                if (string.IsNullOrEmpty(s.id)) problems.Add($"{s.name}: ID 없음");
                else if (!ids.Add(s.id)) problems.Add($"스킬 ID 중복: {s.id}");
            }

            var recipeIds = new HashSet<string>();
            foreach (FusionRecipe r in recipes)
            {
                if (r == null) continue;
                if (string.IsNullOrEmpty(r.id)) problems.Add($"{r.name}: 레시피 ID 없음");
                else if (!recipeIds.Add(r.id)) problems.Add($"레시피 ID 중복: {r.id}");
                if (r.result != null && !skills.Contains(r.result)) problems.Add($"{r.id}: 결과 스킬이 목록에 없음");
            }

            return problems;
        }

        private void Rebuild()
        {
            _byId = new Dictionary<string, SkillData>();
            foreach (SkillData s in skills)
            {
                if (s != null && !string.IsNullOrEmpty(s.id)) _byId[s.id] = s;
            }
        }
    }
}
