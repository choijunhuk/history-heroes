# Codex 교차 리뷰 — 계정 · 결제 (2026-11-01)

> 모델: Codex `gpt` (read-only, effort high) · 범위: 계정(UGS · 카카오 · 클라우드 저장 · 삭제) + 인앱 결제 (Unity IAP 5) · 앞선 Claude sonnet 리뷰 2개(계정 🔴2 · 결제 🔴3)는 반영 뒤

## 결과

**RED 2건, YELLOW 2건입니다. 병합 전 수정이 필요합니다.**

1. [Game/Assets/Scripts/Records/SaveSystem.cs:61](/Users/choi/Documents/history-heroes/Game/Assets/Scripts/Records/SaveSystem.cs:61) — **RED** — 본 파일 손상 후 정상 `.bak`로 복구하면 `IsFresh=false`여서 `LostSaveOnLoad`가 복구를 감지하지 않습니다. 기존 동기화 ID가 유지되어, 구매 전 백업이 최신 클라우드의 보석·구매 장부를 자동으로 덮습니다. 이미 소비·확정된 결제는 재전송되지 않습니다. — **수정:** 과거 백업 복구도 동기화 기준을 무효화하고, 클라우드 복구 선택 전까지 업로드를 차단하세요. 완성된 `.tmp` 승격은 구별해야 합니다.

