# 확장 1차 코드 검토 — 엘리트 속성(4장) · 특수 방(3장)

> Codex (gpt-6-sol, high), 2026-10-28. 대상: d29df7b · 8ae6da8. 아래는 원문, 맨 끝에 반영 결과.

## 검토 결과

**높음 — 갈림길 선택에 따라 이후 난수가 달라집니다.** [`DungeonRun.Special.cs:249`](C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Dungeon/DungeonRun.Special.cs:249), [`DungeonRun.cs:723`](C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Dungeon/DungeonRun.cs:723), [`SkillManager.cs:82`](C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Skills/Runtime/SkillManager.cs:82)  
도전 방과 일반 방은 생성하는 적 수가 달라 `RunRandom.Enemies`의 소모량도 달라집니다. 도전·저주 보상의 추가 카드 묶음은 `RunRandom.Cards`를 더 소모합니다. 따라서 같은 시드라도 고른 문에 따라 **이후 적의 생성 위치·속성과 카드가 달라질 수 있습니다.** 방 계획을 미리 뽑는 것만으로는 설계 규칙을 만족하지 못합니다. 갈림길 양쪽에서 발생하는 적 배치·속성·보상 카드 난수를 이후 방의 난수와 분리해야 합니다.

**높음 — 특수 보상의 ‘받음’ 표시와 실제 보상이 서로 다르게 복원됩니다.** [`DungeonRun.cs:148`](C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Dungeon/DungeonRun.cs:148), [`DungeonRun.cs:191`](C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Dungeon/DungeonRun.cs:191), [`DungeonRun.Special.cs:324`](C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Dungeon/DungeonRun.Special.cs:324)  
스냅샷의 레벨·스킬·체력은 방에 들어갈 때 저장됩니다. 도전 성공, 저주받은 엘리트 처치, 석비 선택 뒤에는 `rewardTaken`과 축복 ID만 현재 스냅샷에 반영합니다. 다음 방에 들어가기 전에 종료하면 추가 레벨과 고른 카드가 사라지고, 석비의 ‘지나가기’ 회복도 사라집니다. 이어하기에서는 보상을 이미 받았다고 판단해 다시 지급하지 않습니다. 보상 선택과 카드 선택이 끝난 시점의 전체 진행 상태를 저장하거나, 미완료 보상을 이어하기에서 복원할 수 있게 해야 합니다.

**중간 — ‘20초 버티기’가 20초 전에 끝날 수 있습니다.** [`DungeonRun.Special.cs:265`](C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Dungeon/DungeonRun.Special.cs:265)  
14초에 나온 마지막 무리를 20초 전에 모두 잡으면 `_alive > 0` 조건이 거짓이 되어 곧바로 문이 열립니다. 규칙은 적을 모두 잡아도 20초까지 버티는 것입니다. 종료 대기는 적 수와 관계없이 20초를 채우도록 바꿔야 합니다.

**중간 — ‘깜빡이는’ 발동 기준이 적의 공격 상태가 아닙니다.** [`EliteAffixes.cs:291`](C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Enemy/EliteAffixes.cs:291)  
코드는 적이 멈춰 쏘기 시작했는지가 아니라 **영웅이 멈춰 있는 시간**을 셉니다. 그래서 근접 엘리트도 영웅이 서 있으면 순간이동할 수 있고, 원거리 엘리트는 자신의 공격 시작과 무관한 때에 이동합니다. 적의 멈춰 쏘기 상태 진입을 기준으로 한 번 예약하고, 2초 뒤 이동하도록 해야 합니다.

**중간 — 희귀 카드 2장 보장이 후보 부족 시 깨집니다.** [`SkillManager.cs:91`](C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Skills/Runtime/SkillManager.cs:91)  
후반에 장착 칸이 차거나 희귀 스킬이 최대 레벨에 도달해 희귀 후보가 한 장 이하가 되면 반복문이 그대로 끝납니다. 저주받은 엘리트의 추가 선택지에 희귀 이상이 두 장 미만일 수 있습니다. 후보가 부족한 경우까지 포함해 보장을 채우는 카드 규칙을 마련해야 합니다.

