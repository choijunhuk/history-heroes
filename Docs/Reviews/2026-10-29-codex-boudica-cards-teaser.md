# Codex 리뷰 — 보상 카드 · 신의 심판 예고 · 부디카 · 블렌더 소품 (2026-10-29)

범위: `e7b076c..f83007a` (feature/art-3d). Codex `--sandbox read-only`, effort high.

## 지적

- 🟡 [Game/Assets/Scripts/Player/PlayerController.cs:884](../../Game/Assets/Scripts/Player/PlayerController.cs:884) — 흡인 중인 적은 `EnemyStatus.FixedUpdate`가 밀침의 `MovePosition` 예약을 덮어 준비만 소비됨 — 강제 이동을 물리 처리 한곳에서 조정하고, 흡인 중 밀침·1회 소비 통합 테스트 추가. [Unity 계약](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/rigidbody2d/moveposition)
- 🟡 [Tools/blender/hero_props.py:6](../../Tools/blender/hero_props.py:6) — 개인 Mac 출력 경로가 고정되어 Windows·다른 checkout에서 실패하거나 엉뚱한 checkout을 덮어씀 — `__file__` 기준 경로 또는 출력 인자 사용.
- 🟢 [Game/Assets/Scripts/Economy/RewardCards.cs:23](../../Game/Assets/Scripts/Economy/RewardCards.cs:23) — `Wallet.Format`은 현재 문화권을 따르지만 파서는 쉼표만 허용해 `de-DE`의 `+2.000`은 카드로 변환되지 않음 — 출력·파싱 문화권 통일 및 회귀 테스트 추가.
- 🟢 [Docs/Design/Heroes.md:220](../../Docs/Design/Heroes.md:220) — 부디카가 빈손이라는 설계가 실제 검 장착과 불일치 — 설계 문구와 `EmptyHands` 테스트 이름 동기화.

판정: 수정 권고. `diff --check`·Python 구문 검사 통과; Unity 실행·테스트·물리 재현은 미실행.


## 처리 (Claude)

| 지적 | 처리 |
|---|---|
| 🟡 흡인 중 밀침이 덮임 | 부디카만이 아니라 모든 밀침(나폴레옹 · 적토마 · 팔문 · 거북선 · 깃발의 파동)이 `MovePosition`을 따로 불러 같은 문제. `EnemyStatus.Push` / `EnemyBase.Push` 한 곳으로 모으고, 흡인 중이면 흡인과 같은 물리 걸음에 더한다 |
| 🟡 블렌더 출력 경로 고정 | `__file__` 기준 저장소 경로로 |
| 🟢 독일어 기기에서 "2.000" | `Wallet.Format`을 문화권 무관 "2,000"으로 + 회귀 테스트 |
| 🟢 부디카 "손 소품 없음" 문서 | Heroes.md 부디카 · 잔 다르크 3D 줄과 테스트 이름 고침 |
