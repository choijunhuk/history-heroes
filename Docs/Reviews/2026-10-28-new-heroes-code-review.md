# 새 영웅 코드 검토 — 이순신 · 이성계 · 다빈치 · 제갈량

> Codex (gpt-6.1-sol, high), 2026-10-28. 대상: 72cc1f7 · f20e1de · 391ec05. 아래는 원문, 맨 끝에 반영 결과.

파일 수정 없이 세 커밋과 관련 경로를 검토했습니다. **수정이 필요한 문제 9건**입니다. Unity 실행 테스트는 하지 않았습니다.

1. **[높음] 다빈치·제갈량이 보석 없이 처음부터 열립니다.**  
   위치: [SceneBuilder.Data.cs:157](Game/Assets/Editor/SceneBuilder.Data.cs:157), [HeroRoster.cs:39](Game/Assets/Scripts/Heroes/HeroRoster.cs:39)  
   두 영웅 모두 `unlockAfterBossChapter = 0`인데, 기존 `IsUnlocked()`는 이 값을 즉시 해금으로 해석합니다. 새 계정에서도 사용할 수 있고 `TryBuy()`는 구매를 거절하므로 `unlockGems = 2000`은 작동하지 않습니다.  
   **수정:** 처음부터 해금·보스 해금·보석 전용을 구분하고, 해금 판정과 안내 문구를 함께 수정해야 합니다.

2. **[중간] 제갈량은 멀티샷을 얻으면 정면의 적에게 전부 빗나갈 수 있습니다.**  
   위치: [PlayerController.cs:550](Game/Assets/Scripts/Player/PlayerController.cs:550), [PlayerController.cs:559](Game/Assets/Scripts/Player/PlayerController.cs:559)  
   기본 3발은 `−18°, 0°, +18°`지만 멀티샷 Lv1에서는 `−27°, −9°, +9°, +27°`가 됩니다. 거리 4.5에서 가장 안쪽 탄도 표적 중심을 약 0.704만큼 비껴갑니다. 기본 적 반경 0.45와 바람탄 반경 0.2를 합친 0.65보다 커서, 가만히 있는 적도 모든 탄을 피합니다. 강화 후 단일 표적 화력이 오히려 사라집니다.  
   **수정:** 정면 탄을 유지하면서 추가 탄을 배치하거나, 추가 발사를 별도 묶음으로 처리하세요.

3. **[중간] 신궁 추가 피해가 실제 명중한 화살 피해의 +100%가 아닙니다.**  
   위치: [PlayerController.cs:498](Game/Assets/Scripts/Player/PlayerController.cs:498)  
   추가 피해를 `_sig.ChargeFull`로 새로 계산해서 멀티샷 감산과 관통 감산을 무시합니다. 공격력 120·치명타 배수 1.5·멀티샷 Lv1이면 본탄은 405인데 추가 피해는 450입니다. 합계가 예정된 810 대신 855가 됩니다. 뒤쪽 적에 관통한 경우 차이가 더 커집니다.  
   **수정:** 명중한 투사체의 `DamageInfo`를 전달해, 감산까지 반영된 같은 피해를 추가하거나 최초 피해 처리에서 두 배로 적용하세요.

4. **[중간] 신궁 표식이 풀에서 다시 나온 다른 적에게 이어집니다.**  
   위치: [PlayerController.cs:417](Game/Assets/Scripts/Player/PlayerController.cs:417), [PlayerController.cs:495](Game/Assets/Scripts/Player/PlayerController.cs:495)  
   표식을 `Health` 객체만으로 구분합니다. 표식이 붙은 적을 액티브로 죽이고 6초 안에 같은 객체가 재사용되면, 새 적도 이전 표식의 추가 피해를 받습니다. `EnemyBase.Spawn()`은 이 외부 사전을 초기화하지 않습니다. 만료된 항목도 자동으로 제거되지 않습니다.  
   **수정:** 객체와 생성 회차를 함께 기록하거나, 적 사망·반환 때 표식을 해제하고 만료 항목도 정리하세요.

5. **[중간] 팔진도에서 재사용된 적은 혼란 효과를 받지 못합니다.**  
   위치: [EightGateBehaviour.cs:101](Game/Assets/Scripts/Skills/Effects/EightGateBehaviour.cs:101)  
   진 안에서 혼란을 받은 적이 죽고, 진의 지속 시간 안에 같은 `EnemyBase`가 다시 나오면 `_confused.Add(e)`가 실패합니다. 새로 나온 적인데도 둔화와 비틀거림을 건너뜁니다. 서바이벌처럼 빠르게 재사용하는 상황에서 발생합니다.  
   **수정:** `_confused`도 생성 회차를 구분하거나, 풀 반환 시 해당 기록을 제거해야 합니다.

