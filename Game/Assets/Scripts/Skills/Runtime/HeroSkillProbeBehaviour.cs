#if UNITY_EDITOR
using HistoryHeroes.Skills;

namespace HistoryHeroes.Tests
{
    // Editor 전용 asmdef의 스크립트는 GameObject에 붙일 수 없어 런타임 경로에서 검사한다.
    public sealed class HeroSkillProbeBehaviour : ActiveSkillBehaviour
    {
        public bool CanActivate;
        public int Attempts;

        public void Tick() => base.Update();

        protected override bool Activate()
        {
            Attempts++;
            return CanActivate;
        }
    }
}
#endif
