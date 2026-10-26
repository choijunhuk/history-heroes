# 📚 비슷한 모바일 로그라이크 조사 (2026-10-26)

> 목적: Archero 계열 히트작과 뱀서류를 조사해서 **History Heroes에 바로 쓸 것 / 나중에 쓸 것 / 피할 것**을 정리한다.
> 관련 문서: [GameDesign](../GameDesign.md) · [Systems](../Design/Systems.md) · [Economy](../Design/Economy.md) · [Heroes](../Design/Heroes.md) · [Skills](../Design/Skills.md) · [UX](../Design/UX.md) · [Ideas](../Ideas.md)
> 상태: **조사 자료 (기획 확정 아님).** (A) 항목을 반영할 때는 해당 기획 문서를 먼저 고치고 구현한다.

**읽는 법**
- 숫자 대부분은 **팬 위키·공략 사이트·Reddit** 기준이다. 업데이트로 자주 바뀐다. 그래서 가능하면 언제 기준인지 같이 적었다.
- "**확인 못 함**" = 찾았지만 믿을 만한 출처가 없었다. "**(한 곳만)**" = 출처가 하나뿐이다.
- 매출·다운로드는 거의 다 AppMagic·Sensor Tower의 **추정치**다.
- **[제안]** 표시는 출처의 사실이 아니라 이 문서가 내는 제안이다.

---

## 0. 한눈에 요약