6. **[중간] 두 합체 스킬이 기존 회오리의 보스 끌어당김을 잃습니다.**  
   위치: [ArmoredTankBehaviour.cs:96](Game/Assets/Scripts/Skills/Effects/ArmoredTankBehaviour.cs:96), [EightGateBehaviour.cs:117](Game/Assets/Scripts/Skills/Effects/EightGateBehaviour.cs:117)  
   `CanBeKnockedBack == false`인 적은 끌어당김을 전부 생략합니다. 기존 회오리는 `EnemyStatus.Pull()`을 사용해 보스도 30% 속도로 끌지만, 나선 비행기·칠성단의 바람으로 합체하면 보스가 완전히 면역이 됩니다. 새 구현은 `IsPulled`도 설정하지 않아 기존 이동 제어 경로를 우회합니다.  
   **수정:** 직접 `MovePosition()`하는 대신 기존 `EnemyStatus.Pull()` 경로를 사용하세요.

7. **[중간] 장갑 전차가 벽 밖을 돌면서 탄을 낭비합니다.**  
   위치: [ArmoredTankBehaviour.cs:72](Game/Assets/Scripts/Skills/Effects/ArmoredTankBehaviour.cs:72), [ArmoredTankBehaviour.cs:84](Game/Assets/Scripts/Skills/Effects/ArmoredTankBehaviour.cs:84)  
   영웅이 벽 가까이에 있으면 반경 2.2의 궤도가 경기장 밖으로 나갑니다. 위치 보정 없이 그 자리에서 발사하므로, 실내로 향한 탄은 외벽에 막히고 바깥쪽 탄은 전투에 기여하지 못합니다. 모서리에서는 손실 구간이 더 커집니다.  
   **수정:** 영웅에서 예정 궤도 위치까지 벽을 검사해 전차와 발사 위치를 안쪽으로 제한하세요.

8. **[중간] 다른 방어 효과가 있으면 철갑이 막은 피해를 잘못 계산합니다.**  
   위치: [PlayerController.cs:348](Game/Assets/Scripts/Player/PlayerController.cs:348)  
   `dealt`는 다른 피해 감소·보호막·남은 체력 제한까지 적용된 값입니다. 여기에 항상 `StopGuard / (1 − StopGuard)`를 곱하면 철갑이 실제로 줄인 피해를 복원할 수 없습니다. 예를 들어 방어력 22%와 철갑 25%에서 원래 피해 100을 받으면 실제 피해는 53이고 저장량은 약 8.83입니다. 철갑이 줄인 25의 절반인 12.5와 다릅니다. 보호막이 전부 흡수하면 저장량은 0이 됩니다.  
   **수정:** `Health`에서 철갑 유무에 따른 실제 감소량 차이를 계산해 전달하세요. 피해 감소 상한과 `DamageTaken`도 같은 계산에 반영해야 합니다.

9. **[낮음] 영웅 전환과 종료 시 신규 대표 효과가 정리되지 않습니다.**  
   위치: [PlayerController.cs:97](Game/Assets/Scripts/Player/PlayerController.cs:97), [PlayerController.cs:239](Game/Assets/Scripts/Player/PlayerController.cs:239)  
   실행 중 `SetHero()`로 이순신을 다른 영웅으로 바꾸면 `_ironOwner`의 방어 보정이 남습니다. 이후 `UpdateIronclad()`는 새 영웅의 `StopGuard`가 0이라 정리하지 않습니다. 다빈치의 기존 기계도 영웅 종류를 확인하지 않아 계속 발사합니다. 동남풍 역시 사망 후 `Update()`가 반환하거나 영웅을 바꾸면 마지막 구역이 남습니다. 다만 새 장면에서는 `EnemyProjectiles.Awake()`가 반경을 초기화하므로, 다음 판까지 유지된다고 보지는 않았습니다.  
   **수정:** 대표 효과 정리 함수를 만들고 영웅 전환·사망·비활성화에 연결하세요. 영웅 전환은 현재 개발·QA용 경로라 낮음으로 분류했습니다.

추가로, `Projectile.Launch()`의 `SlowFactor = 1f` 초기화는 있습니다. `TryBlock()`도 취소 즉시 활성 목록에서 제거하므로 해당 `while`이 무한 반복할 근거는 찾지 못했습니다. 모델 누락 시 대체 도형 처리와 스킬 제거 시 `EffectRoot` 정리도 확인했습니다. 비행 기계 탄은 현재 `BasicAttack`이므로 거울 분신을 맞히면 영웅을 기절시키며, 일반 적 처치도 기본 공격 처치로 집계됩니다.

