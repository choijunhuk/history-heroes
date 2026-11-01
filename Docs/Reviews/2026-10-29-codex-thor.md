# Codex 리뷰 — 신의 시련 1 (토르) + 플레이 토르 (2026-10-29)

범위: `f00c707..0960954` C# (feature/art-3d). Codex `--sandbox read-only`, effort high.

## 지적

🟡 [GameSession.cs:60](../../Game/Assets/Scripts/Common/GameSession.cs:60) — 하트 차감 저장 후 `BossRun.Start()`의 시련 스냅샷 저장 전에 앱이 종료되면 하트만 소모되고 이어하기 불가 — 하트 차감과 시련 ID·기록 ID·`heartSpent`를 하나의 `Commit`으로 저장하고 성공 후 장면 전환.

정적 검토에서 확정한 문제는 1건입니다. 파일 수정 없음. Unity 에디터 연결을 확인하지 못해 테스트·실행 검증은 미수행했습니다.


## 처리 (Claude)

| 지적 | 처리 |
|---|---|
| 🟡 하트 저장 뒤 장면 로딩 중 종료 → 하트만 소모 | 기존 보스도 같은 구조였다. `GameSession.BossSnapshot`을 만들어 `StartBoss` · `TryStartGodTrial`이 장면을 열기 **전에** 하트와 이어하기 스냅샷을 같이 저장. 테스트 `RealAttempt_SnapshotCarriesTrialAndHeart_BeforeSceneLoads` |

🔴 0 → 합침 유지.
