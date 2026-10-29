# 신의 시련 2·3과 플레이 아누비스 인계 (2026-10-29)

## 범위와 변경 계획

- 기존 토르의 시련 ID·첫 보상·주간 목표·해금과 `GodTrialRulesTests`를 회귀 기준으로 둔다.
- 토르 전용 분기를 시련 ID·순서·원형 보스·동행 영웅·칭호·첫 조각 수의 작은 표로 바꾸고, 아누비스·제우스를 순서대로 추가한다.
- 시련 저장은 구조 9의 `godTrials`를 재사용한다. 새 저장 필드가 생기면 구조 번호와 이관 검사를 함께 추가한다.
- 전투·영웅·장면 생성 코드와 순수 EditMode 검사를 작성한다. 생성된 `.unity`·`.prefab`·`.asset`과 폰트 SDF는 이 작업에서 건드리지 않는다.

## 시작 수치와 이유

| 대상 | 체력 | 공격력 | 이유 |
|---|---:|---:|---|
| 토르 시련 | 93,600 | 141 | 기존 수치 유지 |
| 아누비스 시련 | 99,600 | 138 | 토르보다 체력이 높고 망각의 왕 108,000 아래. 수호병이 본체 피해를 줄이므로 기본 공격력은 토르보다 낮춤 |
| 제우스 시련 | 105,600 | 150 | 탄막을 피해야 하는 시련이라 왕의 108,000/162 아래로 두고, 근접 반격 시간을 확보 |
| 플레이 아누비스 | 950 | 90 | 제갈량·선덕여왕 법사 범위에서 시작. 신 전용 능력치 보너스 없음 |

## 검증 상태

- 이 작업 환경에서는 Unity를 실행할 수 없다. Editor 컴파일·EditMode 실행·화면·2~3분 완주·근접 1~2타·폰 성능은 리드가 확인한다.
- `Enemy_Staff`는 적 모델 `Imp_LOD/hand_l` 기준 소품이고 `Male_Ranger` 사람 손과 기준이 달라 이번 아누비스 조립에서 생략했다. 지팡이가 필요하면 Unity에서 사람 손 위치와 회전을 확인한 뒤 별도로 보정한다.
- 코드/생성 에셋/테스트 결과가 모이면 이 파일에 실제 변경과 검증 결과를 덧붙인다.

## 문서 변경

- `Docs/Design/GodsVsHumanity.md` — 시련 2·3과 아누비스의 시작 수치·이유·해금·보상·저장·외형.
- `Docs/Design/Heroes.md` — 14번째 아누비스의 능력치·대표 효과·기술·성장·3D.
- `Docs/Design/Skills.md` — `sand_statue`·`sand_gate`와 진화 재료·기록 비용.
- `Docs/Design/Modes.md` — 세 시련의 순서·기록·주간·환급·반복 보상.
- `Docs/Design/Economy.md` — 첫 조각/칭호·제우스 무조각·공유 상한·해금.
- `Docs/Design/Story.md` — 아누비스·제우스 입장 두 줄씩과 아누비스 일곱 상황 대사.
- `Docs/Design/StorySources.md` — 미술관의 신화 설명과 게임 창작의 경계.
- `Docs/GameDesign.md` — 14명과 v4.35 변경 이력.
- `Docs/TODO.md` — 코드 작성·Unity 검증을 별도 항목으로 추적.

## 리드의 확인 순서

1. Play 종료 뒤 Unity 컴파일. 에러가 없으면 `Art3DCharacters.BuildAll()` 다음 `SceneBuilder.BuildAll()` 실행하고 새 생성 에셋과 메타 파일 확인.
2. 전체 EditMode, 특히 기존 토르 검사와 세 시련 순서·보상·주간 경계·아누비스 표식·전용 진화 검사 실행.
3. 본인 기록·임시/잠금 기록·연습·시련 첫 격파·30조각 해금·앱 종료 복구를 화면으로 확인.
4. 일반/희귀 기록으로 아누비스 소환병 보호 제거와 제우스 탄막 피하기, 공격 뒤 근접 1~2타, 보스 2~3분, 손 소품 생략과 가면·머리 숨김·UI 겹침·폰 성능 확인.

