# 3D 연결 코드 리뷰 (Codex, 2026-10-27)

- 리뷰어: Codex `gpt-6-sol` (read-only, effort high)
- 범위: `19d6010..e6f541a` (3D 전투 화면 연결: 원근 카메라 · Visual3D · Stage3D · 적 17종 · 빌더). 설계 검토는 [3D 전환 검토](2026-10-27-art3d-review.md)

## 결정

| # | 지적 | 결정 | 반영 |
|---|---|---|---|
| 1 | 궁병이 쏜 직후 상태가 바뀌면서 모델이 화살과 다른 쪽을 본다 | 반영 | `Visual3D.Attack`은 0.2초 안에 또 불리면 방향도 바꾸지 않는다 (처음 쏜 쪽 유지). 탄막도 같다 |
| 2 | 모델 없는 영웅으로 바꾸면 이전 영웅 모델이 남는다 | 반영 | 영웅을 바꿀 때 늘 `SetModel(hero.model3D)` — 없으면 모델을 내려놓고 임시 도형을 다시 보인다 |
| 3 | 기울기·시야가 크면 화면 위 광선이 지평선을 넘어 땅 범위가 틀린다 | 반영 | 기울기 + 시야/2 ≤ 80°로 줄인다 (`CameraFit.SafeTilt`), 조절 범위도 기울기 0~55° · 시야 15~60°로. 테스트 `SteepAngles_StayBelowTheHorizon` |
| 4 | 카메라 설정을 합 하나로 키를 만들어 다른 설정이 같아질 수 있다 | 반영 | 값을 하나씩 비교 (`Settings` 구조체) |
| 5 | 탄막 탄마다 `TryGetComponent<Visual3D>` | 반영 | 쏜 적(`EnemyBase.Visual`)이 이미 든 참조를 쓴다 |

테스트 212/212.

## 원문

1. **[버그] 발사 방향과 모델의 공격 방향이 어긋납니다.** [EnemyBase.cs:141](../../Game/Assets/Scripts/Enemy/EnemyBase.cs:141), [EnemyProjectiles.cs:38](../../Game/Assets/Scripts/Enemy/EnemyProjectiles.cs:38) — 궁병은 잠근 방향으로 화살을 쏘며 `Fire`에서 그 방향으로 `visual.Attack`을 호출합니다. 같은 프레임 `Telegraph → Recover`에서도 `Attack(ToPlayer)`을 호출하는데, 0.2초 제한이 애니메이션만 막고 `Aim`은 먼저 갱신합니다. 플레이어가 조준 고정 후 회피하면 모델이 화살과 다른 방향을 봅니다. 발사한 공격은 전환 시 다시 `Aim`하지 않도록 하고, 폭탄병처럼 발사체가 없는 공격에는 고정된 목표 방향을 전달하세요.

2. **[버그] 3D 모델이 없는 영웅으로 바꿔도 이전 영웅 모델이 남습니다.** [PlayerController.cs:157](../../Game/Assets/Scripts/Player/PlayerController.cs:157) — `ShowHeroModel`은 `hero.model3D == null`이면 `SetModel`을 호출하지 않습니다. 기본 Khan 모델이 붙은 뒤 모델 참조가 빠진 영웅을 선택하면 Khan이 계속 표시되고 2D 대체 그림도 숨겨진 채입니다. 모델이 없을 때도 `Visual.SetModel(null, …)`을 호출해 이전 모델을 반환하고 그림을 다시 보이게 하세요.

3. **[위험] 허용된 카메라 각도에서 지면 경계가 실제 화면과 달라집니다.** [CameraFit.cs:199](../../Game/Assets/Scripts/Common/CameraFit.cs:199) — `tilt=70°`, `fieldOfView=70°`처럼 인스펙터가 허용하는 조합에서는 화면 위쪽 광선이 지면과 만나지 않습니다. `GroundBounds`는 이 광선을 임의의 `maxRay` 거리에서 잘라 지면 좌표로 취급하므로, 생존 모드의 카메라 제한과 생성 거리가 잘못 계산됩니다. 지평선이 화면에 들어오는 조합을 제한하거나, 교차하지 않는 광선을 명시적으로 처리하고 극단 각도 테스트를 추가하세요.

4. **[버그] 서로 다른 카메라 설정이 같은 캐시 키를 가질 수 있습니다.** [CameraFit.cs:80](../../Game/Assets/Scripts/Common/CameraFit.cs:80) — 각도 두 값과 여백 세 값을 각각 가중합 하나로 압축합니다. 예를 들어 위쪽 여백을 늘리고 아래쪽 여백을 줄여 합이 같아지면 `Solve`가 다시 실행되지 않습니다. 편집 중 설정을 바꿔도 이전 구도가 남습니다. `tilt`, FOV, 각 여백을 개별 비교하세요.

5. **[개선] 탄막의 발사체마다 같은 컴포넌트를 다시 찾습니다.** [EnemyProjectiles.cs:38](../../Game/Assets/Scripts/Enemy/EnemyProjectiles.cs:38) — 보스의 `Ring`·`Fan`은 한 번의 공격에서 같은 `owner`로 `Fire`를 여러 번 호출합니다. 각 탄마다 `TryGetComponent<Visual3D>`가 반복됩니다. `EnemyBase`에 이미 보관한 `visual`을 발사 경로에서 사용하고, 공격 애니메이션은 발사 묶음당 한 번 호출하세요.

읽기 전용 정적 리뷰이며 Unity 실행 검증은 하지 않았습니다.