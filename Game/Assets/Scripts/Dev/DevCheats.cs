using System;
using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Dev
{
    /// <summary>
    /// 개발용 단축키 (에디터·개발 빌드에서만 동작, 출시 빌드에서는 아무것도 안 함).
    /// K: 화면의 적 모두 처치 · L: 레벨업 1번 · 1/2/3: 레벨업 카드 고르기
    /// F1/F2/F3: 빌드 3종 바로 받기 (연사형 / 칼날 탱커형 / 번개 광역형, GameDesign 6-8 검증용)
    /// </summary>
    public sealed class DevCheats : MonoBehaviour
    {
        [Serializable]
        public struct PresetEntry
        {
            public Skills.SkillData skill;
            [Min(1)] public int level;
            [Tooltip("조합 결과(강철 회오리 등)면 켠다")]
            public bool fusionResult;
        }

        [Serializable]
        public sealed class BuildPreset
        {
            public string name;
            public List<PresetEntry> skills = new List<PresetEntry>();
        }

        // 필드는 #if 밖에 둔다. 빌드마다 저장 형식이 달라지면 장면을 읽을 때 오류가 난다.
#pragma warning disable CS0414, CS0649
        [SerializeField] private Progress.ExperienceSystem experience;
        [SerializeField] private UI.LevelUpScreen levelUp;
        [SerializeField] private Skills.SkillManager skills;
        [SerializeField] private List<BuildPreset> presets = new List<BuildPreset>();
#pragma warning restore CS0414, CS0649

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            UnityEngine.InputSystem.Keyboard k = UnityEngine.InputSystem.Keyboard.current;
            if (k == null) return;

            if (k.kKey.wasPressedThisFrame) KillAll();
            if (k.lKey.wasPressedThisFrame) experience.GrantLevelUp();
            if (k.digit1Key.wasPressedThisFrame) levelUp.ChooseIndex(0);
            if (k.digit2Key.wasPressedThisFrame) levelUp.ChooseIndex(1);
            if (k.digit3Key.wasPressedThisFrame) levelUp.ChooseIndex(2);
            if (k.f1Key.wasPressedThisFrame) ApplyPreset(0);
            if (k.f2Key.wasPressedThisFrame) ApplyPreset(1);
            if (k.f3Key.wasPressedThisFrame) ApplyPreset(2);
        }

        public string ApplyPreset(int index)
        {
            if (index < 0 || index >= presets.Count) return "프리셋 없음";
            BuildPreset preset = presets[index];
            foreach (PresetEntry e in preset.skills) skills.DevSetLevel(e.skill, e.level, e.fusionResult);
            Debug.Log($"[DevCheats] 빌드 프리셋: {preset.name}");
            return preset.name;
        }

        /// <summary>
        /// 화면의 적을 모두 처치. 피해 종류를 Enemy로 넣어서 "기본 공격 피해 비중" 집계(DamageLog)에 섞이지 않게 한다.
        /// </summary>
        public static void KillAll()
        {
            var alive = new List<Enemy.EnemyBase>(Enemy.EnemyBase.Alive);
            foreach (Enemy.EnemyBase e in alive)
            {
                e.Health.TryTakeDamage(new Combat.DamageInfo
                {
                    Amount = 1e9f,
                    Kind = Combat.DamageKind.Enemy,
                    AttackerTeam = Combat.Team.Player,
                });
            }
        }
#endif
    }
}