## 통합 검증 결과

- PASS: Unity 6000.3.27f1 설치본의 모듈 DLL로 런타임 264개·에디터 39개·EditMode 검사 42개 C# 파일을 순서대로 정적 컴파일. 오류 0개. 직렬화 필드 기본값·기존 API 폐기 등 기존 경고는 남아 있다.
- 참조 한계: 패키지는 설치본의 URP 템플릿 DLL(17.0.14)을 썼다. 프로젝트 패키지(URP 17.3.0 포함)로 수행하는 Unity 재컴파일을 대신하지 않는다.
- PASS: 엔진 호출 없는 기존 토르 규칙·확장 시련·표식·둔화 중첩·외형 정의의 NUnit 검사 메서드 33개를 관리 코드에서 직접 실행. Editor의 NUnit runner 실행은 하지 않았다.
- NOT_RUN: ScriptableObject 생성·JsonUtility 저장 복구·★강화/진화와 카드 표시 등 엔진 호출이 필요한 검사의 실제 실행, 전체 EditMode, 생성 에셋과 실제 화면·폰 성능.
- PASS: 기존 `GodTrialRulesTests.cs`·`GodTrialSessionTests.cs`·`ThorCombatTests.cs`는 HEAD와 내용이 같다. 새 기능 검사는 별도 파일에 추가했다.
- PASS: diff 공백 검사, 새 C# 8개와 대응 `.meta` 8개, 금지 경로·생성 에셋·폰트 SDF 변경 없음. LFS 필터를 끄면 수정으로 보이는 149개 바이너리는 SHA-256이 HEAD의 LFS 포인터와 모두 같다.
- 독립 검토에서 모델 명단 순서·둔화 시간 중첩·둔화 성장 표시를 고쳤고, 최신 재검토에서 잔여 지적은 없었다.
- `visual-verdict`: NOT_RUN. Unity 화면과 비교 캡처를 만들 수 없는 환경이라 외형·배치 통과를 주장하지 않는다.
- 커밋하지 않았다. 코드와 문서만 변경했다.

## 단순화와 남은 확인

토르 전용 보상·해금 분기를 작은 시련 표로 합쳤다. 기존 저장 필드·보스 패턴·영웅 성장·공통 보상 상한을 재사용하며 저장 구조나 재화를 늘리지 않았다. 기존 보스 회복 시간의 기본값은 그대로이고 새 시련에서만 설정값을 바꾼다. 모래 수호상은 처음 만든 모델을 다시 쓴다.

표식과 수호상은 둔화 출처별 만료를 나눴다. 기존 출처 없는 둔화 호출은 원래 동작을 유지한다. 진화 파동은 한 수호상에서 적의 같은 생명에 한 번만 피해를 준다.

생성 에셋은 리드의 빌더 실행 전까지 옛 상태다. 근접 1~2타와 2~3분 전투 시간·가면 크기·손 번개 위치·작은 글씨·저사양 기기 비용은 실제 플레이로 판단해야 한다. 지팡이는 넣지 않았다.

## 추가 문서 변경

- `Docs/Design/Systems.md` — 시련 1~3·플레이 토르/아누비스의 코드 작성 상태와 Unity 검증 전 상태를 구분.
- `Docs/Dev/GodTrials-Handoff.md` — 변경 계획·수치 이유·검증 증거·파일별 변경과 리드 확인 순서.

## 코드 변경 (파일별)