**낮음 — 문을 고른 직후에는 선택이 저장되지 않습니다.** [`DungeonRun.Special.cs:199`](C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Dungeon/DungeonRun.Special.cs:199), [`DungeonRun.cs:854`](C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Dungeon/DungeonRun.cs:854)  
선택은 메모리에서만 바뀌고, 다음 방의 `EnterRoom`까지 진행해야 스냅샷에 들어갑니다. 문 진입 후 화면 전환 중 앱이 종료되면 선택이 사라집니다. 문 선택 시 스냅샷을 즉시 갱신해야 합니다.

### 테스트 아이디어

- 같은 시드로 갈림길 양쪽을 각각 선택해, 이후 방의 적 생성 위치·속성과 카드 묶음을 비교합니다.
- 도전 성공·저주 보상·석비 선택 직후 각각 종료하고 이어하기에서 레벨·스킬·체력·보상 수령 상태를 확인합니다.
- 마지막 무리를 17초쯤 모두 잡아도 버티기 문이 20초에 열리는지 확인합니다.
- 근접·원거리 엘리트에 ‘깜빡이는’을 붙여 발동 시점과 이동 뒤 1.2초 정지를 확인합니다.

코드와 테스트를 읽는 방식으로 검토했으며, Unity 실행 테스트는 하지 않았습니다. 파일은 수정하지 않았습니다.
---

## 반영 (Claude, 2026-10-28)

| # | 지적 | 결정 |
|---|---|---|
| 1 | 갈림길 선택에 따라 뒤 난수가 달라짐 | **반영.** 적 난수는 방에 들어갈 때마다 (씨앗, 방 번호)로 새로 시작 (`RunRandom.BeginRoom`) → 앞 방에서 얼마나 썼든 같은 방은 같은 적 자리·속성. 도전 성공·저주 보상 카드는 카드 난수 대신 갈림길 난수로 뽑는다 (`RunRandom.CardDetour`, `SkillManager.DetourOffersNext`). 경험치 차이로 생기는 레벨업 횟수 차이는 플레이 실력 차이와 같은 종류라 그대로 둔다. 테스트 2개 |
| 2 | 보상 뒤 종료하면 레벨·카드·회복이 사라지고 다시도 못 받음 | **반영.** 보상(도전 · 저주 · 석비 · 보상 방)을 받고 레벨업 카드까지 다 고른 뒤 그 방 상태를 "받음"으로 다시 저장 (`SaveAfterRewards`). 보상 방의 같은 구멍도 같이 막음 |
| 3 | 버티기가 20초 전에 끝날 수 있음 | **그대로 둔다 (설계 결정).** 셋째 무리(14초)까지 다 나온 뒤 모두 잡으면 바로 성공 — 센 빌드가 빈 방에서 6초를 기다리지 않게. Expansion1 3-6장에 적음 |
| 4 | 깜빡이는: 영웅이 멈춘 시간 기준 | **반영.** 적이 멈춰 쏘기(예고)를 시작한 순간부터 2초 뒤 한 번 (`EnemyBase.IsAiming`), 0.5초 전 반짝, 뒤 1.2초 멈춤, 쿨다운 6초 |
| 5 | 희귀 후보가 모자라면 2장 보장이 깨짐 | **알고 둔다.** 희귀 후보(새 희귀 + 가진 희귀 레벨업)가 1장 이하인 경우뿐 — 전설 확률을 건드리지 않는 규칙이 우선. 그때는 있는 만큼만 |
| 6 | 문을 고른 직후 저장 안 됨 | **반영.** 문을 고르는 순간 스냅샷의 갈림길 선택을 바로 저장 |
