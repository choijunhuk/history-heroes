# 시장 비교 조사: 유명 게임과 우리 게임 (UI/UX · 확률 · 법)

> 작성: 2026-10-31 · 조사 방법: 웹 검색(공식 법령 안내, 구글 플레이 정책, 게임 위키, 커뮤니티). 설계 문서(Gacha, Relics, Equipment, Economy, UX, UX-Overhaul-Plan, Monetization, GodsVsHumanity)와 코드 일부를 읽고 비교했다.
> **한계 (먼저 읽기):** 게임별 확률표는 게임 안 공개 화면이 아니라 **팬 위키·블로그에서 옮겨 적은 것**이다. 공식 화면을 직접 본 것이 아니므로 숫자는 "참고용"이다. 확인 못 한 것은 "확인 못 함"으로 적었다. 법률 자문이 아니다. 출시 전에 최신 법령을 다시 확인한다.

---

## 1. 참고 게임 요약 표

| 게임 | 핵심 루프 | 홈 화면 | 보상 연출 | 과금 지점 | 좋은 점 / 불만 |
|---|---|---|---|---|---|
| **탕탕특공대** (Survivor.io, Habby) | 뱀서라이크식 자동 사격 + 판 밖 장비 성장 | 위: 프로필·레벨·에너지·보석·골드. 아래 탭 5개: 상점·장비·전투·도전·진화 (나무위키) | 지원품 상자 열기, 소원 목록으로 원하는 S급 장비 지정 | 챕터 패키지(챕터마다 1회, 가성비 좋다는 평), 특공대 패스, 에너지 구매. 광고: 에너지 +5, 일일 상점 보석 30 ×2회 | 좋음: 6칸 장비 + 영웅 화면이 직관적. 불만: "과금 유도가 심하다"는 평(블로그). 7일 카니발 미션을 모르고 지나치는 유저가 많다 (나무위키) |
| **Archero 2** (Habby, 2025) | 정지 사격 + 이동 회피 (Archero 계열) | 시즌 패스가 메인 화면 위쪽 바의 큰 부분을 차지. 28일 시즌, 일일/주간 목표로 진행 (Tiny Teardown) | 열쇠로 상자 열기, 소원(Wish)으로 유물 뽑기 | 신규 팩(Newbie Pack) 3가지 가격대, 레벨업 시 임시 팩, 챕터 팩, 일일 특가, 에너지 구매 (Tiny Teardown) | 좋음: 기본 플레이가 1편보다 낫다는 평 (앱스토어 리뷰). 불만: 확률·천장 논란과 "과금 압박"을 다루는 레딧 글 다수 |
| **Archero 1** (Habby, 2019) | 위와 같음 | 확인 못 함 | 상자 열기 | 시즌 패스가 가장 잘 팔린다는 분석은 우리 Monetization.md에 있으나 원문 출처 확인 못 함 | 불만: "확률 표시를 돌려 달라"는 글(레딧). 1편 공식 확률표는 확인 못 함 |
| **Capybara Go** (Habby) | 자동 전투 + 로그라이트 선택 + 가챠 | 확인 못 함 | 확인 못 함 | 광고·IAP가 곳곳에 있음 | 구글 플레이 평점 4.6 (46만 건). 불만: "귀엽지만 극단적 P2W, 광고와 결제가 눈 닿는 곳마다 있다"(구글 플레이 리뷰), 레딧 "P2W 때문에 망가진다" |
| **Vampire Survivors** (Poncle) | 자동 공격 뱀서라이크, 짧은 판 | 확인 못 함 | 확인 못 함 | 모바일: 강제 광고·미세결제 없음, DLC (구글 플레이 리뷰 인용) | 호평: "강제 광고 없고 DLC도 괜찮다". 평점 4.4 (8만 건) |
| **Soul Knight** | 픽셀 슈팅 로그라이크 | 확인 못 함 | 확인 못 함 | 보석 팩($3.99 등), 캐릭터 $0.99 (앱스토어) | 앱스토어 평점 4.8. 일부 유저는 IAP·광고를 걱정 (레딧) |
| **한국 현재 상위권** | MMORPG·방치형 RPG가 상위, 방치형이 1위를 하기도 함 | 확인 못 함 | 확인 못 함 | 확인 못 함 | 2026년 상반기에도 MMORPG가 매출 상위를 지킨다는 블로그 요약만 확인. 매출 순위 원본은 확인 못 함. 버섯커 키우기 등은 블로그 목록에 이름만 확인 |

