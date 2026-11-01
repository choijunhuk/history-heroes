# 토르 시련·플레이 토르 인계 (2026-10-29)

가지: `feature/thor`. 이번 작업은 토르 시련 1개와 토르 1명만 작성했다. 커밋·Unity 실행·생성 에셋 변경은 하지 않았다.

## 정한 수치와 이유

- 시련 체력 **93,600**(120×780), 기본 공격력 **141**(60×2.35). 수문장 36,000보다 기록 화력을 요구하되 망각왕 108,000/162보다 낮다. 체력·공격에 같은 배수를 쓰지 않았다.
- 수문장의 강타 1번·직선 파동을 재사용하고 공격 뒤 **1.9초/1.8초** 빈틈. 실제 근접 1~2타·2~3분 전투 여부는 아직 측정하지 않았다.
- 토르 HP **1,250**, 공격 **115**, 공속 **0.85**, 사거리 **2.1**, 이동 **5**. 인간 전사 범위 안이며 추가 신 배율은 없다.
- 대표 효과 3타째 **40%/반경1.15**, ★3 반경1.4, ★5 피해50%. 액티브 8초·왕복 각65~105%, 진화 왕복 각80%+귀환점75%·반경0.8. 진화는 Lv1 고정·10pt다.

## 검증 증거와 한계

- 별도 Roslyn C# 컴파일 **PASS**: Runtime 258파일·Editor 37파일·EditMode 37파일, 오류 0. Runtime 경고 649개(직렬화 필드 경고 등이 포함), Editor/Tests 경고0.
- 명령: `python3 /tmp/history-heroes-thor-check/compile.py`. Unity 6000.3.27f1 설치본의 DLL을 참조했다. 프로젝트의 Library/Temp/Logs/UserSettings는 사용하지 않았다.
- 로그·응답 파일·소스 SHA256: `/tmp/history-heroes-thor-check/`. 이 경로는 로컬 임시 증거이며 저장소에 포함되지 않는다.
- 변경한 소스·문서·메타의 `git diff --check` **PASS**. 독립 코드 검토에서 발견한 주간 검사 기대값·열린 화면의 주 경계 갱신은 수정했다.
- **NOT_RUN**: Unity Editor 컴파일·NUnit 실행·실제 화면/입력·시련 밸런스·저사양 기기 성능. C# 별도 컴파일을 Editor 테스트 통과로 해석하지 않는다.
- 새 파일의 `.meta`를 함께 작성했다. 작업 시작 때 이미 보이던 FBX/OGG/TTF의 LFS 상태는 수정하지 않았다.

## 리드의 확인 순서

1. Play를 멈춘 상태에서 Unity 컴파일.
2. `HistoryHeroes.EditorTools.Art3DCharacters.BuildAll()` 다음 `HistoryHeroes.EditorTools.SceneBuilder.BuildAll()` 실행. 토르 모델·스킬·시련 데이터와 로비/전투 연결을 생성한다.
3. 전체 EditMode 실행. 신규 `GodTrialRulesTests`, `GodTrialSessionTests`, `ThorCombatTests`와 기존 영웅 명단 검사 포함.
4. 본인 망각왕 미격파/격파, 번호·임시·잠금 기록, 무료 연습, 첫10/주간5/30소비 해금, 보스 합산 반복3, 패배환급5, 월05시, 앱 종료 복구를 화면에서 확인.
5. 토르/인간 같은 빌드 비교, 매 공격 뒤 근접1~2타, 망치 왕복/귀환점 낙뢰, 3분 체험, 가로 스크롤·선택 영웅 노출·3D 초상을 확인.

## 파일별 변경 내용

