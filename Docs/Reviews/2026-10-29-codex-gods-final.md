# 신의 시련 4·5 수정 기록

## 남은 주의 4건 반영 (2026-10-29, feature/godfix)

| 지적 | 수정 | EditMode 회귀 검사 |
|---|---|---|
| 밀집한 적을 맞히면 검기의 한 프레임 이동 거리가 버려짐 | 검기만 맞은 충돌체를 제외하고 남은 거리로 다시 검사한다. 일반 관통탄의 한 프레임 한 번 충돌·감쇠 규칙은 유지한다 | `ProjectileWaveTests.DenseWaveHitsEveryEnemyAndTravelsFullRangeInOneStep`, `OrdinaryPiercingProjectileStillHitsOnlyOneTargetPerStep` |
| 아테나에게 관통 카드 효과가 없음 | 창 폭 대신 찌르기 길이 +40%를 한 번 적용한다. 영웅 데이터·생성 코드·카드 설명·상세 기획도 맞춘다 | `AthenaCombatTests.PierceExtendsSpearReachOnlyOnce`, `PierceReachesRearEnemyWithoutWideningSpear` |
| 사거리 끝에서 표적을 고르고도 창이 빗나감 | 창 표적 탐색과 명중이 같은 중심 거리 검사와 사거리를 쓴다. 베기와 일반 투사체의 기존 표적 규칙은 유지한다 | `AthenaCombatTests.SpearAndTargetSelectionUseTheSameCenterRange`, `SpearSkipsOutOfRangeCenterWhileProjectileStillTargetsColliderEdge` |
| 두 번째 형상 피격이 닫힌 첫 몸체에 표시됨 | 본체의 체력·단계·처치 알림은 유지하고, 별도 표시 알림으로 피해 숫자와 피격 연출만 맞은 형상에 보낸다. 피해·처치 집계는 한 번이다 | `GodTrialsFinalHealthTests.SecondFormHitShowsDamageOnSecondWithoutDoubleCounting`, `ClosedSecondFormRejectsHitAndFinalHitReportsOnSecond`, `ReinitializedHealthForgetsItsDamageDisplayTarget` |

- `c1c8673`의 공유 HP 잔여값 처리와 해당 검사는 수정하지 않았다.
- Unity 6.3.27f1 설치본 DLL을 참조한 별도 C# 정적 컴파일: 런타임 271개·에디터 41개·EditMode 검사 48개 파일, 오류 0개.
- 엔진 호출이 없는 관리 코드 검사 58개 통과. 기존 공유 HP 잔여값 검사도 포함한다.
- 마지막 형상 처치 검사는 임시 저장 장치에서 기본 공격 처치 횟수가 1인지 확인하며, 저장 장치·연습 여부·시간 배속을 복구한다.
- **Unity Editor의 전체 EditMode 검사와 화면 확인은 미실행**이다. 물리 충돌·표적 탐색·형상 피해 표시 검사는 리드가 Editor에서 실행해야 한다. 관통 카드 설명과 둘째 형상의 2D 피격 효과는 `SceneBuilder.BuildAll()` 실행 후 생성 에셋에 반영된다.