**한 줄 결론:** 같은 장르의 성공작(탕탕특공대, Archero 2)은 **하단 5탭 + 시즌 패스 + 신규 팩 + 보상형 광고 + 50회 천장**을 공통으로 쓴다. 우리 설계는 이미 이 틀과 거의 같다. 평가가 갈리는 지점은 "과금 표면이 너무 많을 때"(Capybara Go)이고, 호평은 "강제 광고 없음"(Vampire Survivors)이다.

---

## 2. 우리 게임과 비교: UI/UX 고칠 곳

우리 현재 구조: 하단 탭 5개(상점·영웅·전투·기록·도감), 홈 미션 띠, 탕탕식 영웅 화면(장비 6칸), 강제 광고 없음, 첫 10분 결제 팝업 없음, 확률 보기 단추.

| 우선순위 | 고칠 곳 | 근거 게임 | 크기 |
|---|---|---|---|
| **1** | **신규 7일 미션을 홈에 눈에 띄게 둔다.** 설계상 튜토리얼 후 "열림"만 하고 단추 표시뿐이다. 탕탕특공대는 같은 7일 카니발을 모르고 지나치는 유저가 많다는 지적이 있다 | 탕탕특공대 | S |
| **1** | **시즌 패스 진행 띠를 홈에 둔다** (남은 일수 + 다음 보상). Archero 2는 패스가 상단 바의 큰 부분을 차지한다. 우리는 홈 미션 띠가 이미 있으니 그 옆/아래에 같은 모양으로 붙이면 된다 | Archero 2 | M |
| **2** | **구간 패키지(챕터 패키지)**: 구간마다 1번만 살 수 있는 상품. 탕탕특공대에서 가성비 좋다는 평이 있고, 반복 구매 압박이 없다. 가격은 확인 못 함 | 탕탕특공대 | M |
| **2** | **신규 패키지를 가격대 2~3개로** 첫 보스 격파 후 상점에 조용히 둔다 (기존 방침 유지). Archero 2는 3단 가격대이고, 일찍 안 사면 높은 가격대는 사라진다 | Archero 2 | M |
| **2** | **과금 표면 상한 규칙**: 한 번의 접속에서 결제 팝업은 최대 1개, 전투 중 결제/광고 팝업 0개. Capybara Go의 대표 불만이 "곳곳에 광고·결제"이다 | Capybara Go | S |
| **2** | **원하는 장비 하나 지정(소원 목록) 검토**: 탕탕특공대는 소원 목록에 넣은 S 장비가 S 당첨 때 16.7% 확률로 나온다(디시 갤러리 글, 확인 정도 낮음). 우리는 "조각 교환소(60/300)"로 확정 경로가 있어서 급하지 않다. 교환소 위치를 상자 화면에서 바로 보이게만 하면 된다 | 탕탕특공대 | S |
| **3** | **천장 카운터 표시를 상자 화면 한가운데**("전설 확정까지 N회"). 설계 문서에 이미 있다. 구현 위치만 확인 | 탕탕특공대 (50/50 표시) | S |
| **3** | **광고 보상 보석 양을 올릴지 결정**: 탕탕특공대는 일일 상점 광고 1회당 보석 30(하루 2회). 우리는 광고 보석 하루 10×2(Economy.md) 또는 20/일(Monetization.md)로 문서 두 곳이 다르다. 숫자부터 통일해야 한다 | 탕탕특공대 | S |
| **3** | **결과 연출**: 10연은 한 화면에 모아 보여 주고 건너뛰기 가능(이미 설계됨). 유지. "거의 당첨" 가짜 연출 금지도 유지 | — | — |
| 유지 | 강제 광고 없음, 보상형 광고만. Vampire Survivors의 호평 이유와 같다. 월정액의 광고 건너뛰기, 광고 제거도 괜찮다 | Vampire Survivors | — |

**우리 탭 구성이 틀리지 않았다는 근거:** 탕탕특공대 탭(상점·장비·전투·도전·진화)과 우리 탭(상점·영웅·전투·기록·도감)은 "가운데가 전투, 왼쪽이 상점"이라는 점이 같다. 다만 우리는 `기록`과 `도감`이 성장 느낌이 약하다. 초보가 "어디서 강해지지?"를 찾기 쉽도록 영웅 탭 아이콘에 **강화 가능 빨간 점**을 확실히 달 것을 권한다. (탕탕특공대 탭 이름이 성장 행동 중심인 것과 비교)

