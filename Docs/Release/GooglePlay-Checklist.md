# 📦 구글 플레이 출시 점검표

> 점검: 2026-11-01 (구글 플레이 정책 · Unity 6000.3.27f1 기준) · 근거 링크는 각 줄 끝
> 표시: 🔴 안 하면 못 올림 · 🟡 고쳐야 함 · 🟢 괜찮음 / 🤖 Claude가 프로젝트에서 · 👤 개발자가 콘솔 · 계정에서

## 1. 이미 한 것 (2026-11-01)

| | 내용 |
|---|---|
| ✅ | **목표 API 36 고정** (`AndroidTargetSdkVersion: 36`). 2026-08-31부터 새 앱 · 업데이트는 API 36 이상 필수. "자동"이면 Unity에 들어 있는 SDK 중 가장 높은 37로 잡혀 Unity가 시험하지 않은 값이 된다 — [목표 API 요구](https://developer.android.com/google/play/requirements/target-sdk) |
| ✅ | **앱 아이콘** (적응형 앞 · 뒤 층 + 기본 아이콘) + 스토어용 512px `Docs/Release/icon-512.png`. 메뉴 **History Heroes → 앱 아이콘 다시 만들기** |
| ✅ | **출시 서명 키** `~/.history-heroes/release.keystore` (저장소 밖). 구글 플레이에서는 이 키를 **업로드 키**로 쓴다 |
| ✅ | **AAB 빌드** 메뉴 **History Heroes → 구글 플레이용 AAB 만들기** (플레이는 AAB만 받는다) |
| ✅ | **16KB 메모리 페이지 정렬 확인**을 `Tools/dev/release.sh`에 넣음 (안드로이드 15+ 요구, 외부 네이티브 플러그인 없음) |
| 🟢 | 알림: 정확한 알람 권한 안 씀(`ScheduleExactAlarms: 0`), 안드로이드 13+ 알림 권한은 첫 판 뒤 이유를 먼저 보여 주고 요청 — 올바른 방식 |
| 🟢 | 권한: 진동 · 부팅 후 알림 다시 걸기 · 알림뿐 (민감 권한 신고 대상 없음) |
| 🟢 | 64비트(ARM64) · 최소 안드로이드 7.1 · "게임" 분류 → 안드로이드 16 대형 화면 방향 강제에서 **게임은 면제** |
| 🟢 | 노치 · 펀치홀: 안전 영역 맞춤(`SafeAreaFitter`) · GameActivity 전체 화면 (안드로이드 15 가장자리까지 그리기 대응) |

## 2. 출시 전에 할 것

| | 할 일 | 누가 |
|---|---|---|
| 🔴 | **Play Console 개발자 계정** ($25, 신분 확인). **개인 계정은 정식 출시 전에 테스터 12명이 14일 동안 비공개 테스트에 참여해야 한다** → 최소 2주 걸리니 계정부터 먼저 — [새 개인 계정 테스트 요건](https://support.google.com/googleplay/android-developer/answer/14151465) | 👤 |
| 🔴 | **개인정보 처리방침 공개 주소**: `Docs/Legal/privacy-ko.md`에 문의 이메일 · 시행일을 채우고 웹에 공개 (GitHub Pages 등). 공개 저장소라 이메일은 개발자가 정해서 넣는다 | 👤 + 🤖 |
| 🔴 | **Play 앱 서명** 등록 (업로드 키 = 위 키). 키 폴더 `~/.history-heroes/`를 다른 곳에 꼭 백업 | 👤 |
| 🟡 | 첫 AAB를 **내부 테스트 트랙**에 올려 **출시 전 보고서**(여러 기기 자동 실행 · 충돌) 확인 | 👤 + 🤖 |
| 🟡 | AAB 크기 확인 (기본 모듈 200MB 한도. `Game/Assets` 177MB → 압축 뒤 100~150MB 예상) | 🤖 |
| 🟡 | GitHub APK와 플레이 판은 **서명이 달라** 서로 업데이트가 안 된다 (지우고 다시 깔기 = 폰 저장 사라짐). README · 릴리스 설명에 적어 둠. 플레이 출시 뒤에는 GitHub APK 배포를 멈추거나, 플레이 앱 서명에 이 키를 그대로 올리는 방식을 고른다 | 👤 결정 |
| 🟡 | 앱 콘텐츠 신고: 데이터 보안(지금은 "수집 없음", 로그인 · 광고를 넣으면 다시), 광고 여부, 타깃 연령(13세 이상), IARC 등급 설문 (`StoreListing.md` 체크리스트) | 👤 |
| 🟡 | 확률 공개: 게임 안 · 스토어 설명 · 웹 (게임산업법, 2024-03) — 문서는 `Docs/Public/probabilities.md`, 웹 게시만 남음 | 👤 |

## 3. 로그인 · 결제 · 광고를 넣을 때 따라오는 것

| 넣는 것 | 따라오는 일 |
|---|---|
| 로그인 (게스트 → 구글 · 카카오) | **계정 삭제**를 앱 안 + 웹 두 곳에서 (구글 정책 필수, LiveOps 3-3). 데이터 보안 신고 바꾸기 |
| 계정 (2026-11-01 구현: 게스트 · 카카오 · 클라우드 저장 · 삭제) | 🤖 앱 안 삭제는 설정 → 계정 → 계정 삭제. 👤 웹 삭제 페이지 `Docs/Public/account-deletion.md`를 GitHub Pages에 올리고 그 주소를 **앱 콘텐츠 → 데이터 보안 → 데이터 삭제**에 등록. 데이터 보안: "사용자 ID(게임 계정 ID) · 앱 활동(게임 진행) 수집, 전송 중 암호화, 삭제 요청 가능"으로 바꾸기 |
| 구글 플레이 게임즈 | Play Console에서 게임즈 프로젝트 · OAuth 클라이언트 (SHA-1은 **플레이 앱 서명 키** 기준) |
| 카카오 로그인 | 카카오 디벨로퍼스 앱 등록 · 네이티브 앱 키 · **키 해시** 등록 (플레이 앱 서명 키 해시도) · OpenID Connect 켜기 |
| 결제 (Unity IAP 5) | 🤖 2026-11-01 구현 (IAP 5.4.4 · 결제 라이브러리 9 · `BILLING` 권한 자동). 👤 아래 "결제 켜기" 순서대로. 환불 · 청약철회 · 미성년자 안내는 게임 안 확인 창과 이용약관 4장에 있다 |
| 광고 (LevelPlay) | 광고 ID(`AD_ID`) 신고 · 데이터 보안 바꾸기 |

새 SDK를 넣을 때마다 16KB 정렬을 다시 확인한다 (`release.sh`가 알려 준다).

### 결제 켜기 (👤 개발자 계정 신분 확인 뒤, 코드 수정 없음)

> 🔴 **영수증 검사 키(5번)를 만들고 다시 빌드하고, 아래 "서버 결제 검증 켜기"를 끝내기 전에는 실제 판매를 시작하지 않는다.** 키가 없는 안드로이드 빌드는 아무것도 팔지 않는다 (상점 카드 "결제 준비 중", 이미 온 결제는 지급 · 확정하지 않아 구글이 3일 안에 환불). 빌드할 때 `[IAP] 영수증 검사 키(GooglePlayTangle)가 없어요` 경고가 뜨면 아직이다.
> 🔴 **IARC 연령 등급을 받기 전에는 실제 판매를 시작하지 않는다.** 등급이 나오면 `Consent.RatingText`(동의 화면 등급 줄)를 그 등급으로 바꾼다.

1. **판매자 계정(결제 프로필)** 만들기: Play Console → 설정 → 결제 프로필. 한국 판매자는 사업자 정보 · 통신판매업 신고가 필요할 수 있다 (출시 전 확인).
2. 앱을 만들고 **`BILLING` 권한이 든 AAB를 내부 테스트 트랙에 한 번 올린다** (그래야 상품을 만들 수 있다).
3. **수익 창출 → 제품 → 인앱 상품**에서 아래 7개를 만든다. **상품 ID는 한 번 만들면 바꾸거나 다시 쓸 수 없다** — 표와 글자 하나까지 같게. 전부 **활성**으로. (원본: `Game/Assets/Scripts/Economy/IapCatalog.cs`)

| 상품 ID | 종류 | 가격 (KRW) | 이름 | 설명 |
|---|---|---|---|---|
| `starter_pack` | 인앱 상품 (소모품으로 씀, 계정당 1번은 게임이 지킴) | ₩1,200 | 첫 결제 패키지 | 보석 300개, 고급 소환권 2장, 골드 10,000을 한 번에 받아요. 계정당 1번만 살 수 있어요. |
| `gems_t1` | 인앱 상품 | ₩1,200 | 보석 한 줌 | 보석 100개. 상품마다 처음 한 번은 기본 보석만큼 더 받아요. |
| `gems_t2` | 인앱 상품 | ₩5,900 | 보석 주머니 | 보석 500개 + 보너스 50개. 상품마다 처음 한 번은 기본 보석만큼 더 받아요. |
| `gems_t3` | 인앱 상품 | ₩12,000 | 보석 상자 | 보석 1,000개 + 보너스 150개. 상품마다 처음 한 번은 기본 보석만큼 더 받아요. |
| `gems_t4` | 인앱 상품 | ₩33,000 | 보석 궤짝 | 보석 2,750개 + 보너스 550개. 상품마다 처음 한 번은 기본 보석만큼 더 받아요. |
| `gems_t5` | 인앱 상품 | ₩59,000 | 보석 수레 | 보석 4,900개 + 보너스 1,100개. 상품마다 처음 한 번은 기본 보석만큼 더 받아요. |
| `gems_t6` | 인앱 상품 | ₩119,000 | 보석 보물고 | 보석 9,900개 + 보너스 2,600개. 상품마다 처음 한 번은 기본 보석만큼 더 받아요. |

   - 구글 플레이의 새 "일회성 제품" 화면이면: 구매 옵션 1개(구매), 다중 수량 끄기. 가격은 한국 KRW로 넣고 다른 나라는 자동 환산.
4. **라이선스 테스터** 등록 (설정 → 라이선스 테스트): 테스터 계정은 실제로 돈이 나가지 않는 시험 카드로 산다.
5. **영수증 검사 키 만들기:** Play Console → 수익 창출 설정 → **라이선스**의 Base64 공개 키를 복사 → 유니티 메뉴 **Services → In-App Purchasing → Receipt Validation Obfuscator** → 키 붙여 넣기 → Obfuscate. `Assets/Scripts/UnityPurchasing/generated/GooglePlayTangle.cs`가 생긴다 (공개 키라 비밀은 아니다). 다시 빌드하면 게임이 알아서 찾아 검사한다 (그 전에는 검사를 건너뛴다).
6. 다시 빌드해 내부 테스트로 올리고, 테스터 폰에서 보석 탭 가격이 ₩로 뜨는지 · 각 상품을 사서 보석이 들어오는지 · 같은 결제가 두 번 들어오지 않는지 확인.
7. **데이터 보안** 신고에 "구매 기록(앱 활동 → 구매 내역)" 수집 추가. 개인정보 처리방침 **4판** 공개 주소 갱신 (서버 결제 장부 · 법정 기간 분리 보관).
8. 아래 **서버 결제 검증 켜기**를 끝낸다. 안 하면 기기는 서버 장부를 받은 뒤 "결제 준비 중"으로 아무것도 팔지 않고, 이미 온 결제는 지급 · 확정하지 않는다 (구글 자동 환불).

### 서버 결제 검증 켜기

> 🤖 이미 한 것 (2026-11-01): Cloud Code C# 모듈 `HistoryHeroesServer`를 UGS 프로젝트 `4c301e37-…` **production** 환경에 배포함. 지금은 비밀값이 없어 모든 결제 검증이 `not_configured`로 답한다 (안전한 쪽). 코드: `Server/CloudCode/HistoryHeroes.Server/` · 게임 쪽 `Game/Assets/Scripts/Economy/PurchaseServer.cs` · 설계 [Monetization 7-1](../Design/Monetization.md#7-1-구현-상태-2026-11-01-1차).
> 🔴 **서비스 계정 키(JSON)는 비밀이다.** 저장소 · 채팅 · 메일에 붙이지 않는다. Secret Manager에 넣은 뒤 내려받은 파일은 지운다 (잃어버리면 새 키를 만들면 된다).

1. 👤 **Google Cloud 프로젝트** 만들기 (또는 있는 것 쓰기): [console.cloud.google.com](https://console.cloud.google.com) → 프로젝트 만들기 (예: `history-heroes`).
2. 👤 **Google Play Android Developer API 켜기:** Cloud Console → API 및 서비스 → 라이브러리 → "Google Play Android Developer API" → **사용**.
3. 👤 **서비스 계정 만들기:** Cloud Console → IAM 및 관리자 → 서비스 계정 → **서비스 계정 만들기** (이름 예: `play-purchase-verifier`). Cloud 쪽 역할은 주지 않아도 된다 (권한은 Play Console에서 준다).
4. 👤 **JSON 키 만들기:** 그 서비스 계정 → 키 → 키 추가 → 새 키 만들기 → **JSON** → 파일이 내려받아진다 (`client_email` · `private_key`가 들어 있음). 조직 정책이 키 만들기를 막으면 그 정책을 이 프로젝트에서만 풀어야 한다.
5. 👤 **Play Console에 초대:** Play Console → **사용자 및 권한** → 새 사용자 초대 → 이메일에 서비스 계정 주소(`…@….iam.gserviceaccount.com`) → 앱 권한에 History Heroes 추가 → 권한: **재무 데이터, 주문, 취소 설문조사 응답 보기** + **주문 및 구독 관리** → 사용자 초대. (구글 안내: [Play Developer API 시작하기](https://developers.google.com/android-publisher/getting_started)) 권한이 퍼지는 데 몇 시간 ~ 하루가 걸릴 수 있다 — 그동안 검증은 `retry`(401/403)로 답하고 결제는 남겨 두었다가 다시 묻는다.
6. 👤 **UGS Secret Manager에 넣기:** [cloud.unity.com](https://cloud.unity.com) → 프로젝트 History Heroes → **Development → Secrets** (프로젝트 단계, 또는 production 환경 단계) → **Add secret** → 키 `GOOGLE_PLAY_SERVICE_ACCOUNT` (글자 그대로) → 값: 4번 JSON 파일 **내용 전부** 붙여 넣기 → 접근 서비스에 **Cloud Code** 체크 → Add. 그다음 내려받은 JSON 파일을 지운다.
7. 🤖 **모듈 다시 배포는 필요 없다** (비밀값은 부를 때마다 읽는다). 확인: 에디터 Play → 동의 → 게스트 로그인 뒤 `unity command eval` 로 `QaTools.ServerCheck()` → 잠시 뒤 `QaTools.ServerCheckResult()` → `GetPurchases 준비=True` · 가짜 토큰 `invalid google_400`(또는 `google_404`)이면 키 · 권한까지 맞다. `retry google_auth` · `google_401/403`이면 5번 권한이 아직 안 퍼졌거나 키가 틀렸다.
8. 👤 + 🤖 **라이선스 테스터로 실제 결제 시험** (내부 테스트 트랙, 위 "결제 켜기" 4 · 6번 뒤): 보석 상품 하나를 사서 ① 보석이 들어오고 ② 다시 열어도 두 번 들어오지 않고 ③ 같은 상품을 한 번 더 사면 첫 구매 2배가 빠지고 ④ 첫 결제 패키지를 두 번 사면 두 번째는 "계정당 1번" 안내 + 3일 안 자동 환불인지 본다. Unity 대시보드 → Cloud Code → Logs에서 `지급 … 테스트 True` 줄과 Cloud Save → 플레이어 → Protected 데이터 `purchases`를 확인한다.
9. 🤖 **모듈을 고쳤을 때 다시 배포:** 유니티 메뉴 **Services → Deployment** → 창 위 환경이 `production`인지 확인 → `Assets/CloudCode/HistoryHeroesServer.ccmr` 고르기 → **Deploy Selected**. 필요한 것: .NET 9 SDK (맥 `/usr/local/share/dotnet/dotnet`, 윈도우는 `dotnet`이 PATH에 — 경로는 Preferences → Cloud Code). 상품표(`IapCatalog.cs`)를 고쳐도 서버가 같은 파일을 쓰므로 **꼭 다시 배포**한다.

아직 없는 것: 환불 회수(Voided Purchases API — 서버 장부의 주문 번호 · 토큰 해시로 맞춘다) · 보석 잔액 · 쓰기의 서버 기준 — [Monetization 7-1](../Design/Monetization.md#7-1-구현-상태-2026-11-01-1차).