2. [Game/Assets/Scripts/Account/AccountService.cs:553](/Users/choi/Documents/history-heroes/Game/Assets/Scripts/Account/AccountService.cs:553) — **RED** — 두 기기가 같은 `uploadId`를 조회한 뒤 동시에 저장하면 모두 충돌 검사를 통과합니다. 현재 `SaveAsync(Dictionary<string, object>)`는 조건 없이 저장하므로 마지막 요청이 다른 기기의 최신 진행·구매 장부를 덮습니다. — **수정:** 조회한 `save`·`meta`의 write lock을 보존해 조건부 저장하고, 충돌 응답은 선택 화면으로 보내세요. 명시적 덮어쓰기도 사용자가 확인한 버전 이후의 변경은 보호해야 합니다. ([Unity 공식 문서](https://docs.unity.com/en-us/cloud-save/concepts/write-locks))

3. [Game/Assets/Scripts/Economy/IapService.cs:145](/Users/choi/Documents/history-heroes/Game/Assets/Scripts/Economy/IapService.cs:145) — **YELLOW** — 계정당 패키지 제한과 상품별 첫 구매 혜택이 로컬 장부만 확인합니다. A에서 구매한 뒤, 과거 저장을 가진 B가 충돌 선택을 미루면 같은 계정에서 다시 구매·지급받습니다. 서로 다른 거래 번호라 병합도 양쪽 혜택을 그대로 유지합니다. 기존의 ‘동일 미확정 거래 재전송’ 문제와 별개입니다. — **수정:** 계정별 서버 원장에서 제한·혜택을 원자적으로 판정하세요. 우선 충돌 해결·동기화가 끝나지 않은 상태에서는 해당 상품 구매를 막아야 합니다.

4. [Game/Assets/Scripts/Account/AccountService.cs:669](/Users/choi/Documents/history-heroes/Game/Assets/Scripts/Account/AccountService.cs:669) — **YELLOW** — 서버 계정 삭제 후 로컬 파일 삭제가 실패해도 로그만 남기고 새 로비로 넘어갑니다. `ResetToNewGame()`은 이 경우 기존 `_data`를 유지하므로 이전 진행·구매 기록·동의가 남아 새 게스트 아래 다시 저장될 수 있습니다. — **수정:** 영속적인 삭제 진행 표시를 먼저 남기고, 파일·백업·메모리 초기화가 완료될 때까지 정상 진입과 저장을 차단하세요. 재실행 시에도 삭제를 재시도해야 합니다.

정적 diff·호출 흐름 검토 결과입니다. Unity 실행, 실제 결제 및 두 기기 경쟁 재현은 수행하지 않았습니다. 파일은 수정하지 않았습니다.



## 반영

| # | 판정 | 처리 |
|---|---|---|
| 1 | 🔴 | **고침.** `SaveStore.LoadReport.FromOlderCopy` (본 파일 · 완성된 `.tmp` 승격이 아닌 `.bak` · 판 백업에서 읽음). `SaveSystem.LostSaveOnLoad` = 못 읽고 새로 시작 **또는** 오래된 백업. 이때 `CloudRecheckNeeded` 표시를 PlayerPrefs에 남겨 다시 켜도(그사이 저장해도) 유지 → 연결된 계정은 기억한 올린 번호를 지워 다음 올리기가 클라우드를 덮지 않고 고르기("이 기기 저장을 읽지 못했어요")를 띄운다. 올리기 성공 · 클라우드 받기 때만 표시를 지운다. 테스트: 오래된 백업 → 표시, 완성된 임시 파일 → 표시 없음 |
| 2 | 🔴 | **고침.** 올리기는 `SaveAsync(Dictionary<string, SaveItem>)` + 읽은 판의 `save` · `meta` write lock. 서버가 `CloudSaveConflictException`을 내면 ConflictPending → 로비가 고르기를 다시 띄운다. 고르기의 "이 기기 진행"도 그 화면이 읽은 판의 lock으로만 덮는다 (그 뒤 바뀌면 거절 → 다시 고르기). 키가 없어 lock 없이 처음 만들 때는 쓴 뒤 다시 읽어 내 `uploadId`인지 확인, 아니면 같은 충돌 처리. Play 확인: 고르기가 뜬 뒤 다른 기기 쓰기를 흉내 내고 "이 기기 진행" → 서버 거절("WriteLock … does not match stored WriteLock") → 고르기 다시 뜸 → 다시 고르면 올라감 |
| 3 | 🟡 | **우선 막음** (서버 원장은 서버 검증 때). `AccountService.SyncSettled` (규칙 `AccountRules.SyncSettled`): 게스트는 늘, 연결된 계정은 이번 실행에서 클라우드와 맞췄고(올리기 성공 · 받기) 충돌 · 고르기 미룸 · 계정 전환 고르기 · 저장 복구 확인이 없을 때만. 아니면 `IapLedger.Sellable`이 첫 결제 패키지를 막고(카드 "계정 확인 중", `IapService.Buy`도 거절), `FirstBonusShown`이 2배 배지 · 확인 창 약속을 뺀다. 보석 상품은 그대로 판다. 실제 지급은 장부대로라 덜 받는 일은 없고, "2배가 다른 결제에 쓰였어요" 안내는 덜 받았을 때만. 테스트 2개 |
| 4 | 🟡 | **고침.** `SaveSystem.ResetToNewGame`: 먼저 `hh.save.wipePending`(PlayerPrefs + Save) → 메모리를 새 게임으로 → 파일 · 백업 전부 지우고 새 본 파일 쓰기 → 성공해야 표시를 지운다. 실패하면 `Save()`는 쓰지 않고(다시 지우기를 시도), 로비는 전체 화면 안내로 막히며, 다음 실행은 옛 파일을 읽지 않고 이어서 지운다. 계정도 표시가 있으면 서버에 닿지 않고 올리지 않는다. 테스트: 새 게임을 못 쓰는 상태 → 옛 진행 메모리에서 사라짐 · 저장 거절 · 막힌 것 풀면 저장이 지우기를 끝냄 |
| + | — | 확인 중 찾음: 설정의 계정 칸이 장면을 열 때 한 번 켜지며 `AccountService.Start()`를 불러 **동의 전에 서버에 닿았다** (첫 반영 때 만든 버그). `Start()`가 동의 전 · 지우기 대기 중이면 아무것도 안 하게 고침. Play 확인: 동의 전 Offline, 동의 뒤 Guest |
| | | 확인: 컴파일 ✅ · EditMode 787/787 · Play (게스트 로그인 · 시험 계정 올리기 · write lock 충돌 · 가짜 상점 구매 `gems_t1` 유료 100 + 2배 무료 100) · `release.sh 1.0.0-test --no-upload` 성공 · ProjectSettings = HEAD |

## 재확인 (Codex, 같은 날)

수정 커밋 `ab761f1`을 다시 봄: 1 · 4 해결. 남은 것 두 가지:
- 🔴 2 — 처음 만들기(키가 없을 때)는 lock 없이 써서 두 기기가 동시에 처음 만들면 막지 못함.
- 🟡 3 — 보석은 동기화 전에도 팔아서 실제 첫 구매 2배가 오래된 장부로 지급됨.

| # | 처리 (Claude, 메인 스레드) |
|---|---|
| 2 | Cloud Save 3.4.2에는 "없을 때만 만들기"가 없다 (`SaveItem` 주석: 새로 만들 때는 lock을 비운다) → 서버 원자성은 Cloud Code가 있어야 가능. 처음 만든 뒤 **바로 · 3초 뒤 두 번** 다시 읽어 둘 다 내 번호일 때만 맞춘 것으로 침. 그래도 남는 동시 생성 틈은 다음 업로드의 번호 검사(`CloudChangedElsewhere`)가 잡아 고르기 창을 연다 — 이 기기 저장은 지우지 않으므로 조용히 사라지지 않는다 |
| 3 | 연결된 계정은 동기화가 정리되기 전에는 **모든 상품**을 팔지 않음 (`IapLedger.Sellable` = 사용 가능 && 동기화 정리). 테스트 기대값을 같은 명세로 바꿈 |

## 최종 판정 (Codex, `de42edf`)

- 🔴 2 → **수용 가능한 잔여 위험**: 두 번 다시 읽은 뒤에만 맞춘 것으로 치고, 늦게 덮인 경우는 다음 실제 업로드의 번호 비교가 잡으며 기기 저장은 남는다. 감지 전 동시 구매 혜택 중복 가능성은 서버 원장(Cloud Code) 전까지 남는다.
- 🟡 3 → **해결**: UI와 구매 진입점 모두 `Available && syncSettled`.
- **main 병합을 막는 🔴 없음.** (정적 검토 · 두 기기 실제 경쟁 재현은 안 함)
