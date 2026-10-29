# 부처·아담 구현 검토와 인계

## 요약

승인된 Heroes 6-16·6-17 수치로 코드와 EditMode 검사를 작성했다. Unity 실행·장면/프리팹/데이터 에셋 생성·화면·균형 검증은 리드에게 남긴다. 커밋은 하지 않았다.

## 발견과 수정

- 오늘의 영웅 빌더의 19명 고정 목록을 실제 로스터 복사로 바꿨다. 순서를 뒤집어도 미션에 그대로 전달되는 검사 추가.
- 장치 외에도 기존 함정·보스가 플레이어를 직접 미는 경로를 연결해 아담의 이동 준비에서 강제 이동량을 뺐다. 장치 신고·기존 연결 경로 검사 추가.
- 독립 코드 검토 재검토: 위 두 MEDIUM 해결, 추가 확정 결함 없음. 아키텍처 재검토: CLEAR. LSP 도구는 제공되지 않아 미실행.

## 검증

- PASS: 설치된 Unity 6.3 참조 DLL과 Roslyn C# 9로 Runtime 281파일·Editor 46파일·EditMode 56파일 컴파일 오류 0.
- PASS: 기존 HEAD와 현재 Runtime 경고 수·종류 동일 662건 (직렬화 필드 652, 기존 API/멤버 경고 10). 새 경고 없음.
- PASS: Unity 엔진을 호출하지 않는 실제 NUnit 메서드 15건을 Mono에서 실행. 보호막 출처 3건, 액티브 간격/취소/반경/고정 방향 8건, 대표 효과/이동/회피 4건.
- PASS: 공백 검사, 신규 C# 13개의 .meta 형식·짝, 금지 경로 변경 없음, 새 TODO 없음.
- NOT_RUN: Unity Editor의 전체 EditMode·실제 물리·Play·폰·균형 검사. 현재 직렬화 로스터는 이전 19명 상태이며 생성 후 21명 검사를 실행해야 한다.
- 로그: `/private/tmp/buddha-adam-compile-jxfhjc43/`. 임시 경로이며 커밋 대상이 아니다.

## 단순화와 남은 위험

기존 회피 25% 상한을 재사용했다. 기존 보호막 API와 파괴 알림을 유지하고 부처 수명만 분리했다. 지연 회복·타격은 같은 시간/세대 취소 상태를 사용한다. 미션의 별도 명단은 로스터 복사로 대체했다.

앞으로 추가하는 강제 이동도 외부 이동량을 보고해야 한다. 현재 모든 기존 경로를 연결했지만 Update/FixedUpdate 물리 순서는 Unity에서 확인한다. 벽에 막힌 입력·컨베이어·보스 밀침·순간이동 직후, 두 번째 회복/타격 직전 방 이동·영웅 변경·사망을 특히 확인한다.

리드는 Play를 멈추고 컴파일한 뒤 `Art3DCharacters.BuildAll()` → `SceneBuilder.BuildAll()` → 전체 EditMode 순으로 진행한다. 체험 Lv1 ★1·기록 성장 복원·해금·옷 피복·손 소품·작은 표시·시장별 묘사는 `Docs/TODO.md`의 항목대로 확인한다. 기존 네 TTF 변경 표시는 작업 시작 전부터 있었으며 이번 작업에서 수정하지 않았다.

## 변경 파일

