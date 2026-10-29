using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>스사노오 두 번째 형상을 기존 적 표적 검색에서 찾도록 하는 수동 몸체.</summary>
    public sealed class ShadowTrialForm : EnemyBase, IPhasedBoss
    {
        public ShadowTwinsBoss Owner { get; set; }
        public int Phase => Owner != null ? Owner.Phase : 1;
        public bool IsOpen => Owner != null && Owner.IsOpen;
        public override bool CanBeKnockedBack => false;

        protected override void Tick()
        {
            // 이동과 공격은 첫 형상의 기존 패턴이 함께 조종한다.
        }
    }
}
