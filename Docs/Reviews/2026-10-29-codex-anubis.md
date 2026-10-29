# Codex 리뷰 — 신의 시련 2·3 + 플레이 아누비스 (2026-10-29)

범위: `15cf719..ab1c883` C#. Codex `--sandbox read-only`, effort high.

## 지적

- 🟡 `Game/Assets/Scripts/Player/PlayerController.cs:829` — A에 표식 3회 → B를 기본탄으로 처치 → A 재명중 시, 사망 분기가 표적 전환을 생략해 기존 표식으로 즉시 둔화 발동 — 치명타격도 표적 전환에 반영하고 사망·Life 변경 시 누적만 초기화, 대상별 쿨다운은 유지.
- 🟡 `Game/Assets/Scripts/Records/SaveStore.cs:421` — 지난주 30조각 보유 → 새 주 목표 첫 승리 → 해당 신 해금 → 재접속하면 목표 잠금이 지워져 같은 주 다른 신으로 변경 가능(주간 5개 초과는 아님) — 해금 후에도 주간 잠금을 보존하고 주 경계에서만 정리; 저장·재로딩 회귀 검사 추가.

정적 검토 완료. 파일 수정 없음. Unity 컴파일·EditMode 실행은 미실시.
## 처리 (Claude)

| 지적 | 처리 |
|---|---|
| 🟡 처치한 명중이 표식 대상 전환을 건너뜀 | `OnMarkProjectileHitTarget`: 죽인 명중도 `RecordHit`으로 대상을 바꾸고, 둔화만 산 적에게 |
| 🟡 목표 신 해금 뒤 다시 불러오면 주간 잠금이 풀림 | `SaveStore.Normalize`: 알 수 없는 목표 id일 때만 정리 (해금으로 비운 null은 그 주 잠금 유지) + 저장·재로딩 테스트 |

🔴 0 → 합침 유지. 테스트 515/515.
