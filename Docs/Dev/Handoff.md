# 🔁 다른 컴퓨터에서 이어 하기 (노트북)

> 2026-10-28 데스크톱에서 정리, **2026-10-29 맥북에서 갱신** (아래 7장). 노트북의 Claude에게 처음에 **"Docs/Dev/Handoff.md 읽고 Docs/TODO.md 이어서 해"** 라고 하면 된다.
> (Claude의 기억은 컴퓨터마다 따로라서, 데스크톱에서 쌓인 기억 중 필요한 것을 여기 적어 둔다)

## 1. 지금 상태

| 항목 | 값 |
|---|---|
| 작업 가지 | **`feature/art-3d`** (main보다 49커밋 앞, 폰 시험 뒤 main에 합친다) |
| 마지막 커밋 | 이 문서를 올린 커밋 |
| 보관 | 3D 그대로: 태그 `art-3d-v1` · 가지 `archive/art-3d` / 2D 시험판: 태그 `art-2d-trial` · 가지 `archive/art-2d` |
| 테스트 | EditMode **661개** 전부 통과 (2026-10-29 맥북) |
| 할 일 | [Docs/TODO.md](../TODO.md) |

**그래픽은 3D로 간다** (2026-10-28 개발자: "3d가 훨 나은거 같다 2d 다시 보관해라"). 2D는 다시 제안하지 않는다.

## 2. 노트북 준비

1. Git + **Git LFS** (`git lfs install`), Git Bash
2. 저장소: `git clone https://github.com/choijunhuk/history-heroes` (이미 있으면 `git fetch`) → `git checkout feature/art-3d` → `git lfs pull`
3. **Unity 6.3 LTS (6000.3.27f1)** + Android 모듈 → `Game/` 폴더 열기 (처음엔 가져오기가 오래 걸린다)
4. Unity CLI: `winget install Unity.CLI` (에디터를 명령으로 다룬다, `com.unity.pipeline`은 이미 들어 있음)
5. Codex CLI (검토 · 화면 점검용): `npm i -g @openai/codex` → 로그인
6. Python 3.12 + Pillow (`pip install pillow`) — `Tools/*.py` 그림 도구용
7. 확인: Unity를 연 채로 Git Bash에서 `bash Tools/dev/test.sh` → 전부 통과면 준비 끝
8. 소품을 만들려면 **Blender** (맥: `brew install --cask blender`) → `Blender -b --factory-startup -P Tools/blender/hero_props.py -- 소품이름`

## 3. 도구 (`Tools/dev/`, Git Bash)

| 명령 | 하는 일 |
|---|---|
| `bash Tools/dev/qa.sh eval '<C#>'` | 열린 에디터에서 C# 한 줄 (예: `return HistoryHeroes.EditorTools.QaTools.State();`) |
| `bash Tools/dev/qa.sh cap 이름` | Game 화면 찍기 → `Game/Assets/Temp/qa/g_이름.png` (git 제외) |
| `bash Tools/dev/ucompile.sh` | 새로고침 → 컴파일 → 오류 보기 |
| `bash Tools/dev/test.sh` | EditMode 테스트 전부 + 요약 |

- 다시 만들기 순서: `Art3DStage.BuildAll` → `Art3DCharacters.BuildAll` → `SceneBuilder.BuildAll` (메뉴 History Heroes, 또는 qa.sh eval)
- QA: `QaTools.Press("Dungeon")` · `Step(0, true)` · `TrySkill("id", 3)` · `SandboxSave(true)` (진짜 저장을 건드리지 않게) · `BossBench(...)`
- **Play 중에는 컴파일하지 말 것** (멈춘 뒤 ucompile). Play 모드는 도메인 다시 불러오기가 꺼져 있다 → 정적 필드는 `SubsystemRegistration`에서 초기화

## 4. 작업 규칙 (CLAUDE.md · AGENTS.md + 데스크톱 기억)

- 개발자에게는 **쉬운 한국어**, 영어 용어 피하기. 코드는 Claude가 다 짠다 (TODO 남기지 않기)
- 물어보지 말고 쭉 진행, 자주 커밋 · 푸시. 설치는 해도 됨, 돈 · 계정 · 지우기는 먼저 묻기
- 커밋: `type: 설명` (feat · fix · balance · docs · refactor · chore). **Claude Co-Authored-By 줄 넣지 않기**
- 공개 저장소: 열쇠 · 비밀번호 · 광고 비밀값 · 개인 정보(이메일 등) 올리지 않기
- `Game/Library · Temp · Logs · UserSettings` 커밋 금지, `.meta`는 꼭 같이
- `Game/Assets/Fonts/NanumGothic-Bold SDF.asset` · `BlackHanSans SDF.asset`은 고쳐져 있어도 **커밋하지 않기** (실행 중 글자가 늘어난 것)
- 기획이 바뀌면 `Docs/GameDesign.md`를 같은 커밋에서 (끝의 바뀐 기록 표에 한 줄). 새 아이디어는 `Docs/Ideas.md`
- Codex 검토: `codex exec --sandbox read-only -c model_reasoning_effort=<high|xhigh|medium>` (설계 xhigh · 코드 high · 화면 medium), 결과는 `Docs/Reviews/YYYY-MM-DD-주제.md` + 맨 끝 "반영" 표
- 개발자 결정이 검토 의견보다 먼저 (예: 던전 ★3 "전설 없이 클리어"는 그대로)

