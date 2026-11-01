# 확장 1차 코드 검토 — 구간 장치(2장) · 서바이벌 큰 모드(6장)

> Codex (gpt-6-sol, high), 2026-10-28. 대상: 52892b6 · 2428742. 아래는 원문, 맨 끝에 반영 결과.

두 커밋의 변경 코드와 관련 설계 규칙을 읽기 전용으로 검토했습니다. 다음은 실제 실행 경로에서 확인한 문제입니다.

1. **높음 — 맵 3의 ★2 임무를 달성할 수 없습니다.** [SceneBuilder.SurvivalMaps.cs](<C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Editor/SceneBuilder.SurvivalMaps.cs:77>)는 6분 전 엘리트 3마리 처치를 요구하지만, [SurvivalData.cs](<C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Survival/SurvivalData.cs:69>)의 세 번째 엘리트는 정확히 360초에 생성됩니다. [SurvivalRun.Pickups.cs](<C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Survival/SurvivalRun.Pickups.cs:233>)는 처치 시각이 360초 이하여야 셉니다. 세 번째를 잡는 시각은 반드시 그보다 늦습니다. 세 번째 생성 시각을 앞당기거나 임무 마감 시각을 늦춰야 합니다.

2. **높음 — 기존 서바이벌 클리어 저장에서 맵 2가 잠긴 채 남습니다.** [SurvivalProgress.cs](<C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Survival/SurvivalProgress.cs:43>)는 맵 1의 `FindMap("field")` 기록으로 해금을 판단하지만, 기존 `clears`를 `field`로 옮기는 [SaveData.cs](<C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Records/SaveData.cs:568>)의 `MapOf`는 읽기 경로에서 호출되지 않습니다. 기존 클리어 이용자는 로비에서 맵 2를 고를 수 없고, 맵 1을 다시 끝내야 이전이 일어납니다. 저장을 불러올 때 맵 기록을 이전하고 저장하거나, 해금 판단 전에 이전을 보장해야 합니다. 이전 시 기존 클리어의 ★1과 `firstClearTaken` 처리도 함께 정해야 합니다.

3. **높음 — 중간 보스가 장애물 안에 생성될 수 있습니다.** [SurvivalRun.Waves.cs](<C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Survival/SurvivalRun.Waves.cs:63>)는 울타리 중심에서 위로 고정 거리만큼 떨어진 곳에 보스를 놓습니다. 성곽 맵에서 플레이어가 집 아래쪽에 서 있으면 그 위치가 집의 원형 충돌체 안에 들어갈 수 있습니다. 마지막 보스 위치도 [SurvivalRun.cs](<C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Survival/SurvivalRun.cs:288>)에서 맵 경계만 확인합니다. 두 보스 모두 장애물과 몸 크기를 검사해 빈 자리를 찾아 생성해야 합니다.

4. **중간 — 울타리가 열린 동안 일부 무리가 울타리 안에 생성됩니다.** 일반 적 생성은 `InsideFence`를 피하지만, [SurvivalRun.Waves.cs](<C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Survival/SurvivalRun.Waves.cs:299>)의 지나가는 무리와 [같은 파일](<C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Survival/SurvivalRun.Waves.cs:311>)의 큰 덩치 행진은 그 검사를 하지 않습니다. 화면 가장자리가 울타리 반지름보다 가까운 방향에서는 무리가 안쪽에 나타나 중간 보스전의 출현 규칙을 깹니다. 모든 무리 생성 위치에 같은 울타리 검사를 적용하고, 바깥의 빈 자리로 옮기거나 해당 개체를 건너뛰어야 합니다.

5. **중간 — 울타리가 밀어낸 적이 벽 속에 들어갈 수 있습니다.** [SurvivalRun.Waves.cs](<C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Survival/SurvivalRun.Waves.cs:137>)는 안쪽 적을 반지름 밖의 한 점으로 바로 옮기며, 그 자리가 집·기계·맵 경계와 겹치는지 확인하지 않습니다. 성곽의 집 근처에서 울타리가 생기면 적이 집 충돌체 안에 놓일 수 있습니다. 몸 크기를 반영한 빈 위치를 찾고, 가능한 방향이 없을 때의 처리도 정해야 합니다.

6. **중간 — 열매 구슬은 적에게 작용하지 않습니다.** [HealOrb.cs](<C:/Users/choij/Desktop/HistoryHeroes/Game/Assets/Scripts/Dungeon/Devices/HealOrb.cs:56>)는 `PlayerController`만 받습니다. 요청에 명시된 “장치는 적에게도 똑같이 작용” 규칙을 열매에도 적용한다면, 적이 닿을 때 회복하는 처리가 필요합니다.

**테스트 제안:** 기존 `clears > 0`이고 `maps`가 비어 있는 저장을 로비에서 열어 맵 2 해금과 맵 1 기록·별을 확인하세요. 맵 3은 360초 직전부터 세 번째 엘리트 처치까지 진행해 ★2 판정을 확인해야 합니다. 성곽에서는 집 옆에서 중간 보스를 불러 생성 위치와 밀려난 적의 충돌 상태를 검사하고, 울타리 중 `Line`·`Flock`·`March` 무리의 생성 위치를 기록하면 됩니다. 이번 검토에서는 Unity 실행이나 테스트는 하지 않았습니다.
---

## 반영 (Claude, 2026-10-28)

| # | 지적 | 결정 |
|---|---|---|
| 1 | 맵 3 ★2 "6분 전 엘리트 3"이 불가능 (셋째가 6:00에 나옴) | **반영.** 엘리트 시간표(2·4·6·8분, 검토 6번)는 그대로 두고 임무를 **7분 전 엘리트 3마리**로 |
| 2 | 옛 클리어 저장에서 맵 2가 잠김 | **반영.** 맵 1 기록을 읽기만 해도 옮긴다 (`FindMap`). 옛 클리어 = 클리어 1 · ★1 · 첫 클리어 받음 |
| 3 | 중간·마지막 보스가 집·기계 안에 생길 수 있음 | **반영.** 몸 크기만큼 빈자리를 둘레에서 찾는다 (`FreeAround`) |
| 4 | 울타리 동안 무리가 울타리 안에 생김 | **반영.** 모든 무리 생성 자리에 울타리·장애물 검사 (안이면 그 개체는 건너뛴다) |
| 5 | 울타리가 민 적이 벽 속에 들어감 | **반영.** 밀려나는 방향부터 좌우 22°씩 돌려 빈자리, 없으면 화면 밖 띠 |
| 6 | 열매 구슬이 적에게 작용하지 않음 | **그대로 (설계 결정).** 회복 구슬은 "장치"가 아니라 **줍는 물건** — 적이 주우면 영웅이 손해만 본다. "적에게도 똑같이"는 위험 장치(폭약·쇠창살·톱니·개울)의 규칙. Expansion1 2-1장에 적음 |