**확인 못 함:** 각 게임의 튜토리얼 길이, 평균 세션 시간. 우리 UX.md의 "첫 공격 20초 / 첫 10분 안에 보스"는 근거 게임 자료를 못 찾았으므로 실제 플레이 테스트 결과로 검증해야 한다.

---

## 3. 확률 비교와 수정 제안

### 3-1. 참고 게임 확률 (팬 위키 기준, 참고용)

| 게임 · 상품 | 최고 등급 | 그 다음 | 중간 | 낮음 | 천장 |
|---|---|---|---|---|---|
| 탕탕특공대 지원품 상자 | 엘리트S **2.0%** | 엘리트 3.75% | 레어 30.0% | 우수 64.25% | **10회 누적마다 엘리트(보라) 확정, 50회 누적에 S급 확정** (나무위키) |
| Archero 2 Mythstone/Chromatic 상자 | Epic S **1.46%** | Epic 2.18% | Rare 38.18% | Fine 58.18% | **50번째 상자에서 S-Epic 확정** (게임 위키, 레딧) |
| Archero 2 소원(Wish) 유물 | Mythic **0.1%** | Legendary 1% | Epic 9%, Rare 18% | 나머지 | 소원에도 천장 있음. 횟수는 확인 못 함 |
| Archero 2 Obsidian 상자 | — | Epic 4% | Rare 40% | Fine 56% | 확인 못 함 |
| **우리 고급 상자** | 전설 **2%** | 희귀 13% | 고급 35% | 일반 50% | **50회 전설 확정**, 10연 안에 희귀 이상 없으면 10번째를 재추첨 |
| **우리 일반 상자** (골드 5,000) | — | 희귀 3% | 고급 27% | 일반 70% | 없음 |
| **우리 유물 뽑기** | 24종 전부 각 **4.1667%** (등급 없음) | — | — | — | 새 유물 없이 19번 → 20번째 미보유 중 확정 |

### 3-2. 읽는 법

- **장비 상자 최고 등급 2%와 50회 천장은 시장 평균과 같다.** 탕탕특공대 2.0%/50, Archero 2 1.46%/50. 우리가 더 박하지도 후하지도 않다. 이 숫자는 바꿀 이유가 없다.
- **우리는 희귀 이상이 15%** (13+2)로 탕탕특공대 5.75%(3.75+2), Archero 2 약 3.6%(1.46+2.18)보다 훨씬 후하다. 대신 낮은 등급 장비가 큰 도움이 안 되는 구조라면 체감은 비슷할 수 있다. **이건 힘 예산(Stats/Equipment)에서 희귀 장비가 얼마나 센지에 달려 있어** 숫자만으로 판단하지 않는다.
- **유물 뽑기는 시장 대비 매우 친절하다.** Archero 2 소원 유물은 최고 0.1%지만 우리는 24종 균등이라 "특정 유물이 안 나오는 억울함"이 작다. 법적으로도 공개할 확률 표가 간단하다는 이점이 있다.

### 3-3. 확률 수정 제안 (숫자)

| 번호 | 제안 | 지금 | 제안 | 이유 | 비고 |
|---|---|---|---|---|---|
| A | **희귀 천장을 "10연 안"이 아니라 "누적 10회"로** | 10연 묶음에서만 희귀 이상 보장 (1회씩 열면 보장 없음) | 고급 상자는 단품·10연 구분 없이 누적 10회 안에 희귀 이상 1개 확정 (희귀 이상 나오면 0으로) | 탕탕특공대는 누적 10회 확정이다. 우리 규칙은 "10연으로 열수록 이득"이라서 1회씩 열면 손해라는 느낌을 준다. 희귀 이상이 한 번도 안 나올 확률은 0.85^10 ≈ **19.7%** (계산값)라 1회씩 열어도 5번 중 1번은 10회 연속 꽝이 된다 | 규칙이 단순해지고 공개 표도 짧아진다. 구현: 카운터 하나 추가, Gacha.md 2-1 수정. 기대 비용 재계산 필요 |
| B | 장비 전설 2%, 50회 천장 | 2% / 50 | **그대로** | 위 비교대로 시장과 같다 | — |
| C | 유물 뽑기 확률 | 각 1/24 | **그대로** | 시장보다 친절. 바꿀 이유 없음 | — |
| D | 유물 새 유물 보장 | 19회 꽝 → 20번째 | **그대로** | 최악이 길지 않다 | — |
| E | 유물 ★3 초과 중복 보상 | 고대 파편 5 (뽑기 1회 가치 100의 5%) | **10으로 올리는 것을 검토** (10%) | 완성한 뒤에도 뽑는 사람이 손해가 크다. 단, 파편 수입이 늘어 "완성 5.8개월" 계산이 바뀐다. 다시 계산해야 하는 안이므로 **제안만** | `Tools/relic_gacha_economy.py`로 재계산 |
| F | 광고 보상 보석 | Economy 10×2/일 vs Monetization 20/일 (두 문서가 다름) | **먼저 숫자를 하나로 통일**, 그다음 15×2 검토 | 탕탕특공대는 1회 30 | 올리면 보석 수입 기준(104/일)이 바뀐다 |

