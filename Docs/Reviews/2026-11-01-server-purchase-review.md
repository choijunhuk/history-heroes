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

## 2차 — Codex 재확인 (`74f7b6d`) + 자동 보안 리뷰

> R1 · R4는 해결로 판정. R2 · R3 · Y1 · Y2가 남았다는 판정과 새 🔴. 대부분의 뿌리: Cloud Save에는 "없을 때만 만들기"가 없는데, 여러 문서를 처음 만들 때 lock 없이 써서 동시에 쓴 내용을 덮을 수 있었다.
> 다시 설계: **진짜 내용이 든 쓰기는 lock 없이 하지 않는다.** lock 없이 쓰는 건 (1) 늘 같은 처음 값(머리 · 작업 상태) (2) 토큰 · 플레이어마다 하나뿐인 새 키뿐. Cloud Save batch는 한 접근 등급 안에서 전부 되거나 전부 안 된다 (Cloud Save API 문서 item-batch: "The batch set operation is considered atomic and if any of the set key operation fails, the entire operation is failed", 충돌 409).
> 확인: 서버 `dotnet test` 74개 · EditMode 814개 · 모듈 production 다시 배포 · Play (`ServerCheck` · 가짜 상점 구매) · `release.sh 1.0.0-test --no-upload` 성공 · ProjectSettings = HEAD.

