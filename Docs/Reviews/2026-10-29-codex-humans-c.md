# Codex 리뷰 — 토모에 고젠 · 무측천 · 마리 퀴리 (2026-10-29)

범위: `ec82a45..d02a875` C#. Codex `--sandbox read-only`, effort high.

## 지적

🟡 [CurieObservationTests.cs:89](/Users/choi/Documents/history-heroes/Game/Assets/Tests/EditMode/CurieObservationTests.cs:89) — 이동 검사가 상태 객체에 한정돼 실제 이벤트·투사체 세대 차단 회귀를 놓침 — PlayerController와 Projectile을 연결한 통합 테스트 추가.

🟢 [PlayerController.cs:523](/Users/choi/Documents/history-heroes/Game/Assets/Scripts/Player/PlayerController.cs:523) — 이동 시 잔 다르크 깃발·부디카 쿨다운·아누비스 상태 보존, 구세대 명중은 863행에서 차단 — 수정 불필요.

그 외 확정 결함 없음. C# `diff --check` 통과. Unity 컴파일·테스트·실기동은 미실행(Editor 연결 미확인).
## 처리 (Claude)

- 🔴 0 · 확정 결함 없음 → 합침 유지 (테스트 623/623).
- 🟡 PlayerController · Projectile 통합 테스트: EditMode에서 PlayerController를 깨우려면 장면 의존이 커서 보류. 이동 초기화는 `ResetStopSignatures(moved)` 한 곳이라 상태 테스트로 덮는다 (TODO).