**A를 쓰면 안 되는 경우:** 이미 Gacha.md 기대 비용(약 31.8회, 10연 기준 3.3묶음)을 다른 문서가 인용하고 있다. A를 채택하면 이 숫자를 모두 다시 계산하고 공개 표 문구도 바꿔야 한다. 출시 전이라 지금이 가장 싸다.

**비용 감각 (계산값):** 보석 100 = 약 ₩1,200 기준(Gacha.md)이면 유물 1회 ₩1,200, 완성까지 평균 약 199회 → 보석만으로 사면 10% 할인을 받아도 약 ₩21만 원대다. 힘 예산은 +10%뿐이라 괜찮지만, 상점에서 "유물 완성"을 크게 광고하면 사람들이 이 총액을 의식할 수 있다. 무과금 5.8개월 경로가 있다는 점을 화면에 함께 보여 주는 쪽이 안전하다.

---

## 4. 법 · 정책 체크리스트

### 4-1. 한국 게임산업진흥법 확률형 아이템 정보공개 (2024-03-22 시행)

확인한 내용 (정책브리핑, 문체부 해설서, 법무법인 해설):

- **대상:** 직접·간접으로 **유료 구매 가능한** 모든 확률형 아이템. 온전한 무상 획득만 제외. 무료 재화(예: 골드)로 사는 것도, 그 재화를 유료로 살 수 있다면 대상이다 (정책브리핑).
- **유형:** 캡슐형·강화형·합성형·기타(수량·기간제한형, 확률변동형, **천장형**)로 나눠 표시사항을 정함.
- **단계별 확률:** 등급을 뽑고 그 안에서 아이템을 뽑는 구조면 **개별 확률을 전부** 공개 (정책브리핑).
- **방법:** 백분율 등 알기 쉬운 방식. 게임 안에서는 **구매·조회·사용 화면**에 표시. 웹사이트에서는 문자열·숫자열로 **검색 가능**해야 함. 광고·홍보물에 **"확률형 아이템 포함"** 문구.
- **확률이 변하는 경우:** 변경 내용(예: 실패하면 다음 강화 확률 상승)도 표시 (대륙아주 해설).
- **제재:** 시정명령(문체부). 시정명령을 안 따르면 **2년 이하 징역 또는 2천만 원 이하 벌금** (정책브리핑 148927317 요약). 별도로 **공정위가 허위 확률 표시로 넥슨·넷마블 등에 과징금·과태료**를 부과한 사례가 있다. 2025년 개정으로 **소송 특례**(표시 위반 소송 특례)가 통과됨(김&장 뉴스레터). 매출 3% 과징금 안은 추진 중이라는 기사만 확인했고 **통과 여부는 확인 못 함**.
- **감시:** 게임위 위반 사례 상위가 패키지·묶음 상품 안 개별 구성품 확률 누락, 이벤트 뽑기 확률 미공개 (조선IT). 해외 게임사 위반 비중이 높다는 기사도 있다.

