<div align="center">

# 🏹 History Heroes

### 역사영웅 로그라이크

**전 세계 역사 인물이 되어 던전을 돌파하고, 최고의 빌드를 기록해 보스에게 도전하라.**

![Unity](https://img.shields.io/badge/Unity-6.3_LTS-000000?logo=unity&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?logo=csharp&logoColor=white)
![Platform](https://img.shields.io/badge/Platform-Android-3DDC84?logo=android&logoColor=white)
![Status](https://img.shields.io/badge/Status-기획_단계-orange)

</div>

---

## 🎮 어떤 게임인가요?

아처(Archero) 스타일의 **세로 화면 모바일 로그라이크**예요. 관우, 나폴레옹, 이순신, 테슬라 같은 **실제 역사 인물**이 영웅이 되어 싸워요.

```
 조이스틱으로 이동 ──▶ 멈추면 기본 공격 + 액티브는 항상 자동 발동
        │
        ▼
 적 처치 → 레벨업 ──▶ 액티브·패시브 3장 중 1장 ──▶ 조합해서 더 강하게 ✨
        │
        ▼
 판이 끝나면 빌드를 「기록」으로 저장 ──▶ 기록을 골라 보스에 도전
```

## ✨ 핵심 재미

| | |
|---|---|
| 🏛️ **역사 인물 × 직업** | 궁수 칭기즈 칸, 전사 관우, 총잡이 나폴레옹, 법사 테슬라, 소환사 이순신… 역사적 업적이 그대로 능력이 돼요. |
| 🎴 **매 판 다른 빌드** | 레벨업할 때마다 액티브·패시브 스킬 3장 중 1장. 칸이 한정돼 있어서 무엇으로 채울지가 곧 빌드예요. |
| ✨ **스킬 조합** | 액티브 + 패시브는 **진화**, 액티브 + 액티브는 **합체**. 레시피는 숨겨져 있어서 직접 찾아내야 해요. |
| 💎 **귀한 전설** | 전설 스킬은 초반에도 끝에도 잘 안 나와요. 그래서 뜨는 순간이 짜릿해요. |
| 💾 **기록 → 보스** | 잘 나온 판을 기록으로 저장하고, 보스에게 어떤 기록을 들고 갈지 골라요. |
| 🧟 **서바이벌 모드** | 뱀서처럼 몰려오는 적 속에서 10분 버티기. 단, 기본 공격은 여기서도 멈춰야 나가요. |
| 🎭 **같은 인물, 다른 분위기** | 귀여운 SD, 코믹 패러디, 웅장한 판타지. 스킨마다 인물의 느낌이 달라져요. |

## 🗺️ 로드맵

- [ ] **v0.1** — 궁수 1명, 패시브 6 + 액티브 4 + 조합 2로 "재밌는지" 확인
- [ ] **v0.2** — 기록 저장 + 보스맵 + 조합 도감 ⭐
- [ ] **v0.3** — 영웅 추가 (전사, 법사, 총잡이)
- [ ] **v0.4** — 장비, 룬
- [ ] **v0.5** — 서바이벌(뱀서) 모드
- [ ] **v1.0** — 구글 플레이 출시

자세한 내용은 [기획서의 로드맵](Docs/GameDesign.md#11-로드맵)을 봐주세요.

## 📚 문서

| 문서 | 내용 |
|---|---|
| [📖 게임 기획서](Docs/GameDesign.md) | 게임의 모든 설계: 영웅, 스킬, 기록, 보스, 로드맵 |
| [📏 개발 규칙](Docs/DevRules.md) | 디자인 원칙, 코딩 규칙, Git 규칙 |
| [💡 아이디어 보관함](Docs/Ideas.md) | 아직 채택되지 않은 아이디어 |
| [🔍 기획 리뷰](Docs/Reviews/) | AI(Codex) 등의 기획 리뷰 기록 |

## 🛠️ 기술 스택

| 항목 | 내용 |
|---|---|
| 엔진 | Unity 6.3 LTS (2D URP) |
| 언어 | C# |
| 플랫폼 | Android (이후 iOS) |
| 데이터 | ScriptableObject (스킬, 영웅, 확률표) |
| 저장 | JSON (`Application.persistentDataPath`) |

## 🚀 시작하기

> ⚠️ 유니티 프로젝트는 아직 만들기 전이에요. 생성되면 이 부분을 업데이트할게요.

1. **Unity Hub**에서 **Unity 6.3 LTS**를 설치해요. (모듈: Android Build Support, OpenJDK, Android SDK & NDK)
2. 이 저장소를 클론해요.
   ```bash
   git clone https://github.com/choijunhuk/history-heroes.git
   ```
   > 경로에 **한글이나 공백이 없는 곳**에 받아주세요. 안드로이드 빌드가 실패할 수 있어요.
3. Unity Hub → **Add** → `Game/` 폴더를 열어요.

## 📁 폴더 구조

```
history-heroes/
 ├─ Docs/          기획서, 규칙, 아이디어, 리뷰
 ├─ Game/          유니티 프로젝트 (생성 예정)
 ├─ AGENTS.md      AI 에이전트(Codex 등) 작업 규칙
 └─ CLAUDE.md      Claude Code 작업 규칙
```

## 🤝 개발 방식

이 프로젝트는 **게임 개발을 배우면서** 만들고 있어요. Claude Code와 Codex의 도움을 받지만, 핵심 게임 로직은 직접 짜는 걸 원칙으로 해요. 규칙은 [개발 규칙](Docs/DevRules.md)에 정리돼 있어요.

---

<div align="center">

© 2026 choijunhuk. All rights reserved.

</div>
