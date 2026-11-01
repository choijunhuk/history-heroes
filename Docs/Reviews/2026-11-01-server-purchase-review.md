# 교차 리뷰 반영 — 서버 결제 검증 (2026-11-01)

> 대상: 커밋 `b423554` (Cloud Code C# 모듈 `HistoryHeroesServer` · 게임 쪽 서버 검증). 리뷰어 2개: Codex · Claude sonnet. 🔴 4건 · 🟡 2건 + sonnet 🟡 묶음.
> 반영: Claude (같은 날, 작업 트리). 서버 `dotnet test` 69개 · EditMode 812개 · 모듈 production 다시 배포 · Play 확인 (`ServerCheck` 준비=True, 가짜 토큰 `invalid google_404:applicationNotFound`, 가짜 상점 구매) · `release.sh 1.0.0-test --no-upload` 성공.

| # | 지적 | 판정 | 처리 |
|---|---|---|---|
| R1 | 서버가 구글에 확정(acknowledge)하지 않는다 → 변조 · 꺼진 게임이 보석만 받고 3일 자동 환불을 받는다 (둘 다) | 🔴 고침 | 장부에 지급을 lock으로 쓴 **뒤** 서버가 `purchases.products.acknowledge`. 구글이 이미 확정 · 소비됐다고 하면(`acknowledgementState 1` · `consumptionState 1`) 부르지 않음. 확정이 거절되면 구매를 다시 읽어 확정 · 소비 · 취소 상태면 끝난 것으로 (끝없이 반복 안 함). 실패하면 지급은 그대로 `granted`, 장부 칸 `acknowledged=false` + 토큰 원문(`ackToken`, 게임에 안 보냄)을 남겨 다음 `GetPurchases` · `VerifyPurchase`(already)에서 다시, 끝나면 토큰 삭제. 3일이 지나면 확정을 멈추고 토큰만 지움 (구글 환불 → R2가 회수). 게임 소비(ConfirmPurchase)는 그대로 — 다시 살 수 있게. 게임의 소비 실패는 `PurchaseMissing`(이미 소비)을 끝난 것으로, 그 밖은 6번까지만 다시. 시험: 확정 성공 · 이미 끝남 · 실패 후 GetPurchases/already 재시도 · 3일 포기 · 응답에 토큰 없음, 구글 클라이언트 확정 POST · 거절 뒤 상태 확인 4가지 |
| R2 | 환불 · 취소된 결제를 `already` · `GetPurchases`가 계속 유효로 돌려준다 (둘 다) | 🔴 고침 | `GetPurchases` 때 게임 전체 정리 작업(10분에 한 번, 작업 상태 lock으로 하나만): `voidedpurchases.list`(지난번 본 환불 시각 − 1시간부터, 최대 29일 전, 1000건 × 3쪽) → 토큰 해시로 **색인**(지급 전에 쓰는 Cloud Save private custom `iap_index_{해시 앞 2자}`: 토큰 해시 → 플레이어)에서 플레이어를 찾아 장부에 `voided`(+시각, 주문 번호도 맞춤). `already`였던 토큰은 `voided`(지급 없음). 게임: `GetPurchases`의 voided 칸 · `voided` 답 → 그 결제로 준 유료 · 무료 보석을 빼고(모자라면 **음수**), 골드 · 소환권은 남은 만큼만, 음수 동안 상점 상자 · 유물 뽑기 잠김(보석 결제는 열어 둠), 새로 얻는 보석은 빚부터, 상점 위 줄 + 안내 문구. 회수한 결제는 서버 맞추기 · 클라우드 고르기 어느 쪽으로도 되살아나지 않음 (`MergeInto`가 반대쪽도 회수). 예약 실행(UGS Scheduler)은 에디터에서 배포할 수 없어(Scheduler 패키지 필요 · 새 패키지 금지) 플레이어 호출에 얹음 — `ponytail:` 주석 · Monetization 8장 결정 항목 |
| R3 | 첫 장부 만들기의 lock 없는 쓰기가 다른 승인된 결제를 지울 수 있다 (Codex 🔴, sonnet 🟡) | 🔴 고침 | `GetPurchases`가 장부가 없으면 빈 장부를 만든다 (게임은 이걸 받은 뒤에만 판다). `VerifyPurchase`도 없으면 빈 장부 → 다시 읽어 lock → lock으로 더하기. **결제가 든 쓰기는 모두 lock** (시험이 모든 쓰기를 검사). 남는 틈(두 호출이 동시에 빈 장부를 쓰고 늦은 쪽이 그사이 더한 결제를 덮는 경우)은 `ponytail:` 주석에 왜 무시할 만한지(늦은 쪽의 한 번 왕복이 다른 쪽의 만들기 → 다시 읽기 → 구글 → 색인 → 쓰기 전체보다 길어야 하고, 판매는 장부가 생긴 뒤에만) |
| R4 | `AskServer`가 기다리는 사이 플레이어 · 저장이 바뀌어도 A의 지급을 B 저장에 넣고 A 결제를 소비한다 (Codex 🔴, sonnet 🟡) | 🔴 고침 | `SaveSystem.Generation`(다시 읽기 · 저장 장치 바꾸기 · `Replace`(클라우드 진행) · 계정 삭제 때 +1). 묻기 직전 `IapService.Owner`(플레이어 ID + 세대)를 잡고, 답을 쓸 때 다르면 지급도 소비도 하지 않고 결제를 남겨 다시 묻는다. 시험: 묻는 사이 `Replace` → 지급 · 확정 0, 주인 비교 4가지. 서버 쪽: 다른 플레이어가 같은 토큰으로 불러도 `account_mismatch`, 장부 · 색인은 원래 플레이어 그대로 |
| Y1 | 처리방침은 토큰 원문을 저장하지 않는다는데 기기 장부(클라우드에도 올라감)에 원문 토큰을 적는다 | 🟡 고침 | 기기 장부의 거래 번호는 늘 `sha256:<토큰 해시>` (`IapLedger.IdOf`, 서버 해시와 같음). 가짜 상점 길도 같게. 옛 저장의 원문 토큰은 읽을 때 바꾼다 (`SaveStore` 정리 단계, 구조 번호는 그대로 — 칸 모양이 같아서). 시험 기대값 2개(`"t-6"` · `"dup"`)를 같은 명세로 바꿈 |
| Y2 | 처리방침의 "삭제 뒤 5년 보관"을 구현한 곳이 없고 보호 데이터가 끝없이 남을 수 있다 | 🟡 고침 | `ArchiveAndDeletePurchases`: 장부를 보관본(Cloud Save private custom `iap_archive_{지운 달}`/`d{일}`, ID = SHA-256("archive:"+플레이어 ID), `deleteAfter` = 지운 날 + 5년, 칸: 상품 · 주문 번호 · 토큰 해시 · 시각 · 수량 · 환불 여부)으로 옮기고 색인 · 장부 삭제. 게임의 계정 삭제가 Cloud Save를 지우기 전에 부르고, 실패하면 삭제를 멈춘다. 정리 작업이 "지운 달 마지막 날 + 5년"이 지난 달을 통째로 지운다. 처리방침 1 · 3장 · 계정 삭제 안내를 정확히 그대로 고침. **처리방침 판은 4 그대로** — 4판이 든 빌드를 아직 내보내지 않았다 (마지막 배포 1.0.0-beta.3은 3판) |
| s1 | 액세스 토큰 · 서비스 계정을 싱글턴에 캐시 | 🟡 고침 | 토큰은 `expires_in − 60초`까지(SemaphoreSlim, 401이면 버림), 서비스 계정은 10분 (없음은 기억하지 않음). 기록에 안 남김 |
| s2 | `PurchaseState`가 없으면 결제된 것으로 보면 안 된다 | 🟡 고침 | `int?`, 없으면 `retry state_missing` |
| s3 | GetPurchases 오류가 날 예외로 나간다 | 🟡 고침 | `{status: "retry"}` 정돈된 답. 게임은 `status`가 ok가 아니면 맞추지 않음 |
| s4 | 함수 전체 시간 한도 하나 | 🟡 고침 | 12초 `CancellationToken`을 모든 Cloud Save · Secret(`WaitAsync`) · 구글 호출에. 정리 작업은 그 절반까지. 시간 초과 → `retry timeout` |
| s5 | 패키지 이름 상수 = 게임 ID 시험 | 🟡 고침 | EditMode `PackageName_MatchesAndroidIdentifier` (서버 소스의 상수 vs `PlayerSettings` 안드로이드 ID) |
| s6 | 구글 오류 이유를 기록 (토큰 말고) | 🟡 고침 | `google_404:purchaseTokenNotFound`처럼 HTTP 상태 + 구글 `errors[0].reason`/`status`만 (메시지 전문 안 씀), 토큰 엔드포인트는 `google_auth_400:invalid_grant` |
| s7 | 막 산 결제의 404는 잠시 다시 | 🟡 고침 | 게임이 처음 물은 뒤 10분 동안 `google_404…`는 "결제 확인 중"으로 다시 묻기 (서버는 404 = invalid 그대로 — 지급 판단은 같다) |
| s8 | 대기 중 결제 안내 | 🟡 고침 | `retry pending` → "결제가 아직 끝나지 않았어요. 결제가 끝나면 들어와요." |
| s9 | 프로모션(`purchaseType 1`) · 보상형(`2`)은 지급 안 함 | 🟡 고침 | `invalid not_paid_promo/rewarded`, 장부에 안 적음(기록만), 게임 안내 "무료 결제(프로모션 코드 · 보상형)는 이 게임에서 지급하지 않아요…" |
| s10 | 기록은 예외 종류 + HTTP 상태만 | 🟡 고침 | `PurchaseVerifier.Describe` (ApiException 종류 · 상태, HttpRequestException 상태) |
| s11 | `MergeServer`가 옛 진행으로 바꾼 뒤 결제를 다시 넣는 것을 의도로 적기 | 🟡 문서 | `IapLedger.MergeServer` 주석 · Monetization 7-1 "맞추기": 산 것은 되살린다(의도), 쓴 기록이 옛 진행에 없어 보석이 다시 생겨 보이는 건 지갑이 기기 기준이라 남는 한계 |

남은 것 (다음 단계): 보석 잔액 · 쓰기 · 회수 실행의 서버 기준, 회수 안내 우편, 예약 실행(Scheduler)으로 정리 작업 옮기기, Play Console 서비스 계정 초대(👤 앱을 만든 뒤).