- `Docs/Design/Economy.md` — 첫/반복/주간 조각 보상·공유 상한·해금 전 제외.
- `Docs/Design/GodsVsHumanity.md` — 토르 1차 상태·시련 수치별 이유·주간/저장 규칙·미확인 조건.
- `Docs/Design/Heroes.md` — 토르 12번째 영웅·성장·대표 효과·외형·가로 선택 줄.
- `Docs/Design/Modes.md` — 토르 시련의 기록·입장·별 제외·주간 목표·복구.
- `Docs/Design/Skills.md` — 귀환 망치·천둥의 귀환의 수치·재료·고정 비용.
- `Docs/Design/Story.md` — 시련 2줄 소개·7상황 대사·신화/각색 구분.
- `Docs/Design/Systems.md` — 토르 1차 코드 작성과 Unity 확인 전 상태 구분.
- `Docs/Dev/Thor-Handoff.md` — 수치·검증 증거·리드 실행 순서·파일별 변경 내용.
- `Docs/GameDesign.md` — v4.33 변경 이력·이번 범위와 영웅 목록.
- `Docs/TODO.md` — 작성 완료한 코드와 리드의 Unity 확인 작업 구분.
- `Game/Assets/Editor/Art3DCharacters.cs` — 토르 조립 외형과 시련의 공유 모델·크기 연결.
- `Game/Assets/Editor/SceneBuilder.Bosses.cs` — 별도 토르 시련 데이터·수문장 패턴 프리팹 생성 코드.
- `Game/Assets/Editor/SceneBuilder.Data.cs` — 12번째 영웅·전용 액티브·고정 레벨 진화와 레시피 등록.
- `Game/Assets/Editor/SceneBuilder.Lobby.cs` — 보스 두 탭·주간 목표 단추·150폭 영웅 가로 스크롤 생성.
- `Game/Assets/Editor/SceneBuilder.Missions.cs` — 미션용 영웅 순서에 토르 추가, 기존 순서 유지.
- `Game/Assets/Editor/SceneBuilder.Prefabs.cs` — 왕복 망치·귀환 낙뢰 효과 프리팹 생성 코드.
- `Game/Assets/Editor/SceneBuilder.Scene.cs` — 기존 5보스 목록과 별도로 시련 데이터를 전투에 연결.
- `Game/Assets/Editor/ThorDefinition.cs` — 인간 전사 범위의 능력치·★3/★5·희귀 비용·고정 진화 수치.
- `Game/Assets/Editor/ThorDefinition.cs.meta` — 새 자산 식별자, 대응 C# 파일과 함께 보관.
- `Game/Assets/Scripts/Common/GameSession.cs` — 정식/연습 입장·하트 1 소비·재도전·시련 ID 복구·정적 초기화.
- `Game/Assets/Scripts/Economy/Hearts.cs` — 시련에서 첫날 무료 판을 적용하지 않는 선택 인수, 기존 호출 기본값 유지.
- `Game/Assets/Scripts/Economy/Wallet.cs` — 미해금 신의 일반 조각 보상 차단.
- `Game/Assets/Scripts/Enemy/Bosses/RiftWardenBoss.cs` — 기존 강타·파동을 쓰는 토르 설정, 원형 보스는 유지.
- `Game/Assets/Scripts/Heroes/HeroData.cs` — 토르 대표 효과의 조정값 필드.
- `Game/Assets/Scripts/Heroes/HeroRoster.cs` — 신 조각 해금 판정·보석/패키지 경로와 10판 선택 제외.
- `Game/Assets/Scripts/Heroes/HeroSignature.cs` — 토르 ★3 반경·★5 피해와 안내 문구.
- `Game/Assets/Scripts/Heroes/ThorThunderState.cs` — 정지 기본 공격 3회 누적·이동 초기화 상태.
- `Game/Assets/Scripts/Heroes/ThorThunderState.cs.meta` — 새 자산 식별자, 대응 C# 파일과 함께 보관.
- `Game/Assets/Scripts/Meta/HeroGrowth.cs` — 잠긴 신의 조각 구매·레벨·각성 차단, 해금 후 기존 성장.
- `Game/Assets/Scripts/Missions/MissionTable.cs` — 12명 기본 영웅 순서, 기존 11명 유지.
- `Game/Assets/Scripts/Modes/BattleBootstrap.cs` — 시련에서 고른 기록·성장만 복원하고 다른 기록으로 대체하지 않음.
- `Game/Assets/Scripts/Modes/BossRun.cs` — 시련 판 선택·이어하기·전용 결과/보상, 일반 보스 별·유물 보상 분리.
- `Game/Assets/Scripts/Modes/DailyRules.cs` — 공정 일일 영웅 순환에서 신 제외·잠긴 신 조각 보상 방어.
- `Game/Assets/Scripts/Modes/GodTrialResult.cs` — 실제 지급 결과·첫 격파·환급과 화면 문구 전달.
- `Game/Assets/Scripts/Modes/GodTrialResult.cs.meta` — 새 자산 식별자, 대응 C# 파일과 함께 보관.
- `Game/Assets/Scripts/Modes/GodTrialRules.cs` — 시련 해금·첫 10조각·공유 반복 3회·환급 5회·주간 5조각·30 소비 해금.
- `Game/Assets/Scripts/Modes/GodTrialRules.cs.meta` — 새 자산 식별자, 대응 C# 파일과 함께 보관.
- `Game/Assets/Scripts/Player/HeroSignatureHud.cs` — 토르의 세 번 공격 준비 표시.
- `Game/Assets/Scripts/Player/PlayerController.cs` — 실제 기본 공격에서 한 번만 횟수 누적·작은 충격파, 이동/방 전환 초기화.
- `Game/Assets/Scripts/Records/SaveData.cs` — 구조 버전 9·시련 표식·주간 승리 스냅샷·입장 ID·미해금 30상한.
- `Game/Assets/Scripts/Records/SaveStore.cs` — 8→9 이관과 새 목록의 빈 값 보정, 이전 기록·성장 보존.
- `Game/Assets/Scripts/Skills/Data/ActiveSkillData.cs` — 귀환 낙뢰 피해 계수 필드.
- `Game/Assets/Scripts/Skills/Effects/BoomerangMover.cs` — 기존 왕복 이동에 선택 모델·귀환 콜백만 추가해 재사용.
- `Game/Assets/Scripts/Skills/Effects/ThorHammerBehaviour.cs` — 8초 왕복 두 타격·진화 귀환점 낙뢰, 기존 효과 풀 사용.
- `Game/Assets/Scripts/Skills/Effects/ThorHammerBehaviour.cs.meta` — 새 자산 식별자, 대응 C# 파일과 함께 보관.
- `Game/Assets/Scripts/Story/GodTrialIntro.cs` — 클리오 지정 대사·토르 창작 대사 2줄.
- `Game/Assets/Scripts/Story/GodTrialIntro.cs.meta` — 새 자산 식별자, 대응 C# 파일과 함께 보관.
- `Game/Assets/Scripts/Story/StoryLines.cs` — 토르 상황별 창작 대사 7종.
- `Game/Assets/Scripts/UI/BossIntroPanel.cs` — 두 탭·첫 격파 표식·주간 목표·주 경계 갱신·건너뛰기 소개.
- `Game/Assets/Scripts/UI/CodexPanel.cs` — 토르 신화·각색 표시와 인류 동행 이유.
- `Game/Assets/Scripts/UI/HeroPanel.cs` — 토르 조각 해금·선택 영웅 자동 스크롤.
- `Game/Assets/Scripts/UI/HeroPortraits.cs` — 작은 3D 초상에서 가로 목록 끌기 입력 전달.
- `Game/Assets/Scripts/UI/PauseMenu.cs` — 시련 포기·미격파 환급 안내.
- `Game/Assets/Scripts/UI/RunResultScreen.cs` — 시련 통과·첫 격파 문구, 다음 구간/별 안내와 분리.
- `Game/Assets/Tests/EditMode/BoudicaTests.cs` — 기존 11명 순서·여성 3명 검사를 보존하며 토르 1명 추가.
- `Game/Assets/Tests/EditMode/GodTrialRulesTests.cs` — 보상·주간 경계·다른 기록·해금·제외·저장 이관 검사.
- `Game/Assets/Tests/EditMode/GodTrialRulesTests.cs.meta` — 새 자산 식별자, 대응 C# 파일과 함께 보관.
- `Game/Assets/Tests/EditMode/GodTrialSessionTests.cs` — 장면 없이 입장·하트·임시/잠금 기록·성장 복원·토르 체험 검사.
- `Game/Assets/Tests/EditMode/GodTrialSessionTests.cs.meta` — 새 자산 식별자, 대응 C# 파일과 함께 보관.
- `Game/Assets/Tests/EditMode/JeanneTests.cs` — 기존 명단 검사를 보존하며 12명으로 확장.
- `Game/Assets/Tests/EditMode/SeondeokTests.cs` — 기존 명단 검사를 보존하며 12명으로 확장.
- `Game/Assets/Tests/EditMode/ThorCombatTests.cs` — 대표 효과 상태·이동 초기화·별 강화·전용 레시피·대사 검사.
- `Game/Assets/Tests/EditMode/ThorCombatTests.cs.meta` — 새 자산 식별자, 대응 C# 파일과 함께 보관.

## 리드 확인 결과 (2026-10-29, 맥북)

- Unity 컴파일 ✅ · `Art3DCharacters.BuildAll()` → `SceneBuilder.BuildAll()` ✅ · EditMode **457/457 통과**.
- 영웅 화면: 토르 (붉은 금발 · 수염 · 구릿빛 어깨 · 손에 `Thor_Hammer`), "토르 조각 0/30 · 신의 시련에서 획득", [토르 조각으로 해금 0/30]. 영웅 줄 가로 스크롤 · 고른 영웅 자동 노출 ✅.
- 보스 화면 두 탭 ✅. 고친 것: 탭이 생기며 "싸울 기록을 고르세요"가 1번 칸에 가려짐 → 칸을 내림. 잠긴 시련의 [잠긴 시련] 단추가 어두운 초록 → 회색.
- 시련 연습 전투: 거대 토르 + 망치, 상단 "신의 시련 토르 (연습)" ✅. 밸런스(2~3분 · 근접 1~2타)는 데스크톱 벤치에서.
- 아직: 정식 도전 보상 · 주간 목표 · 30개 해금의 화면 흐름 (테스트로는 통과), 폰 확인.