현재 `YiSunsinTests.cs`는 주로 수치·별 강화·에셋 연결을 확인합니다. 다음 실행 테스트를 추가하는 것이 좋습니다.

- 새 계정에서 두 보석 전용 영웅이 잠기고, 구매 시 정확히 2,000개가 차감되는지 확인.
- 제갈량 멀티샷 Lv0~3을 거리 2·4·4.5의 단일 적에게 발사해 명중률 비교.
- 신궁의 본탄·추가 피해를 멀티샷 및 두 번째 관통 대상에서 각각 확인.
- 같은 적 객체를 즉시 재사용해 신궁 표식과 팔진도 적용 기록이 초기화되는지 확인.
- 방어력·보호막·`DamageTaken`·피해 감소 상한을 조합해 철갑 저장량 검증.
- 두 합체 스킬의 일반 적·엘리트·보스 끌어당김을 확인하고, 전차를 벽·모서리에서 발동.
- 충전·부품 조립 도중 일시정지, `Root`, 부활, 방 전환, 영웅 전환을 각각 수행해 상태와 타이머 확인.
- 비행 기계·바람탄으로 거울 분신과 `EnemyBase` 없는 수정 기둥을 공격하고, 스킬 제거·장면 종료 후 잔여 효과 확인.

---

## 반영 (Claude, 2026-10-28)

| # | 지적 | 결정 |
|---|---|---|
| 1 | 다빈치·제갈량이 보석 없이 열림 (`unlockAfterBossChapter = 0` = 처음부터) | **반영.** `HeroData.gemsOnly` 추가: 켜면 보석으로 산 영웅만 열림. 안내 글 "보석 2,000개로 열려요" (`HeroUnlock.LockText`) |
| 2 | 제갈량 + 멀티샷이면 4발이 ±27°·±9°로 벌어져 정면 적을 놓침 | **반영.** 부채 넓이(±18°)는 그대로, 늘어난 발을 그 안에 촘촘히 (`PlayerController.ShotSpread`). 멀티샷 1~3 모두 거리 4.5 가운데 적에 맞음 (테스트) |
| 3 | 신궁 추가 피해를 새로 계산해 멀티샷·관통 감소를 무시 | **반영.** 맞은 화살이 그 적에게 준 피해를 그대로 한 번 더 (`+100%` = 같은 화살 두 번) |
| 4 | 신궁 표식이 풀에서 다시 나온 다른 적에게 이어짐 | **반영.** `Health.Life` (Init마다 +1)를 표식에 같이 적고, 삶이 다르면 무시. 표식을 새로 달 때 끝난 표식은 치움 |
| 5 | 팔진도: 다시 나온 적은 헤매지 않음 | **반영.** 같은 방법 (적 → 헤매게 한 삶) |
| 6 | 나선 비행기·칠성단의 바람이 보스를 아예 못 끎 | **반영.** 회오리와 같은 `EnemyStatus.Pull` (보스는 30% 빠르기로 끌림) |
| 7 | 장갑 전차가 벽 밖을 돌며 탄을 버림 | **반영.** 영웅 → 궤도 쪽으로 벽을 재서 벽 0.4 앞에서 멈춘다. 벽에 붙어 쓰는 장면 확인 |
| 8 | 다른 피해 감소·보호막이 있으면 철갑 저장량이 틀림 | **반영.** 감소 전 피해(`Health.LastBeforeReduction`) × (철갑 포함 감소 − 철갑 뺀 감소, `StatSheet.GetWithout`). 상한 60%·받는 피해 증가까지 맞고, 보호막이 받아도 철갑 몫은 쌓임 |
| 9 | 영웅 바꾸기·죽음 뒤 대표 효과가 남음 | **반영.** `ClearSignatureState` (철갑 보정·쌓인 피해, 모으던 화살·표식, 부품·비행 기계, 동남풍 구역)를 영웅 바꿀 때 부르고, 죽으면 동남풍을 바로 끔 |
| + | 비행 기계 탄이 거울 분신을 맞히면 영웅이 기절 | **반영 (설계 결정).** 비행 기계는 영웅이 고른 탄이 아니라서 기절에서 뺀다 (`HelperSourceId`). 기본 공격 처치로 세는 것은 그대로 |

테스트 4개 추가 (보석 전용 해금, 부채 + 멀티샷 각도, 철갑 몫과 상한, `Health.Life`) → 243개 모두 통과. Play 점검: 제갈량 + 팔진도 + 멀티샷, 다빈치 + 나선 비행기를 벽에 붙여서, 오류 없음.