| 항목 | 우리 현황 (문서 + 코드 확인) | 상태 |
|---|---|---|
| 장비 상자 등급·개별 확률 표 | Gacha.md 2-0, 2-2에 42종 개별 확률 · 보장 회차 확률. `GachaTable.cs`, `InfoPanel.cs`에 "확률 보기" 있음 | **지킴** (표시 값과 추첨이 같은 표) |
| 천장(50회)·10연 보장 공개 | 구매 직전 공개로 설계됨 | **지킴** |
| 유물 뽑기 24종 개별 확률 · 보장 · 중복 처리 | Relics.md 5장에 3가지 공개 설계 | **지킴 (설계)**. 구현이 유물 화면에서 같은 수준인지는 확인 못 함 |
| 구매 화면에 표시 | "구매 버튼 바로 옆 확률 보기" | **지킴** |
| 무료 광고 일반 상자도 같은 표 | 일반 상자 표 공개. 골드로 사는 상품도 골드를 유료로 살 수 있으면 대상이라 포함하는 설계가 맞다 | **지킴**. 단 **골드를 보석으로 살 수 없다**는 점을 문서에 못 박아 두는 것이 안전 (찾은 곳: 보석→골드 환전은 문서에서 못 찾음) |
| 이벤트 토큰 → 골드 환전 (Modes.md) | 이벤트 토큰이 유료로 살 수 있는지 확인 필요 | **확인 필요** |
| 던전 클리어 장비 30% 드랍 | 무상이라 의무 아님. 그래도 게임 안 공개로 설계 | 지킴 (초과) |
| 강화·합성 | 골드 비용으로 **확정 성공** (실패 없음) | 확률 공개 대상 아님 (확률이 없으므로). 앞으로 실패 확률을 넣으면 즉시 대상 |
| 웹사이트 공개 (검색 가능한 문자열/숫자) | 설계에 "GitHub Pages 등"만 있고, **실제 페이지는 저장소에서 찾지 못함** | **빠짐** |
| 광고·홍보물에 "확률형 아이템 포함" | 저장소에서 문구를 찾지 못함 | **빠짐** (스토어 설명·영상·광고 소재에 모두) |
| 확률 변경 전 공지·이력 | 이용약관(terms-ko)에 "미리 공지" 한 줄. 이력 보관 | 일부 지킴 (이력 보관 방법은 확인 못 함) |
| 서버 추첨 | 지금 기기 안 추첨 (Gacha.md 6장) | 법적 의무는 아니지만 **확률 조작 의심 방지**용으로 출시 전 결정 필요 |
| 가짜 "거의 당첨" 연출 금지 | 설계에 명시 | 지킴 |

### 4-2. 구글 플레이 정책

- **무작위 아이템 구매:** "구매 **전에**, 구매와 **가깝고 적시에** 확률을 명확히 공개해야 한다" (Play Console Payments 정책 원문). 우리 "구매 버튼 옆 확률 보기"는 이 문장과 맞다. 확률 보기를 한 번 더 눌러야 열리는 것은 "가깝다"에 걸릴 수 있으니, **상자 화면에 천장과 최고 등급 확률 한 줄은 바로 보이게** 하는 것을 권한다 (S).
- **IARC 설문:** 무작위 아이템 구매가 있으면 설문에 체크하고, 결과에 "In-Game Purchases (Includes Random Items)" 표시가 붙는다 (ESRB 안내, IARC도 같은 라벨 채택). 우리는 유료 보석으로 상자를 열 수 있으므로 **체크 대상**이다.
- **보상형 광고 규정(옵트인 등):** 구글 광고 정책 원문은 이번에 **확인 못 함**. 우리 설계는 "플레이어가 고르는 보상형만, 보상 콜백에서 1회 지급"이라 일반적 원칙에는 맞아 보인다. 출시 전 원문 대조 필요.
- **패밀리/아동:** 아동 대상이 아님. 확인 못 함. 이용 등급은 아래 설문에 맡긴다.

### 4-3. 등급 · 종교 인물

- 한국 등급 기준은 12세·15세·청소년이용불가·시험용 (게임콘텐츠등급분류위원회 안내). 세부 기준은 선정성·폭력·범죄·언어 등이다.
- **부처·아담 같은 종교 인물과 신 묘사에 대한 한국 공식 지침은 찾지 못했다 (확인 못 함).** 우리 GodsVsHumanity 7장의 "조롱·비하·개종 강요 금지, 사실/전승/각색 구분, 지역별 검토"는 합리적인 자체 기준이다.
- 폭력 묘사(신체 훼손 표현)와 언어는 등급 기준 항목이므로, 종교 소재보다 **전투 연출 수위**가 실제 등급을 좌우할 가능성이 높다 (추정, 근거 문서는 등급분류규정 조문 확인 못 함).
- 한국에서 구글 플레이 게임이 어떤 등급 경로(IARC 자율 vs 게임위 심의)를 따르는지는 **현재 규정을 확인 못 함**. 출시 전에 구글 콘솔 안내 + 게임위 안내로 확인한다.