1. **"기록" 시스템은 조사한 어떤 게임에도 그대로 있지 않다. 진짜 차별점이다.** Archero 1·2, Survivor.io, Capybara Go!, 뱀서류 4종, Soul Knight 모두 "판에서 만든 빌드를 저장해 보스전에 쓰는" 기능이 없다. 가장 가까운 것은 **카제나 세이브 데이터**, PC 게임 **Mewgenics의 House Boss**, **Inscryption 데스카드**다. ([1-11](#1-11-빌드-저장과-비슷한-사례))
2. **카제나의 최대 불만은 "용량을 넘으면 무작위로 깎인다"였다.** 우리처럼 **플레이어가 직접 고르는 방식**이 그 해법이다. 그대로 지킨다. 대신 카제나에서 나온 다른 불만("상한까지 얼마 남았는지 모르겠다", "뭘 남겨야 할지 어렵다")은 우리도 화면으로 풀어야 한다. → (A5·A7·A8)
3. **"멈추면 쏜다"는 Archero 1·2에만 있는 장치다.** Archero 2도 그대로 유지했다. 뱀서류는 전부 자동 공격이다. Archero에는 공격 후딜을 움직여서 끊는 "stutter step"(DPS 20% 이상 상승)이 있다. 우리도 이걸 허용할지 **일부러 정해야** 한다. → (B16)
4. **우리 하트는 경쟁작보다 3~6배 너그럽다.** 우리는 하트 1개(=1판)가 20분에 찬다. Archero 1·2는 1판 분량(5)이 60분, Survivor.io는 약 100분(한 곳만), Capybara Go!는 120분이다. 에너지는 Archero의 **1위 불만**이었다. 지금 숫자를 바꿀 이유가 없다. ([2-1](#2-1-에너지하트))
5. **리롤 "판당 2회, 결제로 안 늘림"은 업계보다 너그럽고 공정하다.** Archero 2는 판당 1회 + 월정액으로 +1, Survivor.io는 1회 + 9,999젬 카드로 +1이다. 둘 다 **돈으로 판 안의 힘을 판다.** 다만 **리롤하면 방금 본 카드를 빼고 뽑는 것**이 표준(VS, Archero 2)인데 우리 코드는 아직 아니다. → (A1)
6. **조합 힌트는 "발견 전 숨김, 발견 후 카드에 표시"가 업계 표준이다.** (Survivor.io "EVO" 줄, VS 2022년 패치, BALL x PIT) 우리 설계와 같다. 한 걸음 더 나가서 **짝 재료와 진행도까지** 보여주면 좋다. Magic Survival은 "조합이 너무 많아 못 외운다"는 불만을 들었다. → (A3·A4)
7. **손맛(피해 숫자·히트스톱·화면 흔들림)이 지금 코드에 없다.** (피격 흰색 깜빡임만 있다) 가장 싸게 재미를 올릴 수 있는 곳이다. VS·Soul Knight·Archero 2는 모두 끄는 옵션도 준다. → (A9)
8. **"정해진 방에서 좋은 선택"이 표준이다.** Archero 2는 5번째 방마다 Valkyrie(희귀~전설 2지선다), Capybara Go!는 엘리트 → Mythic 3중 1, Brotato는 레벨 5·10에 업그레이드 등급 확정이다. 우리 엘리트 확정 레벨업에 **희귀 1장 보장**을 붙이면 "5·10번 방이 기대되는 순간"이 숫자로 생긴다. → (A10)
9. **공정하다고 칭찬받는 게임의 공통점:** 보상형 광고만, 하루 상한, 영웅은 진행으로 해금하거나 싸게, 판 안 힘은 안 판다 (Soul Knight, VS, BALL x PIT). **욕먹는 공통점:** 파워 크립(장비 등급 계속 추가), 영웅·장비 뽑기 압박, 판 안 힘 판매, 다른 모드로 진행 막기 (Archero 1·2, Survivor.io). 우리 [과금 원칙](../Design/Monetization.md#1-과금-원칙-절대-어기지-않음)과 방향이 같다. ([3-C](#c-피해야-할-것))
10. **이 장르는 한국 시장이 크다.** Capybara Go! 매출의 28%가 한국이었다(AppMagic). Habby 누적 매출 $2B 중 40%가 Survivor.io다. 로그라이크 세부 장르는 2025년 매출이 26% 성장했다(AppMagic). "한국 + 영어권" 첫 출시 결정과 맞는다.

---

## 1. 게임별 정리

### 1-1. Archero (Habby, 2019) — 우리와 조작이 같은 원조

**한 판·조작**
- 챕터 종류: dungeon형 50 스테이지 / arena형 20 / boss형 10. 1챕터는 50 스테이지, 10스테이지마다 보스. ([Fandom Verdant Prairie](https://archero.fandom.com/wiki/Verdant_Prairie))
- 10스테이지 흐름: 시작 방(룰렛) → 적 방 4 → Angel → 적 방 4 → 보스. 적 방 하나는 보통 1분 미만. ([Game Developer: Finding the fun 1](https://www.gamedeveloper.com/design/finding-the-fun-archero-part-1---gameplay))
- 한 판 시간: 이미 깬 챕터 파밍 기준 3챕터 6~7분, 6챕터 10~20분. 처음 도전 평균은 **확인 못 함**. ([gameskeys 가이드](https://gameskeys.net/archero-the-ultimate-farming-guide/))
- **멈추면 가장 가까운 적을 자동으로 쏜다.** 한 번 잡은 표적은 멀어져도 계속 쏜다(바꾸려면 살짝 움직여야 함). ([GameFAQs 리뷰](https://gamefaqs.gamespot.com/android/264554-archero/reviews/169090))
- **Stutter step:** 화살을 쏜 직후 살짝 움직이거나 탭하면 후딜(backswing)이 끊긴다. DPS가 "20% or more" 오른다고 한다. 멈춘 뒤 첫 발까지 몇 프레임인지는 **확인 못 함**. ([Reddit](https://www.reddit.com/r/Archero/comments/r3t01t/stutterstep/), [Android Authority](https://www.androidauthority.com/archero-cheats-tips-hacks-glitches-1087839/))
- 움직임 긴장: 원거리 적은 쏘기 전 빨간 조준선, 적 탄은 장애물을 넘지만 내 화살은 막힘, 돌진형·탄막형·잠수형·탱커형을 섞음. ([Game Developer 1](https://www.gamedeveloper.com/design/finding-the-fun-archero-part-1---gameplay))

**레벨업·선택**
- 레벨업마다 3개 중 1개. 능력 풀 약 50개, 챕터당 레벨업 최대 약 11회(2019 분석). **슬롯 제한 없음**(멀티샷 중복 가능). 판 안 조합(진화) 없음. ([Fandom Abilities](https://archero.fandom.com/wiki/Category:Abilities), [Deconstructor of Fun 2019](https://www.deconstructoroffun.com/blog/2019/8/9/why-archero-banked-25m-but-leaves-25m-hanging-hlx9n))
- 강한 능력에는 대가가 붙는다: Front Arrow는 피해 -25%, Multishot은 피해 -10% · 공속 -15%, Ricochet은 튕길 때마다 -30%. ([Fandom Abilities](https://archero.fandom.com/wiki/Category:Abilities))
- **Angel:** 회복 vs 하급 능력 2지선다. **Devil:** 보스에게 한 대도 안 맞으면 100% 등장, 기본 최대 HP 20%를 주고 능력 1개(거절 가능). ([Fandom Angels](https://archero.fandom.com/wiki/Angels), [Fandom Devil](https://archero.fandom.com/wiki/The_Devil))
- **리롤:** 출시 때 없음. 2020년 8월(2.2.0) 배틀패스 보상 "Reset Card"(1회)로 추가됐는데, 설명이 없어서 "어디 있냐"는 글이 여러 개 올라왔다. ([Fandom Version History](https://archero.fandom.com/wiki/Version_History), [Reddit](https://www.reddit.com/r/Archero/comments/inrr7j/where_is_the_new_reset_card/))
- 능력 카드는 숫자 대신 화살표(ATK ⏫)로만 보여줘서 "실제 숫자를 보여 달라"는 요청이 있었다. ([Reddit](https://www.reddit.com/r/Archero/comments/ejpdhh/unpopular_opinion_give_us_actual_numbers_instead/))

**영웅**
- 해금 예: Urasil 2챕터 클리어, Phoren 코인 5만, Taranis 1,000젬, Helix 1,500젬, Meowgik 1,800젬, 유료 영웅 8.99~16.99€. 2.0부터 조각 30개로도 해금. 2.2.0에 사기 전 써보는 **Hero Trial Mode**. ([Fandom Heroes](https://archero.fandom.com/wiki/Category:Heroes), [Version History](https://archero.fandom.com/wiki/Version_History))
- 대표 효과 예: Helix는 잃은 HP 1%마다 피해 +1.2%, Meowgik은 투사체마다 약 30% 확률로 고양이 소환. ([Helix](https://archero.fandom.com/wiki/Helix), [Meowgik](https://archero.fandom.com/wiki/Meowgik))

**보스**
- 탄막 패턴. Red Skull은 HP 50%에서 2마리로 분열(분열이 단계 역할), Fire Dragon 탄은 벽에 튕김. 소환 많은 보스는 광역 능력이 없으면 포위당함. 일부 레이저는 예고가 너무 짧다는 평. ([Fandom 보스](https://archero.fandom.com/wiki/Verdant_Prairie_Bosses), [Up-close Dangers](https://archero.fandom.com/wiki/Up-close_Dangers))

**메타·경제**
- **에너지(2019 기준): 최대 20, 1판 5, 12분에 1(가득 4시간), 광고 +5 하루 4회, 100젬 → 20.** 배틀패스 구매자 최대 30. 2023년 글 기준 20 에너지가 60젬. ([Fandom Energy](https://archero.fandom.com/wiki/Energy), [Game Developer 3](https://www.gamedeveloper.com/design/finding-the-fun-archero-part-3---monetization), [Reddit 2023](https://www.reddit.com/r/Archero/comments/11y5pm0/ranking_stupidest_things_in_archero_right_now/)) — 이 문서에서 직접 다시 검색해 같은 숫자를 확인함.
- 장비: 같은 등급 3개 합치면 위 등급. 나중에 Ancient Legendary → Mythic → Titan → Chaos 등급이 계속 붙음. 탤런트는 골드를 내면 **무작위로** 하나가 오름(고를 수 없음). ([Game Developer 2](https://www.gamedeveloper.com/design/finding-the-fun-archero-part-2---progression), [Fandom Talent](https://archero.fandom.com/wiki/Talent))
- 튜토리얼은 손잡고 끄는 방식이 아니고, 1챕터는 "Very Easy". **초반엔 에너지가 넘쳐서** 몰입한 뒤에야 에너지 벽을 만난다. 실패해도 시작 룰렛 골드는 보장 → "guaranteed some progression just for trying". ([Game Developer 2](https://www.gamedeveloper.com/design/finding-the-fun-archero-part-2---progression), [3](https://www.gamedeveloper.com/design/finding-the-fun-archero-part-3---monetization))

**수익**
- 강제 광고 없음. 보상형 광고: 에너지(하루 4), 보스 후 룰렛(하루 5), 상자 1, 상점 코인, 부활, 판 중 임시 버프. ([Udonis](https://www.blog.udonis.co/mobile-marketing/mobile-games/archero-advertising), [GameRefinery](https://www.gamerefinery.com/april-2020-product-update-feature-groups-expanded-13-new-ad-monetization-features-added/))
- **부활은 한 판 1회.** 2019년 30젬, 결정 시간 5초. 나중에 광고 부활 추가. ([Game Developer 3](https://www.gamedeveloper.com/design/finding-the-fun-archero-part-3---monetization))
- 가장 많이 팔린 상품: 배틀패스 $4.99 → 첫 구매 팩 → $0.99 젬. ([Game Developer 3](https://www.gamedeveloper.com/design/finding-the-fun-archero-part-3---monetization))
- 누적 순매출 $263M+, 다운로드 약 9,600만 (AppMagic, 2025-01). ([Game World Observer](https://gameworldobserver.com/2025/01/20/archero-2-revenue-8-million-in-11-days-vs-original-game))

**불만:** 에너지 벽("Waiting 1 hour to play…"), 확률 의존 성장·천장 없음, 고래 위주, 장비 등급이 계속 늘어남, 후반 난이도 급상승, 3개 선택지 중 2개가 같음. ([Reddit 1](https://www.reddit.com/r/Archero/comments/bykzs0/this_is_a_really_fun_game_and_its_completely/), [2](https://www.reddit.com/r/Archero/comments/1gziyjv/lets_talk_about_archeros_grind_and_recent_events/), [3](https://www.reddit.com/r/Archero/comments/yrkghi/game_got_too_diluted_rant/), [4](https://www.reddit.com/r/Archero/comments/gcch5c/rant_archero_is_getting_worse_with_every_patch/))

---

### 1-2. Archero 2 (Habby, 2025-01 글로벌) — 가장 가까운 직접 경쟁작

**한 판·조작**
- 챕터 3종: 방 50개(10번째마다 보스) / 한 방 웨이브 50(10번째마다 보스) / **6분 버티기(3분마다 보스)**. ([game-vault Campaign](https://archero-2.game-vault.net/wiki/Campaign))
- **멈춰야 쏘는 규칙 유지.** 회전 구슬 같은 스킬은 움직여도 공격. 규칙을 깨는 것은 큰 대가: 스킬 Weapon Enchantment(무기가 혼자 공격, ATK -30%), 영웅 Thor(이동 중 공격 → "고정 픽" 취급). ([Pocket Gamer](https://www.pocketgamer.com/archero-2/out-now-on-ios-and-android), [game-vault Skills](https://archero-2.game-vault.net/wiki/Skills), [archero2hub](https://archero2hub.com/en/character))
- 한 판 시간: 쉬운 챕터 3~8.5분, 중반 진행은 **30~40분이라는 불만**(2026-09). 깬 챕터는 2~3배속. ([Reddit](https://www.reddit.com/r/ArcheroV2/comments/1iv9lc2/), [Reddit](https://www.reddit.com/r/ArcheroV2/comments/1wkjgk3/))

**레벨업·선택**
- 3개 중 1개, 등급 4단계(초록·파랑·보라·주황). ([Levelwinner](https://www.levelwinner.com/archero-2-beginners-guide/))
- 정해진 방 이벤트: **5로 끝나는 방 Valkyrie(2지선다, 희귀~전설)**, 9로 끝나는 방 Angel, 10번째 보스 뒤 룰렛 또는 Devil(최대 HP 15/23/30%를 내고 희귀/에픽/전설). **진행 바에 이 방들을 아이콘으로 미리 보여준다.** ([game-vault Skills](https://archero-2.game-vault.net/wiki/Skills), [Levelwinner](https://www.levelwinner.com/archero-2-beginners-guide/))
- **리롤:** 출시 때 없음 → 2025-02 탤런트로 판당 1회 + 월정액 +1 = **최대 2회**. 전체를 다시 뽑고, **방금 본 스킬은 다시 안 나옴.** ([Reddit](https://www.reddit.com/r/ArcheroV2/comments/1ioou0k/), [Reddit](https://www.reddit.com/r/ArcheroV2/comments/1iowu5w/), [Reddit](https://www.reddit.com/r/ArcheroV2/comments/1n7cdmg/))
- 맵에 맞춰 카드 풀이 바뀜(벽 통과는 벽 있는 맵에서만). 등급 확률 공식값은 **확인 못 함**. ([game-vault Skills](https://archero-2.game-vault.net/wiki/Skills))

**영웅**
- 약 21명(2026-09). 조각 50개로 해금, 최대 8★. **전설 영웅은 소환 이벤트(뽑기)로만.** Helix 3,000젬, 일부 약 $30. 무과금은 "주사위 4개월 모으고 이벤트 6개월" 해야 한다는 말. ([game-vault Characters](https://archero-2.game-vault.net/wiki/Characters), [Pocket Gamer 가이드](https://www.pocketgamer.com/archero-2/guide/), [Reddit](https://www.reddit.com/r/ArcheroV2/comments/1vlvty6/))
- 영웅 Seraph는 스킬 뽑기 자체를 바꾼다(시작 시 Valkyrie 50%). ([PGG tier list](https://progameguides.com/archero-2/archero-2-skills-tier-list/))

**보스**
- HP가 줄면 빨라지거나, 2단계에서 HP 바가 하나 더 생기고 졸개 소환. 예고: 떨어질 자리 표적 마커, 번개 원 먼저 깔기. ([Theria 1장](https://theriagames.com/guide/archero-2-chapter-1-moonlight-forest-guide/), [Theria 몬스터](https://theriagames.com/guide/archero-2-types-of-monster-guide/))
- **보스 전용 모드 Seal Battle: 보스 상대로 2분 버티며 피해량 경쟁, 하루 2회, 보스 7일마다 교체, 28일 시즌.** 이 모드에서는 15초마다 Valkyrie가 나와 빠르게 빌드를 짠다. ([Tiny Teardown](https://someselectedstories.substack.com/p/tiny-teardown-archero-2-habby-games), [PGG](https://progameguides.com/archero-2/archero-2-beginners-guide/))

**메타·경제**
- **에너지: 1판 5, 12분에 1, 최대 30(유료 카드 50), 광고 +5 하루 3회(출처에 따라 3~4회), 챕터 클리어 +5, 100젬 → 20(2025-01).** ([game-vault Campaign](https://archero-2.game-vault.net/wiki/Campaign), [Reddit](https://www.reddit.com/r/ArcheroV2/comments/1o310l6/), [roonby](https://roonby.com/2024/11/18/archero-2-guide-all-beginner-tips-to-get-you-started/)) — "5와 12분"은 직접 다시 검색해 확인, 최대 30·광고 3회는 위키·Reddit 기준.
- 방치 보상 최대 24시간, 빠른 수령은 에너지 5 또는 광고(하루 2). S장비 상자 3.01%, **50회 천장**. 배틀패스 $9.99·$29.99. ([Levelwinner](https://www.levelwinner.com/archero-2-beginners-guide/), [grindnstrat](https://grindnstrat.com/archero-2-drop-rates), [Tiny Teardown](https://someselectedstories.substack.com/p/tiny-teardown-archero-2-habby-games))
- 튜토리얼 **28초**(Sensor Tower 분석 인용). 설정: 이펙트 줄이기, 스킬 선택 연출 생략, 조이스틱 숨기기. ([GWO](https://gameworldobserver.com/2025/01/20/archero-2-revenue-8-million-in-11-days-vs-original-game), [Levelwinner](https://www.levelwinner.com/archero-2-beginners-guide/))
- 매출: 첫 30일 $32.8M(AppMagic). 누적은 출처마다 다름($156M~$250M+). ([PocketGamer.biz](https://www.pocketgamer.biz/archero-2-makes-328m-in-first-30-days-from-player-spending/), [Mobilegamer.biz](https://mobilegamer.biz/data-digest-savvys-moonton-swoop-liftoff-kills-ipo-habby-hits-2bn-fallout-shelter-more/))

**수익**
- 보상형 광고: 부활(한 판 1), 에너지(하루 3), 빠른 방치 보상(하루 2), 상점 무료 팩. **전면 광고도 쓴다.** ([Levelwinner](https://www.levelwinner.com/archero-2-beginners-guide/), [Tiny Teardown](https://someselectedstories.substack.com/p/tiny-teardown-archero-2-habby-games))
- 카드: 평생 $30, 광고 제거 평생 $10, 월정액 $5(**리롤 +1**, 아레나 스킵), 사냥 카드 $5(에너지 최대 +20). ([Reddit](https://www.reddit.com/r/ArcheroV2/comments/1tmno1i/))
- 장비·룬 **프리셋**은 있지만 **판 안 스킬 빌드 저장은 없다.** "룬 프리셋을 더 달라"는 요청이 많다. ([Reddit](https://www.reddit.com/r/ArcheroV2/comments/1kzzi6b/), [Reddit](https://www.reddit.com/r/ArcheroV2/comments/1pmmph1/))

**불만:** Artifact를 "pure pay-to-win"이라 함, 이벤트에 $50 써서 69회, 캠페인 진입을 Sky Tower 층수로 막음, 한 판이 너무 김, 에너지 최대 30이 낮아서 넘쳐 버려짐, 전설 영웅은 뽑기로만, 이벤트 모드에서 키운 장비를 못 씀. ([Android Authority](https://www.androidauthority.com/archero-2-3517511/), [Reddit](https://www.reddit.com/r/ArcheroV2/comments/1mhqspt/), [Google Play](https://play.google.com/store/apps/details?id=com.xq.archeroii), [Reddit](https://www.reddit.com/r/ArcheroV2/comments/1lhk5r0/))

---

### 1-3. Survivor.io (Habby, 2022) — 진화 시스템의 기준

**한 판·조작**
- 이동만, 공격 자동. 챕터는 15분짜리와 8분짜리. ([minireview](https://minireview.io/arcade/survivor-io), [Reddit](https://www.reddit.com/r/Survivorio/comments/1c26onp/newb_question/))
- 메뉴는 "20분 튜토리얼이 아니라 며칠에 걸쳐" 하나씩 열림. ([Mobilegamer.biz](https://mobilegamer.biz/the-design-secrets-powering-habbys-hit-machine/))

**레벨업·진화**
- 3개 중 1개. **슬롯 액티브 6 + 패시브 6.** 다 차고 만렙이면 골드·회복만. ([onechilledgamer](https://onechilledgamer.com/survivor-io-skill-guide/))
- **진화: 액티브 Lv5 + 짝 패시브 Lv1 이상 → 다음 선택지나 상자에 EVO.** 상자는 진화 가능하면 진화를 준다는 증언. ([allclash](https://www.allclash.com/survivor-io-evo-skill-guide-tier-list/), [Reddit](https://www.reddit.com/r/Survivorio/comments/zdrsiw/))
- 레시피 예: Kunai + Koga Ninja Scroll → Spirit Shuriken, Guardian + Exo-Bracer → Defender, Lightning Emitter + Energy Cube → Supercell. **패시브 하나가 무기 여러 개를 진화**시킨다(Energy Cube 등). **액티브+액티브 합체**도 있다(Type-A + Type-B 드론 → Destroyer, 슬롯 1칸이 빔). ([Fandom 진화 가이드](https://survivorio.fandom.com/wiki/Weapon_Skill_Evolution_Guide), [writerparty](https://writerparty.com/party/survivor-io-best-build-guide-and-skill-tier-list/))
- **UI 힌트: 카드 아래에 작게 "EVO: …". 패시브 카드에도 어떤 액티브를 진화시키는지 보임 → 레시피를 외울 필요 없음.** ([allclash](https://www.allclash.com/survivor-io-evo-skill-guide-tier-list/), [mturbogamer](https://mturbogamer.com/2022/10/survivor-io-what-is-evo-weapon-evolutions/))
- **새로고침:** 계정 성장 트리에서 열고 **판당 1회**, 9,999젬 영구 카드로 +1. "1회는 너무 적다"는 불만. ([pocketgamer.io](https://pocketgamer.io/evolution-chart-guide/), [mturbogamer](https://mturbogamer.com/2022/10/survivor-io-best-way-to-spend-gems/), [Reddit](https://www.reddit.com/r/Survivorio/comments/18lpq2v/))
- **오입력 문제: 새로고침 버튼이 조이스틱 쥐는 자리와 겹쳐서 패널이 뜨자마자 잘못 누름.** ([Reddit 2022](https://www.reddit.com/r/Survivor_io/comments/wwn7mx/relocate_skill_refresh_button/))
- **꼼수: 앱을 껐다 켜면 레벨업 직전으로 돌아가 선택지가 다시 뽑힘(무한 리롤).** ([writerparty](https://writerparty.com/party/survivor-io-reroll-cheat-how-to-re-roll-revive-and-refresh-for-free/)) — 우리 [GameDesign 6-7](../GameDesign.md#6-7-리롤)은 이미 "껐다 켜도 같은 카드"를 요구한다. 구현 때 꼭 지킨다.

**캐릭터·보스**
- 조각 80개(일부 120) 또는 현금($29.99 등). 캐릭터마다 전용 스킬이 판 안 선택지로 나옴. ([whatifgaming](https://whatifgaming.com/how-to-unlock-character-heroes-in-survivor-io/), [Fandom Characters](https://survivorio.fandom.com/wiki/Characters))
- 보스 등장 전 **일반 몹이 모두 죽고, 움직이지 않는 링(케이지)에 갇힌다.** 등장 알림이 먼저 뜬다. 돌진 전 빨간 선 예고. ([PocketGamer.biz](https://www.pocketgamer.biz/how-innovation-and-iteration-has-transformed-survivorio/), [simplegameguide](https://simplegameguide.com/how-to-beat-chapter-1-survivor-io/))

**메타·경제·수익**
- **에너지: 1판 5, 최대 30, 광고 +5 하루 3회, 100젬 → 15 하루 3회(2022).** 회복은 1에 20분(**한 곳만**). ([writerparty](https://writerparty.com/party/survivor-io-energy-guide/), [gamesadda](https://gamesadda.in/gaming/survivor-io-get-energy/))
- 장비 3개 합성 → 등급업, 그 위 S급·SS급. Tech Parts는 미리 끼면 특정 스킬이 강해짐. 방치 보상 최대 16시간. ([Fandom Merging](https://survivorio.fandom.com/wiki/Merging), [Tech Parts](https://survivorio.fandom.com/wiki/Tech_Parts), [theclashify](https://theclashify.com/survivor-io-wiki/))
- **부활: 메인 챕터 1회. 광고 부활 HP 50%, 젬 부활 HP 100%, 부활 때 주변 몹 정리.** ([Reddit](https://www.reddit.com/r/Survivorio/comments/13680ls/))
- 월정액 $5(매일 150젬), 슈퍼 월정액 $10(광고 없음, 에너지 최대 50). 잘 팔리는 것: 챕터 클리어 때 1회 팩, 월정액, Growth Fund. ([Reddit](https://www.reddit.com/r/Survivorio/comments/zm1mht/), [Naavik](https://naavik.co/deep-dives/evolution-of-hybridcasual-deepdive/))
- 매출: 출시 2개월 $75M·3,700만 다운로드(AppMagic), 누적 IAP $500M 돌파(Sensor Tower 2024-04), Habby 누적 $2B 중 40%(2026-02). ([Mobilegamer.biz](https://mobilegamer.biz/two-months-in-survivor-io-passes-75m-from-37m-downloads/), [app2top](https://app2top.com/news/revenue-survivor-io-the-iap-has-exceeded-half-a-billion-dollars-265784.html), [gamigion](https://www.gamigion.com/habby-hits-2b-in-user-spending-and-350m-downloads/))

**불만:** 파워 크립(S 캐릭터, SS 장비, 새 펫 계통)이 계속 쌓여서 **2026-01 커뮤니티 공개서한 → 2026-02 과금 거부 운동**, $150 팩, 추첨형 이벤트. ([Reddit](https://www.reddit.com/r/Survivorio/comments/1q8n7r7/), [Reddit](https://www.reddit.com/r/Survivorio/comments/1qwhv0l/), [Reddit](https://www.reddit.com/r/Survivorio/comments/18rqm4u/))

---

### 1-4. Capybara Go! (Habby, 2024-11 글로벌) — 조작 최소화 히트작

- "다음 날" 버튼만 누르면 이벤트·전투가 자동 진행. 이동·전투 조작 없음, 3지선다 스킬 선택만. 앱을 아무 때나 꺼도 이어짐. ([gamigion](https://www.gamigion.com/capybara-go-global-launch-ua-case-study/), [niaogebiji](https://www.niaogebiji.com/article-692620-1.html))
- 챕터 길이 60·60·30·30·5일이 돌아감. 60일 챕터는 25·40·60일째 보스, **보스 전날은 항상 휴식**. 보스 처치 시 HP 20% 회복 + 빨강 등급 스킬 상자. 한 판 시간(분)은 **확인 못 함**. ([namu 영문](https://en.namu.wiki/w/%EC%B9%B4%ED%94%BC%EB%B0%94%EB%9D%BC%20Go!), [niaogebiji](https://www.niaogebiji.com/article-692620-1.html))
- 스킬 등급 Common·Legendary·Mythic·Immortal. **엘리트 처치 → Mythic 3중 1.** Demon 계약은 최대 HP를 내고 Legendary. 효과를 숫자 대신 "↑↑" 기호로 보여줌(계산 부담↓). 리롤은 탤런트로 1회. ([game-vault Skills](https://capybara-go.game-vault.net/wiki/Skills), [namu 영문](https://en.namu.wiki/w/%EC%B9%B4%ED%94%BC%EB%B0%94%EB%9D%BC%20Go!))
- **스태미나: 최대 30, 1판 5, 24분에 1(가득 12시간, 2024-12).** 광고 15 × 하루 2회, 보석 15 × 하루 4회(90→180) — game-vault 한 곳 기준, 다른 가이드는 180·240젬으로 달라서 **출처마다 다름**. ([altema](https://altema.jp/capybarago/stamina), [game-vault 재화](https://capybara-go.game-vault.net/wiki/Currency_and_Items), [grindnstrat](https://grindnstrat.com/capybara-go-guide/))
- **던전은 실패하면 티켓이 줄지 않는다.** 하루 무료 2 + 광고 2. ([gamigion](https://www.gamigion.com/capybara-go-global-launch-ua-case-study/)) — 우리 "보스 최초 도전 패배 시 하트 환급"과 같은 발상.
- 영웅 뽑기 없음(장비·펫 뽑기 있음). 광고 제거 $10, 평생 $30, 월정액 $5(4배속), 배틀패스 $10. ([note.com](https://note.com/gamestearoom/n/n1bdeda2d22b4?hl=en), [game-vault P2W 가이드](https://capybara-go.game-vault.net/wiki/Guide:Real_Money_Spending_P2W_Guide))
- 매출: 3개월 안에 $100M, **한국 비중 28%**(AppMagic). ([PocketGamer.biz](https://www.pocketgamer.biz/habbys-capybara-go-surpasses-100m-in-gross-player-spending/))
- 성공 이유로 꼽히는 것: 밈 캐릭터, 카지노식 대박 연출(빛·진동), 나쁜 이벤트는 30% 이하로 맞춤, 엉뚱한 유머. ([gamigion](https://www.gamigion.com/what-makes-capybara-go-successful/), [niaogebiji](https://www.niaogebiji.com/article-692620-1.html))
- **불만:** 보스가 "스탯 체크", 무과금 이벤트 축소, 카지노 연출이 "eyesore", 9~10장 진행 벽. ([minireview](https://minireview.io/adventure/capybara-go), [Reddit](https://www.reddit.com/r/CapybaraGoGame/comments/1l9l033/), [Reddit](https://www.reddit.com/r/CapybaraGoGame/comments/1hwnpv9/))

---

### 1-5. Vampire Survivors (poncle, 모바일 2022-12)

- 이동만, 공격 자동. 개발자 Luca Galante는 수동 조준을 넣으면 "the game becomes like any other"라며 거부. 대부분 스테이지 30분(일부 15·20분). ([Metro 인터뷰](https://metro.co.uk/2024/11/01/vampire-survivors-ode-castlevania-interview-i-put-everything-21906264/), [위키 Reaper](https://vampire.survivors.wiki/w/The_Reaper))
- 선택지 3개(Luck으로 4번째 확률). 슬롯 무기 6 + 패시브 6. 올릴 게 없으면 골드나 회복. ([Level up](https://vampire.survivors.wiki/w/Level_up), [Weapons](https://vampire.survivors.wiki/w/Weapons))
- **Reroll / Skip / Banish 모두 판 밖 PowerUp으로 사서 늘림(각 랭크당 +2, 최대 +10).** Skip은 경험치를 줌. Banish한 것은 그 판에 다시 안 나옴. **리롤로 버린 것은 바로 다음 세트에 안 나옴**("cannot appear in the new set") — 직접 다시 확인함. Seal은 판 시작 전에 뽑기 목록에서 빼기. ([Reroll](https://vampire.survivors.wiki/w/Reroll), [Skip](https://vampire.survivors.wiki/w/Skip), [Banish](https://vampire.survivors.wiki/w/Banish), [Seal](https://vampire.survivors.wiki/w/Seal))
- **진화: 만렙 무기 + 짝 패시브 + 보물상자(대부분 10분 이후 보스가 떨굼).** 2022-03 패치로 **발견한 진화는 레벨업 카드에 정보가 뜸.** Grimoire 유물은 발견한 진화만 보여줌. Collection에서 안 써본 것은 "?". ([Evolution](https://vampire.survivors.wiki/w/Evolution), [Grimoire](https://vampire.survivors.wiki/w/Grimoire), [Collection](https://vampire.survivors.wiki/w/Collection))
- Galante: 선택지는 "three to four options… more than three to four felt overwhelming", 보물상자 연출은 "super rewarding and over-the-top"으로 공들임. ([Android Police](https://www.androidpolice.com/vampire-survivors-developer-interview/))
- 옵션: 번쩍임, 화면 흔들림, 피해 숫자, 피 표현 끄기, 조이스틱 종류. 모바일은 세로로 시작(가로 전환 가능). ([Options](https://vampire.survivors.wiki/w/Options), [TouchArcade](https://toucharcade.com/2022/12/09/vampire-survivors-mobile-review-controller-cloud-save-sync-dlc-unlock-iphone-ipad-pro/))
- **모바일 수익: 무료 + 선택형 광고(부활, 골드). 에너지 없음. DLC($1.99)를 사면 선택형 광고가 꺼짐.** 광고 횟수 상한은 **확인 못 함**. Galante: "I'm personally not a fan of microtransactions." ([PocketGamer.biz](https://www.pocketgamer.biz/vampire-survivors-surprise-launches-to-mobile/), [TouchArcade DLC](https://toucharcade.com/2023/03/14/vampire-survivors-legacy-of-the-moonspell-dlc-mobile-download-release-date-price-iphone-android-ios-poncle/))

### 1-6. Magic Survival (leme, 한국 1인 개발, 2019)

- 이동만, 자동 공격, "one-hand control". 구역마다 20~25분, 마지막 맵은 엔드리스. ([namu 영문](https://en.namu.wiki/w/%EB%A7%A4%EC%A7%81%EC%84%9C%EB%B0%94%EC%9D%B4%EB%B2%8C), [Google Play](https://play.google.com/store/apps/details?id=com.vkslrzm.Zombie&hl=en_US))
- 선택지 3개(일부 4). **리롤 없음, 대신 레벨업을 포기하고 마나 20%를 돌려받기(최대 3회).** ([namu 영문](https://en.namu.wiki/w/%EB%A7%A4%EC%A7%81%EC%84%9C%EB%B0%94%EC%9D%B4%EB%B2%8C))
- 조합: Lv7 마법 2개 + 맞는 특성, 캐릭터 레벨 25/50/75에 1번씩. **쓸 수 있으면 버튼 아이콘이 흰색 → 빨간색.** ([FAQ](https://ms-secrets.notion.site/FAQ-1c99ac8db3b54a119bb819314346e160), [Reddit](https://www.reddit.com/r/magic_survival/comments/1cr6ny3/))
- **리뷰 불만: "There are too many combinations to remember… tap on a spell to see all possible combinations; combinations that have already been activated are grayed out".** ([Google Play](https://play.google.com/store/apps/details?id=com.vkslrzm.Zombie&hl=en_US))
- 수익: 광고 + IAP($0.99~$14.99). 광고 제거를 사면 부활이 자동. 불만: 부활 광고가 안 뜬 채 죽으면 30초 광고 강제. ([playstoreradar](https://playstoreradar.com/game/com.vkslrzm.Zombie), [Reddit](https://www.reddit.com/r/magic_survival/comments/192l9i3/), [Reddit](https://www.reddit.com/r/magic_survival/comments/1owodq3/))

### 1-7. Brotato (모바일 2023)

- 20웨이브, 1웨이브 20초에서 웨이브마다 +5초(9웨이브부터 60초), 20웨이브 90초 보스. 전투만 약 17.5분(계산값). ([Waves](https://brotato.wiki.spellsandguns.com/Waves))
- 업그레이드 4개 중 1개, **레벨 5는 티어 II, 10·15·20은 티어 III 확정.** 상점 4개, 리롤 비용이 웨이브에 비례해 오르고, **Lock은 무료·무제한.** ([Upgrades](https://brotato.wiki.spellsandguns.com/Upgrades), [Shop](https://brotato.wiki.spellsandguns.com/Shop))
- 레시피 진화 없음. 같은 무기 2개 → 위 티어, 무기 클래스 세트 보너스(2~6개). 캐릭터 태그가 맞는 아이템을 더 자주 보여줌. ([Weapons](https://brotato.wiki.spellsandguns.com/Weapons), [Weapon Classes](https://brotato.wiki.spellsandguns.com/Weapon_Classes))
- 유료판 $4.99. 따로 있는 무료판은 VIP·광고, "하루 광고 횟수 제한" 패치, "돈 안 내면 막힌다"는 불만. ([Google Play 검색](https://play.google.com/store/search?q=brotato&c=apps), [App Store 무료판](https://apps.apple.com/us/app/brotato/id6445884925), [Google Play 무료판](https://play.google.com/store/apps/details?id=com.brotato.shooting.survivors.action.roguelike&hl=en))

### 1-8. BALL x PIT (Kenny Sun / Devolver, PC 2025-10, 모바일 2026-03)

- 벽돌깨기 + 서바이버. 이동(왼손)과 조준(오른손)이 따로, 자동 발사 토글 → **움직여도 사격이 멈추지 않는다.** 한 레이어 최대 약 20분. 판이 끝나면 마을에 건물을 지음. ([Polygon](https://www.polygon.com/ball-x-pit-review/), [spottis](https://www.spottis.com/games/ball-x-pit-controls/))
- 선택지 3개(건물로 4). **슬롯 공 4 + 패시브 4**(건물로 +1씩) — 우리 4/4와 같다. 리롤은 골드, 점점 비쌈. **Banish는 건물로 열고 전투당 2회.** ([ballxpitguide](https://www.ballxpitguide.com/buildings))
- Fusion Reactor를 주우면 셋 중 하나: 무작위 레벨업 / **Lv3 공 2개 합체(슬롯 1칸 빔)** / 레시피 진화. 진화 쌍은 일반 합체가 막히고 진화가 강제됨. **하지만 재료 3개 이상 레시피는 보호되지 않아 "no warning and no undo"로 사라진다는 불만.** 진화를 한 번 하면 레벨업 화면에 가능한 조합이 표시. ([Fusion Reactor](https://ballxpit.wiki.gg/wiki/Fusion_Reactor), [Balls](https://ballxpit.wiki.gg/wiki/Balls), [playerecho](https://playerecho.com/ball-x-pit/fusion-and-evolution), [GameFAQs](https://gamefaqs.gamespot.com/pc/539487-ball-x-pit/faqs/82305))
- 판 끝 통계 화면이 있고 패치로 계속 보강. ([Game Updates](https://ballxpit.wiki.gg/wiki/Game_Updates))
- 모바일은 **가로 전용이라 "the game is literally a vertical game" 불만.** 무료 체험 후 $9.99 1회 구매, "no microtransactions". ([Reddit](https://www.reddit.com/r/AndroidGaming/comments/1rruaq3/), [Gematsu](https://www.gematsu.com/2026/03/ball-x-pit-now-available-for-ios-android), [Reddit 공지](https://www.reddit.com/r/BALLxPIT/comments/1rrvqso/))

### 1-9. Soul Knight (ChillyRoom, 2017~) — "공정하다"는 평의 기준

- 메인 3층 × 5레벨 = 15레벨, 층 끝마다 보스. 한 판 15~30분. 조이스틱 + 공격 버튼, **가장 가까운 적 자동 조준, 이동과 사격 동시.** ([Playing the Game](https://soul-knight.fandom.com/wiki/Playing_the_Game), [Reddit](https://www.reddit.com/r/SoulKnight/comments/q3vrd2/), [Game Mechanics](https://soul-knight.fandom.com/wiki/Game_Mechanics))
- 버프는 정해진 레벨(1-1, 1-3, 1-5, 2-3, 2-5) 끝에 3중 1, 빈 칸 7개. 무기 합성 버튼은 **두 무기가 가까이 있을 때만 나타난다.** ([Buffs](https://soul-knight.fandom.com/wiki/Buffs), [Mergeable](https://soul-knight.fandom.com/wiki/Category:Mergeable_Weapons))
- **Advanced Tutorial: 고정 15방. 5번 방 상자에 Gatling Gun 1 + Rainbow 2가 반드시 들어 있고 "Rainbow Gatling을 합성하라"고 시킴. 튜토리얼 중 체력은 1 밑으로 안 내려감.** ([Advanced Tutorial](https://soul-knight.fandom.com/wiki/Advanced_Tutorial))
- 보스: **체력 50% 아래로 분노**(새 패턴, 빨라짐), 50%와 처치 때 에너지 구슬 10개. 보스방에 엄폐물. ([Bosses](https://soul-knight.fandom.com/wiki/Bosses), [Dong Zhuo](https://soul-knight.fandom.com/wiki/Dong_Zhuo))
- **역사 인물 사례:** "Three Kingdoms - Gunfight" 시즌 모드에 동탁·화웅 등이 보스로, 조운·장비 등이 스킨으로 나온다. ([Update 6.0.0](https://soul-knight.fandom.com/wiki/Update_6.0.0), [ChillyRoom](https://www.chillyroom.com/en/soul-knight/three-kingdoms))
- 설정: 조이스틱 3종, 버튼 위치 변경, **화면 흔들림 세기 조절**. HUD는 체력 50% 미만 등에서 "!" 표시. ([Controls](https://soul-knight.fandom.com/wiki/Controls), [Pause Menu](https://soul-knight.fandom.com/wiki/Pause_Menu), [HUD](https://soul-knight.fandom.com/wiki/HUD))
- **에너지(스태미나) 없음.** "No energy, No forced Ads, No Timers". ([Reddit](https://www.reddit.com/r/iosgaming/comments/5upqbh/))
- **보상형 광고 하루 5회(첫 회 500보석, 이후 200)**, 부활은 광고 1회 가능, 광고는 판이 끝난 뒤 보게 해준다는 칭찬. 캐릭터는 보석 2,000~20,000 또는 **$0.99~$1.99.** ([Fridge](https://soul-knight.fandom.com/wiki/Fridge), [Characters](https://soul-knight.fandom.com/wiki/Characters), [Metacritic](https://www.metacritic.com/game/soul-knight-2017/user-reviews/))
- 평: "No $99.99 gem pack whale hunting nonsense", "$20이면 거의 모든 캐릭터". 불만: 캐릭터 절반이 유료, 최근 온라인 로그인 강제. ([TouchArcade](https://toucharcade.com/2017/03/10/soul-knight-review), [Google Play](https://play.google.com/store/apps/details?id=com.ChillyRoom.DungeonShooter&hl=en), [Reddit](https://www.reddit.com/r/SoulKnight/comments/1j7z6zf/))

### 1-10. Zombie Waves (Fun Formula, 2023) — 2025 미국 슈팅 매출 상위 3

- 선택 이유: Sensor Tower 2025년 2분기 미국 Shoot'em Up 매출 상위 3이 Archero 2, Zombie Waves, Survivor.io. (작성자가 AI로 표시된 글이라 참고용) ([Sensor Tower](https://sensortower.com/blog/2025-q2-unified-top-5-shoot'em%20up%20games-revenue-us-6019ed4d241bc16eb8623896))
- 3D 자동 조준 한 손 조작, 스토어 설명 "한 판 6~12분", 3중 1. ([Google Play](https://play.google.com/store/apps/details?id=com.ddup.zombiewaves.zw&hl=en))
- 에너지는 2023년 12분에 1(한국 블로그), 지금 값은 **확인 못 함**. ([tistory](https://shylian.tistory.com/1751))
- 불만: "리셋 때 광고만 30분 이상", 다 봐도 보상이 안 들어오는 광고, 캐릭터·무기 하나에 $100. ([Google Play](https://play.google.com/store/apps/details?id=com.ddup.zombiewaves.zw&hl=en), [iosgames.net](https://www.iosgames.net/Zombie-Waves-shooting-game/index.html))
- 시장: AppMagic 2025 보고서 — RPG 매출은 15% 줄었지만 **로그라이크 세부 장르는 26% 성장**(Archero 2, Mech Assemble 주도). ([AppMagic PDF](https://appmagic.rocks/files/view/upload/Reports/EN_Monetization_Report_2025.pdf))

---

### 1-11. 빌드 저장과 비슷한 사례

#### 카제나 (Chaos Zero Nightmare, Smilegate, 2025) — 우리 기록 시스템의 원본
- 로그라이크 모드 "카오스" 한 판에서 모은 카드·번뜩임·장비를 **캐릭터별로** 저장한다. ([gamewith 70847](https://gamewith.net/chaoszeronightmare/70847))
- 저장 종류 3가지 ([gamewith](https://gamewith.net/chaoszeronightmare/70847), [나무위키](https://namu.wiki/w/%EC%B9%B4%EC%98%A4%EC%8A%A4%20%EC%A0%9C%EB%A1%9C%20%EB%82%98%EC%9D%B4%ED%8A%B8%EB%A9%94%EC%96%B4/%EC%B9%B4%EC%98%A4%EC%8A%A4))
  - **선명한 기억**: 장비, 고유·기본 카드 → 100% 저장
  - **흐릿한 기억**: 공용·몬스터 카드, 신 번뜩임, 복제·제거 기록 → **포인트 상한 안에서 무작위로** 기록
  - 저장 안 됨: 운명 선택, 이야기 분기, 층 규칙
- **상한 = 30pt + 티어당 10pt** (1티어 30, 6티어 80). 나이트메어는 티어 +1. 즉 "어려운 판을 깰수록 많이 저장" — 우리와 같은 발상. (이 문서에서 gamewith로 직접 다시 확인함)
- **가장 큰 불만: 상한을 넘으면 무작위로 되돌려짐.** "세이브가 오염된다"(루리웹), "4일 걸려 복제 4장을 띄웠는데 2장만 남았다"(STOVE), "Was wondering why my decks were downgrading randomly"(Reddit). ([루리웹](https://bbs.ruliweb.com/community/board/300143/read/72760150), [STOVE](https://page.onstove.com/chaoszeronightmare/kr/view/11194140), [Reddit](https://www.reddit.com/r/ChaosZeroNightmare/comments/1ov0gwz/save_data_i_still_dont_get_it_why_so_complex/))
- 출시 초기엔 게임 안에 포인트 설명이 없었다. Reddit 요청: "just show me how far away I'm from the cap". ([나무위키](https://namu.wiki/w/%EC%B9%B4%EC%98%A4%EC%8A%A4%20%EC%A0%9C%EB%A1%9C%20%EB%82%98%EC%9D%B4%ED%8A%B8%EB%A9%94%EC%96%B4/%EC%B9%B4%EC%98%A4%EC%8A%A4), [Reddit](https://www.reddit.com/r/ChaosZeroNightmare/comments/1qvxoda/confirmed_information_with_new_save_data_rules/))
- 쓰는 곳: 보스전(성운 왜곡), 점수 경쟁(대균열), 엔드 콘텐츠. 대균열은 처음에 "현재 시즌 세이브만" → 시즌3에 옛 세이브도 허용. ([gamewith 72851](https://gamewith.net/chaoszeronightmare/72851), [Inven Global](https://labs.invenglobal.com/articles/21028/the-time-to-return-is-now-chaos-zero-nightmare-season-3-brings-massive-changes-to-reduce-fatigue))
- 개발진 의도와 현실: "한 판에서 심혈을 기울여 완성한 요소를 저장해 다른 모드에서 재사용"(ZDNet). 한 판 30분·주 1~4회를 예상했는데 플레이어가 **'도자기 빚기'처럼 무한 반복**(게임조선). ([ZDNet](https://zdnet.co.kr/view/?no=20250825223531), [게임조선](https://www.gamechosun.co.kr/webzine/article/view.php?no=220042))
- 이후 고친 것: 시즌2 복제 대상 직접 선택, 시즌3 세이브 없이 하는 '출격' 모드 + 세이브 편집('블랙 매터') + 전투 스킵, 1주년에 "보유 한도가 빡빡하다는 데 공감" + 협동 콘텐츠에 **미리 짠 세이브 프리셋** 제공. 신규 유저는 "무엇을 어떤 순서로 해야 하는지 이해하기 어렵다". ([바이라인](https://byline.network/2026/10/16-731/), [아이뉴스24](https://inews24.com/view/2015536))
- 좋은 평: "같은 양상을 재현하기 어려운 장르에서 빌드를 보존해 성취감 유지". ([그린경제](https://www.greened.kr/news/articleView.html?idxno=332495))
- **확인 못 함:** 캐릭터당 세이브 보유 개수, '딥 트라우마' 확률, 상담 비용 현재 값.

#### 그 밖의 사례
| 게임 | 무엇을 저장·재사용하나 | 힘을 묶는 방법 | 반응 |
|---|---|---|---|
| **Mewgenics** (PC, 2026) | 모험을 마치고 은퇴한 고양이로 7일 카운트다운 뒤 쳐들어오는 **House Boss를 막음** | 이기면 "Super Retired"로 더 못 씀, 지면 그 고양이·장비가 사라짐 | 우리 "기록 → 보스"와 가장 비슷 ([wiki](https://mewgenics.wiki.gg/wiki/House_Bosses)) |
| **Inscryption** 데스카드 | 지면 덱의 카드 3장(비용 / 공격·체력 / 특수능력)을 합쳐 새 카드 1장, 이름 16자까지 | 내 덱에 있던 것만, 이후 판에 **가끔** 등장 | 죽음을 보상으로 바꾼 장치로 평가 ([wiki](https://inscryption.fandom.com/wiki/Deathcard)) |
| **우마무스메** | 육성을 마친 캐릭터를 다음 육성의 부모·PvP 출전에 씀, 친구 것을 빌림 | 대여 하루 3회 → 2026-01부터 5회, **첫 대여 무료** | 반복 피로 언급 ([Siliconera](https://www.siliconera.com/legacy-umamusume-daily-borrows-nearly-double-worldwide/), [Goonhammer](https://www.goonhammer.com/understanding-team-trials-in-umamusume-pretty-derby)) |
| **Super Auto Pets / Backpack Battles** | 다른 사람이 같은 턴에 만든 팀·가방 스냅샷과 싸움 | **같은 진행 단계끼리만** 붙임, 없으면 AI 팀 | 상위권 매칭 불공평 불만 ([SAP wiki](https://superautopets.wiki.gg/wiki/The_Basics), [Steam](https://steamcommunity.com/app/2427700/discussions/0/4290313152626154575)) |
| **Dragon's Dogma 2** 폰 | 다른 사람의 동료(폰)를 고용 | 빌린 폰은 레벨이 안 오름. **빌려준 주인이 보상을 받음.** 고용이 없으면 가짜 플레이어가 대신 고용 | 공정한 장치라는 평 ([PC Gamer](https://pcgamer.com/games/rpg/dragons-dogma-2-pawns), [Automaton](https://automaton-media.com/en/news/dragons-dogma-2-has-fake-players-that-hire-less-desirable-pawns/)) |
| **Soul Knight** Forge Table | 판에서 같은 무기를 여러 번 주우면 제작 가능 → **다음 1판에만** 들고 감, 최대 4개 보관 | 1회용 | ([wiki](https://soul-knight.fandom.com/wiki/Forge_Table)) |
| **Archero 2** | 장비·룬 **프리셋**만 (판 안 빌드 아님) | — | "프리셋 더 달라" 요청 ([Reddit](https://www.reddit.com/r/ArcheroV2/comments/1kzzi6b/)) |

---

### 1-12. 손맛·온보딩·광고·하트·접근성 (게임 공통 자료)

**손맛**
- Vlambeer "The Art of Screenshake"(2013): 큰 총알, 총구 섬광, 피격 흰색 깜빡임, 넉백, 카메라 반동, 저음 효과음, 맞는 순간 **약 20ms 멈춤(sleep)** 등 30가지. ([YouTube](https://www.youtube.com/watch?v=AJdEqssNZ-U))
- 히트스톱: 사쿠라이(스매시브라더스) — 피해가 클수록 길게, 상한을 두고, 투사체는 짧게(프레임 수는 칼럼에 없음). 일본 IEICE 2023 연구 요약 — 적당한 히트스톱 **약 0.1~0.2초**, 슬로모션 0.2~0.4초(요약만 확인). ([sourcegaming](https://sourcegaming.info/2015/11/11/thoughts-on-hitstop-sakurais-famitsu-column-vol-490-1/), [IEICE](https://ken.ieice.org/ken/paper/20230315OCSt/eng/))
- 화면 흔들림(Eiserloh, GDC 2016): "트라우마" 값 0~1을 쌓고 시간에 따라 줄임, **흔들림 크기 = 트라우마²** (0.3/0.6/0.9 → 9%/36%/81%), 무작위 대신 Perlin 노이즈, 2D는 이동 + 회전. ([GDC 슬라이드](http://www.mathforgameprogrammers.com/gdc2016/GDC2016_Eiserloh_Squirrel_JuicingYourCameras.pdf))
- Xbox 접근성 지침 XAG 117: "Avoid camera shake… or provide an option to turn off". ([Microsoft](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/117))
- 피해 숫자 크기·빈도에 대한 근거 있는 수치는 **확인 못 함**.

**온보딩·리텐션**
- GameAnalytics: 2~3분 안에 왜 재밌는지 알게, 켜자마자 결제창·광고 금지, 튜토리얼 단계마다 이탈을 추적. ([GameAnalytics](https://gameanalytics.com/blog/tips-for-a-great-first-time-user-experience-ftue-in-f2p-games))
- **D1 리텐션 기준:** GameAnalytics 2025 보고서(2024 데이터) 상위 25% 게임 D1 26.5~27.7%. 2026 보고서(2025 데이터) 상위 25% 30% 조금 넘음, 상위 10% 약 40%. 하이브리드캐주얼 퍼블리셔 Voodoo 목표 D1 45%·D7 15%. ([gamedevreports 2025](https://gamedevreports.substack.com/p/gameanalytics-mobile-gaming-benchmarks), [2026](https://gamedevreports.substack.com/p/gameanalytics-mobile-and-pc-game), [GWO](https://gameworldobserver.com/2023/07/25/voodoo-hybrid-games-d7-retention-games-and-names-podcast))
  - 참고: 우리 [Monetization 6장](../Design/Monetization.md#6-목표-지표-출시-후-확인) 목표 D1 35%는 **상위 10~25% 사이** 수준이다. 첫 게임 목표로는 높은 편이다.
- 첫 레벨업까지 걸리는 시간의 업계 기준: **확인 못 함**.

**보상형 광고**
- AdMob: 사용자가 직접 고른 뒤에만, 보상을 **보여주기 전에** 알림, 거절해도 진행에 문제없어야 함. 전면 광고는 앱 시작·종료 때, 모든 행동 뒤, 연속, 플레이 집중 중 금지. ([AdMob 7313578](https://support.google.com/admob/answer/7313578), [6201362](https://support.google.com/admob/answer/6201362))
- LevelPlay 문서: 버튼에 "Watch to earn 50 Gold"처럼 보상을 구체적으로, 자리(placement)별 횟수 제한(capping)·간격(pacing) 설정. ([LevelPlay](https://docs.unity.com/grow/levelplay/sdk/react/rewarded-ads-integration))
- Unity 2024: RPG는 DAU의 37.6%가 보상형 광고를 봄. **자원이 떨어진 순간처럼 맥락에 맞는 자리 38.1% vs 레벨 사이 23.8%.** ([PocketGamer.biz](https://www.pocketgamer.biz/unity-global-rewarded-ad-engagement-rose-by-32-in-2023/))
- Kongregate 로그라이크 Slashy Hero: **부활 버튼이 불분명해 참여율 6.5% → 버튼을 고치자 17% → 버그까지 고치자 35~40%.** ([Game Developer](https://www.gamedeveloper.com/business/best-practices-in-game-ads))

**하트(에너지)**
- Mobile Free to Play: 에너지의 역할은 습관·속도 조절·수익. 대안은 "플레이가 아니라 보상을 제한", 같은 수치를 긍정적으로 포장. Midnight Star는 "stop telling me to stop playing your game" 반응 때문에 에너지를 없앰. ([MF2P](https://mobilefreetoplay.com/understanding-and-eliminating-energy-systems/), [Game Developer](https://www.gamedeveloper.com/design/core-players-energy-mechanics-they-don-t-mix))
- 실패 시 하트 환급의 효과를 보여주는 자료: **확인 못 함.**

**접근성**
- Game Accessibility Guidelines 기본: 큰 터치 버튼, **색만으로 정보 전달 금지**(남성 8~10% 적록색약), 깜빡임 피하기, 읽기 쉬운 글자. WCAG 2.3.1: **1초에 3번 넘게 번쩍이지 않기.** ([GAG](https://gameaccessibilityguidelines.com/full-list/), [WCAG](https://www.w3.org/WAI/WCAG22/Understanding/three-flashes-or-below-threshold.html))
- Hades God Mode: 피해 감소 20%에서 시작, 죽을 때마다 +2%, 최대 80%. Celeste Assist Mode: 속도 50~100%. ([Inverse](https://inverse.com/gaming/hades-god-mode-interview), [GAG Celeste](https://gameaccessibilityguidelines.com/celeste-assist-mode))

---

## 2. 주제별 비교 표

### 2-1. 에너지·하트

| 게임 (기준 시점) | 최대 | 1판 비용 | 충전 | **1판 분량이 차는 시간** | 광고 충전 | 보석 충전 |
|---|---|---|---|---|---|---|
| Archero 1 (2019) | 20 (BP 30) | 5 | 12분에 1 | **60분** | +5 × 하루 4 | 100젬 → 20 (2023: 60젬) |
| Archero 2 (2025) | 30 (유료 카드 50) | 5 | 12분에 1 | **60분** | +5 × 하루 3 (출처에 따라 3~4) | 100젬 → 20 |
| Survivor.io (2022~23) | 30 (월정액 50) | 5 | 20분에 1 (한 곳만) | **약 100분** | +5 × 하루 3 | 100젬 → 15 × 하루 3 |
| Capybara Go! (2024~25) | 30 | 5 | 24분에 1 | **120분** | 15 × 하루 2 (한 곳만) | 출처마다 다름 |
| Zombie Waves | 확인 못 함 | 확인 못 함 | 12분에 1 (2023) | 확인 못 함 | 있음 | 있음 |
| Soul Knight · VS 모바일 · Brotato · BALL x PIT | **없음** | — | — | — | — | — |
| **History Heroes** | **5** (월정액 8) | **1** | **20분에 1** | **20분** | +2 × 하루 3 | 💎60 → 5 × 하루 5 |

→ 우리가 3~6배 너그럽다. 첫날 3판 무료, 보스 최초 도전 패배 환급(Capybara 던전 "실패하면 티켓 안 줄어듦"과 같은 발상)도 있다. **바꿀 필요 없음.**

### 2-2. 레벨업 규칙

| 게임 | 선택지 | 리롤 | 숨기기·밴·스킵 | 슬롯 | 조합(진화) 조건 | 조합 힌트 |
|---|---|---|---|---|---|---|
| Archero 1 | 3 | 처음 없음 → BP 보상 카드 1회 (2020) | — | 제한 없음 | 없음 | — |
| Archero 2 | 3 (등급 4단계) | 판당 1 + 월정액 1 = 최대 2, **방금 본 것 제외** | 월정액에 "선택 포기" | 확인 못 함 | 없음 (정해진 방 Valkyrie·Devil) | 진행 바에 이벤트 방 아이콘 |
| Survivor.io | 3 | 판당 1 (+9,999젬 카드 1) | — | 6 + 6 | 액티브 Lv5 + 패시브 Lv1+, 액티브+액티브 합체 | **카드에 "EVO" 줄** |
| Capybara Go! | 3 (등급 4단계) | 탤런트 1회 | — | — | 엘리트 → Mythic 3중 1 | 효과를 ↑↑ 기호로 |
| Vampire Survivors | 3 (Luck 4) | 메타로 최대 +10, **다음 세트에서 제외** | Skip(XP 줌)·Banish 각 최대 +10, Seal | 6 + 6 | 만렙 무기 + 패시브 + 10분 뒤 상자 | **발견 후 카드에 표시**, Grimoire |
| Magic Survival | 3 (4) | 없음 | 마나 20% 회수 × 3 | 확인 못 함 | Lv7 마법 2개 + 특성, 레벨 25/50/75 | 버튼이 빨간색으로 |
| Brotato | 업그레이드 4 / 상점 4 | 재료, 점점 비쌈 | **Lock 무료·무제한** | 무기 6 | 같은 무기 2개 → 위 티어 | 캐릭터 태그로 유도 |
| BALL x PIT | 3 (4) | 골드, 점점 비쌈 | Banish 전투당 2 (건물) | **4 + 4** | Lv3 2개 합체 / 레시피 진화 | 발견 후 레벨업 화면에 표시 |
| Soul Knight | 버프 3 (정해진 레벨) | — | 되돌리기 아이템 | 버프 7, 무기 2 | 무기 합성 | **가까이 있을 때만 버튼** |
| **History Heroes** | 3 | **광고 2/판** (결제로 안 늘림) | 조합 카드 "이번 판 숨기기" | **4 + 4** | 진화 Lv5 + 패시브 / 합체 Lv3+ 둘 | 발견 전 ???, 발견 후 "🔗 조합 재료" |

### 2-3. 보상형 광고 자리와 부활

| 게임 | 광고 자리 (하루 상한) | 부활 | 전면 광고 |
|---|---|---|---|
| Archero 1 | 에너지(4), 룰렛(5), 상자(1), 상점 코인, 임시 버프 | 판당 1 (30젬 → 나중에 광고), 결정 5초 | 없음 |
| Archero 2 | 에너지(3), 빠른 방치(2), 상점 무료 팩, 티켓 | 판당 1 (광고·젬) | **있음** |
| Survivor.io | 에너지(3), 빠른 순찰(1), 상점 | 판당 1 (광고 HP 50% / 젬 100%, 주변 몹 정리) | 확인 못 함 |
| Capybara Go! | 상점 상자 3+1, 펫 알(3), 던전(2), 스태미나(2) | 확인 못 함 | 확인 못 함 |
| Soul Knight | 보석 **하루 5** (첫 500, 이후 200), TV 골드 | 광고 1회 | 없음 |
| Vampire Survivors | 부활·골드 (상한 확인 못 함), DLC 사면 꺼짐 | 광고 | 없음 |
| Magic Survival | 부활 광고, 광고 제거 = 자동 부활 | 광고 | 부활 광고 실패 시 30초 강제(불만) |
| **History Heroes** | 리롤(판당 2), 일반 상자(1), 하트(3), 보석(2) | **출시 이후 검토** | **없음** (원칙) |

### 2-4. 영웅 해금

| 게임 | 해금 방법 | 가격 예 | 힘 차이 |
|---|---|---|---|
| Archero 1 | 진행·코인·젬·현금, 2.0부터 조각 30 | 젬 1,000~2,500, 8.99~16.99€ | 크다 ("S 영웅은 과금 없이 거의 불가") |
| Archero 2 | 조각 50, **전설은 뽑기 이벤트만** | 3,000젬, 약 $30 | 크다 (Thor 고정 픽) |
| Survivor.io | 조각 80 (S 120) 또는 현금 | $29.99, BP $19.99 | 크다 |
| Soul Knight | 보석 또는 현금 | 2,000~20,000 보석, **$0.99~1.99** | 작다 (성장해도 체력 +1 등) |
| Vampire Survivors | 골드 (살 때마다 +10%) + 업적 | — | 작다 |
| Brotato · BALL x PIT | 조건 달성·건물 | — | 작다 |
| **History Heroes** | **진행으로 무료** 또는 💎1,500 / 영웅 패키지 ₩12,000, **뽑기 없음** | | 작게 (대표 효과로 차이) |

### 2-5. 보스 설계

| 게임 | 단계 | 예고 | 빌드를 시험하는 방법 |
|---|---|---|---|
| Archero 1 | 분열이 곧 단계 (Red Skull 50%) | 빨간 조준선, 일부 레이저는 너무 짧음 | 소환 보스는 광역이 없으면 포위, 무피격이면 Devil 100% |
| Archero 2 | 2단계 HP 바 추가 + 졸개 | 표적 마커, 번개 원 | 보스 전용 점수전(Seal Battle 2분) |
| Survivor.io | 시간 고정 등장 | 등장 알림, 돌진 빨간 선 | 몹 정리 + 고정 링 안에서 1:1 |
| Soul Knight | **50% 분노** (새 패턴, 빨라짐) | 공격 전 잠깐 멈춤 | 엄폐물, 50% 때 에너지 구슬 10개 |
| Capybara Go! | — | — | 스탯 체크라는 **비판** |
| **History Heroes** | 1/3·2/3 전환 3단계 | 예고 + 빈틈 보장 | 보스마다 다른 패턴 (멈춤 / 광역 / 탄막 / 순간 화력) |

### 2-6. 한 판 시간

| 게임 | 한 판 |
|---|---|
| Archero 1 | 파밍 기준 6~20분 (챕터마다 다름), 방 하나 1분 미만 |
| Archero 2 | 쉬운 챕터 3~8.5분, 중반 30~40분 (불만), 6분 생존 챕터 |
| Survivor.io | 15분 / 8분 챕터 |
| Zombie Waves | 6~12분 (스토어 설명) |
| Soul Knight | 15~30분 |
| Vampire Survivors | 30분 (일부 15·20) |
| Magic Survival | 20~25분 (+엔드리스) |
| Brotato | 전투만 약 17.5분 + 상점 |
| BALL x PIT | 레이어 최대 약 20분 |
| Capybara Go! | 확인 못 함 (하루 숙제 15~20분) |
| **History Heroes** | **던전 8~10분 · 보스 2~3분 · 서바이벌 10분** |

→ 우리 8~10분은 Zombie Waves·Survivor.io 8분 챕터와 같은 짧은 쪽이다. Archero 2는 판이 길어지면서 불만이 나왔다. **지금 길이를 지킨다.**

---

## 3. 우리 게임에 주는 제안

### (0) 그대로 두면 되는 것 (조사로 확인됨)
- **하트 5개·20분**: 경쟁작보다 3~6배 너그럽다 (2-1).
- **리롤 광고 2회, 결제로 안 늘림**: Archero 2·Survivor.io는 돈으로 리롤을 판다. 우리 원칙이 더 공정하다.
- **카드에 실제 숫자 표시**: Archero 1은 화살표만 보여줘서 불만을 들었다.
- **수련은 직접 고르기**: Archero 1의 무작위 탤런트는 비판받았다.
- **영웅 뽑기 없음**: Archero 2의 "전설 영웅은 뽑기로만"이 큰 불만이다.
- **세로 화면**: BALL x PIT는 가로 전용이라 비판받았다.
- **기록 저장은 직접 고르기, 확률 실패 없음**: 카제나 1위 불만의 해법이다.
- **조합 카드 확정 등장**: Survivor.io도 조건을 채우면 EVO를 확실히 보여준다.
- **"껐다 켜도 같은 카드"** ([6-7](../GameDesign.md#6-7-리롤)): Survivor.io의 무한 리롤 꼼수를 막는 규칙이다. v1.0 앱 복귀 작업 때 꼭 구현한다.

---

### (A) 지금 바로 반영할 것

> 순서 = 추천 작업 순서 (작고 효과가 큰 것부터). 각 항목은 **기획 문서를 먼저 고친 뒤** 구현한다.
> 숫자 중 **[제안]**은 출처의 값이 아니라 첫 시작값이다. 폰에서 맞춘다.

#### A1. 리롤하면 방금 본 카드는 빼고 뽑기
- **무엇:** 리롤할 때 지금 화면에 있던 일반 카드(조합 카드 제외)의 스킬을 후보에서 뺀 뒤 뽑는다.
  - 뺀 뒤 후보가 모자라면 [6-4](../GameDesign.md#6-4-레벨업-카드-3장-뽑기-규칙)의 등급 대체 규칙 → 그래도 없으면 기본 카드(체력 회복·골드)로 채운다. 기본 카드는 빼지 않는다(중복 허용 그대로).
  - 조합 카드는 지금처럼 고정.
- **왜:** VS("cannot appear in the new set")와 Archero 2(방금 본 스킬 제외)가 이렇게 한다. 지금 코드(`SkillManager.Reroll`)는 같은 카드가 다시 나올 수 있어서 **광고 1편을 보고도 같은 카드**가 뜰 수 있다. BALL x PIT에는 "같은 카드가 반복된다"는 불만이 있다.
- **고칠 문서:** [GameDesign 6-7 리롤](../GameDesign.md#6-7-리롤)에 한 줄 추가.
- **구현 범위:** `Skills/Runtime/SkillManager.Reroll` — `BuildCandidates` 결과에서 `current`의 일반 카드 스킬을 `RemoveAll`. EditMode 테스트 3개: ① 후보가 충분하면 리롤 결과에 이전 카드 0장 ② 후보가 1~2개뿐이어도 예외 없이 3장 채움 ③ 조합 카드는 유지.

#### A2. 레벨업 창 오입력 막기 + 광고 버튼 문구 구체화
- **무엇:**
  1. 창이 열린 뒤 **0.4초 [제안]** 동안 카드·리롤·숨기기 버튼을 누를 수 없게 한다. 게임이 멈춘 상태(timeScale 0)이므로 **실제 시간(unscaled)**으로 센다.
  2. 창이 열릴 때 손가락이 화면에 닿아 있었다면(조이스틱을 쥐고 있었다면) **그 손가락을 한 번 뗄 때까지** 입력을 무시한다.
  3. 0.4초 동안 버튼은 반투명(알파 0.5)으로 보여서 "아직 못 누름"을 알 수 있게 한다.
  4. 리롤 버튼 문구: "📺 리롤 2/2" → **"📺 광고 보고 다시 뽑기 (남은 2)"**. 남은 0이면 버튼 숨김.
- **왜:** Survivor.io에서 새로고침 버튼이 조이스틱 자리와 겹쳐 "패널이 뜨자마자 잘못 누른다"는 불만이 있었다. 우리 조이스틱은 **화면 아래 절반 어디든** 생기므로 이 위험이 더 크다. 광고 버튼은 보상을 구체적으로 써야 한다(LevelPlay 문서). Kongregate 사례에서 부활 버튼을 분명하게 고치자 참여율이 6.5% → 17%가 됐다.
- **고칠 문서:** [UX 3 레벨업 카드](../Design/UX.md#3-레벨업-카드).
- **구현 범위:** `UI/LevelUpScreen.Open` (코루틴 `WaitForSecondsRealtime`, `Input.touchCount`/마우스 버튼 상태 확인), 리롤 라벨 문자열. 엘리트 처치처럼 레벨업이 연달아 뜨는 경우에도 매번 적용.

#### A3. 발견한 조합: 카드에 짝·진행도 표시, 도감에 "숨은 조합 수"
- **무엇:**
  1. **레벨업 카드:** 이미 발견한 레시피의 재료 카드면 지금 "🔗 조합 재료"만 뜬다. 이것을 **"🔗 강철 회오리 ← 방어력 · 칼날 3/5"**처럼 바꾼다. (결과 이름 + 짝 재료 + 지금 진행)
     - 한 스킬이 여러 레시피 재료면 **완성에 가장 가까운 것 1개 + "외 1"**.
     - 짝 재료를 이미 가졌으면 ✔ 표시.
  2. **HUD 스킬 칸:** 발견한 레시피 조건까지 **레벨업 1번만 남은** 액티브 아이콘에 반짝이는 테두리.
  3. **도감:** 미발견 레시피는 지금처럼 결과·짝 모두 ???. 대신 스킬 칸을 누르면 **"이 스킬이 들어가는 조합: 2개 (1개 발견)"**만 보여준다. 발견한 조합은 결과와 짝을 보여준다.
  4. 미발견 레시피의 첫 등장 방식(조건을 처음 채우면 조합 카드가 뜨면서 효과·사라질 재료 표시)은 **바꾸지 않는다.**
- **왜:** Survivor.io는 카드에 "EVO" 줄을 넣어 "레시피를 외울 필요가 없게" 했다. VS도 2022년 패치로 발견한 진화를 레벨업 카드에 띄우고, Grimoire는 "이 둘이 무언가가 된다"는 사실만 보여준다. Magic Survival은 "조합이 너무 많아 못 외운다, 마법을 누르면 조합 목록을 보여 달라"는 리뷰를 받았다. 우리는 v0.3에서 조합이 2개 → **14개**로 늘어나 기억 부담이 커진다.
- **고칠 문서:** [GameDesign 6-2 레시피 공개](../GameDesign.md#레시피-공개), [UX 3](../Design/UX.md#3-레벨업-카드), [UX 1 도감](../Design/UX.md#1-로비-구조-아래쪽-탭-5개).
- **구현 범위:** `SkillManager.IsFusionMaterial`을 확장한 `FindNearestKnownRecipe(skill)` (레시피 + 재료별 현재 레벨/필요 레벨), `UI/SkillCardView` 한 줄 추가, `UI/HudView` 테두리, `UI/CodexPanel` 스킬별 개수. 진행도는 진화 = (액티브 Lv/5, 패시브 보유), 합체 = (각 재료 Lv/3).

#### A4. 조합 카드에 "이 조합으로 막히는 조합" 경고
- **무엇:** 조합 카드는 이미 "사라질 재료"를 보여준다. 여기에 **그 재료가 발견한 다른 레시피의 재료이기도 하면** 한 줄을 더한다.
  - 예: "⚠ 불꽃 장판이 사라져서 이번 판엔 **불꽃 발자국**(진화)을 못 만들어요"
  - **발견하지 않은 레시피는 말하지 않는다.** (스포일러 방지)
  - 진화 카드도 같다: 진화하면 원래 액티브가 바뀌어서, 그 액티브가 재료인 합체를 못 한다.
- **왜:** BALL x PIT는 재료가 겹치는 레시피가 "no warning and no undo"로 사라져서 불만을 샀다. 우리는 "재료는 그 판에서 다시 안 나온다" + "조합 결과는 재료가 안 된다" 규칙 때문에 **한 번 고르면 그 판에선 되돌릴 수 없다.** v0.3 목록은 재료가 많이 겹친다.
  - 불꽃 장판: 뇌화 폭풍(합체) · 불꽃 발자국(진화) · 아우스터리츠의 포화(나폴레옹 합체)
  - 낙뢰: 뇌화 폭풍 · 뇌신의 분노(진화) · 워든클리프 탑(테슬라 합체)
  - 얼음 파편: 서리 창(진화) · 눈보라(합체) / 부메랑: 쌍 부메랑(진화) · 천궁(합체)
- **고칠 문서:** [GameDesign 6-2 조합 카드](../GameDesign.md#조합-카드), [UX 3](../Design/UX.md#3-레벨업-카드).
- **구현 범위:** 조합 카드를 그릴 때 `RecipeBook`(발견 목록)에서 사라질 재료를 쓰는 다른 레시피를 찾아 `SkillCardView`에 한 줄. 이 판에서 이미 숨긴 레시피·이미 만든 결과는 빼고 찾는다.

#### A5. 결과 화면과 기록 저장 화면에 스킬별 피해 비율
- **무엇:**
  1. `DamageLog`를 **스킬 ID별**로 모은다. 기본 공격은 `basic`, 영웅 대표 효과(포병의 눈 포탄 등)는 영웅 ID, 조합은 조합 결과 ID로 센다.
  2. 판 결과 화면에 **상위 5개 막대 + "기본 공격 n%"**.
  3. 기록 저장 화면의 스킬 줄마다 **"피해 n%"** 작은 글씨. 패시브는 "—".
- **왜:** Archero 1 유저는 "실제 숫자를 보여 달라"고 했다. BALL x PIT는 판 끝 통계 화면을 패치로 계속 보강했다. 카제나 신규 유저는 "무엇을 남겨야 할지 이해하기 어렵다"고 했다 → 피해 비율이 **무엇을 기록에 남길지 고르는 근거**가 된다. 덤으로 [GameDesign 3-3](../GameDesign.md#3-3-멈춤의-가치-액티브가-강해져도-멈출-이유)의 "기본 공격 피해 40% 이상" 지표를 사람 테스트 중에 바로 볼 수 있다.
- **고칠 문서:** [Content 2-4 판이 끝나면](../Design/Content.md#2-4-판이-끝나면), [GameDesign 7-2 저장 화면 규칙](../GameDesign.md#저장-화면-규칙).
- **구현 범위:** `Combat/DamageInfo`에 `SourceId`(string) 필드, `Health`에서 `DamageLog.Record(info, dealt)`로 넘김, 각 공격·액티브가 `SourceId`를 채움. 기존 `BasicShare`는 그대로 유지(시나리오 벤치가 씀). `UI/RunResultScreen`, `UI/RecordSaveScreen`.

#### A6. HUD 진행 바: 방 칸 + 방 종류 아이콘
- **무엇:** "방 7/15" 글자 아래에 방 수만큼 작은 칸을 그린다. 지난 방은 채운 칸, 지금 방은 테두리.
  - 엘리트 방(5·10) ⭐, 보상 방(6·11) 🎁, 중간 보스(15) 👹 아이콘. 색만이 아니라 **아이콘으로** 구분.
  - 방 수와 종류는 `DungeonData`에서 읽는다(지금 v0.1 던전은 10방, 기획은 15방 — 하드코딩하지 않는다).
  - 보스맵에서는 숨긴다.
- **왜:** Archero 2는 진행 바에 Valkyrie(5번째 방)·Angel(9번째)·보스(10번째) 아이콘을 미리 보여준다. 플레이어가 "엘리트 전에 체력을 채울까" 계획할 수 있다. [Content 2-3](../Design/Content.md#2-3-레벨업-리듬)이 말하는 "5번, 10번 방이 기대되는 순간"을 화면으로 만든다.
- **고칠 문서:** [UX 2 HUD](../Design/UX.md#2-전투-화면-hud).
- **구현 범위:** `UI/HudView` + 방 종류를 알려주는 `Dungeon/DungeonRun`/`DungeonData` 조회. 칸 크기는 15칸이 1080폭 안에 들어가게.

#### A7. 판 도중 "지금 기록 용량" 보여주기
- **무엇:**
  1. 방을 깰 때마다 HUD 위쪽에 1.5초 동안 작은 알림: **"📜 기록 용량 16 / 27pt"** (지금 끝나면 / 클리어하면).
  2. 나중에 일시정지 창을 만들면 거기에도 같은 두 숫자를 보여준다. (지금 코드엔 일시정지 창이 없다)
- **왜:** 카제나에서 "상한까지 얼마 남았는지 보여 달라"(Reddit), "얻을 때 비용을 보여 달라"(STOVE)는 요청이 컸다. 우리 용량은 **깬 방 수에 비례**하므로, 이 숫자가 "한 방만 더" 하게 만드는 동기가 된다. 일부러 죽는 전략이 손해라는 것도 눈으로 보인다.
- **고칠 문서:** [GameDesign 7-2 기록 용량](../GameDesign.md#7-2-기록-용량-카제나-방식에서-착안), [UX 2](../Design/UX.md#2-전투-화면-hud).
- **구현 범위:** `Records/RecordCapacity`로 두 값 계산, `UI/HudView`에 알림 텍스트. 보스맵·서바이벌에서는 안 띄움(기록을 만들지 않으니까).

#### A8. 기록에 "강점" 자동 표시 + 보스 입장 화면에서 맞는 기록 강조
- **무엇:**
  1. 스킬 데이터에 **역할 1개**를 더한다: `단일`(🏹) / `광역`(⚡) / `방어`(🛡️) / `없음`. 기존 태그와 별개. [제안] 나누기:
     - 🏹 단일: 공격속도, 멀티샷, 흡혈, 관통, 벽 튕김, 치명타 확률·피해, 영웅의 기세
     - ⚡ 광역: 낙뢰, 불꽃 장판, 화살비, 유성, 독 구름, 회오리, 얼음 파편, 부메랑, 영웅 전용 액티브 4개, 광역 조합들
     - 🛡️ 방어: 회전 칼날, 강철 회오리, 방어력, 최대체력, 수호 방패, 철벽, 회피, 체력 재생, 불굴
     - 없음: 공격력, 이동속도, 배움, 쿨다운 감소, 기술 숙련
  2. 기록의 강점 = 역할별 **저장 비용(pt) 합**이 가장 큰 것. 기록 카드에 아이콘 3개 비율 막대(예: 🏹60 ⚡30 🛡️10).
  3. 기록 이름을 자동으로 만든다(저장 안 해도 됨, 볼 때 계산): **"칭기즈 칸 · 연사형"** / "· 광역형" / "· 탱커형" ([6-8 빌드 유형](../GameDesign.md#6-8-v01-스킬-목록-확정) 이름 그대로).
  4. `BossData`에 **추천 역할**을 넣는다. [제안] 수문장 = 🏹(멈춤 타이밍), 망령 군주 = ⚡, 강철 거상 = 🛡️, 그림자 쌍둥이 = 🏹, 망각의 왕 = 없음. 보스 입장 화면에서 강점이 같은 기록에 **"👍 추천"** 배지. 고르는 건 플레이어 자유.
- **왜:** 기록 시스템의 성공 기준은 "**보스에 따라 실제로 다른 기록을 고르는가**"([GameDesign 7-4](../GameDesign.md#7-4-보스맵))다. 기록 3개가 다 "스킬 목록"으로만 보이면 고르는 이유가 안 보인다. 카제나는 신규 유저가 이해하기 어려워해서 결국 프리셋을 줬다. Archero 2는 "프리셋 더 달라"는 요청이 많다. Capybara Go!는 보스가 "스탯 체크"라는 비판을 받았다 — 우리는 상성이 보여야 한다. 색만으로 구분하지 않고 아이콘을 쓴다(GAG).
- **고칠 문서:** [Skills 표](../Design/Skills.md)(역할 열), [GameDesign 7-4](../GameDesign.md#7-4-보스맵), [Modes 2-9 보스맵](../Design/Modes.md#2-9-보스맵), [UX 1 기록 탭](../Design/UX.md#1-로비-구조-아래쪽-탭-5개).
- **구현 범위:** `Skills/Data/SkillData`에 `SkillRole role`, `SceneBuilder`에서 값 채움, `Modes/BossData.recommendedRole`, `UI/RecordArchivePanel`·`UI/BossIntroPanel`에 막대·배지. **이름 바꾸기·잠금은 넣지 않는다**(B5).

#### A9. 손맛 기본 세트: 피해 숫자 · 히트스톱 · 화면 흔들림 (+ 설정)
- **무엇:** 지금 코드에는 피격 흰색 깜빡임(`HitFlash`)만 있다. 아래 셋을 더한다. 숫자는 모두 **[제안] 시작값**.
  1. **피해 숫자:** 오브젝트 풀링([8-5](../GameDesign.md#8-5-기술-주의점-휴대폰-성능)). 같은 적에게 **0.15초 안에** 들어온 피해는 숫자 하나로 합친다. 치명타는 1.4배 크기 + 노란색. 기본 공격은 흰색, 액티브는 하늘색(멈춰서 쏜 피해가 눈에 띄게). 화면에 동시에 최대 40개. 0.6초 동안 위로 뜨며 사라짐.
  2. **히트스톱(잠깐 멈춤):** 매 타격엔 **안 한다**(기본 공격이 초당 여러 발이라 끊겨 보임 → 흰색 깜빡임만). 엘리트 처치 **0.08초**, 보스 단계 전환 **0.1초**, 보스 처치 **0.2초 + 0.5초 동안 0.3배 느리게**. (Vlambeer 약 20ms, IEICE 연구 0.1~0.2초, 사쿠라이 "큰 타격일수록 길게"에서 잡은 값)
  3. **화면 흔들림:** 트라우마 방식(Eiserloh). 트라우마 0~1, 흔들림 크기 = 트라우마², 초당 1.5씩 줄어듦. 플레이어 피격 +0.3, 엘리트 처치 +0.4, 보스 강타·처치 +0.5. 최대 이동 0.3 유닛, 최대 회전 2°. 노이즈는 `Mathf.PerlinNoise`.
  4. **설정 3개:** 화면 흔들림 끄기/약하게(50%)/보통, 피해 숫자 켜기/끄기, 번쩍임 줄이기(화면 전체 번쩍임을 1초 3번 이하로, WCAG 2.3.1 — 화살비처럼 화면 전체 효과가 있는 스킬 주의). 설정은 판 밖 저장.
  - **구현 주의:** `GamePause`가 `Time.timeScale = 0`을 요청자 수로 관리한다. 히트스톱은 이 체계 안에 넣어서 **멈춤(레벨업 창·결과 화면)이 항상 이기게** 한다. 예: `GamePause.RequestSlow(owner, scale, realSeconds)` → 멈춤 요청이 하나라도 있으면 0, 아니면 느리게 요청 중 가장 작은 값. 시간은 unscaled로 센다. 히트스톱 중에 레벨업 창이 뜨면 창이 닫힌 뒤 원래 속도로 돌아와야 한다.
- **왜:** 폰 테스트 반응이 "재미는 있는듯"이었다. 손맛은 콘텐츠를 늘리지 않고 재미를 올리는 가장 싼 방법이다. Vlambeer·Eiserloh 강연이 기준이고, VS·Soul Knight·Archero 2 모두 흔들림·숫자·이펙트를 줄이거나 끄는 옵션을 준다(XAG 117). [UX 5](../Design/UX.md#5-접근성편의)·[LiveOps 설정](../Design/LiveOps.md)에 "화면 흔들림 줄이기", "피해 숫자 표시"가 이미 적혀 있다 — 그 구현이다.
- **고칠 문서:** [UX 5 접근성](../Design/UX.md#5-접근성편의)에 설정 3개 확정, UX에 "6. 손맛 규칙" 표(위 숫자) 새로 추가.
- **구현 범위:** `Combat/DamageNumbers`(풀), `Common/GamePause` 확장 + `Common/HitStop`, `Common/CameraShake`(카메라에 붙음), 설정 저장. **v0.3 영웅 작업과 섞지 말고 별도 커밋 묶음**으로.

#### A10. 엘리트 확정 레벨업은 "희귀 이상 1장" 보장 (전설은 그대로)
- **무엇:** 5·10번 방 **엘리트 처치로 생긴 확정 레벨업**에서, 뽑은 3장에 **조합 카드도, 희귀·전설 카드도 없으면** 맨 오른쪽 일반 카드 1장을 희귀 후보 중 하나로 바꾼다.
  - 희귀 후보가 없으면 그대로 둔다.
  - **전설로는 절대 바꾸지 않는다.** 전설은 지금 추첨 확률 그대로([6-5](../GameDesign.md#6-5-전설-확률-처음부터-끝까지-귀하게) 원칙 유지).
  - 카드 화면 위에 "⭐ 엘리트 보상" 띠.
  - 엘리트 처치 경험치로 생긴 **보통 레벨업**에는 적용하지 않는다(확정 레벨업 1회만).
- **왜:** "정해진 순간의 좋은 선택"이 업계 표준이다: Archero 2는 5번째 방마다 Valkyrie(희귀~전설 2지선다), Capybara Go!는 엘리트 → Mythic 3중 1, Brotato는 레벨 5·10에 등급 확정, Survivor.io는 엘리트 상자. [Content 2-3](../Design/Content.md#2-3-레벨업-리듬)의 "5번, 10번 방이 기대되는 순간"을 숫자로 보장한다.
- **고칠 문서:** [GameDesign 6-4](../GameDesign.md#6-4-레벨업-카드-3장-뽑기-규칙)에 "엘리트 보장" 단계 추가, [Content 2-2](../Design/Content.md#2-2-던전-한-판--방-15개) 엘리트 설명.
- **구현 범위:** `Skills/Runtime/SkillManager`(레벨업 원인을 받는 인자), `SkillPicker`, 엘리트 처치 쪽에서 원인 전달. EditMode 테스트: 희귀 후보 있음/없음, 조합 카드 있음, 전설이 이미 있음.
- **주의:** 밸런스 변경이다. 희귀 비용(레벨당 2~3pt)이 약간 늘어 [7-2](../GameDesign.md#7-2-기록-용량-카제나-방식에서-착안) 기준값 34pt를 다음 플레이 기록으로 다시 확인한다. **개발자 OK 후 반영.**

---

### (B) 나중 후보 (Ideas.md로 옮길 것)

| # | 아이디어 | 근거 | 맞는 버전 [제안] |
|---|---|---|---|
| B1 | **광고 부활 판당 1회**: 체력 50% + 화면의 적 탄 제거·주변 적 밀어내기, 결정 시간 8초(Archero는 5초), 보석 부활 없음. 부활해도 ★2 피격 수는 그대로 셈, 기록 용량은 깬 방 기준 그대로 | Archero 1·2, Survivor.io, Soul Knight, VS, Magic Survival 모두 있음. 맥락에 맞는 광고 자리가 참여율이 높음(Unity 38.1% vs 23.8%) | 출시 이후 검토 항목 → 구체안으로 |
| B2 | **엘리트 무피격 → 거래 카드**: 엘리트를 한 대도 안 맞고 잡으면 "최대체력 -15% → 희귀 스킬 1개 더" (거절 가능, 전설 없음) | Archero Devil(보스 무피격 100% 등장, HP 20%), Archero 2·Capybara Demon | v0.4 이후 |
| B3 | **일반 카드 숨기기(밴) 판당 2회**, 무료 | VS Banish, BALL x PIT 전투당 2회. v0.3에서 스킬 46개가 되면 원하는 빌드로 가기 어려워질 수 있음 | v0.3 플레이 후 판단 |
| B4 | **보스 주간 점수전**: 기록으로 보스와 2분 싸워 피해량 순위, 하루 2회, 보스 주마다 교체 | Archero 2 Seal Battle(2분, 하루 2회, 7일 교체, 28일 시즌). 기록만으로 싸우는 우리 보스맵과 딱 맞음. 일일 도전·무한 모드와 겹치는지 검토 | 출시 이후 |
| B5 | **기록 편의**: 이름 바꾸기(16자), 🔒 잠금, 판 끝 "임시 보관 1칸"(24시간), 덮어쓸 때 전/후 비교 | Inscryption 이름 16자, 카제나 "보유 한도 빡빡" 인정·즐겨찾기 | v1.0 전 |
| B6 | **기록 부분 갱신**: 새 판의 스킬 1개를 옛 기록에 바꿔 넣기 (용량 규칙은 그대로) | 카제나 시즌3 '블랙 매터' 편집. "도자기 빚기" 반복 피로 줄이기 | 출시 이후 |
| B7 | **친구 기록 빌리기 보강**: 빌려준 사람도 보상, 친구가 없으면 NPC(개발자가 만든) 기록, 빌린 기록은 복사 금지 | Dragon's Dogma 2(주인 보상, 가짜 고용), SAP(AI 팀), 우마무스메(첫 대여 무료) | v1.0 ([Social](../Design/Social.md)) |
| B8 | **영웅 체험**: 잠긴 영웅으로 튜토리얼 던전 1판 (하트·보상·기록 없음) | Archero 1 Hero Trial Mode | v1.0 |
| B9 | **★3 딴 구간 2배속** | Archero 2 깬 챕터 2~3배속 | 출시 이후 |
| B10 | **서바이벌(v0.5) 기획 메모**: 보스 등장 2~3초 전 경고 배너 → 일반 몹 정리 + 움직이지 않는 링, 엘리트·보스 상자는 "가능하면 조합 보장" | Survivor.io 보스 케이지·상자. VS의 "10분 뒤 상자" 조건은 우리 판 길이에 안 맞음 | v0.5 기획 때 |
| B11 | **카드 잠금(Lock) 1장 / 4번째 카드 확률**을 유물·룬 효과로 | Brotato Lock, VS Luck, BALL x PIT 건물. Galante "4장 넘기지 말 것" | v0.4 유물·룬 때 |
| B12 | **보스 단계 전환 때 회복 구슬** (레벨업 없는 보스전의 중간 보상) | Soul Knight 50% 때 에너지 구슬 10개 | v0.3 보스 추가 후 밸런스 보고 |
| B13 | **레벨업 카드에 기록 비용 표시**("기록 2pt/Lv") | 카제나 "얻을 때 비용을 보여 달라" | A7 반응을 보고 |
| B14 | **보조 모드**: 피해 감소 20%, 죽을 때마다 +2%, 최대 80% (별·랭킹엔 반영 안 함) | Hades God Mode, Celeste Assist | 출시 이후 |
| B15 | **방 태그별 카드 풀** (예: 물 있는 방에서만 나오는 카드) | Archero 2 Wall-Piercing/Water Walker | 특수 방이 생기면 |
| B16 | **(결정 필요) stutter step 허용?** 공격 후딜을 움직여서 끊으면 연사가 빨라지는가. 칭기즈 칸 대표 효과("멈추는 순간 준비 시간 0")와 겹치므로 **일부러 정한다** | Archero stutter step으로 DPS 20%+ | v0.3 영웅 작업 중 확인 |

### (C) 피해야 할 것

| # | 피할 것 | 실제로 욕먹은 사례 |
|---|---|---|
| C1 | **판 안 힘을 돈으로 팔기** (리롤 횟수, 시작 스킬) | Archero 2 월정액 리롤 +1, Survivor.io 9,999젬 카드 새로고침 +1 |
| C2 | **영웅 뽑기·비싼 영웅** | Archero 2 "전설 영웅은 소환 이벤트로만", Survivor.io·Zombie Waves $30~$100 캐릭터 |
| C3 | **장비 등급·성장 레이어를 계속 추가 (파워 크립)** | Archero 1 ALE → Mythic → Titan → Chaos, Survivor.io SS 장비·새 펫 → 2026 과금 거부 운동 |
| C4 | **기록 용량 초과분을 무작위로 깎기** | 카제나 "세이브가 오염된다" |
| C5 | **메인 진행을 다른 모드로 막기** | Archero 2 "Sky Tower 75층 깨야 11장" |
| C6 | **예고가 짧은 공격, 화면을 못 읽는 탄막** — 모든 원거리 공격 예고는 최소 0.5초 (지금 적은 0.6~1초라 OK) | Archero 1 짧은 레이저, Archero 2 "화면이 탄막으로 꽉 참" |
| C7 | **광고 보상이 안 들어오는 버그, 광고 실패 뒤 강제 광고** — 보상은 "보상 획득" 콜백에서 1번, 실패면 재시도 안내 | Zombie Waves "다 봐도 보상 없음", Magic Survival "부활 광고 실패 → 30초 강제 광고" |
| C8 | **죽을 때마다 결제·광고 팝업, 카지노식 연출 과다, 빨간 알림 점 남발** | Capybara Go! "eyesore", Survivor.io·Archero 2 알림투성이 홈 화면 |
| C9 | **처음 준 무료 혜택을 나중에 줄이기** — 출시 때 약속은 작게 하고 지킨다 | Capybara Go! 주간 성장 이벤트 삭제(2025-06) |
| C10 | **레시피를 너무 많이 숨겨서 위키 없이는 못 하는 게임** — 힌트(A3)로 막는다 | Magic Survival 리뷰 |
| C11 | **한 판이 계속 길어지기** — 던전 8~10분을 지킨다 | Archero 2 중반 30~40분 불만 |
| C12 | **무작위 성장 (고를 수 없는 탤런트)** | Archero 1 탤런트 |

---

## 4. 출처 목록

> 본문에 쓴 링크를 게임별로 모았다. Reddit 글은 `https://www.reddit.com/r/<게시판>/comments/<id>/` 형식이다.
> 뺀 출처: archero2.wiki(일본어, 실제로는 Archero 1 내용), Theria Games의 Archero 2 에너지 숫자(Archero 1 숫자로 보임), mobilegamereport.com의 Capybara 기사(개발사를 잘못 적음).

**Archero 1**
- https://archero.fandom.com/wiki/Energy · https://archero.fandom.com/wiki/The_Devil · https://archero.fandom.com/wiki/Angels · https://archero.fandom.com/wiki/Lucky_Wheel · https://archero.fandom.com/wiki/Category:Abilities · https://archero.fandom.com/wiki/Verdant_Prairie · https://archero.fandom.com/wiki/Verdant_Prairie_Bosses · https://archero.fandom.com/wiki/Category:Heroes · https://archero.fandom.com/wiki/Helix · https://archero.fandom.com/wiki/Meowgik · https://archero.fandom.com/wiki/Talent · https://archero.fandom.com/wiki/Version_History · https://archero.fandom.com/wiki/Up-close_Dangers
- https://www.gamedeveloper.com/design/finding-the-fun-archero-part-1---gameplay · https://www.gamedeveloper.com/design/finding-the-fun-archero-part-2---progression · https://www.gamedeveloper.com/design/finding-the-fun-archero-part-3---monetization
- https://www.deconstructoroffun.com/blog/2019/8/9/why-archero-banked-25m-but-leaves-25m-hanging-hlx9n · https://www.blog.udonis.co/mobile-marketing/mobile-games/archero-advertising · https://www.gamerefinery.com/april-2020-product-update-feature-groups-expanded-13-new-ad-monetization-features-added/ · https://gameworldobserver.com/2025/01/20/archero-2-revenue-8-million-in-11-days-vs-original-game
- https://gamefaqs.gamespot.com/android/264554-archero/reviews/169090 · https://www.androidauthority.com/archero-cheats-tips-hacks-glitches-1087839/ · https://www.androidauthority.com/archero-guide-heroes-abilities-1086651/ · https://gameskeys.net/archero-the-ultimate-farming-guide/ · https://progameguides.com/archero/archero-beginners-guide/
- Reddit r/Archero: bykzs0, gcch5c, 11y5pm0, yrkghi, 1gziyjv, 1kos0qx, r3t01t, ejpdhh, inrr7j

**Archero 2**
- https://archero-2.game-vault.net/wiki/Campaign · https://archero-2.game-vault.net/wiki/Skills · https://archero-2.game-vault.net/wiki/Characters · https://archero-2.game-vault.net/wiki/Guide:Skills_Tier_List
- https://www.levelwinner.com/archero-2-beginners-guide/ · https://www.pocketgamer.com/archero-2/guide/ · https://www.pocketgamer.com/archero-2/out-now-on-ios-and-android · https://progameguides.com/archero-2/archero-2-beginners-guide/ · https://progameguides.com/archero-2/archero-2-skills-tier-list/ · https://roonby.com/2024/11/18/archero-2-guide-all-beginner-tips-to-get-you-started/
- https://someselectedstories.substack.com/p/tiny-teardown-archero-2-habby-games · https://www.androidauthority.com/archero-2-3517511/ · https://archero2hub.com/en/character · https://theriagames.com/guide/archero-2-chapter-1-moonlight-forest-guide/ · https://theriagames.com/guide/archero-2-types-of-monster-guide/ · https://grindnstrat.com/archero-2-drop-rates
- https://www.pocketgamer.biz/archero-2-makes-328m-in-first-30-days-from-player-spending/ · https://mobilegamer.biz/data-digest-savvys-moonton-swoop-liftoff-kills-ipo-habby-hits-2bn-fallout-shelter-more/ · https://play.google.com/store/apps/details?id=com.xq.archeroii
- Reddit r/ArcheroV2: 1ioou0k, 1iowu5w, 1n7cdmg, 1o310l6, 1iv9lc2, 1wkjgk3, 1vlvty6, 1mhqspt, 1lhk5r0, 1tmno1i, 1kzzi6b, 1pmmph1

**Survivor.io**
- https://survivorio.fandom.com/wiki/Weapon_Skill_Evolution_Guide · https://survivorio.fandom.com/wiki/Merging · https://survivorio.fandom.com/wiki/Tech_Parts · https://survivorio.fandom.com/wiki/Characters
- https://www.allclash.com/survivor-io-evo-skill-guide-tier-list/ · https://onechilledgamer.com/survivor-io-skill-guide/ · https://mturbogamer.com/2022/10/survivor-io-what-is-evo-weapon-evolutions/ · https://mturbogamer.com/2022/10/survivor-io-best-way-to-spend-gems/ · https://pocketgamer.io/evolution-chart-guide/ · https://writerparty.com/party/survivor-io-energy-guide/ · https://writerparty.com/party/survivor-io-reroll-cheat-how-to-re-roll-revive-and-refresh-for-free/ · https://writerparty.com/party/survivor-io-best-build-guide-and-skill-tier-list/ · https://gamesadda.in/gaming/survivor-io-get-energy/ · https://theclashify.com/survivor-io-wiki/ · https://simplegameguide.com/how-to-beat-chapter-1-survivor-io/ · https://whatifgaming.com/how-to-unlock-character-heroes-in-survivor-io/ · https://minireview.io/arcade/survivor-io
- https://www.pocketgamer.biz/how-innovation-and-iteration-has-transformed-survivorio/ · https://mobilegamer.biz/two-months-in-survivor-io-passes-75m-from-37m-downloads/ · https://mobilegamer.biz/the-design-secrets-powering-habbys-hit-machine/ · https://naavik.co/deep-dives/evolution-of-hybridcasual-deepdive/ · https://app2top.com/news/revenue-survivor-io-the-iap-has-exceeded-half-a-billion-dollars-265784.html · https://www.gamigion.com/habby-hits-2b-in-user-spending-and-350m-downloads/
- Reddit r/Survivorio·r/Survivor_io: 1c26onp, zdrsiw, 18lpq2v, wwn7mx, 13680ls, zm1mht, 1q8n7r7, 1qwhv0l, 18rqm4u

**Capybara Go!**
- https://capybara-go.game-vault.net/wiki/Skills · https://capybara-go.game-vault.net/wiki/Currency_and_Items · https://capybara-go.game-vault.net/wiki/Guide:Real_Money_Spending_P2W_Guide · https://en.namu.wiki/w/%EC%B9%B4%ED%94%BC%EB%B0%94%EB%9D%BC%20Go! · https://altema.jp/capybarago/stamina · https://grindnstrat.com/capybara-go-guide/ · https://note.com/gamestearoom/n/n1bdeda2d22b4?hl=en · https://www.niaogebiji.com/article-692620-1.html
- https://www.gamigion.com/capybara-go-global-launch-ua-case-study/ · https://www.gamigion.com/what-makes-capybara-go-successful/ · https://www.pocketgamer.biz/habbys-capybara-go-surpasses-100m-in-gross-player-spending/ · https://minireview.io/adventure/capybara-go · https://play.google.com/store/apps/details?id=com.habby.capybara · https://www.deconstructoroffun.com/blog/2025/7/31/habbys-hybridcasual-empire-the-template-that-built-a-powerhouse
- Reddit r/CapybaraGoGame: 1l9l033, 1hwnpv9, 1irf7rm

**Vampire Survivors**
- https://vampire.survivors.wiki/w/Evolution · https://vampire.survivors.wiki/w/Reroll · https://vampire.survivors.wiki/w/Skip · https://vampire.survivors.wiki/w/Banish · https://vampire.survivors.wiki/w/Seal · https://vampire.survivors.wiki/w/Level_up · https://vampire.survivors.wiki/w/Weapons · https://vampire.survivors.wiki/w/Grimoire · https://vampire.survivors.wiki/w/Collection · https://vampire.survivors.wiki/w/Options · https://vampire.survivors.wiki/w/The_Reaper
- https://www.pocketgamer.biz/vampire-survivors-surprise-launches-to-mobile/ · https://toucharcade.com/2022/12/09/vampire-survivors-mobile-review-controller-cloud-save-sync-dlc-unlock-iphone-ipad-pro/ · https://toucharcade.com/2023/03/14/vampire-survivors-legacy-of-the-moonspell-dlc-mobile-download-release-date-price-iphone-android-ios-poncle/ · https://www.androidpolice.com/vampire-survivors-developer-interview/ · https://metro.co.uk/2024/11/01/vampire-survivors-ode-castlevania-interview-i-put-everything-21906264/

**Magic Survival**
- https://en.namu.wiki/w/%EB%A7%A4%EC%A7%81%EC%84%9C%EB%B0%94%EC%9D%B4%EB%B2%8C · https://play.google.com/store/apps/details?id=com.vkslrzm.Zombie&hl=en_US · https://playstoreradar.com/game/com.vkslrzm.Zombie · https://ms-secrets.notion.site/FAQ-1c99ac8db3b54a119bb819314346e160
- Reddit r/magic_survival: 1cr6ny3, 192l9i3, 1owodq3

**Brotato**
- https://brotato.wiki.spellsandguns.com/Waves · https://brotato.wiki.spellsandguns.com/Upgrades · https://brotato.wiki.spellsandguns.com/Shop · https://brotato.wiki.spellsandguns.com/Weapons · https://brotato.wiki.spellsandguns.com/Weapon_Classes · https://play.google.com/store/search?q=brotato&c=apps · https://apps.apple.com/us/app/brotato/id6445884925 · https://play.google.com/store/apps/details?id=com.brotato.shooting.survivors.action.roguelike&hl=en

**BALL x PIT**
- https://ballxpit.wiki.gg/wiki/Fusion_Reactor · https://ballxpit.wiki.gg/wiki/Balls · https://ballxpit.wiki.gg/wiki/Game_Updates · https://www.ballxpitguide.com/buildings · https://gamefaqs.gamespot.com/pc/539487-ball-x-pit/faqs/82305 · https://playerecho.com/ball-x-pit/fusion-and-evolution · https://www.polygon.com/ball-x-pit-review/ · https://www.spottis.com/games/ball-x-pit-controls/ · https://www.gematsu.com/2026/03/ball-x-pit-now-available-for-ios-android
- Reddit: r/BALLxPIT 1rrvqso, r/AndroidGaming 1rruaq3

**Soul Knight**
- https://soul-knight.fandom.com/wiki/Playing_the_Game · https://soul-knight.fandom.com/wiki/Game_Mechanics · https://soul-knight.fandom.com/wiki/Buffs · https://soul-knight.fandom.com/wiki/Category:Mergeable_Weapons · https://soul-knight.fandom.com/wiki/Advanced_Tutorial · https://soul-knight.fandom.com/wiki/Bosses · https://soul-knight.fandom.com/wiki/Dong_Zhuo · https://soul-knight.fandom.com/wiki/Update_6.0.0 · https://soul-knight.fandom.com/wiki/Controls · https://soul-knight.fandom.com/wiki/Pause_Menu · https://soul-knight.fandom.com/wiki/HUD · https://soul-knight.fandom.com/wiki/Fridge · https://soul-knight.fandom.com/wiki/Characters · https://soul-knight.fandom.com/wiki/Forge_Table
- https://www.chillyroom.com/en/soul-knight/three-kingdoms · https://toucharcade.com/2017/03/10/soul-knight-review · https://www.metacritic.com/game/soul-knight-2017/user-reviews/ · https://play.google.com/store/apps/details?id=com.ChillyRoom.DungeonShooter&hl=en
- Reddit: r/iosgaming 5upqbh, r/SoulKnight q3vrd2, 1j7z6zf

**Zombie Waves · 시장 자료**
- https://play.google.com/store/apps/details?id=com.ddup.zombiewaves.zw&hl=en · https://shylian.tistory.com/1751 · https://www.iosgames.net/Zombie-Waves-shooting-game/index.html
- https://sensortower.com/blog/2025-q2-unified-top-5-shoot'em%20up%20games-revenue-us-6019ed4d241bc16eb8623896 · https://appmagic.rocks/files/view/upload/Reports/EN_Monetization_Report_2025.pdf

**카제나 · 빌드 저장 사례**
- https://gamewith.net/chaoszeronightmare/70847 · https://gamewith.net/chaoszeronightmare/72851 · https://namu.wiki/w/%EC%B9%B4%EC%98%A4%EC%8A%A4%20%EC%A0%9C%EB%A1%9C%20%EB%82%98%EC%9D%B4%ED%8A%B8%EB%A9%94%EC%96%B4/%EC%B9%B4%EC%98%A4%EC%8A%A4 · https://www.reddit.com/r/ChaosZeroNightmare/comments/1ov0gwz/save_data_i_still_dont_get_it_why_so_complex/ · https://www.reddit.com/r/ChaosZeroNightmare/comments/1qvxoda/confirmed_information_with_new_save_data_rules/ · https://labs.invenglobal.com/articles/21028/the-time-to-return-is-now-chaos-zero-nightmare-season-3-brings-massive-changes-to-reduce-fatigue · https://byline.network/2026/10/16-731/ · https://inews24.com/view/2015536 · https://www.gamechosun.co.kr/webzine/article/view.php?no=220042 · https://zdnet.co.kr/view/?no=20250825223531 · https://www.greened.kr/news/articleView.html?idxno=332495 · https://page.onstove.com/chaoszeronightmare/kr/view/11194140 · https://bbs.ruliweb.com/community/board/300143/read/72760150
- https://mewgenics.wiki.gg/wiki/House_Bosses · https://inscryption.fandom.com/wiki/Deathcard · https://www.siliconera.com/legacy-umamusume-daily-borrows-nearly-double-worldwide/ · https://www.goonhammer.com/understanding-team-trials-in-umamusume-pretty-derby · https://superautopets.wiki.gg/wiki/The_Basics · https://steamcommunity.com/app/2427700/discussions/0/4290313152626154575 · https://pcgamer.com/games/rpg/dragons-dogma-2-pawns · https://automaton-media.com/en/news/dragons-dogma-2-has-fake-players-that-hire-less-desirable-pawns/

**손맛 · 온보딩 · 광고 · 하트 · 접근성**
- https://www.youtube.com/watch?v=AJdEqssNZ-U (The Art of Screenshake) · https://www.youtube.com/watch?v=Fy0aCDmgnxg (Juice it or lose it) · https://sourcegaming.info/2015/11/11/thoughts-on-hitstop-sakurais-famitsu-column-vol-490-1/ · https://ken.ieice.org/ken/paper/20230315OCSt/eng/ · http://www.mathforgameprogrammers.com/gdc2016/GDC2016_Eiserloh_Squirrel_JuicingYourCameras.pdf
- https://gameanalytics.com/blog/tips-for-a-great-first-time-user-experience-ftue-in-f2p-games · https://gamedevreports.substack.com/p/gameanalytics-mobile-gaming-benchmarks · https://gamedevreports.substack.com/p/gameanalytics-mobile-and-pc-game · https://gameworldobserver.com/2023/07/25/voodoo-hybrid-games-d7-retention-games-and-names-podcast
- https://support.google.com/admob/answer/7313578 · https://support.google.com/admob/answer/6201362 · https://support.google.com/googleplay/android-developer/answer/9857753 · https://docs.unity.com/grow/levelplay/sdk/react/rewarded-ads-integration · https://www.pocketgamer.biz/unity-global-rewarded-ad-engagement-rose-by-32-in-2023/ · https://www.gamedeveloper.com/business/best-practices-in-game-ads · https://www.pocketgamer.biz/tapjoy-modern-mobile-gamer-report/
- https://mobilefreetoplay.com/understanding-and-eliminating-energy-systems/ · https://www.gamedeveloper.com/design/core-players-energy-mechanics-they-don-t-mix · https://www.gamedeveloper.com/design/energy-systems-are-back-clash-royale
- https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/117 · https://gameaccessibilityguidelines.com/full-list/ · https://www.w3.org/WAI/WCAG22/Understanding/three-flashes-or-below-threshold.html · https://inverse.com/gaming/hades-god-mode-interview · https://gameaccessibilityguidelines.com/celeste-assist-mode