| # | 지적 | 처리 |
|---|---|---|
| A 🔴 | 게임이 부를 수 있는 `ArchiveAndDeletePurchases`가 장부와 토큰 색인을 지워 → 기록을 지우고 2배 · 패키지를 다시 받거나 이미 받은(소비된) 토큰을 다시 받을 수 있다 (보안 리뷰 + Codex) | **고침.** 색인은 지우지 않는다 — 지운 계정의 칸은 플레이어 ID를 지우고 보관본 ID만 남긴다. 지급 전에 게임 전체 색인을 보고 다른 플레이어 · 지운 계정의 토큰이면 `invalid token_used` (구글에 묻지도 않음). 보관은 한 방향: 서버 전용 **닫힘 표시**(private 플레이어 데이터 `iap_closed`, 게임은 쓸 수 없음)를 남기고, 닫힌 플레이어의 `VerifyPurchase`(`account_closed`) · `GetPurchases`(`status: closed`)를 모두 거절, 두 번 보관하지 않음(남은 장부 지우기만 다시). 게임은 계정을 지우는 모든 길에서만 부른다: 설정의 계정 삭제 + **카카오 계정으로 바꿀 때 지우는 게스트**(보관이 실패하면 게스트를 지우지 않고 로그아웃만). 닫힌 계정으로 들어오면 "계정 삭제를 마무리해 주세요" 안내. 시험: 보관 뒤 같은 플레이어 · 다른 플레이어 다시 받기 거절, 닫힌 플레이어 거절, 두 번 보관 안 함 |
| B 🔴 (R3) | 빈 장부를 lock 없이 만드는 쓰기가 그사이 다른 호출이 지급한 장부를 덮을 수 있다 | **고침 (저장 방식 변경).** 결제 한 건 = protected 키 하나 `p_{토큰 해시}`(만든 뒤엔 확정 · 환불 표시만 lock으로 고침). 판정은 머리 `iap_head`로 한 줄: 머리 lock 읽기 → 결제 전부 읽기 → 판정(패키지 · 2배) → [머리(그 lock) + 새 결제 키] 한 batch. 그사이 다른 판정이 끼면 409로 통째로 거절 → 처음부터. 머리의 처음 값은 늘 `{"v":1,"n":""}` — 동시에 lock 없이 만들어도 같은 글이고, 판정은 머리 내용이 아니라 결제 키로 하므로 늦게 다시 만들어져도 잃는 결제가 없다 (`ponytail:` 주석). 시험: 동시 첫 결제(두 토큰 — 2배는 먼저 쓴 쪽), 동시 패키지 두 번(하나만), 늦은 머리 다시 만들기, 모든 쓰기 기록에서 lock 없이 쓴 것은 처음 머리뿐 |
| C 🔴 | 첫 보관이 날짜 칸을 lock 없이 써서 같은 날 두 계정의 보관본이 서로 덮고 둘 다 장부를 지운다 | **고침.** 보관본 키를 플레이어마다 따로 (`iap_archive_{ID 앞 2자}/{ID}`, ID = SHA-256("archive:"+플레이어)). 나눠 둔 칸 없음. 지우기는 정리 작업이 칸(256개)을 8개씩 돌아가며 `deleteAfter`가 지난 키만. 시험: 같은 날 두 계정 보관 → 둘 다 남음, 5년 전 남고 뒤 지움 |
| D 🔴 | 환불 표시가 5번 충돌로 실패해도 체크포인트가 넘어가 그 환불을 잃는다 | **고침.** 표시를 못 한 환불이 있으면 체크포인트를 그 가장 오래된 환불 시각에 둔다 → 다음 차례에 그것부터 다시 (겹쳐 봐도 표시는 한 번). 시험: 표시가 계속 충돌 → 체크포인트 유지 → 풀리면 다음 차례에 표시 · 체크포인트 전진 |
| E 🔴 | `MergeInto`가 회수만 했을 때 0을 돌려줘 "이 기기 진행" 길이 저장을 건너뛰고, 그 뒤 올리기가 회수 전 디스크 글을 올린다 | **고침.** 여기서도 회수한 결제를 바뀐 것으로 센다 → `AccountService.KeepPurchases`가 **올리기 전에 저장**. 시험(EditMode): 회수만 일어나는 고르기 → 저장 → 올리는 파일(`ReadUploadFile`)에 회수가 들어 있음 |
| Y1 🔴 | sha256 거래 번호 옮기기가 메모리에서만 일어나고 올리기는 디스크 글을 읽어 → 원문 토큰이 올라갈 수 있다 | **고침.** `AccountService.ReadUploadFile`: 파일 글에 읽은 데이터의 거래 번호가 그대로 없으면(옛 모양) 지금 메모리(바꾼 모양)로 먼저 저장하고 다시 읽은 뒤에만 올린다 — 옮기기를 거치지 않은 파일은 올라가지 않는다. 시험(EditMode): 원문 토큰 파일 → 올릴 글 · 디스크 모두 해시 |
| F 🟡 | 확정을 다시 하려고 남긴 토큰 원문이 플레이어가 읽을 수 있는 protected 데이터에 있다 | **고침.** 서버 전용 private custom `iap_ack/{토큰 해시}`로 옮김 (지급 전에 쓰고, 확정이 끝나면 지움, 3일이 지나면 정리 작업이 지움, 지급이 끝내 안 된 것은 1시간 뒤 지움). 시험: 확정 실패 때도 protected 데이터 · 응답에 토큰 없음 |
| G 🟡 | 기한 · 지우기 · 환불 작업이 그 플레이어가 GetPurchases를 부를 때만 돈다 | **고침.** 독립 함수 `RunMaintenance` (환불 목록 · 확정 대기 다시 확정/3일 지우기 · 보관본 지우기). 플레이어가 부르면 `forbidden`, 서버(예약) 호출끼리 1분 안 두 번은 `skipped`. 예약(Scheduler → Trigger)은 새 패키지 없이 에디터에서 배포할 수 없어 **대시보드 순서를 출시 점검표 3장 10번에 적음** (이벤트 `iap-maintenance`, cron `*/10 * * * *`, payload `{}`, 트리거 → `urn:ugs:cloud-code:HistoryHeroesServer/RunMaintenance`). 그때까지 `GetPurchases`의 10분 대비 경로를 남김. 처리방침은 "서버 정리 작업이 지운다"로만 약속하고, 점검표가 예약 만들기를 출시 전 🔴로 둔다 |
| H 🟡 | 작업 상태를 처음 만들 때 lock 없이 써서 동시 호출이 모두 "잡기"에 이긴다 | **고침.** 처음 값(늘 같은 `new JobState()`)만 lock 없이 만들고 다시 읽어 lock을 얻은 뒤 lock으로 "잡기" → 하나만 돈다. 시험: 처음 만든 직후 다른 호출이 먼저 잡음 → 우리는 돌지 않음(구글 호출 0) |

처리방침 판은 4 그대로: 4판이 든 빌드를 아직 내보내지 않았다 (마지막 배포 1.0.0-beta.3은 3판). 바뀐 글: 토큰 원문은 서버 전용 칸에만 · 정리 작업 · 색인은 지운 뒤 보관본 ID만 남김.