### 4-4. 빠진 것 요약 (해야 할 일)

1. **확률 공개 웹페이지 만들기** (유물·장비·천장 전부, 문자 검색 가능, 앱에서 링크). S~M
2. **"확률형 아이템 포함" 문구**를 스토어 설명·광고·영상에 넣기. S
3. 상자 화면에서 확률 한 줄을 **누르지 않고도** 보이게. S
4. **골드/이벤트 토큰이 유료 재화로 살 수 없다**는 점을 Economy.md에 명시하거나, 살 수 있다면 대상으로 처리. S
5. IARC 설문에서 무작위 아이템 항목 체크. S
6. 서버 추첨 여부 결정 (Monetization.md 8장). L
7. 확률을 바꿀 때 변경 이력 보관 방법 정하기. S

---

## 5. 출처 목록

법·정책
- 문체부·정책브리핑, 확률형 아이템 정보 공개 해설서 소개: https://m.korea.kr/news/policyNewsView.do?newsId=148926013
- 정책브리핑, 확률형 아이템 정보 22일부터 공개, 위반 시 시정조치(벌칙): https://m.korea.kr/news/policyNewsView.do?newsId=148927317 (검색 결과 요약만 확인)
- 법제처, 게임산업진흥법 시행령 (2024-03-22, 별표 3의2 신설): https://www.law.go.kr/lsInfoP.do?lsiSeq=258459&viewCls=lsRvsDocInfoR
- 문체부 설명 자료: https://mcst.go.kr/attachFiles/viewer/skin/doc.html?fn=1709616521580.hwpx&rs=/attachFiles/viewer/result/studentNews/
- KOCCA 해설: https://www.kocca.kr/global/2023_11+12/sub02_02.html
- 김&장 뉴스레터 (소송 특례 개정): https://www.kimchang.com/ko/insights/detail.kc?sch_section=4&idx=31242
- 대륙아주 해설 (표시 사항, 공정위 사례): https://www.draju.com/ko/sub/newsletters.html?type=view&bsNo=3840
- 조선IT, 확률 표시제 위반 사례: https://it.chosun.com/news/articleView.html?idxno=2023092154083
- 매출 3% 과징금 추진 기사(2025-12): https://m.ceoscoredaily.com/page/view/2025121710551225003 (본문 확인 못 함, 제목·요약만)
- 구글 플레이 Payments 정책 (무작위 아이템 확률 공개): https://support.google.com/googleplay/android-developer/answer/9858738?hl=en
- Fenwick 해설: https://www.fenwick.com/insights/publications/google-play-now-requires-disclosure-of-loot-box-odds
- Game Developer 보도: https://www.gamedeveloper.com/business/games-on-the-google-play-store-now-required-to-disclose-loot-box-odds
- ESRB, In-Game Purchases (Includes Random Items) 라벨: https://www.esrb.org/blog/in-game-purchases-includes-random-items/
- 게임콘텐츠등급분류위원회 등급 안내: https://www.gcrb.or.kr/Institution/EtcForm01.aspx
- 게임물관리위원회 등급분류규정: https://www.law.go.kr/LSW/schlPubRulInfoP.do?schlPubRulSeq=2200000088951

