using UnityEngine;

namespace HistoryHeroes.Combat
{
    /// <summary>
    /// 3D 캐릭터 프리팹에 붙는 정보 (Art3D-Plan 4번). 에디터에서 조립할 때(Art3DCharacters) 잰다.
    /// 게임 중에는 재지 않는다 (재려고 프리팹을 하나 더 만들면 첫 등장 때 끊긴다 — Codex 3D 리뷰 5번).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Model3DInfo : MonoBehaviour
    {
        [Tooltip("모델 원래 키 (배율 1, 서 있는 자세, 유닛)")]
        public float height = 1.8f;
        [Tooltip("그림자를 드리운다 (영웅·보스만. 일반 적은 끈다 — 250마리 성능)")]
        public bool castShadows = true;
        [Tooltip("공격 동작에서 타격·발사 순간까지 걸리는 시간 (클립 기준 초, 클립 재생 속도 1). 0이면 모름. Art3DCharacters.ClipImpact 표에서 쓴다")]
        public float attackImpact;
        [Tooltip("공격 동작 재생 속도 배수 (애니메이터 AttackSpeed). 실제 타격까지 = attackImpact / attackSpeed. 영웅 근접은 휘두름을 짧게 해 ≈0.1초에 맞춘다")]
        public float attackSpeed = 1.4f;
    }
}