- `Game/Assets/Editor/AnubisDefinition.cs` — 법사 능력치·별 강화·수호상과 진화 수치의 공통 정의.
- `Game/Assets/Editor/AnubisDefinition.cs.meta` — 대응 C# 파일의 새 GUID. 파일과 함께 반영한다.
- `Game/Assets/Editor/Art3DCharacters.cs` — 아누비스 가면·머리 숨김·공유 보스 외형, 기억의 수호병과 제우스 조립.
- `Game/Assets/Editor/SceneBuilder.Bosses.cs` — 시련 2·3과 기억의 수호병 데이터·프리팹 생성 코드.
- `Game/Assets/Editor/SceneBuilder.Data.cs` — 14번째 영웅·모래 기술·최대체력 진화·투사체 연결.
- `Game/Assets/Editor/SceneBuilder.Fx3D.cs` — 모래 구체·번개 모양 탄과 제우스 손의 전격 생성.
- `Game/Assets/Editor/SceneBuilder.Lobby.cs` — 세 시련 선택 단추·주간 목표와 기록 칸 배치.
- `Game/Assets/Editor/SceneBuilder.Missions.cs` — 생성된 미션 표에도 14명 순서 적용.
- `Game/Assets/Editor/SceneBuilder.Prefabs.cs` — 수호상·진화·모래 구체·번개 탄·기억의 수호병 생성.
- `Game/Assets/Editor/SceneBuilder.Scene.cs` — 시련 배열과 번개 탄 풀을 전투 장면에 연결.
- `Game/Assets/Scripts/Combat/Projectile.cs` — 아누비스의 첫 기본 탄은 첫 명중만 표식에 통지.
- `Game/Assets/Scripts/Common/GameSession.cs` — 시련 표에 맞는 기존 1·2·3구간 경기장 선택.
- `Game/Assets/Scripts/Enemy/Bosses/SteelColossusBoss.cs` — 기존 기본값을 보존하며 제우스의 번개 모양·공격 뒤 빈틈 설정.
- `Game/Assets/Scripts/Enemy/Bosses/WraithLordBoss.cs` — 기존 기본값을 보존하며 아누비스 공격 뒤 빈틈 설정.
- `Game/Assets/Scripts/Enemy/EnemyProjectiles.cs` — 번개 탄을 별도 풀에서 꺼내 기존 차단·둔화 목록으로 관리.
- `Game/Assets/Scripts/Enemy/EnemyStatus.cs` — 표식·수호상 둔화 출처를 나눠 강도와 만료를 적용.
- `Game/Assets/Scripts/Enemy/SlowChannels.cs` — 약한 둔화가 강한 표식의 시간을 연장하지 않는 순수 상태.
- `Game/Assets/Scripts/Enemy/SlowChannels.cs.meta` — 대응 C# 파일의 새 GUID. 파일과 함께 반영한다.
- `Game/Assets/Scripts/Heroes/AnubisMarkState.cs` — 같은 대상 3회+다음 공격·한 표식·대상별 6초 제한.
- `Game/Assets/Scripts/Heroes/AnubisMarkState.cs.meta` — 대응 C# 파일의 새 GUID. 파일과 함께 반영한다.
- `Game/Assets/Scripts/Heroes/HeroData.cs` — 아누비스 표식과 별 강화의 조정 필드.
- `Game/Assets/Scripts/Heroes/HeroRoster.cs` — 신의 조각 해금 안내에서 토르 고정 이름 제거.
- `Game/Assets/Scripts/Heroes/HeroSignature.cs` — ★1·★3·★5 표식 수치와 강화 안내.
- `Game/Assets/Scripts/Missions/MissionTable.cs` — 기본 영웅 순서 끝에 아누비스 추가.
- `Game/Assets/Scripts/Modes/BossRun.cs` — 일반 보스와 분리한 시련 배열 선택, 기존 토르 필드 호환.
- `Game/Assets/Scripts/Modes/GodTrialDefinition.cs` — 시련 순서·원형 보스·경기장·영웅·칭호·첫 보상 정의.
- `Game/Assets/Scripts/Modes/GodTrialDefinition.cs.meta` — 대응 C# 파일의 새 GUID. 파일과 함께 반영한다.
- `Game/Assets/Scripts/Modes/GodTrialRules.cs` — 순차 해금·제우스 무조각·신별 해금·공유 주간 보상 일반화.
- `Game/Assets/Scripts/Player/HeroSignatureHud.cs` — 아누비스 표식 누적·준비 상태 표시.
- `Game/Assets/Scripts/Player/PlayerController.cs` — 실제 기본 첫 탄에서만 표식 누적과 보스 30% 둔화 적용.
- `Game/Assets/Scripts/Records/SaveData.cs` — 해금 전 조각 30개 상한을 토르·아누비스에 공통 적용.
- `Game/Assets/Scripts/Records/SaveStore.cs` — 구조 9 유지, 기존 토르 주간 상태와 신별 조각 상한 복구.
- `Game/Assets/Scripts/Skills/Data/ActiveSkillData.cs` — 둔화 레벨 성장·보스 적용 비율·진입 파동 반경.
- `Game/Assets/Scripts/Skills/Effects/SandStatueBehaviour.cs` — 재사용하는 수호상 1기·4초 둔화·진화 진입 파동.
- `Game/Assets/Scripts/Skills/Effects/SandStatueBehaviour.cs.meta` — 대응 C# 파일의 새 GUID. 파일과 함께 반영한다.
- `Game/Assets/Scripts/Story/GodTrialIntro.cs` — 아누비스·제우스 입장 대사 선택, 기존 토르 API 유지.
- `Game/Assets/Scripts/Story/StoryLines.cs` — 아누비스의 7개 상황별 게임 창작 대사.
- `Game/Assets/Scripts/UI/BossIntroPanel.cs` — 잠긴 시련 조건·순서·선택과 미해금 신 주간 목표 변경.
- `Game/Assets/Scripts/UI/CodexPanel.cs` — 아누비스·제우스 신화/각색과 인류 동행 이유.
- `Game/Assets/Scripts/UI/HeroPanel.cs` — 신별 조각 수·30개 해금 버튼 일반화.
- `Game/Assets/Scripts/UI/RunResultScreen.cs` — 시련 결과에서 선택한 신 이름 사용.
- `Game/Assets/Scripts/UI/SkillCardView.cs` — 모래 수호상의 둔화 성장과 범위를 카드에 표시.
- `Game/Assets/Tests/EditMode/AnubisCombatTests.cs` — 표식·재사용·둔화 중첩·별 강화·전용 진화·대사 검사.
- `Game/Assets/Tests/EditMode/AnubisCombatTests.cs.meta` — 대응 C# 파일의 새 GUID. 파일과 함께 반영한다.
- `Game/Assets/Tests/EditMode/BoudicaTests.cs` — 기존 순서와 여자 4명 조건을 보존하며 명단을 14명으로 확장.
- `Game/Assets/Tests/EditMode/CleopatraTests.cs` — 기존 순서와 여자 4명 조건을 보존하며 명단을 14명으로 확장.
- `Game/Assets/Tests/EditMode/GodTrialBuilderTests.cs` — 정확한 14명 순서·가면·제우스·입장 대사·둔화 성장 표시 검사.
- `Game/Assets/Tests/EditMode/GodTrialBuilderTests.cs.meta` — 대응 C# 파일의 새 GUID. 파일과 함께 반영한다.
- `Game/Assets/Tests/EditMode/GodTrialExpansionTests.cs` — 세 시련 순서·보상·주간/일일 경계·해금·구조 9 복구 검사.
- `Game/Assets/Tests/EditMode/GodTrialExpansionTests.cs.meta` — 대응 C# 파일의 새 GUID. 파일과 함께 반영한다.
- `Game/Assets/Tests/EditMode/JeanneTests.cs` — 기존 명단 순서 뒤에 아누비스를 추가하는 정확한 비교 유지.
- `Game/Assets/Tests/EditMode/SeondeokTests.cs` — 영웅·미션 명단의 정확한 14명 비교 유지.