참고 게임 (공식 아님: 위키·커뮤니티·분석 블로그)
- 나무위키 탕탕특공대 (홈 탭, 확률, 천장, 광고 보상, 카니발): https://namu.wiki/w/탕탕특공대
- 탕탕특공대 갤러리 소원 목록 글: https://gall.dcinside.com/mgallery/board/view/?id=xkdxkd&no=76140
- 탕탕특공대 후기 블로그 (엘리트 확률·천장 언급): https://m.blog.naver.com/snsdkvkzz/222857796246
- Archero 2 위키 Pull Rates: https://archero-2.game-vault.net/wiki/Pull_Rates
- Archero 2 천장 50 레딧: https://www.reddit.com/r/ArcheroV2/comments/1jdp9x1/450_mythstone_chests_and_not_a_single/
- Archero 2 레딧 (천장 50, 키 100개 S 2개): https://www.reddit.com/r/ArcheroV2/comments/1oanuxr/habby_finally_making_sense/
- Tiny Teardown Archero 2 (패스 위치, 신규 팩, 28일 시즌): https://someselectedstories.substack.com/p/tiny-teardown-archero-2-habby-games
- Archero 2 앱스토어 리뷰: https://apps.apple.com/pl/app/6502820653?see-all=reviews&platform=iphone
- Archero 1 확률 공개 요청 레딧: https://www.reddit.com/r/Archero/comments/krh43l/request_ultimate_guide_to_itemequipment_drop/
- Capybara Go 구글 플레이: https://play.google.com/store/apps/details?id=com.habby.capybara
- Capybara Go P2W 레딧: https://www.reddit.com/r/CapybaraGoGame/comments/1u08kiv/capybara_go_is_being_ruined_by_p2w/
- Capybara Go MiniReview: https://minireview.io/adventure/capybara-go/faq
- Vampire Survivors 구글 플레이: https://play.google.com/store/apps/details?id=com.poncle.vampiresurvivors
- Soul Knight 앱스토어: https://apps.apple.com/us/app/soul-knight/id1184159988
- 2026 상반기 국내 모바일 게임 순위 블로그: https://m.blog.naver.com/PostView.naver?blogId=gaechuni&logNo=224302586379
- 2026 모바일 게임 추천 목록 블로그: https://dreaminfo.tistory.com/1072

내부 문서 (비교 기준)
- `Docs/Design/Gacha.md`, `Relics.md`, `Equipment.md`, `Economy.md`, `UX.md`, `Monetization.md`, `GodsVsHumanity.md`, `Docs/Dev/UX-Overhaul-Plan.md`, `Docs/Legal/terms-ko.md`

## 처리

2026-10-31, `feature/ux-gacha`의 Batch C. 아래는 코드·문서 반영 내역이며 Editor 실행이나 출시 승인을 뜻하지 않는다.

| 항목 | 반영 파일·내용 | 검증 |
|---|---|---|
| 1. 미션 | `MissionPanel.cs`, `ButtonList.cs`, `SceneBuilder.Missions.cs`: 받을 업적을 위쪽에 `[받기]`로 표시, 진행 막대·활동 점수·상자 수령 상태·교체 안내 추가. 일일·주간 점수 자동 지급은 유지 | `MissionPresentationTests.ProgressAmount_ClampsAndUsesCompletedState`, `ButtonList_ProgressChangesWidthAndHidesWhenAbsent` |
| 2. 알림 점 | `UpgradeNotice.cs`, `LobbyScreen.cs`, 로비 빌더: 레벨·각성·장착 장비 성장 가능 여부와 7일 보상 준비 상태 표시. 알림 판정은 저장을 변경하지 않음 | `UpgradeNoticeTests`의 레벨·잠긴 영웅·각성·장비·7일 보상 검사 |
| 3. 상점 | `ShopPanel.cs`, `SceneBuilder.MetaUi.cs`: 유물 잠김을 회색·조건으로 표시, 짧은 구매 문구와 중앙 보장 횟수, 구매 버튼에 확률·보장 표시, 재화 전환 설명·빈 결과 안내 추가 | `EquipmentTests.Premium_NextLegendaryRateReflectsOrdinaryRareGuaranteeAndPity`, `EquipmentSixSlotTests.BothBoxes_ReachEverySlotAndShowItsIndividualRate` |
| 4. 시련 안내 | `HeroVoice.cs`, `BossIntroPanel.cs`, `GodTrialRules.cs`, `RunResultScreen.cs` 첫 격파 문구만 국소 수정: 일반 보스 대사 제외, 신의 시련 개방, 서로 다른 본인 기록·액티브 구성 차이 조건과 미집계 이유 | `TrialPresentationTests` |
| 5. 영웅 타일 | `SceneBuilder.Lobby.cs`: 초상 높이를 줄이고 이름·상태에 별도 영역 배정 | 빌더 정적 컴파일. 실제 줄바꿈·겹침은 Editor 확인 전 |
| 6. 도감 | `HeroStories.cs`에 승인된 21명 소개를 모음. `CodexPanel.cs`, `SceneBuilder.Codex.cs`에서 같은 경로로 표시하고 잠긴 조합은 3열 카드로 정리. 기존 로비 빌더의 도감 메서드를 전용 partial 파일로 이동 | `CodexPresentationTests.EveryRosterHeroHasOneBiographyWithSeparateSourceLabels`, `UnknownHeroDoesNotBorrowAnotherPersonsStory`, `LockedRecipeCardHidesNameAndMaterials` |
| 7. 맵 선택 | `SurvivalMapPanel.cs`: 개발용 전체 입장 허용 대신 실제 저장 진행으로 첫 맵 선택 | `SurvivalSelectionTests` |
| 8. 고급 상자 | `GachaService.cs`, `GachaTable.cs`: 희귀 미만 9회 뒤 다음 회차 보장. 기존 로그에서 누적 상태를 복원하고 마지막 고급 9회 기록 보존. 전설 50회 보장·기본 표 유지. `Gacha.md`, `Equipment.md`, `GameDesign.md` 동기화 | `EquipmentTests.Premium_RareGuaranteeAccumulatesAcrossSinglesTicketsAndTenPulls`, `Premium_RareGuaranteeSurvivesNormalLogTrimmingAndJsonRoundtrip`, `Premium_RareGuaranteeUnknownLegacyItemResetsStreak_AndPityWins` |
| 9. 공개 확률 | `Tools/export_probabilities.py` → `Docs/Public/probabilities.md`. 실제 에셋 GUID·표에서 장비 42종·유물 24종의 회차별 확률·가격·보장·중복·확정 강화 규칙 생성. 보장 회차 실제 확률과 재추첨 표를 구분 | `ProbabilityExportTests` 4개 통과, `python3 Tools/export_probabilities.py --check` 통과 |
| 10. 출시 문구 | `Docs/Release/StoreListing.md`: 한국어 초안, ‘확률형 아이템 포함’, 보상형 광고 안내, 무작위 상품·종교 인물 표현을 포함한 IARC 목록 | 문서 검토. 실제 게시·등급 심사 미실행 |
| 11. 경제 문서 | `Economy.md` 9장·`Monetization.md`·`Handoff.md` 7장·`Tools/economy_21.py`: 상점 5개/일은 현재 기준, 3개는 이전 비교. 광고 보석은 Economy의 계획값 10개×2회=20개/일로 통일하고 미구현 수입과 구분 | `python3 Tools/economy_21.py` 내부 단언·실행 통과 |

