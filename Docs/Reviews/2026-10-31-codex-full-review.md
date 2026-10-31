`feature/art-3d` · `472d9f5` 기준 읽기 전용 검토입니다. 이전 리뷰에서 수정된 항목은 제외했습니다. Unity 실행·EditMode·실기기 측정은 하지 않았으며, 화면 겹침은 코드상 위험으로 표시했습니다. 파일 변경 없음.

| 순위 | 범주 | 경로:줄 | 문제 | 제안 | 크기 | 심각도 |
|---:|---|---|---|---|:---:|:---:|
| 1 | 보상·저장 | `Game/Assets/Scripts/Dungeon/DungeonRun.cs:1061` · `Game/Assets/Scripts/UI/RecordSaveScreen.cs:196` | **튜토리얼 기록 미리보기마다 300골드 지급.** 화면 열기·±·고정·자동맞춤이 지급 콜백을 반복 호출한다. 저장 실패 롤백에서도 빠진다. | 미리보기는 데이터만 생성하고, 기록·튜토리얼 ID·보상을 단일 `Commit`에서 한 번 처리. | M | 🔴 |
| 2 | 이어하기 | `Game/Assets/Scripts/Player/PlayerController.cs:357` · `Game/Assets/Scripts/Records/SaveData.cs:386` | **불굴 사용 여부 미저장.** 부활 후 다음 체크포인트를 저장하고 재접속하면 같은 판에서 다시 부활 가능. | 런·균열 체크포인트에 사용 여부 저장·복원. | M | 🔴 |
| 3 | 미션 | `Game/Assets/Scripts/Common/GameSession.cs:457` | 보스·시련·일일 **연습 후 로비에도 `Practice=true` 유지**. 이후 레벨업·장착은 재화를 쓰면서 미션 진척이 누락된다. | 로비 복귀 시 연습 상태 해제하고 성장 미션 회귀 검사. | S | 🟡 |
| 4 | 일일 공정성 | `Game/Assets/Scripts/Skills/Runtime/SkillManager.cs:129` | 개인 영웅 `runs==0` 여부가 전용 카드 보정과 난수 소비를 바꾼다. 같은 날짜라도 계정별 카드열이 달라진다. | 일일 도전은 개인 첫판 보정 제외, 동일 난수 소비 보장. | S | 🟡 |
| 5 | 기록 복구 | `Game/Assets/Scripts/Dungeon/DungeonRun.cs:922` · `:983` | 종료 시 이어하기를 지우지만 **저장할 기록 후보는 메모리에만 존재**. 결과·기록 선택 중 앱 종료 시 빌드 소실. | 미확정 기록 후보를 영속화하고 저장·명시적 포기 때 제거. | M | 🟡 |
| 6 | 하트·입장 | `Game/Assets/Scripts/Common/GameSession.cs:356` · `:402` | 던전·서바이벌·일반 재도전은 하트를 먼저 저장한다. 특히 서바이벌 첫 30초 전에 종료하면 복구 스냅샷 없이 하트만 소비된다. | 시련에 적용한 하트+초기 스냅샷 단일 저장을 나머지 입장에도 적용. | M | 🟡 |
| 7 | 서바이벌 복구 | `Game/Assets/Scripts/Survival/SurvivalRun.cs:106` · `:139` | 스냅샷에 시드·카드 RNG 상태를 저장·복원하지 않는다. 강제 종료로 이후 카드 재추첨 가능. | 던전의 RNG 캡처·복원 경로 재사용. | S | 🟡 |
| 8 | 전투 규칙 | `Game/Assets/Scripts/Player/PlayerController.cs:998` | 아누비스 명중 콜백에는 정지·생존·세대 검사가 없다. 발사 후 이동해도 지연 명중으로 표식 누적·둔화가 발생한다. | 인간 영웅 명중 경로처럼 상태 검사 적용. 이동 후 명중 통합 검사. | S | 🟡 |
| 9 | 이동 초기화 | `Game/Assets/Scripts/Player/PlayerController.cs:1084` | 연속 베기는 다음 타격 순간의 정지만 확인한다. 간격 중 짧게 이동→재정지하면 취소돼야 할 후속 베기가 실행된다. | 공격 시작 세대를 보관하고 이동 시 남은 베기 취소. | S | 🟡 |
| 10 | 성장 스냅샷 | `Game/Assets/Scripts/Modes/BattleBootstrap.cs:159` | 이어하기의 장비는 당시 값이지만 **룬 가중치는 현재 계정 장착·레벨**로 만든다. 로비에서 바꾸면 진행 중 카드 분포 변경. | 런 시작 당시 룬 ID·레벨도 저장·복원. | M | 🟡 |
| 11 | 전투 성능 | `Game/Assets/Scripts/Enemy/EnemyStatus.cs:174` · `:229` | 독이 없어도 모든 적이 매 프레임 캡처 람다 생성. 피격 표시도 매번 `TryGetComponent`. 250마리에서 반복 비용 확대. | 역순 삭제로 캡처 제거, `HitFlash` 캐시. GC 실측. | S | 🟡 |
| 12 | 영웅 화면 성능 | `Game/Assets/Scripts/UI/HeroPortraits.cs:49` · `:170` | 최초에 모델·RT 21개 생성, 열 때마다 21회 렌더. 비선택 영웅도 `AlwaysAnimate`로 계속 평가된다. | 타일 정적 캐시·선택 영웅만 애니메이션, 필요 시 지연 생성. | M | 🟡 |
| 13 | 시련 안내 | `Game/Assets/Scripts/UI/BossIntroPanel.cs:239` | 주간 목표는 “1/2 기록”만 표시. 실제 **다른 ID+액티브 구성 차이 조건**이 숨겨져 같은 빌드로 두 번 이겨도 이유 없이 미달. | 입장 전 적격 여부·구성 조건, 승리 후 불인정 이유 표시. | M | 🟡 |
| 14 | 글자 배치 | `Game/Assets/Editor/SceneBuilder.Lobby.cs:398` · `:412` | 영웅 타일 이름과 초상 영역이 28px 중첩. 이름 하한16·상태줄70%인데 공통 줄 간격은26으로 증가한다. **축소·가림 위험, 화면 미확인.** | 이름·상태 전용 영역 확보, 초상과 분리하고 긴 이름 검사. | S | 🟡 |
| 15 | 시련 콘텐츠 | `Game/Assets/Scripts/UI/HeroVoice.cs:58` | 시련에도 경기장 번호로 일반 보스 대사 호출. 토르에게 수문장, 아누비스에게 망령 군주 대사가 나온다. | `GodTrialId`로 대사 선택하거나 일반 소개 생략. | S | 🟡 |
| 16 | 진행 안내 | `Game/Assets/Scripts/UI/RunResultScreen.cs:93` | 망각의 왕 첫 승리에 **“6구간이 열렸어요”** 표시. 실제 다음 콘텐츠는 신의 시련. | 최종 보스 완료 문구를 분리하고 시련 진입 안내. | S | 🟡 |
| 17 | 도감 일관성 | `Game/Assets/Scripts/UI/CodexPanel.cs:115` | 인물 설명은 플레이 영웅10명+제우스만 존재. 초기8명·잔·선덕·부디카 11명은 대응 설명이 없다. | 승인된 설명을 공통 데이터로 연결하고 로스터 누락 검사. | M | 🟡 |
| 18 | 저장 호환성 | `Game/Assets/Scripts/Records/SaveStore.cs:285` · `:239` | 미래 스키마를 손상으로 취급해 옛 백업·새 계정으로 진행한다. 후속 자동저장이 최신 본 파일을 교체한다. | 지원 불가 버전은 별도 상태로 반환하고 자동저장 차단·안내. | M | 🟡 |
| 19 | 처치 성능 | `Game/Assets/Scripts/Missions/MissionEvents.cs:81` | 처치마다 여러 이벤트가 날짜·주 문자열과 완료 목록을 반복 생성한다. 광역 처치 시 할당 집중. | 경계 계산 캐시, 완료 알림 지연 생성·버퍼 재사용. | M | 🟡 |
| 20 | 풀 초기화 | `Game/Assets/Scripts/Enemy/EnemyStatus.cs:69` | `Clear()`가 `_push`·`_poisonTimer`를 비우지 않는다. 재사용 개체에 이전 밀침·독 틱 시점이 넘어갈 수 있다. | 두 필드 초기화와 사망→즉시 재사용 검사. | S | 🟢 |
| 21 | 체험 복귀 | `Game/Assets/Scripts/UI/RunResultScreen.cs:109` | “영웅 보러 가기”도 일반 로비 복귀. 다시 열면 기존 선택 영웅이라 체험한 영웅을 21명 목록에서 재탐색해야 한다. | 체험 ID를 전달해 해당 영웅 화면으로 직행. | S | 🟢 |
| 22 | 경제 문서 | `Docs/Design/Economy.md:346` · `Tools/economy_21.py:218` | 9-0은 하루5개 확정인데 후속 설명·계산 출력은 “3개 현행·5개 제안”. 인계에도 결정 전 문구가 남는다. | 과거 비교와 현재 기준을 구분하고 TODO·인계·출력 동기화. | S | 🟢 |

확장 후보는 출시 전 v1.0 항목부터 진행하는 순서입니다.

- **상점·미션 첫 사용 안내** — ★★★ · **M**. 조작 튜토리얼 뒤 필요한 기능만 단계별 안내. `Docs/GameDesign.md:625`, `Docs/TODO.md:12`.
- **실제 리롤 광고 연결** — ★★★ · **L**. 성공·실패·취소·앱 복귀까지 기존 계약 연결. `Docs/GameDesign.md:628`.
- **A-5 긴 방** — ★★ · **M**. 기존 영웅·적을 재사용해 전투 공간 변화 추가. `Docs/GameDesign.md:640`.
- **첫 스킨 묶음** — ★★ · **M**, 출시 이후. `Docs/GameDesign.md:643`.