## 5. 데스크톱에만 있는 것

| 것 | 위치 | 노트북에서 |
|---|---|---|
| 블렌더 + MCP 소품 작업 (예전 소품) | `D:\Tools` · `D:\HH_Blender` | 2026-10-29부터 **스크립트로 어디서나**: `Tools/blender/hero_props.py` (Blender만 깔면 됨) |
| 2D 시험판 그림 원본 | `D:\HH_2D` | 저장소 `archive/art-2d`에 다듬은 그림은 있음 |
| 폰 시험 (adb) | Unity Android 모듈의 adb | 노트북에 폰을 연결하면 같은 방법 |

## 6. 최근에 한 일 (2026-10-28)

- 새 영웅 4명: 이순신 · 이성계 · 다빈치 · 제갈량 (+ 코드 검토 9건, 화면 점검 2번, 보스 벤치로 밸런스)
- 영웅 외곽선 · 머리 위 대표 효과 표시 · 키 +20%, 제갈량 흰 도포, 컨베이어 화살표
- Codex 전체 검토 2개 (기획 · 그래픽/UI) → 출시 전 고치기 1차 (TODO 1장 "이미 한 것")
- 2D 그림 시험판 → 보관, 3D 유지

## 7. 2026-10-29 맥북에서 한 일 (개발자 피드백 한 바퀴)

피드백: 여자 영웅 · 홈 미션 · 보상 그림 · 줄 간격 · 탕탕특공대식 영웅 화면 · 역사 유물 · 신 vs 인류 (부처 · 아담). 자세한 기록: [UX-Overhaul-Plan.md](UX-Overhaul-Plan.md) 5장, 신 설계: [GodsVsHumanity.md](../Design/GodsVsHumanity.md).

- **화면:** 줄 간격 · 홈 미션 띠 · 탕탕식 영웅 화면 (장비 6칸 양옆, 영웅 줄 가로 스크롤) · 보상 · 재화 그림 + 글 속 아이콘 · 유물 그림 24장 · 결과 화면 보상 카드 · 상점 그림
- **성장:** 유물 뽑기 · 장비 6칸 (저장 구조 8 → 9)
- **영웅 8 → 21명:** 여자 영웅 7 (잔 다르크 · 선덕여왕 · 부디카 · 클레오파트라 · 토모에 고젠 · 무측천 · 마리 퀴리), 신 4 (토르 · 아누비스 · 스사노오 · 아테나 — 각자 신의 시련 조각 30개로 해금, 뽑기 없음), 부처 · 아담
- **신의 시련 5개:** 망각의 왕을 깨면 보스 화면 [신의 시련] 탭 (토르 → 아누비스 → 제우스 → 스사노오 → 아테나 순서 개방, 기존 보스 패턴 재사용). 왕 첫 격파 뒤 예고 3장면
- **소품:** 맥에 Blender 설치 → 스크립트로 검 · 깃발 · 망치 · 머리장식 · 가면 · 창 · 투구 · 어깨 덧갑 · 관 · 실험병 · 발우 · 목봉. 좌표: 블렌더 (X, Y, Z) = Unity (−x, −z, y), 얼굴 = +Y, 등 = −Y. 여자 머리 소품은 `Art3DProps.Basis`에 `Superhero_Female_FullBody`
- **작업 방식:** 큰 구현은 Codex (`codex exec --sandbox workspace-write -c model_reasoning_effort=xhigh`)를 git worktree에서 → Claude가 합치고 컴파일 · `Art3DCharacters.BuildAll` → `SceneBuilder.BuildAll` · 테스트 · 화면 확인 → Codex 읽기 전용 리뷰 (`Docs/Reviews/2026-10-29-codex-*.md`) → 고침
- **알아 둘 것:** `PlayerController`의 멈춤 · 이동 대표 효과 초기화는 `ResetStopSignatures(bool moved)` 한 곳에만. 보호막은 출처별 (`Health.SetShieldFrom`, 합산 없이 큰 것 하나, 방패 연출은 `GuardShield`). 글꼴에 이모지(🔒 ✔)가 없으니 UI 글에 쓰지 말 것. `M_Prop_Copper.mat`은 3D → 장면 순서로 만들어야 빛(_EMISSION)이 유지됨
- **사람이 할 것:** 폰 확인 · 시련 난이도(2~3분) 벤치 · 상점 조각 하루 한도(3 → 5) 결정 · **첫 출시 국가** (종교 인물 표현 · 등급)