- `Docs/Design/Heroes.md`: 두 영웅 구현 상태와 보호막·소품 설명 갱신
- `Docs/Design/Skills.md`: 네 기술의 코드 등록 상태 갱신
- `Docs/Design/Story.md`: 창작 대사와 도감 연결 상태 갱신
- `Docs/GameDesign.md`: v4.40 변경 이력과 21명 구현 범위 기록
- `Docs/TODO.md`: 구현 완료와 리드 Unity 검증 범위 분리
- `Game/Assets/Editor/AdamDefinition.cs`: 아담 승인 수치·전용 기술·진화 정의 (새 스크립트의 .meta 포함)
- `Game/Assets/Editor/Art3DCharacters.cs`: 기존 몸·머리·발우·목봉 외형 등록
- `Game/Assets/Editor/BuddhaDefinition.cs`: 부처 승인 수치·전용 기술·진화 정의 (새 스크립트의 .meta 포함)
- `Game/Assets/Editor/SceneBuilder.Data.cs`: 21명 로스터·기술·레시피·역할·초상 생성 연결
- `Game/Assets/Editor/SceneBuilder.Meta.cs`: 실제 로스터를 미션 생성에 전달
- `Game/Assets/Editor/SceneBuilder.Missions.cs`: 오늘의 영웅 순서를 실제 로스터에서 복사
- `Game/Assets/Editor/SceneBuilder.Prefabs.cs`: 회복·고정 전방 타격 동작 생성
- `Game/Assets/Editor/SceneBuilder.Scene.cs`: 기본·진화 QA 프리셋 네 개 등록
- `Game/Assets/Scripts/Combat/Health.cs`: 출처별 보호막 소비·만료·기존 파괴 알림 연결
- `Game/Assets/Scripts/Combat/ShieldSources.cs`: 최댓값 흡수·모든 출처 동시 차감·독립 수명 (새 스크립트의 .meta 포함)
- `Game/Assets/Scripts/Dungeon/Devices/RoomDevices.cs`: 장치가 민 이동량을 플레이어에 보고
- `Game/Assets/Scripts/Dungeon/TrapZone.cs`: 컨베이어·밀어내기를 공통 이동 경로로 통일
- `Game/Assets/Scripts/Enemy/PatternBoss.cs`: 보스가 민 이동량을 보고
- `Game/Assets/Scripts/Heroes/AdamFootstepState.cs`: 실제 이동 0.8초·첫 공격·6초 재사용 상태 (새 스크립트의 .meta 포함)
- `Game/Assets/Scripts/Heroes/BuddhaBreathState.cs`: 실제 공격 3회와 8초 재사용 상태 (새 스크립트의 .meta 포함)
- `Game/Assets/Scripts/Heroes/HeroData.cs`: 보호·이동 준비·각성 데이터 필드 추가
- `Game/Assets/Scripts/Heroes/HeroSignature.cs`: 각성값·재사용 시간·성장 설명 연결
- `Game/Assets/Scripts/Missions/MissionTable.cs`: 기본 오늘의 영웅 목록에 두 명 추가
- `Game/Assets/Scripts/Player/HeroSignatureHud.cs`: 작은 대표 효과 상태 표시
- `Game/Assets/Scripts/Player/PlayerController.cs`: 공격 시작·실제 이동·독립 출처·공통 초기화 연결
- `Game/Assets/Scripts/Skills/Data/ActiveSkillData.cs`: 피해와 분리한 최대체력 회복 데이터
- `Game/Assets/Scripts/Skills/Effects/AdamPathBehaviour.cs`: 고정 중심·방향의 1회/2회 타격 (새 스크립트의 .meta 포함)
- `Game/Assets/Scripts/Skills/Effects/BuddhaRestBehaviour.cs`: 살아 있는 적 반경 조건·자기 회복·지연 취소 (새 스크립트의 .meta 포함)
- `Game/Assets/Scripts/Skills/Effects/FixedArcState.cs`: 전방 부채꼴 고정과 포함 판정 (새 스크립트의 .meta 포함)
- `Game/Assets/Scripts/Skills/Runtime/SkillPulseSequence.cs`: 게임 시간 간격과 수명주기별 지연 취소 (새 스크립트의 .meta 포함)
- `Game/Assets/Scripts/Story/StoryLines.cs`: 승인된 창작 대사 일곱 줄씩 등록
- `Game/Assets/Scripts/UI/CodexPanel.cs`: 사실·전승·각색 세 줄씩 구분
- `Game/Assets/Scripts/UI/SkillCardView.cs`: 회복 기술 카드에 피해 대신 회복량 표시
- `Game/Assets/Tests/EditMode/BoudicaTests.cs`: 정확한 19명 기대 목록을 21명으로 확장
- `Game/Assets/Tests/EditMode/BuddhaAdamActiveTests.cs`: 반경·생존 조건·초과 회복·간격·취소·고정 방향 검사 (새 스크립트의 .meta 포함)
- `Game/Assets/Tests/EditMode/BuddhaAdamDefinitionsTests.cs`: 정의·해금·전용 조합·미션·소품·대사 검사 (새 스크립트의 .meta 포함)
- `Game/Assets/Tests/EditMode/BuddhaAdamSignatureTests.cs`: 공격/이동 조건·재사용·회피 상한·강제 이동 회귀 검사 (새 스크립트의 .meta 포함)
- `Game/Assets/Tests/EditMode/CleopatraTests.cs`: 정확한 19명 기대 목록을 21명으로 확장
- `Game/Assets/Tests/EditMode/GodFinalPresentationTests.cs`: 정확한 19명 기대 목록을 21명으로 확장
- `Game/Assets/Tests/EditMode/GodTrialBuilderTests.cs`: 정확한 19명 기대 목록을 21명으로 확장
- `Game/Assets/Tests/EditMode/HumansCDefinitionsTests.cs`: 정확한 19명 기대 목록을 21명으로 확장
- `Game/Assets/Tests/EditMode/JeanneTests.cs`: 정확한 19명 기대 목록을 21명으로 확장
- `Game/Assets/Tests/EditMode/SeondeokTests.cs`: 정확한 19명 기대 목록을 21명으로 확장
- `Game/Assets/Tests/EditMode/ShieldSourcesTests.cs`: 최댓값·동시 소모·독립 만료·기존 API 검사 (새 스크립트의 .meta 포함)
- `Docs/Reviews/2026-10-29-buddha-adam.md`: 변경 파일별 요약·검증 근거·리드 인계
