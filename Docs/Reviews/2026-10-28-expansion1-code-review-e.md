# 확장 1차 코드 검토 — 기믹 보스(5장) · 상자 열기 연출

> Codex (gpt-6-sol, high), 2026-10-28. 대상: a0838e3 · 129e3d7. 아래는 원문, 맨 끝에 반영 결과.

두 커밋의 변경분과 관련 호출 경로를 읽기 전용으로 검토했습니다. 아래는 실제 플레이에 영향을 줄 수 있는 발견 사항입니다.

1. **높음 — 무너지는 바닥이 보스와 수정 기둥을 공격합니다.** [CrumblingFloorGimmick.cs](</C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Enemy/Gimmicks/CrumblingFloorGimmick.cs:76>)에서 만든 `TrapZone`은 Player와 Enemy를 모두 검사하고, 적에게는 Player 편 피해를 줍니다. 왕의 2단계에서 바닥 칸이 기둥이나 왕과 겹치면 영웅이 공격하지 않아도 기둥이 부서지고 왕이 피해를 받으며 밀려날 수 있습니다. 기믹용 바닥은 영웅만 피해·밀어내기 대상으로 삼도록 구분하는 편이 안전합니다.

2. **높음 — 바닥 칸이 예고 없이 이미 무너진 상태로 시작할 수 있습니다.** [CrumblingFloorGimmick.cs](</C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Enemy/Gimmicks/CrumblingFloorGimmick.cs:79>)는 새 칸에 `Random.Range(0f, 6f)`를 주입합니다. `TrapZone`의 6초 주기에서 이 값이 활성 구간이면 생성 직후부터 피해가 발생합니다. 2단계 전환 순간 영웅이 그 자리에 서 있으면 1.2초 예고를 볼 기회가 없습니다. 새 칸은 항상 예고 단계부터 시작시키세요.

3. **중간 — 거울 분신의 ‘기절’ 중에도 영웅이 움직일 수 있습니다.** [MirrorStunGimmick.cs](</C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Enemy/Gimmicks/MirrorStunGimmick.cs:53>)는 이동 속도에 `-100%` 보정을 더하지만, `StatCaps`는 최종 이동 속도를 최소 `0.5`로 제한합니다. 이동 속도 강화가 있으면 더 빠르게 움직일 수도 있습니다. 0.5초 동안 입력이나 이동 자체를 막는 상태를 사용하고, 종료·보스 사망 시 해제하세요.

4. **중간 — 원형 보스 경기장에서는 무너지는 바닥이 늘지 않습니다.** [CrumblingFloorGimmick.cs](</C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Enemy/Gimmicks/CrumblingFloorGimmick.cs:30>)의 현재 칸 계산에 `BossRun`의 반지름 `3.9`를 넣으면 후보가 8칸뿐입니다. 45%를 내림한 상한은 3칸이므로 처음 3칸 이후 `AddZone()`이 호출되지 않습니다. 반대로 직사각형 방에서는 `_nextAdd = 0` 때문에 첫 `Update`에서 곧바로 네 번째 칸이 생깁니다. 경기장별 칸 배치와 상한을 다시 계산하고 첫 추가 시각을 6초로 설정하세요.

5. **중간 — 균열 망각의 왕의 2단계 기믹이 3단계까지 늦어집니다.** [PillarShieldGimmick.cs](</C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Enemy/Gimmicks/PillarShieldGimmick.cs:103>)는 `PhaseChanged`에서만 기둥 재생과 바닥 시작을 처리합니다. 균열 왕은 `PatternBoss.Spawn()`에서 이미 `phase = 2`로 시작하므로, 기둥 기믹을 뽑은 층에서는 체력이 1/3까지 내려가 3단계가 될 때까지 바닥이 시작하지 않습니다. `OnBegin()`에서 현재 단계를 확인해 2단계 시작 상태를 적용하세요.

6. **중간 — 표식과 불길의 반복 간격이 설계보다 깁니다.** [ShadowMarksGimmick.cs](</C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Enemy/Gimmicks/ShadowMarksGimmick.cs:137>)는 표식이 끝난 뒤 15초를 더 기다려 첫 발동 8초 다음 발동이 약 27초입니다. [HalfFireGimmick.cs](</C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Enemy/Gimmicks/HalfFireGimmick.cs:70>)도 불길이 끝난 뒤 12초를 더 기다려 첫 발동 6초 다음이 약 21.5초입니다. 발동 시작 시각을 기준으로 각각 15초·12초 간격을 예약하세요.

**테스트 아이디어:** 왕의 2단계에서 기둥·왕·영웅을 바닥 칸 위에 놓고 피해 대상과 첫 예고를 확인하세요. 원형·직사각형 경기장에서 0초, 6초, 12초의 칸 수를 기록하고, 균열 왕은 시작 단계부터 바닥 상태를 확인하세요. 이동 속도 강화 상태에서 가짜 분신을 기본 공격해 0.5초간 이동이 완전히 막히는지, 표식·불길은 첫 두 발동 시각이 설계와 맞는지 확인하면 됩니다. 상자 연출은 장면 전환 및 레벨업 일시정지 중 정리 여부를 Play 모드에서 확인하는 것이 좋습니다. 이번 검토에서는 실행 테스트를 하지 않았습니다.
---

## 반영 (Claude, 2026-10-28)

| # | 지적 | 결정 |
|---|---|---|
| 1 | 무너지는 바닥이 보스·기둥을 때림 | **반영.** 기믹 바닥은 영웅만 (`TrapZone.HeroOnly`) |
| 2 | 바닥 칸이 예고 없이 무너진 채 시작 | **반영.** 새 칸은 늘 "쉼" 구간(0~1.7초)에서 시작 → 1.2초 금을 보고 무너진다 |
| 3 | 기절 중에도 움직임 (이동 속도 하한 0.5) | **반영.** 영웅 묶기(`PlayerController.Root`)로 정말 멈춘다. 같은 문제가 있던 4구간 **덩굴 함정도** 묶기로 고침 |
| 4 | 원형 경기장에서 칸이 안 늘어남 / 네모 방은 첫 프레임에 넷째 칸 | **반영.** 칸 크기를 싸움터에 맞춰(반지름 ÷ 2.6, 1.2~1.8) 늘어날 칸을 남기고, 첫 추가는 6초 뒤 |
| 5 | 균열 망각의 왕(2단계로 시작)이 바닥을 3단계까지 안 씀 | **반영.** 시작 단계가 2 이상이면 바로 2단계 상태 |
| 6 | 표식·불길 간격이 설계보다 김 | **반영.** 발동 시작 기준 15초 · 12초마다 |