### 검증 범위와 남은 확인

- PASS: Python 테스트 4개, 확률표 동기화 검사, 경제 계산, Python 구문 검사, `git diff --check`.
- PASS: Unity를 실행하지 않고 설치된 Unity 6.3 엔진·템플릿 패키지 DLL과 `.asmdef`의 직접 참조로 Roslyn C# 9 정적 컴파일. Runtime·Editor·EditMode 테스트 어셈블리에서 오류 0개. Runtime에는 직렬화 필드 미할당·기존 API 경고 등이 있으며 무경고 통과를 뜻하지 않는다. Android 전용 알림 코드는 이 Editor 조건 검사 대상이 아니다.
- NOT_RUN: 실제 Unity 프로젝트 컴파일, EditMode 테스트 실행, `SceneBuilder.BuildAll`, Play·폰·화면 비교. 장면·에셋·폰트는 변경하지 않았으므로 리드는 빌더 재생성 후 연결·줄바꿈·진행 막대·버튼 상태를 확인한다. 화면 캡처가 없어 visual-verdict 점수는 만들지 않았다.
- 공개 확률표는 저장소 Markdown이다. 실제 공개 사이트 게시와 스토어·게임 내 연결 URL 확인은 출시 작업으로 남는다. 스토어 문구도 실제 상품과 광고 구현에 맞춰 게시 전에 확인한다.
- 다른 배치 소유인 `Records/*`는 변경하지 않았다. `SaveData.cs`의 조각 상점 ‘하루 3개’ 주석과 뽑기 로그 ‘최근 100개’ 주석은 저장 담당이 각각 ‘5개’, ‘100개 한도 안에서 마지막 고급 9회 보존’으로 맞출 수 있다. 기존 버전에서 이미 지워진 로그나 삭제된 장비 ID의 누적 상태는 복원할 수 없어 확인 가능한 기록까지만 센다.
- 커밋하지 않았다. 작업 시작부터 보이던 LFS 에셋 차이는 이번 코드 변경에 포함하지 않는다.
