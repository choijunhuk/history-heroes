using UnityEngine;

namespace HistoryHeroes.Dev
{
    /// <summary>
    /// 개발용 단축키 (에디터·개발 빌드에서만 동작, 출시 빌드에서는 아무것도 안 함).
    /// K: 화면의 적 모두 처치 · L: 레벨업 1번 · 1/2/3: 레벨업 카드 고르기
    /// 조합이나 전설 스킬을 빨리 확인할 때 쓴다.
    /// </summary>
    public sealed class DevCheats : MonoBehaviour
    {
        // 필드는 #if 밖에 둔다. 빌드마다 저장 형식이 달라지면 장면을 읽을 때 오류가 난다.
#pragma warning disable CS0414, CS0649
        [SerializeField] private Progress.ExperienceSystem experience;
        [SerializeField] private UI.LevelUpScreen levelUp;
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
        }

        public static void KillAll()
        {
            var alive = new System.Collections.Generic.List<Enemy.EnemyBase>(Enemy.EnemyBase.Alive);
            foreach (Enemy.EnemyBase e in alive)
            {
                e.Health.TryTakeDamage(new Combat.DamageInfo
                {
                    Amount = 1e9f,
                    Kind = Combat.DamageKind.Active,
                    AttackerTeam = Combat.Team.Player,
                });
            }
        }
#endif
    }
}
